namespace Commute360.Models;

public class User
{
    //user id,name,email,harsed password,role
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Staff"; 
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
 //to show a staff can have mulitple bbokings
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public bool EmailVerified { get; set; } = false;
    // International format, e.g. +233241234567. Used for SMS alerts.
    public string? PhoneNumber { get; set; }
}