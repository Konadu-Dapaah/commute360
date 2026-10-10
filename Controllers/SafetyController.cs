using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commute360.Controllers
{
    [ApiController]
    [Route("api/safety")]
    [Authorize]
    public class SafetyController : ControllerBase
    {
        // GET: api/safety/sos/active
        [HttpGet("sos/active")]
        public IActionResult GetActiveSos()
        {
            // Returns an empty list so the frontend stops throwing 404 errors
            return Ok(new List<object>());
        }
    }
}