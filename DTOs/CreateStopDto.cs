namespace Commute360.DTOs;
/// CreateStopDto class is used to accept stop creation data from the client and send it to the server for processing. It contains properties for the name, latitude, longitude, and order of the stop in the route sequence.
public class CreateStopDto
{
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Order { get; set; }
}
