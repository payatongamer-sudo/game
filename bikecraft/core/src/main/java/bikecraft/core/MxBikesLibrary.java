package bikecraft.core;

import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;
import java.util.zip.ZipEntry;
import java.util.zip.ZipFile;

/**
 * Finds bikes in the player's own MX Bikes install and mods folder and reads what is readable.
 * Nothing is copied: sample files are opened in place when played.
 * Encrypted stock .pkz archives are listed by name only (readable=false); they are never decrypted.
 */
public final class MxBikesLibrary {
    private final List<Path> roots;

    public MxBikesLibrary(List<Path> roots) { this.roots = roots; }

    /** Default roots: {game:mx-bikes}/mods/bikes and Documents/PiBoSo/MX Bikes/mods/bikes. */
    public static List<Path> defaultRoots(Path gameDir, Path documents) {
        List<Path> r = new ArrayList<>();
        if (gameDir != null) r.add(gameDir.resolve("mods").resolve("bikes"));
        if (documents != null) r.add(documents.resolve("PiBoSo").resolve("MX Bikes").resolve("mods").resolve("bikes"));
        return r;
    }

    public List<BikeSpec> scan() {
        Map<String, BikeSpec> byId = new TreeMap<>(String.CASE_INSENSITIVE_ORDER);
        for (Path root : roots) {
            if (!Files.isDirectory(root)) continue;
            try (var ds = Files.newDirectoryStream(root)) {
                for (Path p : ds) {
                    String n = p.getFileName().toString();
                    BikeSpec b = null;
                    if (Files.isDirectory(p)) b = readFolder(p, n);
                    else if (n.toLowerCase().endsWith(".pkz")) b = readPkz(p, n.substring(0, n.length() - 4));
                    if (b != null) byId.putIfAbsent(b.id(), b);
                }
            } catch (IOException ignored) {}
        }
        return new ArrayList<>(byId.values());
    }

    interface Source { List<String> names(); String read(String name) throws IOException; }

    private BikeSpec readFolder(Path dir, String id) {
        List<String> names = new ArrayList<>();
        try (var w = Files.walk(dir, 3)) {
            w.filter(Files::isRegularFile).forEach(f -> names.add(dir.relativize(f).toString().replace('\\', '/')));
        } catch (IOException e) { return unreadable(id); }
        return build(id, new Source() {
            public List<String> names() { return names; }
            public String read(String n) throws IOException { return Files.readString(dir.resolve(n), StandardCharsets.ISO_8859_1); }
        }, dir.toString());
    }

    private BikeSpec readPkz(Path pkz, String id) {
        try (ZipFile z = new ZipFile(pkz.toFile())) {
            List<String> names = new ArrayList<>();
            for (var en = z.entries(); en.hasMoreElements(); ) { ZipEntry e = en.nextElement(); if (!e.isDirectory()) names.add(e.getName()); }
            Map<String, String> cache = new HashMap<>();
            for (String n : names) if (isText(n)) {
                try (InputStream in = z.getInputStream(z.getEntry(n))) { cache.put(n, new String(in.readAllBytes(), StandardCharsets.ISO_8859_1)); }
            }
            return build(id, new Source() {
                public List<String> names() { return names; }
                public String read(String n) { return cache.getOrDefault(n, ""); }
            }, pkz + "!");
        } catch (Exception e) {
            return unreadable(id); // encrypted / protected archive: list by name only
        }
    }

    private static boolean isText(String n) {
        String l = n.toLowerCase();
        return l.endsWith(".cfg") || l.endsWith(".engn") || l.endsWith(".scl") || l.endsWith(".ini");
    }

    private static BikeSpec unreadable(String id) {
        return new BikeSpec(id, pretty(id), false, BikeSpec.DEF_MASS, BikeSpec.DEF_IDLE, BikeSpec.DEF_MAX, BikeSpec.DEF_POWER,
                BikeSpec.DEF_POWER_RPM, BikeSpec.DEF_GEARS, BikeSpec.DEF_FINAL, BikeSpec.DEF_WHEEL, BikeSpec.DEF_WHEELBASE, List.of());
    }

    private BikeSpec build(String id, Source src, String base) {
        Map<String, String> cfg = new LinkedHashMap<>();
        String scl = null;
        try {
            for (String n : src.names()) {
                String l = n.toLowerCase();
                if (l.endsWith(".cfg") || l.endsWith(".engn")) cfg.putAll(PiBoSoCfg.parse(src.read(n)));
                if (l.endsWith(".scl") && scl == null) scl = src.read(n);
            }
        } catch (IOException e) { return unreadable(id); }
        if (cfg.isEmpty() && scl == null) return unreadable(id);
        double mass = or(PiBoSoCfg.find(cfg, "mass", "weight"), BikeSpec.DEF_MASS);
        double idle = or(PiBoSoCfg.find(cfg, "idle_rpm", "idlerpm", "idle"), BikeSpec.DEF_IDLE);
        double max = or(PiBoSoCfg.find(cfg, "limiter", "rev_limit", "max_rpm", "maxrpm", "redline"), BikeSpec.DEF_MAX);
        double pw = or(PiBoSoCfg.find(cfg, "max_power", "power"), BikeSpec.DEF_POWER / 1000) ;
        if (pw < 1000) pw *= pw < 200 ? 1000 : 1; // kW -> W when small
        double prpm = or(PiBoSoCfg.find(cfg, "max_power_rpm", "power_rpm"), max * 0.78);
        List<Double> gears = new ArrayList<>();
        for (int g = 1; g <= 6; g++) { Double r = PiBoSoCfg.find(cfg, "gear" + g, "gear_" + g, "ratio" + g); if (r != null && r > 0) gears.add(r); }
        if (gears.isEmpty()) gears = BikeSpec.DEF_GEARS;
        double fin = or(PiBoSoCfg.find(cfg, "final_ratio", "final_drive", "final"), BikeSpec.DEF_FINAL)
                * or(PiBoSoCfg.find(cfg, "primary_ratio", "primary"), BikeSpec.DEF_PRIMARY);
        double wr = or(PiBoSoCfg.find(cfg, "rear_wheel_radius", "wheel_radius", "radius"), BikeSpec.DEF_WHEEL);
        if (wr > 5) wr /= 1000; // mm
        double wb = or(PiBoSoCfg.find(cfg, "wheelbase"), BikeSpec.DEF_WHEELBASE);
        if (wb > 10) wb /= 1000;
        String name = cfg.getOrDefault("name", cfg.getOrDefault("info.name", pretty(id)));
        List<BikeSpec.EngineSample> samples = scl == null ? List.of() : parseScl(scl, base);
        return new BikeSpec(id, name, true, mass, idle, max, pw, prpm, gears, fin, wr, wb, samples);
    }

    /** Pulls every .wav reference and the rpm number on the same line. Format UNVERIFIED. */
    static List<BikeSpec.EngineSample> parseScl(String scl, String base) {
        List<BikeSpec.EngineSample> out = new ArrayList<>();
        var wav = java.util.regex.Pattern.compile("([\\w./\\\\ -]+\\.wav)", java.util.regex.Pattern.CASE_INSENSITIVE);
        for (String line : scl.split("\\r?\\n")) {
            var m = wav.matcher(line);
            if (!m.find()) continue;
            String file = m.group(1).trim();
            Double rpm = PiBoSoCfg.firstNumber(line.replace(file, ""));
            if (rpm == null) {
                String fname = file.substring(Math.max(file.lastIndexOf('/'), file.lastIndexOf('\\')) + 1);
                var d = java.util.regex.Pattern.compile("(\\d{3,5})").matcher(fname);
                rpm = 0.0;
                while (d.find()) rpm = Double.valueOf(d.group(1)); // last number in the file name
            }
            out.add(new BikeSpec.EngineSample(file, rpm));
        }
        out.sort(Comparator.comparingDouble(BikeSpec.EngineSample::rpm));
        return out;
    }

    private static double or(Double d, double def) { return d == null || d <= 0 ? def : d; }

    static String pretty(String id) { return id.replace('_', ' ').replace('-', ' ').trim(); }
}
