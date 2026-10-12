namespace Commute360.Models;

public class Booking
{
    //booking id, primary key
    public int Id { get; set; }
    // user id, foreign key to user table. points to the exact user making the bookings
    public int UserId { get; set; }
    //Links the foreign key (UserId) to the full User object.
    public User? User { get; set; }
// route id, foreign key to route table. points to the exact route for which the booking is made
    public int RouteId { get; set; }
    public Route? Route { get; set; }
    //status of booking; confirmed,cancelled. set to default since that is the normal case when booking is first created
    public int BoardingStopId { get; set; }
    public Stop? BoardingStop { get; set; }

    public int DropOffStopId { get; set; }
    public Stop? DropOffStop { get; set; }

    public string Days { get; set; } = string.Empty;
    public string Status { get; set; } = "Confirmed"; 
    //automatically records the exact date and time the booking was made in coordinated universal time
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}