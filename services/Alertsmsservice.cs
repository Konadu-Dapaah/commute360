using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Commute360.Data;
using Commute360.Models;

namespace Commute360.Services;

// Texts the riders of a route when their driver sends an alert.
//
// Settings (all optional):
//   Sms:DriverAlerts           true/false, default true (needs a working SMS provider to actually send)
//   Sms:AlertCooldownMinutes   minimum gap between SMS broadcasts on one route, default 3 (emergencies ignore it)
//   Sms:MaxRecipients          safety cap per alert, default 300
public class AlertSmsService
{
    // When each route last had an SMS broadcast. Stops a driver pressing Send repeatedly and running up the bill.
    private static readonly ConcurrentDictionary<int, DateTime> LastSent = new();

    private readonly AppDbContext _context;
    private readonly ISmsSender _sms;
    private readonly IConfiguration _config;
    private readonly ILogger<AlertSmsService> _logger;

    public AlertSmsService(AppDbContext context, ISmsSender sms, IConfiguration config, ILogger<AlertSmsService> logger)
    {
        _context = context;
        _sms = sms;
        _config = config;
        _logger = logger;
    }

    private static bool RidesOn(string? days, string dayName) =>
        (days ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(dayName);

    // Returns how many phone numbers were texted (0 if nothing was sent).
    public async Task<int> SendDriverAlertAsync(DriverAlert alert)
    {
        try
        {
            if (!_config.GetValue("Sms:DriverAlerts", true)) return 0;

            var emergency = alert.Type == "Emergency";

            // Pause between ordinary alerts on the same route. Emergencies always go out.
            var cooldown = TimeSpan.FromMinutes(_config.GetValue("Sms:AlertCooldownMinutes", 3));
            if (!emergency && LastSent.TryGetValue(alert.RouteId, out var last) && DateTime.UtcNow - last < cooldown)
                return 0;

            var route = await _context.Routes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == alert.RouteId);
            if (route == null) return 0;

            // Only people expected on the bus today, and not people who skipped today
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var dayName = today.ToString("ddd", CultureInfo.InvariantCulture);

            var riders = await (
                from b in _context.Bookings.AsNoTracking()
                join u in _context.Users.AsNoTracking() on b.UserId equals u.Id
                where b.RouteId == alert.RouteId && b.Status == "Confirmed"
                      && u.PhoneNumber != null && u.PhoneNumber != ""
                select new { b.Id, b.Days, u.PhoneNumber }).ToListAsync();

            var skipped = await _context.SkippedRides.AsNoTracking()
                .Where(s => s.RideDate == today)
                .Select(s => s.BookingId)
                .ToListAsync();

            var max = _config.GetValue("Sms:MaxRecipients", 300);
            var numbers = riders
                .Where(r => RidesOn(r.Days, dayName) && !skipped.Contains(r.Id))
                .Select(r => r.PhoneNumber!)
                .Distinct()
                .Take(max)
                .ToList();

            if (numbers.Count == 0) return 0;

            await _sms.SendManyAsync(numbers, BuildText(alert.Type, alert.Message, route.Origin, route.Destination));
            LastSent[alert.RouteId] = DateTime.UtcNow;
            return numbers.Count;
        }
        catch (Exception ex)
        {
            // An SMS problem must never stop the alert from reaching riders in the app
            _logger.LogError(ex, "Could not send driver alert SMS for route {RouteId}", alert.RouteId);
            return 0;
        }
    }

    // One SMS segment is 160 characters, so keep it short. Plain text only (an arrow would force a shorter segment).
    private static string BuildText(string type, string message, string origin, string destination)
    {
        var label = type.ToUpperInvariant();
        var prefix = $"Commute360 {label} ({origin} to {destination}): ";
        if (160 - prefix.Length < 40) prefix = $"Commute360 {label}: ";

        var room = 160 - prefix.Length;
        var body = message.Length > room ? message.Substring(0, Math.Max(room - 3, 1)) + "..." : message;
        return prefix + body;
    }
}