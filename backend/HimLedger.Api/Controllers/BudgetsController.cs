using HimLedger.Infrastructure;
using HimLedger.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HimLedger.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BudgetsController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetBudgets([FromQuery] int? fiscalYear, [FromQuery] int? fiscalQuarter)
    {
        var year = fiscalYear ?? DateTime.UtcNow.Year;
        var quarter = fiscalQuarter ?? ((DateTime.UtcNow.Month - 1) / 3) + 1;
        if (year is < 1 or > 9998 || quarter is < 1 or > 4)
        {
            return BadRequest(new { message = "Fiscal year and quarter must be valid" });
        }

        var periodStart = new DateTime(year, ((quarter - 1) * 3) + 1, 1);
        var periodEnd = periodStart.AddMonths(3);
        var budgetsQuery = context.Budgets
            .Where(budget => budget.FiscalYear == year && budget.FiscalQuarter == quarter);
        if (User.IsInRole("Manager"))
        {
            _ = int.TryParse(User.FindFirst("DepartmentId")?.Value, out var departmentId);
            budgetsQuery = budgetsQuery.Where(budget => budget.DepartmentId == departmentId);
        }

        var budgets = await budgetsQuery
            .OrderBy(budget => budget.Department.Name)
            .Select(budget => new
            {
                budget.BudgetId,
                budget.DepartmentId,
                DepartmentName = budget.Department.Name,
                budget.FiscalYear,
                budget.FiscalQuarter,
                budget.AllocatedAmount
            })
            .ToListAsync();

        var departmentIds = budgets.Select(budget => budget.DepartmentId).ToArray();
        var spendingByDepartment = await context.Expenses
            .Where(expense => departmentIds.Contains(expense.DepartmentId)
                && expense.Status == "Approved"
                && expense.ExpenseDate >= periodStart
                && expense.ExpenseDate < periodEnd)
            .GroupBy(expense => expense.DepartmentId)
            .Select(group => new { DepartmentId = group.Key, SpentAmount = group.Sum(expense => expense.Amount) })
            .ToDictionaryAsync(group => group.DepartmentId, group => group.SpentAmount);

        var result = budgets.Select(budget =>
        {
            spendingByDepartment.TryGetValue(budget.DepartmentId, out var spentAmount);
            return new
            {
                budget.BudgetId,
                budget.DepartmentId,
                budget.DepartmentName,
                budget.FiscalYear,
                budget.FiscalQuarter,
                budget.AllocatedAmount,
                SpentAmount = spentAmount,
                RemainingAmount = budget.AllocatedAmount - spentAmount
            };
        });

        return Ok(result);
    }

    [HttpPut]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpsertBudget([FromBody] UpdateBudgetRequest request)
    {
        if (request.FiscalYear is < 1 or > 9998 || request.FiscalQuarter is < 1 or > 4 || request.AllocatedAmount < 0)
        {
            return BadRequest(new { message = "Fiscal period or allocated amount is invalid" });
        }

        if (!await context.Departments.AnyAsync(department => department.DepartmentId == request.DepartmentId))
        {
            return BadRequest(new { message = "Department does not exist" });
        }

        var budget = await context.Budgets.SingleOrDefaultAsync(item =>
            item.DepartmentId == request.DepartmentId
            && item.FiscalYear == request.FiscalYear
            && item.FiscalQuarter == request.FiscalQuarter);
        if (budget is null)
        {
            budget = new Budget
            {
                DepartmentId = request.DepartmentId,
                FiscalYear = request.FiscalYear,
                FiscalQuarter = request.FiscalQuarter,
                AllocatedAmount = request.AllocatedAmount
            };
            context.Budgets.Add(budget);
        }
        else
        {
            budget.AllocatedAmount = request.AllocatedAmount;
        }

        var periodStart = new DateTime(request.FiscalYear, ((request.FiscalQuarter - 1) * 3) + 1, 1);
        var periodEnd = periodStart.AddMonths(3);
        var spentAmount = await context.Expenses
            .Where(expense => expense.DepartmentId == request.DepartmentId
                && expense.Status == "Approved"
                && expense.ExpenseDate >= periodStart
                && expense.ExpenseDate < periodEnd)
            .SumAsync(expense => (decimal?)expense.Amount) ?? 0;
        budget.RemainingAmount = request.AllocatedAmount - spentAmount;

        await context.SaveChangesAsync();
        return NoContent();
    }

    public sealed record UpdateBudgetRequest(int DepartmentId, int FiscalYear, int FiscalQuarter, decimal AllocatedAmount);
}