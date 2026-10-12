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
}
