using HimLedger.Application.DTOs;
using HimLedger.Domain.Entities;
using HimLedger.Infrastructure;
using HimLedger.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HimLedger.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    ApplicationDbContext context,
    JwtTokenService jwtService) : ControllerBase
{
    private readonly PasswordHasher<User> _passwordHasher = new();

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .Include(user => user.Role)
            .FirstOrDefaultAsync(user => user.Email == request.Email, cancellationToken);

        if (user is null)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "Invalid email or password.");
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            if (user.PasswordHash != request.Password)
            {
                return Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Unauthorized",
                    detail: "Invalid email or password.");
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            await context.SaveChangesAsync(cancellationToken);
        }
        else if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            await context.SaveChangesAsync(cancellationToken);
        }

        var token = jwtService.GenerateToken(user);
        return Ok(new AuthResponseDto(
            user.UserId,
            user.FirstName,
            user.LastName,
            user.Email,
            user.Role?.Name ?? "Employee",
            token));
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterUserDto request, CancellationToken cancellationToken)
    {
        if (await context.Users.AnyAsync(user => user.Email == request.Email, cancellationToken))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Email is already registered.");
        }

        var employeeRole = await context.Roles.SingleOrDefaultAsync(
            role => role.Name == "Employee",
            cancellationToken);
        if (employeeRole is null)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: "Employee role is not configured.");
        }

        if (request.RoleId != employeeRole.RoleId)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Public registration is restricted to the Employee role.");
        }

        if (request.DepartmentId is int departmentId &&
            !await context.Departments.AnyAsync(
                department => department.DepartmentId == departmentId,
                cancellationToken))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Department does not exist.");
        }

        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            RoleId = employeeRole.RoleId,
            DepartmentId = request.DepartmentId
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "User registered successfully" });
    }
}