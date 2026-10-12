using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Commute360.Data;
using Commute360.DTOs;
using Commute360.Models;

namespace Commute360.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;

    public BookingsController(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingDto dto)
    {
        // 1. Optional email verification check
        if (_config.GetValue<bool>("Email:RequireVerifiedToBook"))
        {
            var verified = await _context.Users
                .Where(u => u.Id == CurrentUserId)
                .Select(u => u.EmailVerified)
                .FirstOrDefaultAsync();

            if (!verified)
                return StatusCode(403, new { message = "Please verify your email before booking." });
        }

        // 2. Validate selected days
        if (dto.Days == null || dto.Days.Count == 0)
            return BadRequest(new { message = "Pick at least one riding day." });

        // 3. Find route with associated stops
        var route = await _context.Routes
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(r => r.Id == dto.RouteId);

        if (route == null) 
            return NotFound(new { message = "Route not found." });

        // 4. Validate stop selections
        bool boardingValid = route.Stops.Any(s => s.Id == dto.BoardingStopId);
        bool dropOffValid = route.Stops.Any(s => s.Id == dto.DropOffStopId);

        if (!boardingValid || !dropOffValid)
            return BadRequest(new { message = "Boarding or drop-off stop does not belong to this route." });

        if (dto.BoardingStopId == dto.DropOffStopId)
            return BadRequest(new { message = "Boarding and drop-off stops must be different." });

        // 5. Check capacity (excluding seats held by current user)
        int confirmedCount = await _context.Bookings
            .CountAsync(b => b.RouteId == dto.RouteId && b.Status == "Confirmed" && b.UserId != CurrentUserId);

        if (confirmedCount >= route.Capacity)
            return BadRequest(new { message = "This route is fully booked." });

        // 6. Replace any active existing booking for this user
        var existingBookings = await _context.Bookings
            .Where(b => b.UserId == CurrentUserId && b.Status == "Confirmed")
            .ToListAsync();

        foreach (var existing in existingBookings)
            existing.Status = "Cancelled";

        // 7. Save booking
        var booking = new Booking
        {
            UserId = CurrentUserId,
            RouteId = dto.RouteId,
            BoardingStopId = dto.BoardingStopId,
            DropOffStopId = dto.DropOffStopId,
            Days = string.Join(",", dto.Days),
            Status = "Confirmed",
            CreatedAt = DateTime.UtcNow
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        return Ok(booking);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMyBookings()
    {
        var bookings = await _context.Bookings
            .Include(b => b.Route)
            .Include(b => b.BoardingStop)
            .Include(b => b.DropOffStop)
            .Where(b => b.UserId == CurrentUserId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return Ok(bookings);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == id && b.UserId == CurrentUserId && b.Status == "Confirmed");

        if (booking == null) 
            return NotFound(new { message = "Booking not found." });

        booking.Status = "Cancelled";
        await _context.SaveChangesAsync();

        return NoContent();
    }
}