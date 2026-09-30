using HimLedger.Infrastructure;
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
        var budgets = await context.Budgets
            .Where(budget => budget.FiscalYear == year && budget.FiscalQuarter == quarter)
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
}