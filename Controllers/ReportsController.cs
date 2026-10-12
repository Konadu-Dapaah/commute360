using Microsoft.AspNetCore.Authorization;
using Commute360.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace Commute360.Controllers
{
    [ApiController]
    [Route("api/reports")]
    [Authorize(Roles = "Admin")]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReportsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/reports/{type}?days=30
        [HttpGet("{type}")]
        public async Task<IActionResult> GetReport(string type, [FromQuery] int days = 30)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-days);

            return type.ToLower() switch
            {
                "utilisation" => Ok(new
                {
                    Title = $"Seat Utilisation by Weekday (Last {days} Days)",
                    Header = new[] { "Route", "Total Capacity", "Total Booked", "Utilisation Rate" },
                    Rows = await _context.Routes
                        .Select(r => new object[] {
                            $"{r.Origin} → {r.Destination}",
                            r.Capacity,
                            r.BookedRidersCount,
                            r.Capacity > 0 ? $"{Math.Round((double)r.BookedRidersCount / (r.Capacity * 5) * 100, 1)}%" : "0%"
                        }).ToListAsync()
                }),
                
                "stops" => Ok(new
                {
                    Title = $"Busiest Stops (Last {days} Days)",
                    Header = new[] { "Stop Name", "Route", "Total Boardings", "Total Drop-offs" },
                    Rows = await _context.Stops
                        .Select(s => new object[] {
                            s.Name,
                            $"{s.Route.Origin} → {s.Route.Destination}",
                            s.BoardingCount,
                            s.DropOffCount
                        }).ToListAsync()
                }),

                "attendance" => Ok(new
                {
                    Title = $"Attendance Tracking (Last {days} Days)",
                    Header = new[] { "Passenger Name", "Route", "Boardings", "Skips" },
                    Rows = await _context.Bookings
                        .Where(b => b.CreatedAt >= cutoffDate)
                        .Select(b => new object[] {
                            b.User.Name,
                            $"{b.Route.Origin} → {b.Route.Destination}",
                            b.BoardingsCount,
                            b.SkipsCount
                        }).ToListAsync()
                }),

                "riders" => Ok(new
                {
                    Title = $"All Registered Riders",
                    Header = new[] { "Name", "Email", "Role", "Joined Date" },
                    Rows = await _context.Users
                        .Where(u => u.Role == "Staff")
                        .Select(u => new object[] {
                            u.Name,
                            u.Email,
                            u.Role,
                            u.CreatedAt.ToString("yyyy-MM-dd")
                        }).ToListAsync()
                }),

                _ => BadRequest(new { message = "Invalid report type specified." })
            };
        }

        // GET: api/reports/{type}/csv?days=30
        [HttpGet("{type}/csv")]
        public async Task<IActionResult> DownloadCsv(string type, [FromQuery] int days = 30)
        {
            // Simple CSV export generator fallback
            var csvContent = "ReportType,Days,GeneratedAt\n" + $"{type},{days},{DateTime.UtcNow:yyyy-MM-dd}";
            var bytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
            return File(bytes, "text/csv", $"commute360-{type}-report.csv");
        }
    }
}