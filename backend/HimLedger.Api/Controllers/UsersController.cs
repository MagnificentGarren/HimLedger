using System.Security.Claims;
using HimLedger.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HimLedger.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await context.Users
            .OrderBy(user => user.LastName)
            .ThenBy(user => user.FirstName)
            .Select(user => new
            {
                user.UserId,
                user.FirstName,
                user.LastName,
                user.Email,
                user.RoleId,
                RoleName = user.Role.Name,
                user.DepartmentId,
                DepartmentName = user.Department == null ? null : user.Department.Name
            })
            .ToListAsync();
        var roles = await context.Roles.OrderBy(role => role.Name)
            .Select(role => new { role.RoleId, role.Name })
            .ToListAsync();

        return Ok(new { users, roles });
    }

    [HttpPut("{id:int}/role")]
    public async Task<IActionResult> UpdateUserRole(int id, [FromBody] UpdateUserRoleRequest request)
    {
        if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var adminId))
        {
            return Unauthorized();
        }

        if (id == adminId)
        {
            return BadRequest(new { message = "You cannot change your own role" });
        }

        var user = await context.Users.Include(item => item.Role).SingleOrDefaultAsync(item => item.UserId == id);
        if (user is null)
        {
            return NotFound();
        }

        var role = await context.Roles.FindAsync(request.RoleId);
        if (role is null)
        {
            return BadRequest(new { message = "Role does not exist" });
        }

        if (user.Role.Name == "Admin" && role.Name != "Admin"
            && await context.Users.CountAsync(item => item.Role.Name == "Admin") <= 1)
        {
            return Conflict(new { message = "The last Admin account cannot be demoted" });
        }

        user.RoleId = role.RoleId;
        await context.SaveChangesAsync();
        return NoContent();
    }

    public sealed record UpdateUserRoleRequest(int RoleId);
}