using System.Security.Claims;
using HimLedger.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HimLedger.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetNotifications(CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var userId))
        {
            return Unauthorized();
        }

        var unreadCount = await context.UserNotifications
            .CountAsync(item => item.UserId == userId && item.ReadAt == null, cancellationToken);
        var notifications = await context.UserNotifications
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.UserNotificationId)
            .Take(30)
            .Select(item => new
            {
                item.UserNotificationId,
                item.ExpenseId,
                item.Title,
                item.Message,
                item.CreatedAt,
                item.ReadAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new { unreadCount, notifications });
    }

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var userId))
        {
            return Unauthorized();
        }

        var notification = await context.UserNotifications
            .SingleOrDefaultAsync(item => item.UserNotificationId == id && item.UserId == userId, cancellationToken);
        if (notification is null)
        {
            return NotFound();
        }

        notification.ReadAt ??= DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var userId))
        {
            return Unauthorized();
        }

        var unreadNotifications = await context.UserNotifications
            .Where(item => item.UserId == userId && item.ReadAt == null)
            .ToListAsync(cancellationToken);
        var readAt = DateTimeOffset.UtcNow;
        foreach (var notification in unreadNotifications)
        {
            notification.ReadAt = readAt;
        }
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private bool TryGetCallerId(out int userId) =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId);
}
