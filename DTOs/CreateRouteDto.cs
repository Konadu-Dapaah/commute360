namespace Commute360.DTOs;
/// CreateRouteDto class is used to accept route creation data from the client and send it to the server for processing. It contains properties for the origin, destination, schedule time, and capacity of the route.
public class CreateRouteDto
{
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime ScheduleTime { get; set; }
    public int Capacity { get; set; }

    public int? DriverId { get; set; }

    // List of stops supplied when creating the route
    public List<CreateStopDto> Stops { get; set; } = new();
}