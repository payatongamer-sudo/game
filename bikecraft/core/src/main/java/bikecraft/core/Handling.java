package bikecraft.core;

/** A handling model. Both modes drive the same BikeState so H can swap them mid-ride. */
public interface Handling {
    String id();
    void step(BikeSpec spec, BikeState s, BikeInput in, double dt);
    /** How many blocks the bike climbs without jumping. */
    double stepHeight();

    static double gearSpeed(BikeSpec spec, int gear, double rpm) {
        double ratio = spec.gearRatios().get(gear - 1) * spec.finalDrive();
        return rpm / 60.0 * 2 * Math.PI * spec.wheelRadiusM() / ratio;
    }
}
