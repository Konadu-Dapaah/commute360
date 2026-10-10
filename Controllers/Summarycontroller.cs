using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Commute360.Data;

namespace Commute360.Controllers;

// Admins and drivers can see counts. Only admins can see who the riders are.
[ApiController]
[Route("api/summary")]
[Authorize(Roles = "Admin,Driver")]
public class SummaryController : ControllerBase
{
    private static readonly string[] WeekDays = { "Mon", "Tue", "Wed", "Thu", "Fri" };

    private readonly AppDbContext _context;

    public SummaryController(AppDbContext context)
    {
        _context = context;
    }

    // Every route with: riders booked, riders per weekday, and per-stop boarding / drop-off counts.
    [HttpGet("routes")]
    public async Task<IActionResult> GetRoutes()
    {
        var routes = await _context.Routes
            .AsNoTracking()
            .Include(r => r.Stops)
            .OrderBy(r => r.Id)
            .ToListAsync();

        var bookings = await _context.Bookings
            .AsNoTracking()
            .Where(b => b.Status == "Confirmed")
            .ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayName = today.ToString("ddd", CultureInfo.InvariantCulture);
        var skippedToday = await _context.SkippedRides
            .AsNoTracking()
            .Where(s => s.RideDate == today)
            .Select(s => s.BookingId)
            .ToListAsync();

        static bool RidesOn(string? days, string day) =>
            (days ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(day);

        var result = routes.Select(r =>
        {
            var routeBookings = bookings.Where(b => b.RouteId == r.Id).ToList();

            var days = WeekDays.ToDictionary(
                d => d,
                d => routeBookings.Count(b =>
                    (b.Days ?? "")
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Contains(d)));

            return new
            {
                r.Id,
                r.Origin,
                r.Destination,
                r.ScheduleTime,
                r.Capacity,
                r.DriverId,
                Riders = routeBookings.Count,
                ExpectedToday = routeBookings.Count(b => RidesOn(b.Days, todayName) && !skippedToday.Contains(b.Id)),
                SkippedToday = routeBookings.Count(b => RidesOn(b.Days, todayName) && skippedToday.Contains(b.Id)),
                Days = days,
                Stops = r.Stops
                    .OrderBy(s => s.Order)
                    .Select(s => new
                    {
                        s.Id,
                        s.Name,
                        s.Order,
                        s.Latitude,
                        s.Longitude,
                        Boarding = routeBookings.Count(b => b.BoardingStopId == s.Id),
                        DropOff = routeBookings.Count(b => b.DropOffStopId == s.Id)
                    })
            };
        });

        return Ok(result);
    }

    // Admin only: who is riding a given route.
    [HttpGet("routes/{routeId:int}/riders")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetRiders(int routeId)
    {
        var riders = await (
            from b in _context.Bookings.AsNoTracking()
            join u in _context.Users.AsNoTracking() on b.UserId equals u.Id
            where b.RouteId == routeId && b.Status == "Confirmed"
            orderby u.Name
            select new
            {
                u.Name,
                u.Email,
                BoardingStop = b.BoardingStop.Name,
                DropOffStop = b.DropOffStop.Name,
                b.Days
            }).ToListAsync();

        return Ok(riders);
    }

    // Admin only: every bus currently sending a position.
    [HttpGet("live")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetLive()
    {
        var buses = await (
            from l in _context.BusLocations.AsNoTracking()
            join r in _context.Routes.AsNoTracking() on l.RouteId equals r.Id
            select new
            {
                l.RouteId,
                r.Origin,
                r.Destination,
                l.Latitude,
                l.Longitude,
                l.SpeedKmh,
                l.UpdatedAt
            }).ToListAsync();

        return Ok(buses);
    }
}