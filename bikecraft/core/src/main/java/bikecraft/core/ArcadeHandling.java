package bikecraft.core;

/** "Minecraft handling": easy, horse-like. Same bike's top speed and engine sound; no crashes; climbs a block. */
public final class ArcadeHandling implements Handling {
    public String id() { return "mc"; }
    public double stepHeight() { return 1.0; }

    public void step(BikeSpec b, BikeState s, BikeInput in, double dt) {
        s.crashed = false;
        double top = Math.min(b.topSpeed(), 24) * 0.75;
        double target = in.throttle() * top - in.brake() * 4;
        s.speed += (target - s.speed) * Math.min(1, dt * (in.throttle() > 0 ? 1.4 : 2.5));
        s.yaw += in.steer() * 2.2 * dt * Math.min(1, Math.abs(s.speed) / 3);
        s.roll += (in.steer() * 0.35 - s.roll) * Math.min(1, dt * 6); // cosmetic
        s.pitch *= Math.max(0, 1 - dt * 6);
        double frac = Math.abs(s.speed) / Math.max(1, top);
        s.gear = 1 + (int) Math.min(b.gearRatios().size() - 1, frac * b.gearRatios().size());
        s.rpm = b.idleRpm() + (b.maxRpm() * 0.85 - b.idleRpm()) * ((frac * b.gearRatios().size()) % 1.0 * 0.7 + 0.3 * in.throttle());
    }
}
