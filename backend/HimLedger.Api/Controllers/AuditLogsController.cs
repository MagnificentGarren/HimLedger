using System.ComponentModel.DataAnnotations;
using HimLedger.Api.Models;
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
    public async Task<IActionResult> GetAuditLogs(
        CancellationToken cancellationToken,
        [FromQuery, Range(1, 21_474_836)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 25)
    {
        var records = await context.ApprovalLogs
            .OrderByDescending(log => log.ActionedAt)
            .ThenByDescending(log => log.ApprovalLogId)
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
            .ToPagedResponseAsync(page, pageSize, cancellationToken);

        return Ok(new PagedResponse<AuditLogItem>(
            records.Items.Select(record => new AuditLogItem(
                record.ApprovalLogId,
                DateTime.SpecifyKind(record.ActionedAt, DateTimeKind.Utc),
                record.ExpenseId,
                record.ExpenseTitle,
                record.Action,
                record.ReviewedBy,
                record.Comments)).ToArray(),
            records.Page,
            records.PageSize,
            records.TotalCount));
    }

    public sealed record AuditLogItem(
        int ApprovalLogId,
        DateTime TimestampUtc,
        int ExpenseId,
        string ExpenseTitle,
        string Action,
        string ReviewedBy,
        string? Comments);
}