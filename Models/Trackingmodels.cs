namespace Commute360.Models;

// Latest known position of the bus on a route.
// One row per route; it is overwritten on every update, so the table stays tiny.
public class BusLocation
{
    public int Id { get; set; }
    public int RouteId { get; set; }
    public int DriverId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Heading { get; set; }
    public double? SpeedKmh { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// A message the driver sends to everyone riding a route
// (delay, traffic, breakdown, general info).
public class DriverAlert
{
    public int Id { get; set; }
    public int RouteId { get; set; }
    public int DriverId { get; set; }
    public string Type { get; set; } = "Info";
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}