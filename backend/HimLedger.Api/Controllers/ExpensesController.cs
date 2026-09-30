using System.Linq.Expressions;
using System.Security.Claims;
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
    public async Task<ActionResult<IEnumerable<ExpenseResponseDto>>> GetExpenses()
    {
        var expenses = await context.Expenses
            .Select(ResponseProjection)
            .ToListAsync();

        return Ok(expenses);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ExpenseResponseDto>> GetExpense(int id)
    {
        var expense = await context.Expenses
            .Where(expense => expense.ExpenseId == id)
            .Select(ResponseProjection)
            .SingleOrDefaultAsync();

        return expense is null ? NotFound() : Ok(expense);
    }

    [HttpPost]
    public async Task<ActionResult<ExpenseResponseDto>> CreateExpense([FromBody] CreateExpenseDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount must be greater than zero" });
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
        await context.SaveChangesAsync();

        var response = await context.Expenses
            .Where(savedExpense => savedExpense.ExpenseId == expense.ExpenseId)
            .Select(ResponseProjection)
            .SingleAsync();

        return CreatedAtAction(nameof(GetExpense), new { id = expense.ExpenseId }, response);
    }

    [HttpPut("{id:int}/status")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateExpenseStatusDto request)
    {
        if (request.Status is not ("Approved" or "Rejected"))
        {
            return BadRequest(new { message = "Status must be Approved or Rejected" });
        }

        var expense = await context.Expenses.FindAsync(id);
        if (expense is null)
        {
            return NotFound();
        }

        var reviewerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(reviewerIdClaim, out var reviewerId))
        {
            return Unauthorized();
        }

        expense.Status = request.Status;
        context.ApprovalLogs.Add(new ApprovalLog
        {
            ExpenseId = id,
            ReviewedByUserId = reviewerId,
            Action = request.Status,
            Comments = request.Comments
        });

        await context.SaveChangesAsync();
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