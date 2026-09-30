using HimLedger.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HimLedger.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriesController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await context.Categories
            .OrderBy(category => category.Name)
            .Select(category => new
            {
                category.CategoryId,
                category.Name,
                category.Description
            })
            .ToListAsync();

        return Ok(categories);
    }
}