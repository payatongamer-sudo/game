package bikecraft.core;

/**
 * "MX Bikes handling": force-based, built from the bike's own mass, power curve, gears and rev limit.
 * Auto clutch + sequential auto-shift; leaning sets turn radius; weight back on throttle lifts the front;
 * a hard landing with lean or pitch crashes the rider off.
 */
public final class MxbHandling implements Handling {
    static final double RIDER_KG = 75, G = 9.81, MU = 1.1, DRAG = 0.38, ROLL_RES = 0.03, MAX_LEAN = Math.toRadians(52);

    public String id() { return "mxb"; }
    public double stepHeight() { return 0.6; }

    public void step(BikeSpec b, BikeState s, BikeInput in, double dt) {
        if (s.crashed) { s.speed *= Math.pow(0.05, dt); return; }
        double m = b.massKg() + RIDER_KG;
        // engine rpm follows rear wheel in current gear
        double wheelRps = Math.abs(s.speed) / (2 * Math.PI * b.wheelRadiusM());
        double ratio = b.gearRatios().get(s.gear - 1) * b.finalDrive();
        s.rpm = Math.max(b.idleRpm(), wheelRps * ratio * 60);
        if (s.rpm > b.maxRpm() * 0.93 && s.gear < b.gearRatios().size()) s.gear++;
        else if (s.gear > 1 && s.rpm < b.maxRpm() * 0.45) s.gear--;
        s.rpm = Math.min(s.rpm, b.maxRpm());
        double power = b.peakPowerW() * torqueShape(s.rpm, b) * in.throttle();
        if (s.rpm >= b.maxRpm()) power = 0; // limiter
        double drive = power / Math.max(2.0, Math.abs(s.speed));
        drive = Math.min(drive, MU * m * G * 0.62); // rear grip
        double brake = in.brake() * MU * m * G * 0.9 * Math.signum(s.speed);
        double resist = DRAG * s.speed * Math.abs(s.speed) + ROLL_RES * m * G * Math.signum(s.speed);
        double a = s.airborne ? 0 : (drive - brake - resist) / m;
        double before = s.speed;
        s.speed += a * dt;
        if (in.brake() > 0 && Math.signum(before) != Math.signum(s.speed)) s.speed = 0;
        // lean: steer sets target roll (positive = right); roll sets yaw rate g*tan(roll)/v (yaw grows turning right)
        double target = in.steer() * MAX_LEAN * Math.min(1, Math.abs(s.speed) / 6.0);
        s.roll += (target - s.roll) * Math.min(1, dt * 4.5);
        if (!s.airborne && Math.abs(s.speed) > 0.5) s.yaw += G * Math.tan(s.roll) / Math.max(3, Math.abs(s.speed)) * dt * Math.signum(s.speed);
        // wheelie: weight back + throttle at low gear
        double lift = (in.lean() < 0 ? -in.lean() : 0) * in.throttle() * (drive / (m * G)) * 3 - (in.lean() > 0 ? in.lean() * 2 : 0);
        if (s.airborne) lift = -in.lean() * 1.5;
        s.pitch += (lift - (s.airborne ? 0 : 1.4 * s.pitch)) * dt;
        s.pitch = Math.max(-0.5, Math.min(1.25, s.pitch));
        if (!s.airborne && s.pitch > 1.2) crash(s); // looped out
    }

    /** Call when the bike touches down. vy is downward speed in m/s. */
    public static void land(BikeState s, double vy) {
        s.airborne = false;
        if (vy > 9 || (vy > 4 && (Math.abs(s.pitch) > 0.6 || Math.abs(s.roll) > 0.9))) crash(s);
    }

    static void crash(BikeState s) { s.crashed = true; s.rpm = 0; }

    static double torqueShape(double rpm, BikeSpec b) {
        double x = rpm / b.peakPowerRpm();
        return Math.max(0.15, Math.min(1, x < 1 ? 0.25 + 0.75 * x : 1 - 1.4 * (x - 1) * (x - 1)));
    }
}
