namespace HimLedger.Domain.Entities;

public class Expense
{
    public int ExpenseId { get; set; }
    public int UserId { get; set; }
    public int CategoryId { get; set; }
    public int DepartmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string? ReceiptUrl { get; set; }
    public string Status { get; set; } = ClaimStatuses.Draft;
    public byte[] RowVersion { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public ICollection<ApprovalLog> ApprovalLogs { get; set; } = new List<ApprovalLog>();
    public ICollection<ClaimStatusHistory> StatusHistory { get; set; } = new List<ClaimStatusHistory>();
    public ICollection<ExpenseAttachment> Attachments { get; set; } = new List<ExpenseAttachment>();
}