namespace Commute360.DTOs;

public class StopResponseDto
{
    public int Id { get; set; }
    public int RouteId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Order { get; set; }
}