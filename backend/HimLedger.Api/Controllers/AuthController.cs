using HimLedger.Application.DTOs;
using HimLedger.Domain.Entities;
using HimLedger.Infrastructure;
using HimLedger.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HimLedger.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    ApplicationDbContext context,
    JwtTokenService jwtService,
    IConfiguration configuration) : ControllerBase
{
    private readonly PasswordHasher<User> _passwordHasher = new();

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuration["JwtSettings:Secret"]))
        {
            return Problem(
                statusCode: StatusCodes.Status501NotImplemented,
                title: "Not Implemented",
                detail: "Password login is disabled. Sign in with your organization's Entra ID account.");
        }

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

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId))
        {
            return Unauthorized();
        }
        var user = await context.Users
            .Where(item => item.UserId == userId)
            .Select(item => new
            {
                item.UserId,
                item.FirstName,
                item.LastName,
                item.Email,
                Role = item.Role.Name,
                item.DepartmentId
            })
            .SingleOrDefaultAsync(cancellationToken);
        return user is null ? Unauthorized() : Ok(user);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterUserDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuration["JwtSettings:Secret"]))
        {
            return Problem(
                statusCode: StatusCodes.Status501NotImplemented,
                title: "Not Implemented",
                detail: "Public registration is disabled. Ask an administrator to provision your account.");
        }

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

        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            RoleId = employeeRole.RoleId,
            DepartmentId = null
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "User registered successfully" });
    }
}