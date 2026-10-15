namespace Commute360.Models;

// One rider's trip on one day. The rider records it themselves ("I've boarded" and "I've got off").
// The record is kept for the admin's history even if the booking or account is deleted later.
public class Boarding
{
    public int Id { get; set; }

    // Null after the booking (or the account that owned it) was deleted. The record itself stays.
    public int? BookingId { get; set; }

    // Only set on older records that a driver ticked. Null when the rider checked in themselves.
    public int? DriverId { get; set; }

    public DateOnly RideDate { get; set; }

    // When the rider boarded
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // When the rider got off. Setting this ends the trip.
    public DateTime? AlightedAt { get; set; }

    // "Rider" (self check-in) or "Driver" (older records)
    public string Source { get; set; } = "Rider";

    // A copy of the details on the day, so the history stays readable if the route, stops or account change
    public int? RouteId { get; set; }
    public string? RiderName { get; set; }
    public string? RouteName { get; set; }
    public string? BoardingStopName { get; set; }
    public string? DropOffStopName { get; set; }

    public Booking? Booking { get; set; }
    public User? Driver { get; set; }
}