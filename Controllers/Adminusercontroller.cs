using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Commute360.Data;

namespace Commute360.Controllers;

public class SetRoleDto
{
    [Required]
    [RegularExpression("^(Staff|Driver|Admin)$", ErrorMessage = "Role must be Staff, Driver or Admin.")]
    public string Role { get; set; } = "";
}

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminUsersController(AppDbContext context)
    {
        _context = context;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _context.Users
            .AsNoTracking()
            .OrderBy(u => u.Name)
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.Email,
                Role = u.Role == "" ? "Staff" : u.Role
            })
            .ToListAsync();

        return Ok(users);
    }

    // The change takes effect the next time that person logs in (the role lives in their token).
    [HttpPut("{id:int}/role")]
    public async Task<IActionResult> SetRole(int id, [FromBody] SetRoleDto dto)
    {
        if (id == CurrentUserId)
            return BadRequest("You can't change your own role.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound("User not found.");

        user.Role = dto.Role;
        await _context.SaveChangesAsync();

        return NoContent();
    }
    // Delete an account. Their bookings, skips, waitlist entries, tokens and announcements go with them
    // (the database cascades those). Routes they drove stay, just without a driver.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        if (id == CurrentUserId)
            return BadRequest("You can't delete your own account.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound("User not found.");

        if (user.Role == "Admin" && !await _context.Users.AnyAsync(u => u.Role == "Admin" && u.Id != id))
            return BadRequest("You can't delete the last admin.");

        await using var tx = await _context.Database.BeginTransactionAsync();

        // Routes keep existing; they just lose this driver
        var routes = await _context.Routes.Where(r => r.DriverId == id).ToListAsync();
        foreach (var route in routes) route.DriverId = null;

        // Keep the headcount history, but forget which driver ticked it
        await _context.Boardings
            .Where(b => b.DriverId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.DriverId, (int?)null));

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        return NoContent();
    }
}