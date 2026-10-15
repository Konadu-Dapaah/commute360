using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Commute360.Data;

namespace Commute360.Controllers;

public record HeadcountRow(
    int? RouteId,
    string Rider,
    string Route,
    string BoardingStop,
    string DropOffStop,
    DateTime? BoardedAt,
    DateTime? AlightedAt,
    int? MinutesOnBus,
    string Status);

// The admin's record of who boarded and who got off, day by day.
[ApiController]
[Route("api/headcount")]
[Authorize(Roles = "Admin")]
public class HeadcountController : ControllerBase
{
    private readonly AppDbContext _context;

    public HeadcountController(AppDbContext context)
    {
        _context = context;
    }

    private static bool RidesOn(string? days, string dayName) =>
        (days ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(dayName);

    // GET api/headcount?date=2026-10-15&routeId=1
    // Past dates list the trips that were recorded. Today also lists riders who have not boarded yet or skipped.
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? date, [FromQuery] int? routeId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var day = today;
        if (!string.IsNullOrWhiteSpace(date) &&
            !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
            return BadRequest("Use the date format yyyy-MM-dd.");

        var records = await _context.Boardings.AsNoTracking().Where(x => x.RideDate == day).ToListAsync();

        var bookingIds = records.Where(r => r.BookingId != null).Select(r => r.BookingId!.Value).ToList();
        var bookings = await _context.Bookings.AsNoTracking().Where(b => bookingIds.Contains(b.Id)).ToListAsync();
        var users = await _context.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name);
        var routeNames = await _context.Routes.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Origin + " to " + r.Destination);
        var stopNames = await _context.Stops.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name);

        var rows = new List<HeadcountRow>();

        foreach (var r in records)
        {
            var b = bookings.FirstOrDefault(x => x.Id == r.BookingId);
            int? rId = r.RouteId ?? b?.RouteId;

            // Use the copy saved on the day; fall back to live data for older records
            string rider = r.RiderName ?? (b != null ? users.GetValueOrDefault(b.UserId) : null) ?? "Deleted account";
            string route = r.RouteName ?? (rId != null ? routeNames.GetValueOrDefault(rId.Value) : null) ?? "Deleted route";
            string boardStop = r.BoardingStopName ?? (b != null ? stopNames.GetValueOrDefault(b.BoardingStopId) : null) ?? "-";
            string dropStop = r.DropOffStopName ?? (b != null ? stopNames.GetValueOrDefault(b.DropOffStopId) : null) ?? "-";
            int? minutes = r.AlightedAt != null ? (int)Math.Round((r.AlightedAt.Value - r.CreatedAt).TotalMinutes) : null;

            rows.Add(new HeadcountRow(rId, rider, route, boardStop, dropStop, r.CreatedAt, r.AlightedAt, minutes,
                r.AlightedAt != null ? "Trip completed" : "On the bus"));
        }

        if (routeId != null) rows = rows.Where(x => x.RouteId == routeId).ToList();

        // For today, also show who is expected but has not boarded, and who skipped
        if (day == today)
        {
            var dayName = day.ToString("ddd", CultureInfo.InvariantCulture);
            var expected = await _context.Bookings.AsNoTracking()
                .Where(b => b.Status == "Confirmed" && (routeId == null || b.RouteId == routeId))
                .ToListAsync();
            var skippedIds = await _context.SkippedRides.AsNoTracking()
                .Where(s => s.RideDate == day)
                .Select(s => s.BookingId)
                .ToListAsync();
            var boardedBookingIds = records.Where(r => r.BookingId != null).Select(r => r.BookingId!.Value).ToHashSet();

            foreach (var b in expected.Where(b => RidesOn(b.Days, dayName) && !boardedBookingIds.Contains(b.Id)))
            {
                rows.Add(new HeadcountRow(
                    b.RouteId,
                    users.GetValueOrDefault(b.UserId) ?? "Deleted account",
                    routeNames.GetValueOrDefault(b.RouteId) ?? "Deleted route",
                    stopNames.GetValueOrDefault(b.BoardingStopId) ?? "-",
                    stopNames.GetValueOrDefault(b.DropOffStopId) ?? "-",
                    null, null, null,
                    skippedIds.Contains(b.Id) ? "Skipped" : "Not boarded yet"));
            }
        }

        var summary = new
        {
            Boarded = rows.Count(x => x.BoardedAt != null),
            OnBus = rows.Count(x => x.Status == "On the bus"),
            Completed = rows.Count(x => x.Status == "Trip completed"),
            NotBoarded = rows.Count(x => x.Status == "Not boarded yet"),
            Skipped = rows.Count(x => x.Status == "Skipped")
        };

        return Ok(new
        {
            Date = day.ToString("yyyy-MM-dd"),
            Summary = summary,
            Rows = rows.OrderBy(x => x.BoardedAt == null).ThenBy(x => x.BoardedAt).ThenBy(x => x.Rider).ToList()
        });
    }
}