using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Commute360.Data;
using Commute360.DTOs;
using Commute360.Models;

namespace Commute360.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoutesController : ControllerBase
{
    private readonly AppDbContext _context;

    public RoutesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetRoutes()
    {
        var routes = await _context.Routes
            .Include(r => r.Stops.OrderBy(s => s.Order))
            .ToListAsync();

        return Ok(routes);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetRoute(int id)
    {
        var route = await _context.Routes
            .Include(r => r.Stops.OrderBy(s => s.Order))
            .FirstOrDefaultAsync(r => r.Id == id);

        if (route == null) return NotFound();
        return Ok(route);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> CreateRoute([FromBody] CreateRouteDto dto)
    {
        if (dto.Capacity <= 0)
            return BadRequest("Capacity must be greater than zero.");

        if (string.IsNullOrWhiteSpace(dto.Origin) || string.IsNullOrWhiteSpace(dto.Destination))
            return BadRequest("Origin and destination are required.");

        var route = new Commute360.Models.Route
        {
            Origin = dto.Origin,
            Destination = dto.Destination,
            ScheduleTime = dto.ScheduleTime,
            Capacity = dto.Capacity
        };

        _context.Routes.Add(route);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetRoute), new { id = route.Id }, route);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id}/stops")]
    public async Task<IActionResult> AddStop(int id, [FromBody] CreateStopDto dto)
    {
        var route = await _context.Routes.FindAsync(id);
        if (route == null) return NotFound("Route not found.");

        var stop = new Stop
        {
            RouteId = id,
            Name = dto.Name,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Order = dto.Order
        };

        _context.Stops.Add(stop);
        await _context.SaveChangesAsync();

        return Ok(stop);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRoute(int id)
    {
        var route = await _context.Routes
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (route == null) return NotFound();

        var stopIds = route.Stops.Select(s => s.Id).ToList();

        // Remove any bookings referencing this route or its stops first —
        // the foreign keys are set to Restrict, so leaving them in place
        // causes PostgreSQL to reject the delete with a 500 error.
        var relatedBookings = await _context.Bookings
            .Where(b => b.RouteId == id
                     || stopIds.Contains(b.BoardingStopId)
                     || stopIds.Contains(b.DropOffStopId))
            .ToListAsync();

        _context.Bookings.RemoveRange(relatedBookings);
        _context.Stops.RemoveRange(route.Stops);
        _context.Routes.Remove(route);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Route deleted." });
    }
}