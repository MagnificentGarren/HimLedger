namespace HimLedger.Domain.Entities;

public class ApprovalDelegation
{
    public int ApprovalDelegationId { get; set; }
    public int DepartmentId { get; set; }
    public int DelegatorUserId { get; set; }
    public int DelegateUserId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Department Department { get; set; } = null!;
    public User Delegator { get; set; } = null!;
    public User Delegate { get; set; } = null!;
}
