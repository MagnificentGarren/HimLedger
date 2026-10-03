namespace HimLedger.Domain.Entities;

public class User
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? EntraTenantId { get; set; }
    public string? EntraObjectId { get; set; }
    public int RoleId { get; set; }
    public int? DepartmentId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Role Role { get; set; } = null!;
    public Department? Department { get; set; }
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public ICollection<ApprovalLog> ApprovalLogs { get; set; } = new List<ApprovalLog>();
}