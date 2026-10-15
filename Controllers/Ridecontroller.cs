using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Commute360.Data;
using Commute360.Models;

namespace Commute360.Controllers;

public class SkipRideDto
{
    [Required] public string Date { get; set; } = "";   // yyyy-MM-dd
}

[ApiController]
[Route("api/rides")]
[Authorize]
public class RidesController : ControllerBase
{
    private readonly AppDbContext _context;

    public RidesController(AppDbContext context)
    {
        _context = context;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static bool TryDate(string text, out DateOnly date) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private Task<Booking?> MyBooking() =>
        _context.Bookings
            .Include(b => b.Route)
            .FirstOrDefaultAsync(b => b.UserId == CurrentUserId && b.Status == "Confirmed");

    // GET: api/rides/alerts — Fetches driver alerts for staff user's booked route
    [HttpGet("alerts")]
    public async Task<IActionResult> GetMyRouteAlerts()
    {
        var booking = await MyBooking();
        if (booking == null) return Ok(new List<object>());

        var cutoff = DateTime.UtcNow.AddHours(-12);

        var alerts = await _context.DriverAlerts
            .AsNoTracking()
            .Where(a => a.RouteId == booking.RouteId && a.CreatedAt >= cutoff)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                a.Type,
                a.Message,
                a.CreatedAt
            })
            .ToListAsync();

        return Ok(alerts);
    }

    // Upcoming dates marked as skipped
    [HttpGet("skips")]
    public async Task<IActionResult> MySkips()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var dates = await (
            from s in _context.SkippedRides.AsNoTracking()
            join b in _context.Bookings.AsNoTracking() on s.BookingId equals b.Id
            where b.UserId == CurrentUserId && b.Status == "Confirmed" && s.RideDate >= today
            orderby s.RideDate
            select s.RideDate).ToListAsync();

        return Ok(dates.Select(d => d.ToString("yyyy-MM-dd")));
    }

    [HttpPost("skip")]
    public async Task<IActionResult> Skip([FromBody] SkipRideDto dto)
    {
        if (!TryDate(dto.Date, out var date)) return BadRequest("Use the format yyyy-MM-dd.");

        var booking = await MyBooking();
        if (booking == null) return BadRequest("You don't have an active booking.");

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        if (date < today) return BadRequest("You can't skip a day that has already passed.");
        if (date > today.AddDays(30)) return BadRequest("You can only skip days within the next 30 days.");

        var dayName = date.ToString("ddd", CultureInfo.InvariantCulture);
        var rideDays = (booking.Days ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!rideDays.Contains(dayName))
            return BadRequest($"You don't ride on {dayName}.");

        if (date == today && booking.Route != null && now.TimeOfDay >= booking.Route.ScheduleTime.TimeOfDay)
            return BadRequest("The bus has already left today.");

        bool already = await _context.SkippedRides
            .AnyAsync(s => s.BookingId == booking.Id && s.RideDate == date);
        if (!already)
        {
            _context.SkippedRides.Add(new SkippedRide { BookingId = booking.Id, RideDate = date });
            await _context.SaveChangesAsync();
        }

        return NoContent();
    }

    [HttpDelete("skip")]
    public async Task<IActionResult> Unskip([FromQuery] string date)
    {
        if (!TryDate(date, out var rideDate)) return BadRequest("Use the format yyyy-MM-dd.");

        var booking = await MyBooking();
        if (booking == null) return NoContent();

        var skip = await _context.SkippedRides
            .FirstOrDefaultAsync(s => s.BookingId == booking.Id && s.RideDate == rideDate);

        if (skip != null)
        {
            _context.SkippedRides.Remove(skip);
            await _context.SaveChangesAsync();
        }

        return NoContent();
    }
    // GET: api/rides/track
[HttpGet("track")]
public async Task<IActionResult> TrackMyBus()
{
    var booking = await MyBooking();
    if (booking == null) return BadRequest("You don't have an active booking.");

    var location = await _context.BusLocations
        .AsNoTracking()
        .FirstOrDefaultAsync(l => l.RouteId == booking.RouteId);

    var alerts = await _context.DriverAlerts
        .AsNoTracking()
        .Where(a => a.RouteId == booking.RouteId && a.CreatedAt >= DateTime.UtcNow.AddHours(-12))
        .OrderByDescending(a => a.CreatedAt)
        .ToListAsync();

    return Ok(new
    {
        Route = new
        {
            booking.Route.Id,
            
            booking.Route.Origin,
            booking.Route.Destination
        },
        BoardingStop = booking.BoardingStop != null ? new { booking.BoardingStop.Name, booking.BoardingStop.Latitude, booking.BoardingStop.Longitude } : null,
        DropOffStop = booking.DropOffStop != null ? new { booking.DropOffStop.Name, booking.DropOffStop.Latitude, booking.DropOffStop.Longitude } : null,
        BusLocation = location != null ? new { location.Latitude, location.Longitude, location.UpdatedAt } : null,
        Alerts = alerts
    });
}
    // ---------- Check-in: riders mark themselves as boarded or got off (feeds the headcount) ----------

    private static bool RidesOnDay(string? days, string dayName) =>
        (days ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(dayName);

    private async Task<object> BuildTodayAsync(Booking? booking)
    {
        if (booking == null) return new { HasBooking = false };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dayName = today.ToString("ddd", CultureInfo.InvariantCulture);

        var skipped = await _context.SkippedRides.AnyAsync(s => s.BookingId == booking.Id && s.RideDate == today);
        var record = await _context.Boardings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.BookingId == booking.Id && x.RideDate == today);

        return new
        {
            HasBooking = true,
            booking.RouteId,
            RidingToday = RidesOnDay(booking.Days, dayName),
            Skipped = skipped,
            Boarded = record != null,
            BoardedAt = record?.CreatedAt,
            Alighted = record?.AlightedAt != null,
            AlightedAt = record?.AlightedAt
        };
    }

    // GET api/rides/today: where am I in today's ride?
    [HttpGet("today")]
    public async Task<IActionResult> Today() => Ok(await BuildTodayAsync(await MyBooking()));

    // POST api/rides/board: "I've boarded"
    [HttpPost("board")]
    public async Task<IActionResult> Board()
    {
        var booking = await MyBooking();
        if (booking == null) return BadRequest("You don't have an active booking.");

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var dayName = today.ToString("ddd", CultureInfo.InvariantCulture);

        if (!RidesOnDay(booking.Days, dayName)) return BadRequest("You aren't booked on this route today.");

        if (await _context.SkippedRides.AnyAsync(s => s.BookingId == booking.Id && s.RideDate == today))
            return BadRequest("You marked today as skipped. Undo the skip first if you are riding.");

        // Boarding opens one hour before departure, so nobody ticks it hours early
        if (booking.Route != null && now.TimeOfDay < booking.Route.ScheduleTime.TimeOfDay - TimeSpan.FromHours(1))
            return BadRequest("Boarding opens one hour before departure.");

        var exists = await _context.Boardings.AnyAsync(x => x.BookingId == booking.Id && x.RideDate == today);
        if (!exists)
        {
            // Save a copy of the details, so the admin's history survives later changes or deletions
            var riderName = await _context.Users.Where(u => u.Id == CurrentUserId).Select(u => u.Name).FirstOrDefaultAsync();
            var stopNames = await _context.Stops
                .Where(s => s.Id == booking.BoardingStopId || s.Id == booking.DropOffStopId)
                .ToDictionaryAsync(s => s.Id, s => s.Name);

            _context.Boardings.Add(new Boarding
            {
                BookingId = booking.Id,
                RideDate = today,
                DriverId = null,
                Source = "Rider",
                RouteId = booking.RouteId,
                RiderName = riderName,
                RouteName = booking.Route != null ? $"{booking.Route.Origin} to {booking.Route.Destination}" : null,
                BoardingStopName = stopNames.GetValueOrDefault(booking.BoardingStopId),
                DropOffStopName = stopNames.GetValueOrDefault(booking.DropOffStopId)
            });
            await _context.SaveChangesAsync();
        }

        return Ok(await BuildTodayAsync(booking));
    }

    // POST api/rides/alight: "I've got off"
    [HttpPost("alight")]
    public async Task<IActionResult> Alight()
    {
        var booking = await MyBooking();
        if (booking == null) return BadRequest("You don't have an active booking.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var record = await _context.Boardings.FirstOrDefaultAsync(x => x.BookingId == booking.Id && x.RideDate == today);
        if (record == null) return BadRequest("Mark yourself as boarded first.");

        if (record.AlightedAt == null)
        {
            record.AlightedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return Ok(await BuildTodayAsync(booking));
    }

    // DELETE api/rides/board: undo a mistaken "boarded" (also clears "got off")
    [HttpDelete("board")]
    public async Task<IActionResult> UndoBoard()
    {
        var booking = await MyBooking();
        if (booking == null) return BadRequest("You don't have an active booking.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var record = await _context.Boardings.FirstOrDefaultAsync(x => x.BookingId == booking.Id && x.RideDate == today);
        if (record != null && record.AlightedAt != null)
            return BadRequest("Your trip is complete and has been saved, so it can't be undone.");

        if (record != null)
        {
            _context.Boardings.Remove(record);
            await _context.SaveChangesAsync();
        }

        return Ok(await BuildTodayAsync(booking));
    }
}