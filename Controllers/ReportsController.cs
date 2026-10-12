using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commute360.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        // GET: api/reports/utilisation?days=30
        [HttpGet("utilisation")]
        public IActionResult GetUtilisation([FromQuery] int days = 30)
        {
            // Returns placeholder structure so the dashboard loads cleanly
            return Ok(new
            {
                periodDays = days,
                averageUtilisation = 0,
                routes = new List<object>()
            });
        }
    }
}