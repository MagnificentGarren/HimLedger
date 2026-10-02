namespace HimLedger.Domain.Entities;

public static class ClaimStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string PendingApproval = "Pending Approval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string ChangesRequested = "Changes Requested";
    public const string Resubmitted = "Resubmitted";
    public const string Reimbursed = "Reimbursed";

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Transitions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [Draft] = new HashSet<string>([Submitted], StringComparer.Ordinal),
            [Submitted] = new HashSet<string>([PendingApproval], StringComparer.Ordinal),
            [PendingApproval] = new HashSet<string>([Approved, Rejected, ChangesRequested], StringComparer.Ordinal),
            [Approved] = new HashSet<string>([Reimbursed], StringComparer.Ordinal),
            [ChangesRequested] = new HashSet<string>([Resubmitted], StringComparer.Ordinal),
            [Resubmitted] = new HashSet<string>([PendingApproval], StringComparer.Ordinal)
        };

    public static bool CanTransition(string currentStatus, string nextStatus) =>
        Transitions.TryGetValue(currentStatus, out var allowed) && allowed.Contains(nextStatus);
}
