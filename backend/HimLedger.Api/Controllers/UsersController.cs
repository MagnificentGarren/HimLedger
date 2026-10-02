using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HimLedger.Api.Models;
using HimLedger.Infrastructure;
using Microsoft.Data.SqlClient;
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
                DepartmentName = user.Department == null ? null : user.Department.Name,
                user.EntraTenantId,
                user.EntraObjectId
            })
            .ToPagedResponseAsync(page, pageSize, cancellationToken);

        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        if (await context.Users.AnyAsync(item => item.Email == email, cancellationToken))
        {
            return Conflict("An account with that email already exists.");
        }
        var role = await context.Roles.SingleOrDefaultAsync(item => item.RoleId == request.RoleId, cancellationToken);
        if (role is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Role does not exist.");
        }
        if ((role.Name is "Employee" or "Manager") && request.DepartmentId is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Employee and Manager accounts must be assigned to a department.");
        }
        if (request.DepartmentId is int departmentId
            && !await context.Departments.AnyAsync(item => item.DepartmentId == departmentId, cancellationToken))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Department does not exist.");
        }

        var user = new HimLedger.Domain.Entities.User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PasswordHash = string.Empty,
            RoleId = role.RoleId,
            DepartmentId = request.DepartmentId
        };
        context.Users.Add(user);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            return Conflict("An account with that email already exists.");
        }

        return Created("/api/Users", new { user.UserId, user.FirstName, user.LastName, user.Email, user.RoleId, user.DepartmentId });
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
        if ((role.Name is "Employee" or "Manager") && user.DepartmentId is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Assign a department before assigning the Employee or Manager role.");
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

    [HttpPut("{id:int}/department")]
    public async Task<IActionResult> UpdateUserDepartment(
        int id,
        [FromBody] UpdateUserDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .Include(item => item.Role)
            .SingleOrDefaultAsync(item => item.UserId == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }
        if (request.DepartmentId is null && (user.Role.Name is "Employee" or "Manager"))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Employee and Manager accounts must remain assigned to a department.");
        }
        if (request.DepartmentId is int departmentId
            && !await context.Departments.AnyAsync(item => item.DepartmentId == departmentId, cancellationToken))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Department does not exist.");
        }

        user.DepartmentId = request.DepartmentId;
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:int}/entra-identity")]
    public async Task<IActionResult> UpdateEntraIdentity(
        int id,
        [FromBody] UpdateEntraIdentityRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.TenantId, out var tenantId)
            || !Guid.TryParse(request.ObjectId, out var objectId))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "TenantId and ObjectId must be valid GUIDs.");
        }

        var user = await context.Users.SingleOrDefaultAsync(item => item.UserId == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }
        var normalizedTenantId = tenantId.ToString("D");
        var normalizedObjectId = objectId.ToString("D");
        if (await context.Users.AnyAsync(
                item => item.UserId != id
                    && item.EntraTenantId == normalizedTenantId
                    && item.EntraObjectId == normalizedObjectId,
                cancellationToken))
        {
            return Conflict("That Entra identity is already assigned to another internal account.");
        }

        user.EntraTenantId = normalizedTenantId;
        user.EntraObjectId = normalizedObjectId;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            return Conflict("That Entra identity is already assigned to another internal account.");
        }
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

    public sealed record CreateUserRequest
    {
        [Required, MaxLength(50)]
        public required string FirstName { get; init; }

        [Required, MaxLength(50)]
        public required string LastName { get; init; }

        [Required, EmailAddress, MaxLength(100)]
        public required string Email { get; init; }

        [Range(1, int.MaxValue)]
        public int RoleId { get; init; }

        [Range(1, int.MaxValue)]
        public int? DepartmentId { get; init; }
    }

    public sealed record UpdateUserDepartmentRequest
    {
        [Range(1, int.MaxValue)]
        public int? DepartmentId { get; init; }
    }

    public sealed record UpdateEntraIdentityRequest
    {
        [Required]
        public required string TenantId { get; init; }

        [Required]
        public required string ObjectId { get; init; }
    }
}