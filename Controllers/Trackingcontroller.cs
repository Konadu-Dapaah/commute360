using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Commute360.Data;
using Commute360.DTOs;
using Commute360.Models;

namespace Commute360.Controllers;

[ApiController]
[Route("api/tracking")]
[Authorize]
public class TrackingController : ControllerBase
{
    private readonly AppDbContext _context;

    public TrackingController(AppDbContext context)
    {
        _context = context;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Drivers can see any route. Passengers can only see routes they have a confirmed booking on.
    private async Task<bool> CanViewRoute(int routeId)
    {
        if (User.IsInRole("Driver") || User.IsInRole("Admin")) return true;

        return await _context.Bookings.AnyAsync(b =>
            b.UserId == CurrentUserId &&
            b.RouteId == routeId &&
            b.Status == "Confirmed");
    }

    // Returns null when this driver may use the route, otherwise the error to send back.
    // A route assigned to a driver can only be driven by that driver; unassigned routes are open to any driver.
    private async Task<IActionResult?> CheckDriverRoute(int routeId)
    {
        var route = await _context.Routes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == routeId);
        if (route == null) return NotFound("Route not found.");
        if (route.DriverId != null && route.DriverId != CurrentUserId)
            return StatusCode(403, "This route is assigned to another driver.");
        return null;
    }

    // ---------- DRIVER: send my position ----------
    [HttpPost("location")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> UpdateLocation([FromBody] UpdateLocationDto dto)
    {
        var denied = await CheckDriverRoute(dto.RouteId);
        if (denied != null) return denied;

        var loc = await _context.BusLocations
            .FirstOrDefaultAsync(l => l.RouteId == dto.RouteId);

        // One bus per route: refuse if a different driver sent a position in the last minute
        if (loc != null &&
            loc.DriverId != CurrentUserId &&
            loc.UpdatedAt > DateTime.UtcNow.AddSeconds(-60))
        {
            return Conflict("Another driver is already broadcasting on this route.");
        }

        if (loc == null)
        {
            loc = new BusLocation { RouteId = dto.RouteId };
            _context.BusLocations.Add(loc);
        }

        loc.DriverId = CurrentUserId;
        loc.Latitude = dto.Latitude;
        loc.Longitude = dto.Longitude;
        loc.Heading = dto.Heading;
        loc.SpeedKmh = dto.SpeedKmh;
        loc.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ---------- DRIVER: end trip (removes the bus from the map) ----------
    [HttpDelete("routes/{routeId:int}/location")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> EndTrip(int routeId)
    {
        var loc = await _context.BusLocations
            .FirstOrDefaultAsync(l => l.RouteId == routeId && l.DriverId == CurrentUserId);

        if (loc != null)
        {
            _context.BusLocations.Remove(loc);
            await _context.SaveChangesAsync();
        }

        return NoContent();
    }

    // ---------- PASSENGER: where is my bus? ----------
    [HttpGet("routes/{routeId:int}/location")]
    public async Task<IActionResult> GetLocation(int routeId)
    {
        if (!await CanViewRoute(routeId)) return Forbid();

        var loc = await _context.BusLocations
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.RouteId == routeId);

        // 204 = no bus on the road right now (not an error)
        if (loc == null) return NoContent();

        return Ok(new
        {
            loc.Latitude,
            loc.Longitude,
            loc.Heading,
            loc.SpeedKmh,
            loc.UpdatedAt
        });
    }

    // ---------- DRIVER: send an alert to riders ----------
    [HttpPost("alerts")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> CreateAlert([FromBody] CreateAlertDto dto)
    {
        var denied = await CheckDriverRoute(dto.RouteId);
        if (denied != null) return denied;

        var alert = new DriverAlert
        {
            RouteId = dto.RouteId,
            DriverId = CurrentUserId,
            Type = dto.Type,
            Message = dto.Message.Trim()
        };

        _context.DriverAlerts.Add(alert);
        await _context.SaveChangesAsync();

        return Ok(new { alert.Id, alert.Type, alert.Message, alert.CreatedAt });
    }

    // ---------- PASSENGER: alerts for my route (last 2 hours) ----------
    // Pass sinceId to receive only alerts newer than the last one you have.
    [HttpGet("routes/{routeId:int}/alerts")]
    public async Task<IActionResult> GetAlerts(int routeId, [FromQuery] int sinceId = 0)
    {
        if (!await CanViewRoute(routeId)) return Forbid();

        var cutoff = DateTime.UtcNow.AddHours(-2);

        var alerts = await _context.DriverAlerts
            .AsNoTracking()
            .Where(a => a.RouteId == routeId && a.Id > sinceId && a.CreatedAt > cutoff)
            .OrderBy(a => a.Id)
            .Take(50)
            .Select(a => new { a.Id, a.Type, a.Message, a.CreatedAt })
            .ToListAsync();

        return Ok(alerts);
    }
}