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
        var expensesQuery = context.Expenses.AsQueryable();
        if (User.IsInRole("Employee"))
        {
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            {
                return Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Unauthorized",
                    detail: "The authenticated user identifier is invalid.");
            }

            expensesQuery = expensesQuery.Where(expense => expense.UserId == userId);
        }
        else if (User.IsInRole("Manager"))
        {
            if (!int.TryParse(User.FindFirst("DepartmentId")?.Value, out var departmentId))
            {
                return Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Unauthorized",
                    detail: "The authenticated department identifier is invalid.");
            }

            expensesQuery = expensesQuery.Where(expense => expense.DepartmentId == departmentId);
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
        var expenseQuery = context.Expenses.Where(expense => expense.ExpenseId == id);
        if (User.IsInRole("Employee"))
        {
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            {
                return Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Unauthorized",
                    detail: "The authenticated user identifier is invalid.");
            }

            expenseQuery = expenseQuery.Where(expense => expense.UserId == userId);
        }
        else if (User.IsInRole("Manager"))
        {
            if (!int.TryParse(User.FindFirst("DepartmentId")?.Value, out var departmentId))
            {
                return Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Unauthorized",
                    detail: "The authenticated department identifier is invalid.");
            }

            expenseQuery = expenseQuery.Where(expense => expense.DepartmentId == departmentId);
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
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "The authenticated user identifier is invalid.");
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
            DepartmentId = request.DepartmentId,
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

        var expense = await context.Expenses.FindAsync([id], cancellationToken);
        if (expense is null)
        {
            return NotFound();
        }

        if (User.IsInRole("Manager"))
        {
            if (!int.TryParse(User.FindFirst("DepartmentId")?.Value, out var departmentId))
            {
                return Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Unauthorized",
                    detail: "The authenticated department identifier is invalid.");
            }

            if (expense.DepartmentId != departmentId)
            {
                return Forbid();
            }
        }

        var reviewerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(reviewerIdClaim, out var reviewerId))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "The authenticated user identifier is invalid.");
        }

        expense.Status = request.Status;
        context.ApprovalLogs.Add(new ApprovalLog
        {
            ExpenseId = id,
            ReviewedByUserId = reviewerId,
            Action = request.Status,
            Comments = request.Comments
        });

        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
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
        expense.CreatedAt);
}