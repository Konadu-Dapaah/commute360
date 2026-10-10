namespace Commute360.DTOs;

public class CreateBookingDto
{
    public int RouteId { get; set; }
    public int BoardingStopId { get; set; }
    public int DropOffStopId { get; set; }
    public List<string> Days { get; set; } = new();
}