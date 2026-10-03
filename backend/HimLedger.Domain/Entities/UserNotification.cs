namespace HimLedger.Domain.Entities;

public class UserNotification
{
    public long UserNotificationId { get; set; }
    public int UserId { get; set; }
    public int? ExpenseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }

    public User User { get; set; } = null!;
    public Expense? Expense { get; set; }
}
