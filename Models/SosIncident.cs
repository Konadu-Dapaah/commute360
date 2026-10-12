namespace Commute360.Models;

public class SosIncident
{
    public int Id { get; set; }
    public int RouteId { get; set; }
    public int DriverId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}