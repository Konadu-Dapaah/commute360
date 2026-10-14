using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Commute360.Data;
using Commute360.DTOs;
using Commute360.Models;
using Commute360.Services;

namespace Commute360.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TokenService _tokenService;
    private readonly AccountMailer _mailer;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext context, TokenService tokenService, AccountMailer mailer, IConfiguration config)
    {
        _context = context;
        _tokenService = tokenService;
        _mailer = mailer;
        _config = config;
    }

    // Compares an invite code without leaking timing information.
    private static bool CodeMatches(string? supplied, string? expected)
    {
        if (string.IsNullOrEmpty(supplied) || string.IsNullOrEmpty(expected)) return false;
        var a = Encoding.UTF8.GetBytes(supplied);
        var b = Encoding.UTF8.GetBytes(expected);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    // A real address needs something@something.xx (the browser's own check is much looser)
    private static readonly Regex EmailPattern =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.Compiled);

    private static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Length <= 254 && EmailPattern.IsMatch(email);

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        // 1. Email must be well formed. Store it trimmed and lower-case so that
        //    "Ama@x.com" and "ama@x.com" are the same account.
        var email = (dto.Email ?? "").Trim().ToLowerInvariant();
        if (!IsValidEmail(email))
            return BadRequest("Please enter a valid email address.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Please enter your name.");

        // The phone number receives SMS alerts. Stored in international format.
        if (!PhoneNumberHelper.TryNormalize(dto.PhoneNumber, out var phone))
            return BadRequest("Please enter a valid phone number, for example 0241234567.");

        // 2. Duplicate email check (case-insensitive)
        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == email))
            return BadRequest("Email already registered.");

        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8)
            return BadRequest("Password must be at least 8 characters.");

        // 2. Role validation and invite code verification
        var role = string.IsNullOrWhiteSpace(dto.Role) ? "Staff" : dto.Role;

        var allowedRoles = new[] { "Staff", "Driver", "Admin" };
        if (!allowedRoles.Contains(role))
            return BadRequest("Role must be Staff, Driver, or Admin.");

        if (role == "Driver" && !CodeMatches(dto.InviteCode, _config["Registration:DriverCode"]))
            return BadRequest("A valid driver invite code is required.");

        if (role == "Admin" && !CodeMatches(dto.InviteCode, _config["Registration:AdminCode"]))
            return BadRequest("A valid admin invite code is required.");

        // 3. Create the new user entity and hash password using PasswordHasher
        var user = new User
        {
            Name = dto.Name,
            Email = email,
            PhoneNumber = phone,
            Role = role,
            EmailVerified = false
        };

        var hasher = new PasswordHasher<User>();
        user.PasswordHash = hasher.HashPassword(user, dto.Password);

        // 4. Save to database
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // 5. Ask them to confirm their email
        try { await _mailer.SendVerificationAsync(user); }
        catch (Exception ex) { Console.WriteLine($"Verification email failed: {ex.Message}"); }

        return Ok(new { message = "Registration successful. Check your email for a verification link." });
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var email = (dto.Email ?? "").Trim().ToLowerInvariant();
        if (!IsValidEmail(email))
            return BadRequest("Please enter a valid email address.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
        if (user == null)
            return Unauthorized("Invalid email or password.");

        // Securely verify password using PasswordHasher instead of BCrypt
        var hasher = new PasswordHasher<User>();
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);

        if (result == PasswordVerificationResult.Failed)
            return Unauthorized("Invalid email or password.");

        // Optional gate: only people who proved they own the address may log in.
        // Switch on with the setting Email:RequireVerifiedToLogin = true (Render: Email__RequireVerifiedToLogin).
        // It is checked AFTER the password, so only the real owner learns the account is unverified.
        if (_config.GetValue<bool>("Email:RequireVerifiedToLogin") && !user.EmailVerified)
            return StatusCode(403, new
            {
                message = "Please verify your email address first. We sent you a link when you registered.",
                code = "EMAIL_NOT_VERIFIED"
            });

        return Ok(new { token = _tokenService.CreateToken(user), role = user.Role });
    }
}