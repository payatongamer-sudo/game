package bikecraft.core;

/** Per-tick rider input. throttle/brake 0..1, steer -1..1 (right positive), lean -1 back .. 1 forward. */
public record BikeInput(double throttle, double brake, double steer, double lean) {
    public static final BikeInput NONE = new BikeInput(0, 0, 0, 0);
}
