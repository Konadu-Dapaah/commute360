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
}