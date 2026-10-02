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
public class BudgetsController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetBudgets(
        CancellationToken cancellationToken,
        [FromQuery] int? fiscalYear,
        [FromQuery] int? fiscalQuarter,
        [FromQuery, Range(1, 21_474_836)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 25)
    {
        var year = fiscalYear ?? DateTime.UtcNow.Year;
        var quarter = fiscalQuarter ?? ((DateTime.UtcNow.Month - 1) / 3) + 1;
        if (year is < 1 or > 9998 || quarter is < 1 or > 4)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Fiscal year and quarter must be valid.");
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
            .ThenBy(budget => budget.BudgetId)
            .Select(budget => new
            {
                budget.BudgetId,
                budget.DepartmentId,
                DepartmentName = budget.Department.Name,
                budget.FiscalYear,
                budget.FiscalQuarter,
                budget.AllocatedAmount
            })
            .ToPagedResponseAsync(page, pageSize, cancellationToken);

        var departmentIds = budgets.Items.Select(budget => budget.DepartmentId).ToArray();
        var spendingByDepartment = await context.Expenses
            .Where(expense => departmentIds.Contains(expense.DepartmentId)
                && expense.Status == "Approved"
                && expense.ExpenseDate >= periodStart
                && expense.ExpenseDate < periodEnd)
            .GroupBy(expense => expense.DepartmentId)
            .Select(group => new { DepartmentId = group.Key, SpentAmount = group.Sum(expense => expense.Amount) })
            .ToDictionaryAsync(group => group.DepartmentId, group => group.SpentAmount, cancellationToken);

        var result = new PagedResponse<BudgetListItem>(
            budgets.Items.Select(budget =>
            {
                spendingByDepartment.TryGetValue(budget.DepartmentId, out var spentAmount);
                return new BudgetListItem(
                    budget.BudgetId,
                    budget.DepartmentId,
                    budget.DepartmentName,
                    budget.FiscalYear,
                    budget.FiscalQuarter,
                    budget.AllocatedAmount,
                    spentAmount,
                    budget.AllocatedAmount - spentAmount);
            }).ToArray(),
            budgets.Page,
            budgets.PageSize,
            budgets.TotalCount);

        return Ok(result);
    }

    [HttpPut]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpsertBudget(
        [FromBody] UpdateBudgetRequest request,
        CancellationToken cancellationToken)
    {
        if (request.FiscalYear is < 1 or > 9998 || request.FiscalQuarter is < 1 or > 4 || request.AllocatedAmount < 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Fiscal period or allocated amount is invalid.");
        }

        if (!await context.Departments.AnyAsync(
                department => department.DepartmentId == request.DepartmentId,
                cancellationToken))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Department does not exist.");
        }

        var budget = await context.Budgets.SingleOrDefaultAsync(item =>
            item.DepartmentId == request.DepartmentId
            && item.FiscalYear == request.FiscalYear
            && item.FiscalQuarter == request.FiscalQuarter,
            cancellationToken);
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
            .SumAsync(expense => (decimal?)expense.Amount, cancellationToken) ?? 0;
        budget.RemainingAmount = request.AllocatedAmount - spentAmount;

        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    public sealed record BudgetListItem(
        int BudgetId,
        int DepartmentId,
        string DepartmentName,
        int FiscalYear,
        int FiscalQuarter,
        decimal AllocatedAmount,
        decimal SpentAmount,
        decimal RemainingAmount);

    public sealed record UpdateBudgetRequest
    {
        [Range(1, int.MaxValue)]
        public int DepartmentId { get; init; }

        [Range(1, 9998)]
        public int FiscalYear { get; init; }

        [Range(1, 4)]
        public int FiscalQuarter { get; init; }

        [Range(typeof(decimal), "0", "79228162514264337593543950335", ParseLimitsInInvariantCulture = true)]
        public decimal AllocatedAmount { get; init; }
    }
}