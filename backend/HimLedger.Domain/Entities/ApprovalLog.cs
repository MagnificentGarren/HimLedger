namespace HimLedger.Domain.Entities;

public class ApprovalLog
{
    public int ApprovalLogId { get; set; }
    public int ExpenseId { get; set; }
    public int ReviewedByUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public DateTime ActionedAt { get; set; } = DateTime.UtcNow;

    public Expense Expense { get; set; } = null!;
    public User ReviewedByUser { get; set; } = null!;
}