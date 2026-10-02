using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HimLedger.Domain.Entities;
using HimLedger.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HimLedger.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Manager,Admin")]
public sealed class ApprovalDelegationsController(ApplicationDbContext context) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateApprovalDelegationRequest request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var delegatorId))
        {
            return Unauthorized();
        }

        var delegator = await context.Users
            .Where(user => user.UserId == delegatorId)
            .Select(user => new { user.DepartmentId, user.Role.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (delegator is null || delegator.DepartmentId is null || delegator.Name != "Manager")
        {
            return Forbid();
        }
        if (request.DelegateUserId == delegatorId || request.StartsAt >= request.EndsAt
            || request.EndsAt <= DateTimeOffset.UtcNow
            || request.EndsAt - request.StartsAt > TimeSpan.FromDays(90))
        {
            return BadRequest("A delegation must target another user and last no more than 90 days.");
        }

        var delegateIsInDepartment = await context.Users.AnyAsync(user =>
            user.UserId == request.DelegateUserId && user.DepartmentId == delegator.DepartmentId,
            cancellationToken);
        if (!delegateIsInDepartment)
        {
            return BadRequest("The delegate must be an active user in the manager's department.");
        }

        var overlaps = await context.ApprovalDelegations.AnyAsync(item =>
            item.DelegatorUserId == delegatorId
            && item.StartsAt < request.EndsAt
            && request.StartsAt < item.EndsAt,
            cancellationToken);
        if (overlaps)
        {
            return Conflict("This manager already has an overlapping delegation.");
        }

        var delegation = new ApprovalDelegation
        {
            DepartmentId = delegator.DepartmentId.Value,
            DelegatorUserId = delegatorId,
            DelegateUserId = request.DelegateUserId,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt
        };
        context.ApprovalDelegations.Add(delegation);
        await context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetMine), new { id = delegation.ApprovalDelegationId }, new
        {
            delegation.ApprovalDelegationId,
            delegation.DepartmentId,
            delegation.DelegatorUserId,
            delegation.DelegateUserId,
            delegation.StartsAt,
            delegation.EndsAt
        });
    }

    [HttpGet("{id:int}", Name = nameof(GetMine))]
    public async Task<IActionResult> GetMine(int id, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
        {
            return Unauthorized();
        }

        var delegation = await context.ApprovalDelegations
            .Where(item => item.ApprovalDelegationId == id && item.DelegatorUserId == userId)
            .Select(item => new
            {
                item.ApprovalDelegationId,
                item.DepartmentId,
                item.DelegatorUserId,
                item.DelegateUserId,
                item.StartsAt,
                item.EndsAt
            })
            .SingleOrDefaultAsync(cancellationToken);
        return delegation is null ? NotFound() : Ok(delegation);
    }

    /// <summary>Defines a department-scoped approval delegation period.</summary>
    public sealed record CreateApprovalDelegationRequest
    {
        [Range(1, int.MaxValue)]
        public int DelegateUserId { get; init; }

        public DateTimeOffset StartsAt { get; init; }
        public DateTimeOffset EndsAt { get; init; }
    }
}
