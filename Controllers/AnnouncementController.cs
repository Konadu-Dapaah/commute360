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

        // POST: api/announcements
        [HttpPost]
        public async Task<IActionResult> CreateAnnouncement([FromBody] CreateAnnouncementDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Message))
            {
                return BadRequest(new { message = "Message cannot be empty." });
            }

            if (dto.Message.Length > 300)
            {
                return BadRequest(new { message = "Message exceeds 300 characters limit." });
            }

            var announcement = new Announcement
            {
                SendTo = string.IsNullOrWhiteSpace(dto.SendTo) ? "Everyone" : dto.SendTo,
                Message = dto.Message,
                CreatedAt = DateTime.UtcNow
            };

            _context.Announcements.Add(announcement);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Announcement sent successfully!", id = announcement.Id });
        }

        // GET: api/announcements
        [HttpGet]
        public async Task<IActionResult> GetAnnouncements()
        {
            var announcements = await _context.Announcements
                .OrderByDescending(a => a.CreatedAt)
                .Take(10)
                .ToListAsync();

            return Ok(announcements);
        }

        // GET: api/announcements/sent
        [HttpGet("sent")]
        public async Task<IActionResult> GetSentAnnouncements()
        {
            var sent = await _context.Announcements
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return Ok(sent);
        }
    }

    public class CreateAnnouncementDto
    {
        public string SendTo { get; set; } = "Everyone";
        public string Message { get; set; } = string.Empty;
    }
}