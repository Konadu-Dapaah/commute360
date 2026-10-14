using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Commute360.Data;
using Commute360.Services;

namespace Commute360.Controllers;

public class ChangePasswordDto
{
    [Required] public string CurrentPassword { get; set; } = "";
    [Required, MinLength(8, ErrorMessage = "The new password must be at least 8 characters.")]
    public string NewPassword { get; set; } = "";
}

public class ForgotPasswordDto
{
    [Required, EmailAddress] public string Email { get; set; } = "";
}

public class ResetPasswordDto
{
    [Required] public string Token { get; set; } = "";
    [Required, MinLength(8, ErrorMessage = "The new password must be at least 8 characters.")]
    public string NewPassword { get; set; } = "";
}

public class TokenDto
{
    [Required] public string Token { get; set; } = "";
}

[ApiController]
[Route("api/account")]
[EnableRateLimiting("auth")]
public class AccountController : ControllerBase
{
    private const string IncompatibleHash =
        "Password hashing in PasswordService doesn't match your login code. See the comments in Services/AccountServices.cs.";

    private readonly AppDbContext _context;
    private readonly PasswordService _passwords;
    private readonly AccountMailer _mailer;

    public AccountController(AppDbContext context, PasswordService passwords, AccountMailer mailer)
    {
        _context = context;
        _passwords = passwords;
        _mailer = mailer;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
        if (user == null) return Unauthorized();

        if (!_passwords.IsCompatible(user.PasswordHash))
            return StatusCode(500, IncompatibleHash);

        if (!_passwords.Verify(user, dto.CurrentPassword))
            return BadRequest("Your current password is incorrect.");

        user.PasswordHash = _passwords.Hash(user, dto.NewPassword);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Password changed." });
    }

    // Always answers the same way, so nobody can use this to find out which emails have accounts.
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        var email = dto.Email.Trim().ToLower();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        // 1. The address must belong to an account
        if (user == null)
            return NotFound(new { message = "No account was found with that email address." });

        // 2. The email must be verified first. If it is not, send the verification link instead of a reset link.
        if (!user.EmailVerified)
        {
            await _mailer.SendVerificationAsync(user);
            return Ok(new
            {
                message = "Your email address isn't verified yet. We've sent you a verification link. Open it, then come back and request the reset link again.",
                needsVerification = true
            });
        }

        // 3. Verified account: send the reset link
        await _mailer.SendResetAsync(user);
        return Ok(new { message = "A password reset link has been sent to your email. It expires in 1 hour." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var userId = await _mailer.ConsumeAsync(dto.Token, "Reset");
        if (userId == null) return BadRequest("This reset link is invalid or has expired.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return BadRequest("This reset link is invalid or has expired.");

        if (!_passwords.IsCompatible(user.PasswordHash))
            return StatusCode(500, IncompatibleHash);

        user.PasswordHash = _passwords.Hash(user, dto.NewPassword);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Password updated. You can log in now." });
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromBody] TokenDto dto)
    {
        var userId = await _mailer.ConsumeAsync(dto.Token, "Verify");
        if (userId == null) return BadRequest("This verification link is invalid or has expired.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return BadRequest("This verification link is invalid or has expired.");

        user.EmailVerified = true;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Email verified. Thank you!" });
    }

    [HttpPost("resend-verification")]
    [Authorize]
    public async Task<IActionResult> ResendVerification()
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
        if (user == null) return Unauthorized();
        if (user.EmailVerified) return Ok(new { message = "Your email is already verified." });

        await _mailer.SendVerificationAsync(user);
        return Ok(new { message = "Verification email sent." });
    }
    // For people who cannot log in yet because their email is not verified.
    // Always answers the same way, so it cannot be used to discover which emails have accounts.
    [HttpPost("resend-verification-public")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendVerificationPublic([FromBody] ForgotPasswordDto dto)
    {
        var email = dto.Email.Trim().ToLower();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
        if (user != null && !user.EmailVerified)
            await _mailer.SendVerificationAsync(user);

        return Ok(new { message = "If that account still needs verifying, a new link is on its way." });
    }
}