package bikecraft.core;

import java.util.List;

/** One bike as read from the player's MX Bikes folder. Missing values fall back to a 450cc-class default. */
public record BikeSpec(
        String id, String displayName, boolean readable,
        double massKg, double idleRpm, double maxRpm, double peakPowerW, double peakPowerRpm,
        List<Double> gearRatios, double finalDrive /* primary x final */, double wheelRadiusM, double wheelbaseM,
        List<EngineSample> samples) {

    public record EngineSample(String file, double rpm) {}

    public static final double DEF_MASS = 105, DEF_IDLE = 1800, DEF_MAX = 12500, DEF_POWER = 41000,
            DEF_POWER_RPM = 9500, DEF_FINAL = 3.92, DEF_PRIMARY = 3.4, DEF_WHEEL = 0.36, DEF_WHEELBASE = 1.48;
    public static final List<Double> DEF_GEARS = List.of(2.0, 1.6, 1.3, 1.1, 0.95);

    /** Rider + bike top speed estimate (m/s) in top gear at max rpm. */
    public double topSpeed() {
        double g = gearRatios.get(gearRatios.size() - 1) * finalDrive;
        return maxRpm / 60.0 * 2 * Math.PI * wheelRadiusM / g;
    }
}
