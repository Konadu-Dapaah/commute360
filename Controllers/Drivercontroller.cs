using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Commute360.Data;
using Commute360.Models;

namespace Commute360.Controllers;

public class BoardRiderDto
{
    public int BookingId { get; set; }
    public bool Boarded { get; set; }
}

[ApiController]
[Route("api/driver")]
[Authorize(Roles = "Driver")]
public class DriverController : ControllerBase
{
    private readonly AppDbContext _context;

    public DriverController(AppDbContext context)
    {
        _context = context;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Routes this driver may drive: assigned to them, or not assigned to anyone yet.
    [HttpGet("routes")]
    public async Task<IActionResult> MyRoutes()
    {
        var routes = await _context.Routes
            .AsNoTracking()
            .Where(r => r.DriverId == null || r.DriverId == CurrentUserId)
            .OrderBy(r => r.Id)
            .Select(r => new
            {
                r.Id,
                r.Origin,
                r.Destination,
                r.ScheduleTime,
                Assigned = r.DriverId == CurrentUserId
            })
            .ToListAsync();

        return Ok(routes);
    }

    private async Task<bool> MayDrive(int routeId) =>
        await _context.Routes.AnyAsync(r => r.Id == routeId && (r.DriverId == null || r.DriverId == CurrentUserId));

    private static bool Rides(string? days, string dayName) =>
        (days ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(dayName);

    // Today's list of expected riders for a route (people who skipped today are left out).
    [HttpGet("routes/{routeId:int}/manifest")]
    public async Task<IActionResult> Manifest(int routeId)
    {
        if (!await MayDrive(routeId)) return StatusCode(403, "This route is assigned to another driver.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dayName = today.ToString("ddd", CultureInfo.InvariantCulture);

        var bookings = await (
            from b in _context.Bookings.AsNoTracking()
            join u in _context.Users.AsNoTracking() on b.UserId equals u.Id
            where b.RouteId == routeId && b.Status == "Confirmed"
            orderby u.Name
            select new
            {
                BookingId = b.Id,
                u.Name,
                b.Days,
                BoardingStop = b.BoardingStop != null ? b.BoardingStop.Name : string.Empty,
                DropOffStop = b.DropOffStop != null ? b.DropOffStop.Name : string.Empty
            }).ToListAsync();

        var todays = bookings.Where(b => Rides(b.Days, dayName)).ToList();
        var ids = todays.Select(b => b.BookingId).ToList();

        var skipped = await _context.SkippedRides.AsNoTracking()
            .Where(s => s.RideDate == today && ids.Contains(s.BookingId))
            .Select(s => s.BookingId).ToListAsync();

        var boarded = await _context.Boardings.AsNoTracking()
            .Where(x => x.RideDate == today && ids.Contains(x.BookingId))
            .Select(x => x.BookingId).ToListAsync();

        var riders = todays
            .Where(b => !skipped.Contains(b.BookingId))
            .Select(b => new
            {
                b.BookingId,
                b.Name,
                b.BoardingStop,
                b.DropOffStop,
                Boarded = boarded.Contains(b.BookingId)
            })
            .ToList();

        return Ok(new
        {
            Date = today.ToString("yyyy-MM-dd"),
            Skipped = skipped.Count,
            Riders = riders
        });
    }

    [HttpPost("boardings")]
    public async Task<IActionResult> SetBoarded([FromBody] BoardRiderDto dto)
    {
        var booking = await _context.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == dto.BookingId && b.Status == "Confirmed");
        if (booking == null) return NotFound("Booking not found.");

        if (!await MayDrive(booking.RouteId)) return StatusCode(403, "This route is assigned to another driver.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dayName = today.ToString("ddd", CultureInfo.InvariantCulture);
        if (!Rides(booking.Days, dayName)) return BadRequest("This rider isn't booked for today.");

        var record = await _context.Boardings
            .FirstOrDefaultAsync(x => x.BookingId == dto.BookingId && x.RideDate == today);

        if (dto.Boarded && record == null)
        {
            _context.Boardings.Add(new Boarding { BookingId = dto.BookingId, RideDate = today, DriverId = CurrentUserId });
        }
        else if (!dto.Boarded && record != null)
        {
            _context.Boardings.Remove(record);
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }
}