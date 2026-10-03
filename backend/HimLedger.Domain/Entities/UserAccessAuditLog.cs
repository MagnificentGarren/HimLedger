namespace HimLedger.Domain.Entities;

public class UserAccessAuditLog
{
    public long UserAccessAuditLogId { get; set; }
    public int UserId { get; set; }
    public int ActorUserId { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    public User User { get; set; } = null!;
    public User Actor { get; set; } = null!;
}
