using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Commute360.Data;

namespace Commute360.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly AppDbContext _context;

    public MeController(AppDbContext context)
    {
        _context = context;
    }

    // Used by the frontend after login to decide which dashboard to open.
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return Unauthorized();

        // The role comes from the token, because that is what the server
        // enforces on every other endpoint. This keeps redirects and 403s consistent.
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrEmpty(role)) role = "Staff";

        return Ok(new { user.Id, user.Name, user.Email, Role = role, user.EmailVerified });
    }
}