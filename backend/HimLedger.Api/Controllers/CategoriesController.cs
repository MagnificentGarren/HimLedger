using System.ComponentModel.DataAnnotations;
using HimLedger.Api.Models;
using HimLedger.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HimLedger.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriesController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCategories(
        CancellationToken cancellationToken,
        [FromQuery, Range(1, 21_474_836)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 25)
    {
        var categories = await context.Categories
            .OrderBy(category => category.Name)
            .ThenBy(category => category.CategoryId)
            .Select(category => new
            {
                category.CategoryId,
                category.Name,
                category.Description
            })
            .ToPagedResponseAsync(page, pageSize, cancellationToken);

        return Ok(categories);
    }
}