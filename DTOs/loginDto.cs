namespace Commute360.DTOs;
// accepts user login data from the client and sends it to the server for processing
public class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}