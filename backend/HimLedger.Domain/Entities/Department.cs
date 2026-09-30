namespace HimLedger.Domain.Entities;

public class Department
{
    public int DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Budget> Budgets { get; set; } = new List<Budget>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}