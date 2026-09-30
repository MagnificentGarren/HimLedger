using HimLedger.Infrastructure;
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
}