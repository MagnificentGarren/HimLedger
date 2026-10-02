using System.ComponentModel.DataAnnotations;
using HimLedger.Api.Models;
using HimLedger.Infrastructure;
using HimLedger.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HimLedger.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DepartmentsController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDepartments(
        CancellationToken cancellationToken,
        [FromQuery, Range(1, 21_474_836)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 25)
    {
        var departmentsQuery = context.Departments.AsQueryable();
        if (User.IsInRole("Employee") || User.IsInRole("Manager"))
        {
            if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId))
            {
                return Unauthorized();
            }
            var departmentId = await context.Users
                .Where(user => user.UserId == userId)
                .Select(user => user.DepartmentId)
                .SingleOrDefaultAsync(cancellationToken);
            departmentsQuery = departmentId is int assignedDepartmentId
                ? departmentsQuery.Where(department => department.DepartmentId == assignedDepartmentId)
                : departmentsQuery.Where(_ => false);
        }
        else if (!User.IsInRole("Admin") && !User.IsInRole("Finance"))
        {
            return Forbid();
        }

        var departments = await departmentsQuery
            .OrderBy(department => department.Name)
            .ThenBy(department => department.DepartmentId)
            .Select(department => new
            {
                department.DepartmentId,
                department.Name,
                department.Code
            })
            .ToPagedResponseAsync(page, pageSize, cancellationToken);

        return Ok(departments);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateDepartment(
        [FromBody] DepartmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValid(request))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Department name and code are required and must fit their limits.");
        }

        var name = request.Name.Trim();
        var code = request.Code.Trim().ToUpperInvariant();
        if (await context.Departments.AnyAsync(
                department => department.Name == name || department.Code == code,
                cancellationToken))
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: "Department name or code already exists.");
        }

        var department = new Department { Name = name, Code = code };
        context.Departments.Add(department);
        await context.SaveChangesAsync(cancellationToken);
        return Created("/api/Departments", new
        {
            department.DepartmentId,
            department.Name,
            department.Code
        });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateDepartment(
        int id,
        [FromBody] DepartmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValid(request))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Department name and code are required and must fit their limits.");
        }

        var department = await context.Departments.FindAsync([id], cancellationToken);
        if (department is null)
        {
            return NotFound();
        }

        var name = request.Name.Trim();
        var code = request.Code.Trim().ToUpperInvariant();
        if (await context.Departments.AnyAsync(
                item => item.DepartmentId != id && (item.Name == name || item.Code == code),
                cancellationToken))
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: "Department name or code already exists.");
        }

        department.Name = name;
        department.Code = code;
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static bool IsValid(DepartmentRequest request) =>
        !string.IsNullOrWhiteSpace(request.Name)
        && request.Name.Trim().Length <= 100
        && !string.IsNullOrWhiteSpace(request.Code)
        && request.Code.Trim().Length <= 10;

    public sealed record DepartmentRequest
    {
        [Required, MaxLength(100)]
        public required string Name { get; init; }

        [Required, MaxLength(10)]
        public required string Code { get; init; }
    }
}