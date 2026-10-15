using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Commute360.Data;
using Commute360.Models;

namespace Commute360.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Requires authenticated user
    public class AnnouncementsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AnnouncementsController(AppDbContext context)
        {
            _context = context;
        }

        private int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // POST: api/announcements  (admin only)
        // SendTo is "Everyone", or "Route:5" for the riders booked on route 5.
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateAnnouncement([FromBody] CreateAnnouncementDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Message))
                return BadRequest(new { message = "Message cannot be empty." });

            if (dto.Message.Length > 300)
                return BadRequest(new { message = "Message exceeds 300 characters limit." });

            string sendTo = "Everyone";
            if (dto.RouteId != null)
            {
                if (!await _context.Routes.AnyAsync(r => r.Id == dto.RouteId))
                    return NotFound(new { message = "Route not found." });
                sendTo = $"Route:{dto.RouteId}";
            }
            else if (!string.IsNullOrWhiteSpace(dto.SendTo))
            {
                sendTo = dto.SendTo.Trim();
            }

            var announcement = new Announcement
            {
                // UserId is a required link to the user table: it records who sent the announcement
                UserId = CurrentUserId,
                SendTo = sendTo,
                Message = dto.Message.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Announcements.Add(announcement);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Announcement sent successfully!", id = announcement.Id });
        }

        // GET: api/announcements  (what I should see: for everyone, or for my booked route, last 7 days)
        [HttpGet]
        public async Task<IActionResult> GetAnnouncements()
        {
            var me = CurrentUserId;
            int? myRouteId = await _context.Bookings
                .Where(b => b.UserId == me && b.Status == "Confirmed")
                .Select(b => (int?)b.RouteId)
                .FirstOrDefaultAsync();

            var mine = $"Route:{myRouteId}";
            var cutoff = DateTime.UtcNow.AddDays(-7);

            var announcements = await _context.Announcements
                .AsNoTracking()
                .Where(a => a.CreatedAt > cutoff &&
                            (a.SendTo == "Everyone" || (myRouteId != null && a.SendTo == mine)))
                .OrderByDescending(a => a.CreatedAt)
                .Take(10)
                .Select(a => new { a.Id, a.Message, a.CreatedAt })
                .ToListAsync();

            return Ok(announcements);
        }

        // GET: api/announcements/sent  (admin only: everything that was sent)
        [HttpGet("sent")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetSentAnnouncements()
        {
            var routeNames = await _context.Routes.AsNoTracking()
                .ToDictionaryAsync(r => r.Id, r => r.Origin + " to " + r.Destination);

            var raw = await _context.Announcements
                .AsNoTracking()
                .OrderByDescending(a => a.CreatedAt)
                .Take(100)
                .ToListAsync();

            var sent = raw.Select(a => new
            {
                a.Id,
                a.Message,
                a.CreatedAt,
                Route = Describe(a.SendTo, routeNames)
            });

            return Ok(sent);
        }

        // DELETE: api/announcements/5  (admin only)
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.Announcements.FirstOrDefaultAsync(a => a.Id == id);
            if (item == null) return NotFound();

            _context.Announcements.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static string Describe(string sendTo, Dictionary<int, string> routeNames)
        {
            if (sendTo.StartsWith("Route:") && int.TryParse(sendTo.Substring(6), out var id))
                return "Riders on " + (routeNames.GetValueOrDefault(id) ?? "a deleted route");
            return sendTo;
        }
    }

    public class CreateAnnouncementDto
    {
        public string SendTo { get; set; } = "Everyone";
        public string Message { get; set; } = string.Empty;
        public int? RouteId { get; set; }
    }
}