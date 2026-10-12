namespace Commute360.Models;

public class Route
{
    //route id
    public int Id { get; set; }
    // starting point of the route
    public string Origin { get; set; } = string.Empty;
    //final destination of the route
    public string Destination { get; set; } = string.Empty;
    //displays the current date and time
    public DateTime ScheduleTime { get; set; }
    //the capacity of the bus
    public int Capacity { get; set; }
    public int? DriverId { get; set; }
    public User? Driver { get; set; }
//takes care of one route having multiple stops
    public ICollection<Stop> Stops { get; set; } = new List<Stop>();
    //takes care of one route having multiple bookings
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}