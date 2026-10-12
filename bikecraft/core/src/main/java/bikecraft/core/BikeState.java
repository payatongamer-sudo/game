package bikecraft.core;

/** Bike state in the ground plane. speed m/s along heading, yaw rad, roll/pitch rad (pitch>0 = front wheel up). */
public final class BikeState {
    public double speed, yaw, roll, pitch, rpm;
    public int gear = 1;
    public boolean airborne, crashed;
}
