namespace Commute360.DTOs;

public class RegisterDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "Staff"; // "Staff" or "Driver"
    public string? InviteCode { get; set; }
}