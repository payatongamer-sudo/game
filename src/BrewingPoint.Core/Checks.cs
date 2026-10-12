using System;
using System.Collections.Generic;
using System.IO;

namespace BrewingPoint
{
    /// <summary>
    /// Self-checks a test run can read: one "BrewingPoint check: &lt;name&gt; ok|fail - detail" line per check,
    /// written to UserData/BrewingPoint/checks.log (fresh each start) and to the MelonLoader log.
    /// </summary>
    public static class Checks
    {
        public const string Prefix = "BrewingPoint check: ";
        private static string? _path;
        public static Action<string>? Echo;
        public static readonly List<string> Lines = new List<string>();

        public static void Begin(string userDataDir)
        {
            var dir = Path.Combine(userDataDir, "BrewingPoint");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "checks.log");
            File.WriteAllText(_path, "");
            Lines.Clear();
        }

        public static void Ok(string name, string detail = "") => Write(name, "ok", detail);
        public static void Fail(string name, string detail = "") => Write(name, "fail", detail);

        private static void Write(string name, string status, string detail)
        {
            var line = Prefix + name + " " + status + (detail.Length > 0 ? " - " + detail.Replace(Environment.NewLine, " | ") : "");
            Lines.Add(line);
            Echo?.Invoke(line);
            if (_path != null)
            {
                try { File.AppendAllText(_path, line + Environment.NewLine); }
                catch (IOException) { }
            }
        }
    }
}
