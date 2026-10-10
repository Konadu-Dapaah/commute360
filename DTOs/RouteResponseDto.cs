namespace Commute360.DTOs;
public class RouteResponseDto
{
    public int Id { get; set; }
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime ScheduleTime { get; set; }
    public int Capacity { get; set; }
    public int? DriverId { get; set; }
    public List<StopResponseDto> Stops { get; set; } = new();
}