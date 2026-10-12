namespace Commute360.Models;
//takes stop name, latitude, longitude and order of the stop in the route sequence
public class Stop
{
    // Unique identifier for the stop
    public int Id { get; set; }

    public int RouteId { get; set; }
    // Navigation property to the associated route
    public Route? Route { get; set; }

    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    // Order of the stop in the route sequence
    public int Order { get; set; } 
}