using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HimLedger.Api.Models;
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
    public async Task<IActionResult> GetUsers(
        CancellationToken cancellationToken,
        [FromQuery, Range(1, 21_474_836)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 25)
    {
        var users = await context.Users
            .OrderBy(user => user.LastName)
            .ThenBy(user => user.FirstName)
            .ThenBy(user => user.UserId)
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
            .ToPagedResponseAsync(page, pageSize, cancellationToken);

        return Ok(users);
    }

    [HttpPut("{id:int}/role")]
    public async Task<IActionResult> UpdateUserRole(
        int id,
        [FromBody] UpdateUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var adminId))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "The authenticated user identifier is invalid.");
        }

        if (id == adminId)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "You cannot change your own role.");
        }

        var user = await context.Users
            .Include(item => item.Role)
            .SingleOrDefaultAsync(item => item.UserId == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var role = await context.Roles.FindAsync([request.RoleId], cancellationToken);
        if (role is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Role does not exist.");
        }

        if (user.Role.Name == "Admin" && role.Name != "Admin"
            && await context.Users.CountAsync(item => item.Role.Name == "Admin", cancellationToken) <= 1)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: "The last Admin account cannot be demoted.");
        }

        user.RoleId = role.RoleId;
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        var roles = await context.Roles
            .OrderBy(role => role.Name)
            .Select(role => new { role.RoleId, role.Name })
            .ToListAsync(cancellationToken);

        return Ok(roles);
    }

    public sealed record UpdateUserRoleRequest
    {
        [Range(1, int.MaxValue)]
        public int RoleId { get; init; }
    }
}