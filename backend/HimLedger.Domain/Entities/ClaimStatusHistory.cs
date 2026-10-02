namespace HimLedger.Domain.Entities;

public class ClaimStatusHistory
{
    public long ClaimStatusHistoryId { get; set; }
    public int ExpenseId { get; set; }
    public int ActorUserId { get; set; }
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    public Expense Expense { get; set; } = null!;
    public User Actor { get; set; } = null!;
}
