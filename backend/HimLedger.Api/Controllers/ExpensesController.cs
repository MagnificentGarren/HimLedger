using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Data;
using System.Text;
using System.Text.Json;
using HimLedger.Api.Models;
using HimLedger.Application.DTOs;
using HimLedger.Domain.Entities;
using HimLedger.Infrastructure;
using HimLedger.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HimLedger.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExpensesController(ApplicationDbContext context, IReceiptStorage receiptStorage) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ExpenseResponseDto>>> GetExpenses(
        CancellationToken cancellationToken,
        [FromQuery] string? status,
        [FromQuery] int? departmentId,
        [FromQuery] int? categoryId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
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
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
        {
            return BadRequest("fromDate must be on or before toDate.");
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            expensesQuery = expensesQuery.Where(expense => expense.Status == status);
        }
        if (departmentId.HasValue)
        {
            expensesQuery = expensesQuery.Where(expense => expense.DepartmentId == departmentId);
        }
        if (categoryId.HasValue)
        {
            expensesQuery = expensesQuery.Where(expense => expense.CategoryId == categoryId);
        }
        if (fromDate.HasValue)
        {
            expensesQuery = expensesQuery.Where(expense => expense.ExpenseDate >= fromDate.Value.Date);
        }
        if (toDate.HasValue)
        {
            expensesQuery = expensesQuery.Where(expense => expense.ExpenseDate <= toDate.Value.Date);
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
            Status = ClaimStatuses.Draft
        };

        context.Expenses.Add(expense);
        context.ClaimStatusHistory.Add(new ClaimStatusHistory
        {
            Expense = expense,
            ActorUserId = userId,
            FromStatus = string.Empty,
            ToStatus = ClaimStatuses.Draft,
            Decision = "Created",
            Notes = "Claim draft created."
        });
        await context.SaveChangesAsync(cancellationToken);

        var response = await context.Expenses
            .Where(savedExpense => savedExpense.ExpenseId == expense.ExpenseId)
            .Select(ResponseProjection)
            .SingleAsync(cancellationToken);

        return CreatedAtAction(nameof(GetExpense), new { id = expense.ExpenseId }, response);
    }

    [HttpPost("{id:int}/submit")]
    public Task<IActionResult> Submit(int id, [FromBody] ClaimTransitionRequest request, CancellationToken cancellationToken) =>
        TransitionAsync(id, ClaimStatuses.Submitted, request.Notes, null, cancellationToken);

    [HttpPost("{id:int}/resubmit")]
    public Task<IActionResult> Resubmit(int id, [FromBody] ClaimTransitionRequest request, CancellationToken cancellationToken) =>
        TransitionAsync(id, ClaimStatuses.Resubmitted, request.Notes, null, cancellationToken);

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] UpdateExpenseStatusDto request,
        CancellationToken cancellationToken)
    {
        if (request.Status is not (ClaimStatuses.Approved or ClaimStatuses.Rejected or ClaimStatuses.ChangesRequested))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Status must be Approved, Rejected, or Changes Requested.");
        }

        if (!TryGetCallerId(out var reviewerId))
        {
            return Unauthorized();
        }

        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        var expense = await context.Expenses.SingleOrDefaultAsync(item => item.ExpenseId == id, cancellationToken);
        if (expense is null)
        {
            return NotFound();
        }
        if (!await CanReviewAsync(expense.DepartmentId, reviewerId, cancellationToken))
        {
            if (User.IsInRole("Manager")
                && !await context.Users.AnyAsync(user =>
                    user.UserId == reviewerId && user.Role.Name == "Manager" && user.DepartmentId != null,
                    cancellationToken))
            {
                return Forbid();
            }
            return NotFound();
        }
        if (expense.UserId == reviewerId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }
        if (!ClaimStatuses.CanTransition(expense.Status, request.Status))
        {
            return Conflict($"A claim in '{expense.Status}' cannot transition to '{request.Status}'.");
        }

        if (!TryDecodeRowVersion(request.RowVersion, out var rowVersion))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "A valid claim row version is required.");
        }
        context.Entry(expense).Property(item => item.RowVersion).OriginalValue = rowVersion;

        if (request.Status == ClaimStatuses.Approved
            && await GetBudgetApprovalIssueAsync(expense, cancellationToken) is { } budgetIssue)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Budget approval unavailable",
                detail: budgetIssue);
        }

        var previousStatus = expense.Status;
        expense.Status = request.Status;
        context.ClaimStatusHistory.Add(NewHistory(
            id,
            reviewerId,
            previousStatus,
            request.Status,
            request.Status,
            request.Comments));
        context.ApprovalLogs.Add(new ApprovalLog
        {
            ExpenseId = id,
            ReviewedByUserId = reviewerId,
            Action = request.Status,
            Comments = request.Comments
        });
        AddOutboxMessage(id, request.Status, reviewerId, request.Comments);
        var employeeMessage = request.Status switch
        {
            ClaimStatuses.Approved => "Your claim was accepted.",
            ClaimStatuses.Rejected => "Your claim was rejected.",
            _ => "Your claim needs changes before it can be approved."
        };
        AddUserNotifications(
            [expense.UserId],
            id,
            $"Claim {request.Status.ToLowerInvariant()}",
            $"{employeeMessage}{(string.IsNullOrWhiteSpace(request.Comments) ? string.Empty : $" Reviewer comment: {request.Comments.Trim()}")}");
        if (request.Status == ClaimStatuses.Approved)
        {
            var financeUserIds = await context.Users
                .Where(user => user.IsActive
                    && user.Role.Name == "Finance"
                    && (user.DepartmentId == null || user.DepartmentId == expense.DepartmentId))
                .Select(user => user.UserId)
                .ToListAsync(cancellationToken);
            AddUserNotifications(
                financeUserIds,
                id,
                "Claim ready for reimbursement",
                $"Claim HL-{id:D5} ({expense.Title}) was approved and is ready for Finance to review.");
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("The claim changed after it was loaded. Refresh and try again.");
        }
        return NoContent();
    }

    [HttpPost("{id:int}/reimburse")]
    [Authorize(Roles = "Finance,Admin")]
    public async Task<IActionResult> Reimburse(
        int id,
        [FromBody] ClaimTransitionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var actorId))
        {
            return Unauthorized();
        }

        return await TransitionAsync(id, ClaimStatuses.Reimbursed, request.Notes, actorId, cancellationToken);
    }

    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> GetHistory(int id, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized();
        }

        var expense = await context.Expenses.SingleOrDefaultAsync(item => item.ExpenseId == id, cancellationToken);
        var scopedExpenseQuery = await ScopeExpensesToCallerAsync(
            context.Expenses.Where(item => item.ExpenseId == id), callerId, cancellationToken);
        if (expense is null || scopedExpenseQuery is null
            || !await scopedExpenseQuery.AnyAsync(cancellationToken))
        {
            return NotFound();
        }

        var history = await context.ClaimStatusHistory
            .Where(item => item.ExpenseId == id)
            .OrderBy(item => item.ClaimStatusHistoryId)
            .Select(item => new
            {
                item.ClaimStatusHistoryId,
                item.ExpenseId,
                item.ActorUserId,
                item.FromStatus,
                item.ToStatus,
                item.Decision,
                item.Notes,
                item.OccurredAt
            })
            .ToListAsync(cancellationToken);
        return Ok(history);
    }

    [HttpGet("{id:int}/duplicates")]
    [Authorize(Roles = "Manager,Admin,Finance")]
    public async Task<IActionResult> GetPotentialDuplicates(int id, CancellationToken cancellationToken)
    {
        var expense = await context.Expenses.SingleOrDefaultAsync(item => item.ExpenseId == id, cancellationToken);
        if (expense is null)
        {
            return NotFound();
        }
        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized();
        }
        if (User.IsInRole("Manager") && !await IsInManagerDepartmentAsync(expense.DepartmentId, callerId, cancellationToken))
        {
            return NotFound();
        }

        var from = expense.ExpenseDate.AddDays(-7);
        var to = expense.ExpenseDate.AddDays(7);
        var candidates = await context.Expenses
            .Where(item => item.ExpenseId != id
                && item.Amount == expense.Amount
                && item.ExpenseDate >= from
                && item.ExpenseDate <= to
                && item.Title.ToUpper() == expense.Title.ToUpper())
            .OrderBy(item => item.ExpenseDate)
            .Select(ResponseProjection)
            .ToListAsync(cancellationToken);
        return Ok(candidates);
    }

    [HttpPost("{id:int}/attachments")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAttachment(
        int id,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized();
        }
        if (file is null || file.Length is <= 0 or > 10 * 1024 * 1024)
        {
            return BadRequest("Receipt must be between 1 byte and 10 MiB.");
        }

        var expense = await context.Expenses.SingleOrDefaultAsync(item => item.ExpenseId == id, cancellationToken);
        if (expense is null || !await CanAccessExpenseAsync(expense, callerId, cancellationToken))
        {
            return NotFound();
        }
        if (expense.UserId != callerId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }
        if (expense.Status is not (ClaimStatuses.Draft or ClaimStatuses.ChangesRequested))
        {
            return Conflict("Receipts can only be added to a draft or a claim requiring changes.");
        }

        var validatedType = await DetectReceiptContentTypeAsync(file, cancellationToken);
        if (validatedType is null)
        {
            return BadRequest("Only valid PDF, JPEG, and PNG receipts are accepted.");
        }

        var blobName = $"{id}/{Guid.NewGuid():N}{ExtensionFor(validatedType)}";
        var savedToBlob = false;
        var metadataSaved = false;
        try
        {
            await using (var stream = file.OpenReadStream())
            {
                await receiptStorage.StoreAsync(blobName, stream, validatedType, cancellationToken);
            }
            savedToBlob = true;

            var attachment = new ExpenseAttachment
            {
                ExpenseId = id,
                UploadedByUserId = callerId,
                BlobName = blobName,
                FileName = SafeFileName(file.FileName),
                ContentType = validatedType,
                SizeBytes = file.Length
            };
            context.ExpenseAttachments.Add(attachment);
            await context.SaveChangesAsync(cancellationToken);
            metadataSaved = true;

            return CreatedAtAction(nameof(GetAttachment), new { id, attachmentId = attachment.ExpenseAttachmentId },
                new { attachment.ExpenseAttachmentId, attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.UploadedAt });
        }
        finally
        {
            if (savedToBlob && !metadataSaved)
            {
                await receiptStorage.DeleteIfExistsAsync(blobName, cancellationToken);
            }
        }
    }

    [HttpGet("{id:int}/attachments/{attachmentId:int}")]
    public async Task<IActionResult> GetAttachment(int id, int attachmentId, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized();
        }
        var attachment = await context.ExpenseAttachments
            .Where(item => item.ExpenseId == id && item.ExpenseAttachmentId == attachmentId)
            .Select(item => new { item.BlobName, item.FileName, item.ContentType })
            .SingleOrDefaultAsync(cancellationToken);
        if (attachment is null)
        {
            return NotFound();
        }
        var expense = await context.Expenses.SingleAsync(item => item.ExpenseId == id, cancellationToken);
        if (!await CanAccessExpenseAsync(expense, callerId, cancellationToken))
        {
            return NotFound();
        }

        var sasUri = await receiptStorage.CreateReadSasAsync(attachment.BlobName, cancellationToken);
        return Ok(new { url = sasUri, attachment.FileName, attachment.ContentType, expiresInSeconds = 300 });
    }

    [HttpGet("reconciliation.csv")]
    [Authorize(Roles = "Admin,Finance")]
    public async Task<IActionResult> ExportReconciliation(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int? departmentId,
        [FromQuery] int? categoryId,
        CancellationToken cancellationToken)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
        {
            return BadRequest("fromDate must be on or before toDate.");
        }
        var query = context.Expenses.AsNoTracking();
        if (fromDate.HasValue) query = query.Where(item => item.ExpenseDate >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(item => item.ExpenseDate <= toDate.Value.Date);
        if (departmentId.HasValue) query = query.Where(item => item.DepartmentId == departmentId.Value);
        if (categoryId.HasValue) query = query.Where(item => item.CategoryId == categoryId.Value);

        var claims = await query
            .OrderBy(item => item.ExpenseDate)
            .ThenBy(item => item.ExpenseId)
            .Select(item => new
            {
                item.ExpenseId,
                item.UserId,
                item.Title,
                item.Amount,
                item.ExpenseDate,
                item.CategoryId,
                Category = item.Category.Name,
                item.DepartmentId,
                Department = item.Department.Name,
                item.Status,
                item.CreatedAt
            })
            .ToListAsync(cancellationToken);
        var ids = claims.Select(item => item.ExpenseId).ToArray();
        var history = await context.ClaimStatusHistory
            .Where(item => ids.Contains(item.ExpenseId))
            .OrderBy(item => item.ClaimStatusHistoryId)
            .Select(item => new { item.ExpenseId, item.ActorUserId, item.FromStatus, item.ToStatus, item.Decision, item.Notes, item.OccurredAt })
            .ToListAsync(cancellationToken);

        var csv = new StringBuilder("ExpenseId,UserId,Title,Amount,ExpenseDate,CategoryId,Category,DepartmentId,Department,Status,CreatedAt,ApprovalHistory\r\n");
        foreach (var claim in claims)
        {
            var trace = string.Join(" | ", history.Where(item => item.ExpenseId == claim.ExpenseId).Select(item =>
                $"{item.OccurredAt:O}: {item.FromStatus}->{item.ToStatus}; actor={item.ActorUserId}; decision={item.Decision}; notes={item.Notes}"));
            csv.AppendLine(string.Join(",", new[]
            {
                Csv(claim.ExpenseId.ToString()),
                Csv(claim.UserId.ToString()),
                Csv(claim.Title, protectSpreadsheetFormula: true),
                Csv(claim.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Csv(claim.ExpenseDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
                Csv(claim.CategoryId.ToString()),
                Csv(claim.Category, protectSpreadsheetFormula: true),
                Csv(claim.DepartmentId.ToString()),
                Csv(claim.Department, protectSpreadsheetFormula: true),
                Csv(claim.Status),
                Csv(claim.CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
                Csv(trace, protectSpreadsheetFormula: true)
            }));
        }
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8", "himledger-reconciliation.csv");
    }

    private async Task<IActionResult> TransitionAsync(
        int expenseId,
        string nextStatus,
        string? notes,
        int? knownActorId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized();
        }
        var actorId = knownActorId ?? callerId;
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        var expense = await context.Expenses.SingleOrDefaultAsync(item => item.ExpenseId == expenseId, cancellationToken);
        if (expense is null || !await CanAccessExpenseAsync(expense, callerId, cancellationToken))
        {
            return NotFound();
        }
        if (nextStatus is ClaimStatuses.Submitted or ClaimStatuses.Resubmitted)
        {
            if (expense.UserId != callerId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }
            var expected = nextStatus == ClaimStatuses.Submitted ? ClaimStatuses.Draft : ClaimStatuses.ChangesRequested;
            if (expense.Status != expected)
            {
                return Conflict($"A claim in '{expense.Status}' cannot transition to '{nextStatus}'.");
            }
        }
        else if (nextStatus == ClaimStatuses.Reimbursed && !User.IsInRole("Finance") && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        if (!ClaimStatuses.CanTransition(expense.Status, nextStatus))
        {
            return Conflict($"A claim in '{expense.Status}' cannot transition to '{nextStatus}'.");
        }

        var originalStatus = expense.Status;
        expense.Status = nextStatus;
        context.ClaimStatusHistory.Add(NewHistory(expenseId, actorId, originalStatus, nextStatus, nextStatus, notes));
        AddOutboxMessage(expenseId, nextStatus, actorId, notes);

        var autoAdvance = nextStatus is ClaimStatuses.Submitted or ClaimStatuses.Resubmitted;
        if (autoAdvance)
        {
            var submittedStatus = expense.Status;
            expense.Status = ClaimStatuses.PendingApproval;
            context.ClaimStatusHistory.Add(NewHistory(
                expenseId, actorId, submittedStatus, ClaimStatuses.PendingApproval, "Routed for approval", notes));
            AddOutboxMessage(expenseId, ClaimStatuses.PendingApproval, actorId, notes);
            var recipients = await context.Users
                .Where(user => user.IsActive
                    && (user.Role.Name == "Admin"
                        || (user.Role.Name == "Finance"
                            && (user.DepartmentId == null || user.DepartmentId == expense.DepartmentId))
                        || (user.Role.Name == "Manager" && user.DepartmentId == expense.DepartmentId)))
                .Select(user => new { user.UserId, Role = user.Role.Name })
                .ToListAsync(cancellationToken);
            var reviewRecipients = recipients
                .Where(item => item.Role is "Admin" or "Manager")
                .Select(item => item.UserId);
            AddUserNotifications(
                reviewRecipients,
                expenseId,
                "Claim submitted for approval",
                $"Claim HL-{expenseId:D5} ({expense.Title}) is awaiting review. Check its budget status before deciding.");
            AddUserNotifications(
                recipients.Where(item => item.Role == "Finance").Select(item => item.UserId),
                expenseId,
                "Claim submitted for budget review",
                $"Claim HL-{expenseId:D5} ({expense.Title}) was submitted. Review the budget status and coordinate an allocation if needed.");
        }

        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return NoContent();
    }

    private async Task<bool> CanAccessExpenseAsync(Expense expense, int callerId, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Employee"))
        {
            return expense.UserId == callerId
                || await IsDelegatedForDepartmentAsync(expense.DepartmentId, callerId, cancellationToken);
        }
        if (User.IsInRole("Manager"))
        {
            return await IsInManagerDepartmentAsync(expense.DepartmentId, callerId, cancellationToken)
                || await IsDelegatedForDepartmentAsync(expense.DepartmentId, callerId, cancellationToken);
        }
        return User.IsInRole("Admin") || User.IsInRole("Finance");
    }

    private async Task<bool> CanReviewAsync(int departmentId, int reviewerId, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Admin"))
        {
            return true;
        }
        return await IsInManagerDepartmentAsync(departmentId, reviewerId, cancellationToken)
            || await IsDelegatedForDepartmentAsync(departmentId, reviewerId, cancellationToken);
    }

    private Task<bool> IsInManagerDepartmentAsync(
        int departmentId,
        int callerId,
        CancellationToken cancellationToken) =>
        context.Users.AnyAsync(user =>
            user.UserId == callerId
            && user.Role.Name == "Manager"
            && user.DepartmentId == departmentId,
            cancellationToken);

    private Task<bool> IsDelegatedForDepartmentAsync(int departmentId, int delegateUserId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return context.ApprovalDelegations.AnyAsync(delegation =>
            delegation.DepartmentId == departmentId
            && delegation.DelegateUserId == delegateUserId
            && delegation.StartsAt <= now
            && delegation.EndsAt > now
            && delegation.Delegate.DepartmentId == departmentId
            && delegation.Delegator.DepartmentId == departmentId
            && delegation.Delegator.Role.Name == "Manager",
            cancellationToken);
    }

    private async Task<string?> GetBudgetApprovalIssueAsync(Expense expense, CancellationToken cancellationToken)
    {
        var fiscalYear = expense.ExpenseDate.Year;
        var fiscalQuarter = ((expense.ExpenseDate.Month - 1) / 3) + 1;
        await LockBudgetPeriodAsync(expense.DepartmentId, fiscalYear, fiscalQuarter, cancellationToken);
        var budget = await context.Budgets.SingleOrDefaultAsync(item =>
            item.DepartmentId == expense.DepartmentId
            && item.FiscalYear == fiscalYear
            && item.FiscalQuarter == fiscalQuarter,
            cancellationToken);
        if (budget is null)
        {
            var departmentName = await context.Departments
                .Where(item => item.DepartmentId == expense.DepartmentId)
                .Select(item => item.Name)
                .SingleAsync(cancellationToken);
            return $"No budget is allocated to {departmentName} for FY {fiscalYear}, Q{fiscalQuarter}. Ask Finance or an administrator to allocate a budget, then retry approval.";
        }

        var committedOrApproved = await context.Expenses
            .Where(item => item.ExpenseId != expense.ExpenseId
                && item.DepartmentId == expense.DepartmentId
                && item.ExpenseDate.Year == fiscalYear
                && ((item.ExpenseDate.Month - 1) / 3) + 1 == fiscalQuarter
                && (item.Status == ClaimStatuses.Approved
                    || item.Status == ClaimStatuses.Reimbursed
                    || item.Status == ClaimStatuses.Submitted
                    || item.Status == ClaimStatuses.PendingApproval
                    || item.Status == ClaimStatuses.Resubmitted
                    || item.Status == ClaimStatuses.ChangesRequested))
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0;
        return committedOrApproved + expense.Amount <= budget.AllocatedAmount
            ? null
            : $"This claim would exceed the available budget for FY {fiscalYear}, Q{fiscalQuarter}. Ask Finance or an administrator to review the allocation or claim amount.";
    }

    private Task<bool> LockBudgetPeriodAsync(
        int departmentId,
        int fiscalYear,
        int fiscalQuarter,
        CancellationToken cancellationToken)
    {
        if (!context.Database.IsSqlServer())
        {
            return Task.FromResult(false);
        }

        return context.Budgets
            .FromSqlInterpolated($"""
                SELECT * FROM [dbo].[Budgets] WITH (UPDLOCK, HOLDLOCK)
                WHERE DepartmentId = {departmentId}
                    AND FiscalYear = {fiscalYear}
                    AND FiscalQuarter = {fiscalQuarter}
                """)
            .AsNoTracking()
            .AnyAsync(cancellationToken);
    }

    private async Task<string?> DetectReceiptContentTypeAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var header = new byte[8];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header, cancellationToken);
        if (read >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
        {
            return "application/pdf";
        }
        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return "image/jpeg";
        }
        if (read >= 8 && header.AsSpan().SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "image/png";
        }
        return null;
    }

    private static string ExtensionFor(string contentType) =>
        contentType switch
        {
            "application/pdf" => ".pdf",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            _ => throw new InvalidOperationException("Unexpected receipt content type.")
        };

    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        return name.Length <= 255 ? name : name[..255];
    }

    private static ClaimStatusHistory NewHistory(
        int expenseId, int actorId, string from, string to, string decision, string? notes) =>
        new()
        {
            ExpenseId = expenseId,
            ActorUserId = actorId,
            FromStatus = from,
            ToStatus = to,
            Decision = decision,
            Notes = notes
        };

    private void AddOutboxMessage(int expenseId, string status, int actorId, string? notes)
    {
        context.NotificationOutboxMessages.Add(new NotificationOutboxMessage
        {
            IdempotencyKey = $"claim:{expenseId}:{Guid.NewGuid():N}",
            EventType = "ClaimStatusChanged",
            Payload = JsonSerializer.Serialize(new { expenseId, status, actorId, notes })
        });
    }

    private void AddUserNotifications(IEnumerable<int> recipientIds, int expenseId, string title, string message)
    {
        foreach (var recipientId in recipientIds.Distinct())
        {
            context.UserNotifications.Add(new UserNotification
            {
                UserId = recipientId,
                ExpenseId = expenseId,
                Title = title,
                Message = message
            });
        }
    }

    private static string Csv(string value, bool protectSpreadsheetFormula = false)
    {
        var safeValue = value;
        if (protectSpreadsheetFormula)
        {
            var firstMeaningfulCharacter = value.AsSpan().TrimStart();
            if (!firstMeaningfulCharacter.IsEmpty
                && firstMeaningfulCharacter[0] is '=' or '+' or '-' or '@')
            {
                safeValue = $"'{value}";
            }
        }
        return $"\"{safeValue.Replace("\"", "\"\"")}\"";
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
            var now = DateTimeOffset.UtcNow;
            return query.Where(expense => expense.UserId == callerId
                || context.ApprovalDelegations.Any(delegation =>
                    delegation.DepartmentId == expense.DepartmentId
                    && delegation.DelegateUserId == callerId
                    && delegation.StartsAt <= now
                    && delegation.EndsAt > now
                    && delegation.Delegate.DepartmentId == expense.DepartmentId
                    && delegation.Delegator.DepartmentId == expense.DepartmentId
                    && delegation.Delegator.Role.Name == "Manager"));
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
        null,
        expense.Status,
        expense.CreatedAt,
        Convert.ToBase64String(expense.RowVersion));
}