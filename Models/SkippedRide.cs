namespace Commute360.Models;

public class SkippedRide
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public DateOnly RideDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Booking? Booking { get; set; }
}