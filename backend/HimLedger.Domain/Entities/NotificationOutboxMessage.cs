namespace HimLedger.Domain.Entities;

public class NotificationOutboxMessage
{
    public long NotificationOutboxMessageId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DispatchedAt { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public string? LockId { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
