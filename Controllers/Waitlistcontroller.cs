using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Commute360.Data;
using Commute360.DTOs;
using Commute360.Models;

namespace Commute360.Controllers;

[ApiController]
[Route("api/waitlist")]
[Authorize]
public class WaitlistController : ControllerBase
{
    private readonly AppDbContext _context;

    public WaitlistController(AppDbContext context)
    {
        _context = context;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Same body as a booking request. Only allowed when the route is actually full.
    [HttpPost]
    public async Task<IActionResult> Join([FromBody] CreateBookingDto dto)
    {
        if (dto.Days == null || dto.Days.Count == 0)
            return BadRequest("Pick at least one riding day.");

        var route = await _context.Routes
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(r => r.Id == dto.RouteId);
        if (route == null) return NotFound("Route not found.");

        if (!route.Stops.Any(s => s.Id == dto.BoardingStopId) ||
            !route.Stops.Any(s => s.Id == dto.DropOffStopId))
            return BadRequest("Boarding or drop-off stop does not belong to this route.");

        if (dto.BoardingStopId == dto.DropOffStopId)
            return BadRequest("Boarding and drop-off stops must be different.");

        int taken = await _context.Bookings
            .CountAsync(b => b.RouteId == dto.RouteId && b.Status == "Confirmed" && b.UserId != CurrentUserId);
        if (taken < route.Capacity)
            return BadRequest("This route still has seats. Book it directly.");

        var existing = await _context.WaitlistEntries
            .FirstOrDefaultAsync(w => w.UserId == CurrentUserId && w.RouteId == dto.RouteId && w.Status == "Waiting");

        if (existing == null)
        {
            existing = new WaitlistEntry
            {
                UserId = CurrentUserId,
                RouteId = dto.RouteId,
                BoardingStopId = dto.BoardingStopId,
                DropOffStopId = dto.DropOffStopId,
                Days = string.Join(",", dto.Days)
            };
            _context.WaitlistEntries.Add(existing);
        }
        else
        {
            // Joining again just updates their choices; they keep their place in the queue
            existing.BoardingStopId = dto.BoardingStopId;
            existing.DropOffStopId = dto.DropOffStopId;
            existing.Days = string.Join(",", dto.Days);
        }

        await _context.SaveChangesAsync();

        int position = await _context.WaitlistEntries.CountAsync(w =>
            w.RouteId == dto.RouteId && w.Status == "Waiting" && w.CreatedAt <= existing.CreatedAt);

        return Ok(new { existing.Id, Position = position });
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        var mine = await (
            from w in _context.WaitlistEntries.AsNoTracking()
            join r in _context.Routes.AsNoTracking() on w.RouteId equals r.Id
            where w.UserId == CurrentUserId && w.Status == "Waiting"
            select new
            {
                w.Id,
                w.RouteId,
                r.Origin,
                r.Destination,
                w.Days,
                w.CreatedAt,
                Position = _context.WaitlistEntries.Count(x =>
                    x.RouteId == w.RouteId && x.Status == "Waiting" && x.CreatedAt <= w.CreatedAt)
            }).ToListAsync();

        return Ok(mine);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Leave(int id)
    {
        var entry = await _context.WaitlistEntries
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == CurrentUserId && w.Status == "Waiting");
        if (entry == null) return NotFound();

        entry.Status = "Cancelled";
        await _context.SaveChangesAsync();
        return NoContent();
    }
}