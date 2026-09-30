using HimLedger.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HimLedger.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AuditLogsController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAuditLogs()
    {
        var records = await context.ApprovalLogs
            .OrderByDescending(log => log.ActionedAt)
            .Select(log => new
            {
                log.ApprovalLogId,
                log.ActionedAt,
                log.ExpenseId,
                ExpenseTitle = log.Expense.Title,
                log.Action,
                ReviewedBy = log.ReviewedByUser.FirstName + " " + log.ReviewedByUser.LastName,
                log.Comments
            })
            .ToListAsync();

        return Ok(records.Select(record => new
        {
            record.ApprovalLogId,
            TimestampUtc = DateTime.SpecifyKind(record.ActionedAt, DateTimeKind.Utc),
            record.ExpenseId,
            record.ExpenseTitle,
            record.Action,
            record.ReviewedBy,
            record.Comments
        }));
    }
}