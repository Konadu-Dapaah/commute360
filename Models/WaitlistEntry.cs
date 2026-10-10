namespace Commute360.Models;

public class WaitlistEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int RouteId { get; set; }
    public int BoardingStopId { get; set; }
    public int DropOffStopId { get; set; }
    public string Days { get; set; } = string.Empty;
    public string Status { get; set; } = "Waiting"; // "Waiting", "Promoted", "Cancelled"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public Route? Route { get; set; }
    public Stop? BoardingStop { get; set; }
    public Stop? DropOffStop { get; set; }
}