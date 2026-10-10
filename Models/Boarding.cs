namespace Commute360.Models;

public class Boarding
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public int DriverId { get; set; }
    public DateOnly RideDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Booking? Booking { get; set; }
    public User? Driver { get; set; }
}