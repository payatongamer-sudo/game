using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BrewingPoint.Minecraft
{
    /// <summary>
    /// Finds the player's own Minecraft: Java Edition files. Melty passes the companion folder
    /// ({game:minecraft-java}) as --brewingpoint-minecraft=&lt;dir&gt; or BREWINGPOINT_MINECRAFT_DIR;
    /// the usual launcher folders are tried after it. Layouts: sheets/minecraft_install.json.
    /// </summary>
    public static class MinecraftLocator
    {
        public const string ArgPrefix = "--brewingpoint-minecraft=";
        public const string EnvVar = "BREWINGPOINT_MINECRAFT_DIR";
        public const string ConfigKey = "minecraft_dir";

        /// <summary>Folders to try, most specific first.</summary>
        public static List<string> Candidates(IEnumerable<string> commandLine, Func<string, string?> env, string? configFile)
        {
            var list = new List<string>();
            foreach (var arg in commandLine)
                if (arg.StartsWith(ArgPrefix, StringComparison.OrdinalIgnoreCase))
                    list.Add(arg.Substring(ArgPrefix.Length).Trim('"'));
            var fromEnv = env(EnvVar);
            if (!string.IsNullOrWhiteSpace(fromEnv))
                list.Add(fromEnv!);
            if (configFile != null && File.Exists(configFile))
            {
                foreach (var line in File.ReadAllLines(configFile))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith(ConfigKey + "=", StringComparison.OrdinalIgnoreCase))
                        list.Add(trimmed.Substring(ConfigKey.Length + 1).Trim().Trim('"'));
                }
            }
            var appData = env("APPDATA");
            if (!string.IsNullOrEmpty(appData))
            {
                list.Add(Path.Combine(appData!, ".minecraft"));
                list.Add(Path.Combine(appData!, "PrismLauncher"));
                list.Add(Path.Combine(appData!, "MultiMC"));
            }
            var home = env("USERPROFILE") ?? env("HOME");
            if (!string.IsNullOrEmpty(home))
            {
                list.Add(Path.Combine(home!, "curseforge", "minecraft", "Install"));
                list.Add(Path.Combine(home!, ".minecraft"));
                list.Add(Path.Combine(home!, ".local", "share", "PrismLauncher"));
            }
            return list.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Resolves a folder (a launcher root, a Prism instance or its .minecraft, or anything below a root)
        /// to Minecraft roots that hold a client jar. Walks up a few levels so an instance folder works.
        /// </summary>
        public static IEnumerable<string> Roots(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                yield break;
            var dir = new DirectoryInfo(folder);
            var probe = new List<DirectoryInfo> { dir };
            foreach (var child in new[] { ".minecraft", "minecraft" })
            {
                var sub = new DirectoryInfo(Path.Combine(dir.FullName, child));
                if (sub.Exists)
                    probe.Insert(0, sub);
            }
            for (var up = dir.Parent; up != null && probe.Count < 8; up = up.Parent)
                probe.Add(up);
            foreach (var candidate in probe)
                if (ClientJars(candidate.FullName).Any())
                    yield return candidate.FullName;
        }

        /// <summary>Client jars under a root, for the official and the Prism/MultiMC layouts.</summary>
        public static IEnumerable<string> ClientJars(string root)
        {
            var versions = Path.Combine(root, "versions");
            if (Directory.Exists(versions))
            {
                foreach (var versionDir in Directory.GetDirectories(versions))
                {
                    var id = Path.GetFileName(versionDir);
                    var jar = Path.Combine(versionDir, id + ".jar");
                    if (File.Exists(jar))
                        yield return jar;
                }
            }
            var libraries = Path.Combine(root, "libraries", "com", "mojang", "minecraft");
            if (Directory.Exists(libraries))
            {
                foreach (var versionDir in Directory.GetDirectories(libraries))
                {
                    var id = Path.GetFileName(versionDir);
                    var jar = Path.Combine(versionDir, "minecraft-" + id + "-client.jar");
                    if (File.Exists(jar))
                        yield return jar;
                }
            }
        }

        /// <summary>
        /// Opens the newest install that has every jar entry the mashup needs. Returns null and fills
        /// <paramref name="report"/> when nothing usable is found.
        /// </summary>
        public static MinecraftInstall? Open(IEnumerable<string> candidates, IReadOnlyCollection<string> requiredJarEntries, out string report)
        {
            var lines = new List<string>();
            foreach (var candidate in candidates)
            {
                var roots = Roots(candidate).ToList();
                if (roots.Count == 0)
                {
                    lines.Add($"{candidate}: no Minecraft client jar");
                    continue;
                }
                foreach (var root in roots)
                {
                    var jars = ClientJars(root)
                        .Select(j => new FileInfo(j))
                        .OrderByDescending(f => f.LastWriteTimeUtc)
                        .ToList();
                    foreach (var jar in jars)
                    {
                        MinecraftInstall? install = null;
                        try
                        {
                            install = new MinecraftInstall(root, jar.FullName);
                            var missing = requiredJarEntries.Where(e => !install.HasJarEntry(e)).ToList();
                            if (missing.Count == 0)
                            {
                                lines.Add($"{jar.FullName}: ok");
                                report = string.Join(Environment.NewLine, lines);
                                return install;
                            }
                            lines.Add($"{jar.FullName}: missing {missing.Count} file(s), e.g. {missing[0]}");
                        }
                        catch (Exception ex)
                        {
                            lines.Add($"{jar.FullName}: unreadable ({ex.Message})");
                        }
                        install?.Dispose();
                    }
                }
            }
            report = string.Join(Environment.NewLine, lines);
            return null;
        }
    }
}
