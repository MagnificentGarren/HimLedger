namespace HimLedger.Domain.Entities;

public class Budget
{
    public int BudgetId { get; set; }
    public int DepartmentId { get; set; }
    public int FiscalYear { get; set; }
    public int FiscalQuarter { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Department Department { get; set; } = null!;
}