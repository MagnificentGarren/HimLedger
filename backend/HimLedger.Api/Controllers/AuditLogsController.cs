using System.ComponentModel.DataAnnotations;
using HimLedger.Api.Models;
using HimLedger.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HimLedger.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Finance")]
public class AuditLogsController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        CancellationToken cancellationToken,
        [FromQuery, Range(1, 21_474_836)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 25)
    {
        var records = await context.ClaimStatusHistory
            .OrderByDescending(history => history.OccurredAt)
            .ThenByDescending(history => history.ClaimStatusHistoryId)
            .Select(history => new
            {
                history.ClaimStatusHistoryId,
                history.OccurredAt,
                history.ExpenseId,
                ExpenseTitle = history.Expense.Title,
                Action = history.FromStatus == string.Empty
                    ? history.ToStatus
                    : history.FromStatus + " -> " + history.ToStatus,
                ReviewedBy = history.Actor.FirstName + " " + history.Actor.LastName,
                history.Notes
            })
            .ToPagedResponseAsync(page, pageSize, cancellationToken);

        return Ok(new PagedResponse<AuditLogItem>(
            records.Items.Select(record => new AuditLogItem(
                record.ClaimStatusHistoryId,
                record.OccurredAt,
                record.ExpenseId,
                record.ExpenseTitle,
                record.Action,
                record.ReviewedBy,
                record.Notes)).ToArray(),
            records.Page,
            records.PageSize,
            records.TotalCount));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("user-access")]
    public async Task<IActionResult> GetUserAccessAuditLogs(
        CancellationToken cancellationToken,
        [FromQuery, Range(1, 21_474_836)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 25)
    {
        var records = await context.UserAccessAuditLogs
            .OrderByDescending(log => log.OccurredAt)
            .ThenByDescending(log => log.UserAccessAuditLogId)
            .Select(log => new
            {
                log.UserAccessAuditLogId,
                log.OccurredAt,
                User = log.User.FirstName + " " + log.User.LastName,
                log.User.Email,
                Actor = log.Actor.FirstName + " " + log.Actor.LastName,
                log.IsActive
            })
            .ToPagedResponseAsync(page, pageSize, cancellationToken);

        return Ok(records);
    }

    /// <summary>One immutable claim lifecycle event.</summary>
    public sealed record AuditLogItem(
        long ApprovalLogId,
        DateTimeOffset TimestampUtc,
        int ExpenseId,
        string ExpenseTitle,
        string Action,
        string ReviewedBy,
        string? Comments);
}