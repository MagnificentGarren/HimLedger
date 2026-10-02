namespace HimLedger.Domain.Entities;

public class ExpenseAttachment
{
    public int ExpenseAttachmentId { get; set; }
    public int ExpenseId { get; set; }
    public int UploadedByUserId { get; set; }
    public string BlobName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

    public Expense Expense { get; set; } = null!;
    public User UploadedBy { get; set; } = null!;
}
