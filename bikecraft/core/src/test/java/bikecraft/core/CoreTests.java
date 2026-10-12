package bikecraft.core;

import java.nio.file.*;
import java.util.List;
import java.util.zip.*;

/** Dependency-free tests: run with `java -cp out bikecraft.core.CoreTests`. Uses synthetic files only. */
public final class CoreTests {
    static int fails = 0;
    static void check(boolean ok, String what) { System.out.println((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    public static void main(String[] a) throws Exception {
        Path tmp = Files.createTempDirectory("bikecraft-test");
        Path bikes = tmp.resolve("mods/bikes");
        Path folder = bikes.resolve("Test_450");
        Files.createDirectories(folder);
        Files.writeString(folder.resolve("bike.cfg"), "name = \"Test 450\"\nchassis\n{\n\tmass = 104.5 // kg\n\twheelbase = 1480\n}\n");
        Files.writeString(folder.resolve("test.engn"), "engine {\n limiter = 12000\n idle_rpm = 1900\n max_power = 40\n gear1 = 2.1\n gear2 = 1.7\n gear3 = 1.35\n gear4 = 1.12\n gear5 = 0.96\n final_ratio = 3.9\n}\n");
        Files.writeString(folder.resolve("eng.scl"), "sample = SoundMods/t450/on_9000.wav 9000\nsample = SoundMods/t450/on_3000.wav 3000\nsample = SoundMods/t450/on_6000.wav\n");
        try (var z = new ZipOutputStream(Files.newOutputStream(bikes.resolve("Zip_250.pkz")))) {
            z.putNextEntry(new ZipEntry("Zip_250/bike.cfg")); z.write("mass = 98\nlimiter = 13500\n".getBytes()); z.closeEntry();
        }
        Files.write(bikes.resolve("Stock_Locked.pkz"), new byte[]{1, 2, 3, 4, 5, 6, 7, 8});

        var lib = new MxBikesLibrary(MxBikesLibrary.defaultRoots(tmp, null));
        List<BikeSpec> list = lib.scan();
        check(list.size() == 3, "scan finds folder, zip pkz and locked pkz (" + list.size() + ")");
        BikeSpec t = list.stream().filter(b -> b.id().equals("Test_450")).findFirst().orElseThrow();
        check(t.readable() && t.displayName().equals("Test 450"), "folder bike name read");
        check(Math.abs(t.massKg() - 104.5) < 1e-9, "mass from nested section");
        check(Math.abs(t.wheelbaseM() - 1.48) < 1e-9, "wheelbase mm -> m");
        check(t.maxRpm() == 12000 && t.idleRpm() == 1900, "rpm limits from engn");
        check(t.peakPowerW() == 40000, "power kW -> W");
        check(t.gearRatios().size() == 5 && t.gearRatios().get(0) == 2.1, "gear ratios");
        check(t.samples().size() == 3 && t.samples().get(0).rpm() == 3000 && t.samples().get(1).rpm() == 6000, "scl samples sorted by rpm (inferred from filename)");
        BikeSpec z = list.stream().filter(b -> b.id().equals("Zip_250")).findFirst().orElseThrow();
        check(z.readable() && z.maxRpm() == 13500 && z.massKg() == 98, "plain-zip pkz read in place");
        BikeSpec s = list.stream().filter(b -> b.id().equals("Stock_Locked")).findFirst().orElseThrow();
        check(!s.readable() && s.displayName().equals("Stock Locked"), "locked pkz listed by name only");

        // MX Bikes handling: accelerates, shifts, tops out near topSpeed
        var mxb = new MxbHandling();
        var st = new BikeState();
        for (int i = 0; i < 20 * 3; i++) mxb.step(t, st, new BikeInput(1, 0, 0, 0), 0.05);
        check(st.speed > 12, "0-3s full throttle > 12 m/s (" + fmt(st.speed) + ")");
        check(st.gear >= 2, "auto-shifted up (gear " + st.gear + ")");
        for (int i = 0; i < 20 * 60; i++) mxb.step(t, st, new BikeInput(1, 0, 0, 0), 0.05);
        check(st.speed <= t.topSpeed() + 0.5 && st.speed > 20 && t.topSpeed() < 45, "top speed bounded by gearing (" + fmt(st.speed) + " <= " + fmt(t.topSpeed()) + ")");
        double yaw0 = st.yaw;
        for (int i = 0; i < 20; i++) mxb.step(t, st, new BikeInput(0.5, 0, 1, 0), 0.05);
        check(st.roll > 0.3 && st.yaw > yaw0, "steer right leans and turns");
        for (int i = 0; i < 20 * 10; i++) mxb.step(t, st, new BikeInput(0, 1, 0, 0), 0.05);
        check(Math.abs(st.speed) < 0.01, "brakes to a stop without reversing");
        var w = new BikeState();
        for (int i = 0; i < 20 * 2; i++) mxb.step(t, w, new BikeInput(1, 0, 0, -1), 0.05);
        check(w.pitch > 0.1, "weight back + throttle lifts the front (" + fmt(w.pitch) + ")");
        var l = new BikeState(); l.airborne = true; MxbHandling.land(l, 12);
        check(l.crashed, "hard landing crashes in MX Bikes mode");

        // Minecraft handling: same state carries over, no crash, smaller top speed
        var arc = new ArcadeHandling();
        l.speed = 10;
        for (int i = 0; i < 20; i++) arc.step(t, l, new BikeInput(1, 0, 0, 0), 0.05);
        check(!l.crashed && l.speed > 0, "toggle to Minecraft handling rides on (crash cleared)");
        check(arc.stepHeight() == 1.0 && mxb.stepHeight() < 1.0, "only Minecraft handling climbs full blocks");

        var mix = EngineSound.mix(t.samples(), 4500);
        check(mix.lo() == 0 && mix.hi() == 1 && Math.abs(mix.hiWeight() - 0.5) < 1e-9, "engine crossfade between 3000 and 6000 samples");
        System.out.println(fails == 0 ? "ALL PASS" : fails + " FAILED");
        System.exit(fails == 0 ? 0 : 1);
    }
    static String fmt(double d) { return String.format("%.2f", d); }
}
