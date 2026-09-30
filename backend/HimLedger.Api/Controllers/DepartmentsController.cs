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
    public async Task<IActionResult> GetDepartments()
    {
        var departments = await context.Departments
            .OrderBy(department => department.Name)
            .Select(department => new
            {
                department.DepartmentId,
                department.Name,
                department.Code
            })
            .ToListAsync();

        return Ok(departments);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateDepartment([FromBody] DepartmentRequest request)
    {
        if (!IsValid(request))
        {
            return BadRequest(new { message = "Department name and code are required and must fit their limits" });
        }

        var name = request.Name.Trim();
        var code = request.Code.Trim().ToUpperInvariant();
        if (await context.Departments.AnyAsync(department => department.Name == name || department.Code == code))
        {
            return Conflict(new { message = "Department name or code already exists" });
        }

        var department = new Department { Name = name, Code = code };
        context.Departments.Add(department);
        await context.SaveChangesAsync();
        return Created("/api/Departments", new
        {
            department.DepartmentId,
            department.Name,
            department.Code
        });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateDepartment(int id, [FromBody] DepartmentRequest request)
    {
        if (!IsValid(request))
        {
            return BadRequest(new { message = "Department name and code are required and must fit their limits" });
        }

        var department = await context.Departments.FindAsync(id);
        if (department is null)
        {
            return NotFound();
        }

        var name = request.Name.Trim();
        var code = request.Code.Trim().ToUpperInvariant();
        if (await context.Departments.AnyAsync(item => item.DepartmentId != id && (item.Name == name || item.Code == code)))
        {
            return Conflict(new { message = "Department name or code already exists" });
        }

        department.Name = name;
        department.Code = code;
        await context.SaveChangesAsync();
        return NoContent();
    }

    private static bool IsValid(DepartmentRequest request) =>
        !string.IsNullOrWhiteSpace(request.Name)
        && request.Name.Trim().Length <= 100
        && !string.IsNullOrWhiteSpace(request.Code)
        && request.Code.Trim().Length <= 10;

    public sealed record DepartmentRequest(string Name, string Code);
}