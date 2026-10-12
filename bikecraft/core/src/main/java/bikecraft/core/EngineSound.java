package bikecraft.core;

import java.util.List;

/** Picks the two engine samples around the current rpm and their crossfade + pitch, like an rpm-layered engine. */
public final class EngineSound {
    public record Mix(int lo, int hi, double hiWeight, double loPitch, double hiPitch) {}

    public static Mix mix(List<BikeSpec.EngineSample> s, double rpm) {
        if (s.isEmpty()) return null;
        int hi = 0;
        while (hi < s.size() && s.get(hi).rpm() < rpm) hi++;
        if (hi == 0) return new Mix(0, 0, 0, pitch(rpm, s.get(0)), 1);
        if (hi == s.size()) { int l = s.size() - 1; return new Mix(l, l, 0, pitch(rpm, s.get(l)), 1); }
        int lo = hi - 1;
        double t = (rpm - s.get(lo).rpm()) / Math.max(1, s.get(hi).rpm() - s.get(lo).rpm());
        return new Mix(lo, hi, t, pitch(rpm, s.get(lo)), pitch(rpm, s.get(hi)));
    }

    static double pitch(double rpm, BikeSpec.EngineSample e) {
        return e.rpm() <= 0 ? 1 : Math.max(0.5, Math.min(2.0, rpm / e.rpm()));
    }
}
