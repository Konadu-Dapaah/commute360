using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Commute360.Data;
using Commute360.Models;

namespace Commute360.Services;

// ---------------------------------------------------------------------------
// PASSWORD HASHING
// This MUST hash passwords the same way your login/register code does,
// otherwise people can't log in after a reset or change.
// Default below = ASP.NET's built-in PasswordHasher. If your AuthController
// uses BCrypt, swap to the BCrypt lines shown in the comments.
// ---------------------------------------------------------------------------
public class PasswordService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password) =>
        _hasher.HashPassword(user, password);
    // BCrypt:  => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(User user, string password) =>
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
    // BCrypt:  => BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);

    // Safety check: refuse to change a password if the stored hash is in a different format,
    // because we would lock that person out.
    public bool IsCompatible(string? existingHash) =>
        !string.IsNullOrEmpty(existingHash) && existingHash.StartsWith("AQAAAA");
    // BCrypt:  => !string.IsNullOrEmpty(existingHash) && existingHash.StartsWith("$2");
}

// ---------------------------------------------------------------------------
// EMAIL
// With no "Email:Host" configured, emails are written to the log instead,
// so in development you can copy the link from the console.
// ---------------------------------------------------------------------------
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body);
}

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string body)
    {
        var host = _config["Email:Host"];

        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogWarning("Email is not configured. Would have sent to {To}\nSubject: {Subject}\n{Body}", to, subject, body);
            return;
        }

        try
        {
            var from = _config["Email:From"] ?? _config["Email:User"] ?? "no-reply@commute360.local";
            using var client = new SmtpClient(host, int.Parse(_config["Email:Port"] ?? "587"))
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_config["Email:User"], _config["Email:Password"])
            };
            using var message = new MailMessage(from, to, subject, body);
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            // Never break the request because an email failed
            _logger.LogError(ex, "Failed to send email to {To}", to);
        }
    }
}

// ---------------------------------------------------------------------------
// Creates and checks the one-time links used for verification and password reset
// ---------------------------------------------------------------------------
public class AccountMailer
{
    private readonly AppDbContext _context;
    private readonly IEmailSender _email;
    private readonly IConfiguration _config;

    public AccountMailer(AppDbContext context, IEmailSender email, IConfiguration config)
    {
        _context = context;
        _email = email;
        _config = config;
    }

    private string BaseUrl => (_config["App:BaseUrl"] ?? "http://localhost:5291").TrimEnd('/');

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    private async Task<string> CreateTokenAsync(int userId, string purpose, TimeSpan life)
    {
        // Only one live token per purpose: retire older ones
        var old = await _context.EmailTokens
            .Where(t => t.UserId == userId && t.Purpose == purpose && t.UsedAt == null)
            .ToListAsync();
        foreach (var t in old) t.UsedAt = DateTime.UtcNow;

        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        _context.EmailTokens.Add(new EmailToken
        {
            UserId = userId,
            TokenHash = Hash(raw),
            Purpose = purpose,
            ExpiresAt = DateTime.UtcNow.Add(life)
        });
        await _context.SaveChangesAsync();
        return raw;
    }

    // Returns the user id if the token is valid, and marks it used.
    public async Task<int?> ConsumeAsync(string raw, string purpose)
    {
        var hash = Hash(raw);
        var token = await _context.EmailTokens.FirstOrDefaultAsync(t =>
            t.TokenHash == hash && t.Purpose == purpose && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow);

        if (token == null) return null;

        token.UsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return token.UserId;
    }

    public async Task SendVerificationAsync(User user)
    {
        var raw = await CreateTokenAsync(user.Id, "Verify", TimeSpan.FromDays(2));
        var link = $"{BaseUrl}/verify-email.html?token={raw}";
        await _email.SendAsync(user.Email, "Verify your Commute360 email",
            $"Hi {user.Name},\n\nConfirm your email address by opening this link:\n{link}\n\nThe link expires in 2 days. If you didn't create an account, ignore this email.");
    }

    public async Task SendResetAsync(User user)
    {
        var raw = await CreateTokenAsync(user.Id, "Reset", TimeSpan.FromHours(1));
        var link = $"{BaseUrl}/reset-password.html?token={raw}";
        await _email.SendAsync(user.Email, "Reset your Commute360 password",
            $"Hi {user.Name},\n\nReset your password using this link:\n{link}\n\nThe link expires in 1 hour. If you didn't ask for this, ignore this email and your password stays the same.");
    }
}