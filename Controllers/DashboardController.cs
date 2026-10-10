using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Commute360.Data;

namespace Commute360.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        var routes = await _context.Routes
            .Include(r => r.Stops.OrderBy(s => s.Order))
            .ToListAsync();

        var myBooking = await _context.Bookings
            .Include(b => b.Route)
            .Include(b => b.BoardingStop)
            .Include(b => b.DropOffStop)
            .Where(b => b.UserId == CurrentUserId && b.Status == "Confirmed")
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync();

        return Ok(new { routes, myBooking });
    }
}