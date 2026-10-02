using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Security.Claims;
using HimLedger.Api.Models;
using HimLedger.Application.DTOs;
using HimLedger.Domain.Entities;
using HimLedger.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HimLedger.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExpensesController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ExpenseResponseDto>>> GetExpenses(
        CancellationToken cancellationToken,
        [FromQuery, Range(1, 21_474_836)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 25)
    {
        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized();
        }

        var expensesQuery = await ScopeExpensesToCallerAsync(context.Expenses, callerId, cancellationToken);
        if (expensesQuery is null)
        {
            return Unauthorized();
        }

        var expenses = await expensesQuery
            .OrderBy(expense => expense.ExpenseId)
            .Select(ResponseProjection)
            .ToPagedResponseAsync(page, pageSize, cancellationToken);

        return Ok(expenses);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ExpenseResponseDto>> GetExpense(int id, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized();
        }

        var expenseQuery = await ScopeExpensesToCallerAsync(
            context.Expenses.Where(expense => expense.ExpenseId == id),
            callerId,
            cancellationToken);
        if (expenseQuery is null)
        {
            return Unauthorized();
        }

        var expense = await expenseQuery
            .Select(ResponseProjection)
            .SingleOrDefaultAsync(cancellationToken);

        return expense is null ? NotFound() : Ok(expense);
    }

    [HttpPost]
    public async Task<ActionResult<ExpenseResponseDto>> CreateExpense(
        [FromBody] CreateExpenseDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var userId))
        {
            return Unauthorized();
        }

        var caller = await context.Users
            .Where(user => user.UserId == userId)
            .Select(user => new { user.DepartmentId })
            .SingleOrDefaultAsync(cancellationToken);
        if (caller?.DepartmentId is not int assignedDepartmentId)
        {
            return Forbid();
        }
        if (request.DepartmentId != assignedDepartmentId)
        {
            return Forbid();
        }
        if (request.Amount <= 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Amount must be greater than zero.");
        }

        var expense = new Expense
        {
            UserId = userId,
            CategoryId = request.CategoryId,
            DepartmentId = assignedDepartmentId,
            Title = request.Title,
            Description = request.Description,
            Amount = request.Amount,
            ExpenseDate = request.ExpenseDate,
            ReceiptUrl = request.ReceiptUrl,
            Status = "Pending"
        };

        context.Expenses.Add(expense);
        await context.SaveChangesAsync(cancellationToken);

        var response = await context.Expenses
            .Where(savedExpense => savedExpense.ExpenseId == expense.ExpenseId)
            .Select(ResponseProjection)
            .SingleAsync(cancellationToken);

        return CreatedAtAction(nameof(GetExpense), new { id = expense.ExpenseId }, response);
    }

    [HttpPut("{id:int}/status")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] UpdateExpenseStatusDto request,
        CancellationToken cancellationToken)
    {
        if (request.Status is not ("Approved" or "Rejected"))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Status must be Approved or Rejected.");
        }

        if (!TryGetCallerId(out var reviewerId))
        {
            return Unauthorized();
        }

        var expenseQuery = context.Expenses.Where(item => item.ExpenseId == id);
        if (User.IsInRole("Manager"))
        {
            var departmentId = await context.Users
                .Where(user => user.UserId == reviewerId)
                .Select(user => user.DepartmentId)
                .SingleOrDefaultAsync(cancellationToken);
            if (departmentId is null)
            {
                return Forbid();
            }
            expenseQuery = expenseQuery.Where(item => item.DepartmentId == departmentId);
        }

        var expense = await expenseQuery.SingleOrDefaultAsync(cancellationToken);
        if (expense is null)
        {
            return NotFound();
        }
        if (expense.Status != "Pending")
        {
            return Conflict("Only pending claims can be reviewed.");
        }

        if (!TryDecodeRowVersion(request.RowVersion, out var rowVersion))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "A valid claim row version is required.");
        }
        context.Entry(expense).Property(item => item.RowVersion).OriginalValue = rowVersion;

        expense.Status = request.Status;
        context.ApprovalLogs.Add(new ApprovalLog
        {
            ExpenseId = id,
            ReviewedByUserId = reviewerId,
            Action = request.Status,
            Comments = request.Comments
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("The claim changed after it was loaded. Refresh and try again.");
        }
        return NoContent();
    }

    private bool TryGetCallerId(out int userId) =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId);

    private async Task<IQueryable<Expense>?> ScopeExpensesToCallerAsync(
        IQueryable<Expense> query,
        int callerId,
        CancellationToken cancellationToken)
    {
        if (User.IsInRole("Employee"))
        {
            return query.Where(expense => expense.UserId == callerId);
        }

        if (User.IsInRole("Manager"))
        {
            var departmentId = await context.Users
                .Where(user => user.UserId == callerId)
                .Select(user => user.DepartmentId)
                .SingleOrDefaultAsync(cancellationToken);
            return departmentId is null
                ? null
                : query.Where(expense => expense.DepartmentId == departmentId);
        }

        return User.IsInRole("Admin") || User.IsInRole("Finance") ? query : null;
    }

    private static bool TryDecodeRowVersion(string value, out byte[] rowVersion)
    {
        try
        {
            rowVersion = Convert.FromBase64String(value);
            return rowVersion.Length == 8;
        }
        catch (FormatException)
        {
            rowVersion = [];
            return false;
        }
    }

    private static readonly Expression<Func<Expense, ExpenseResponseDto>> ResponseProjection = expense => new(
        expense.ExpenseId,
        expense.UserId,
        expense.User.FirstName + " " + expense.User.LastName,
        expense.CategoryId,
        expense.Category.Name,
        expense.DepartmentId,
        expense.Department.Name,
        expense.Title,
        expense.Description,
        expense.Amount,
        expense.ExpenseDate,
        expense.ReceiptUrl,
        expense.Status,
        expense.CreatedAt,
        Convert.ToBase64String(expense.RowVersion));
}