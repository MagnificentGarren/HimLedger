using HimLedger.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HimLedger.Infrastructure;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    private static readonly User SeedAdminUser = new()
    {
        UserId = 1,
        FirstName = "System",
        LastName = "Admin",
        Email = "admin@himledger.com",
        PasswordHash = string.Empty,
        RoleId = 1,
        DepartmentId = null,
        CreatedAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc)
    };

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ApprovalLog> ApprovalLogs => Set<ApprovalLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Department>(entity =>
        {
            entity.Property(department => department.Name).HasMaxLength(100);
            entity.Property(department => department.Code).HasMaxLength(10);
            entity.Property(department => department.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(department => department.Name).IsUnique();
            entity.HasIndex(department => department.Code).IsUnique();
            entity.HasData(
                new Department { DepartmentId = 1, Name = "Logistics & Operations", Code = "LOG", CreatedAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc) },
                new Department { DepartmentId = 2, Name = "Finance & Accounting", Code = "FIN", CreatedAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc) },
                new Department { DepartmentId = 3, Name = "Software & IT", Code = "IT", CreatedAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc) },
                new Department { DepartmentId = 4, Name = "Human Resources (HR)", Code = "HR", CreatedAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc) },
                new Department { DepartmentId = 5, Name = "Sales & Marketing", Code = "MKT", CreatedAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc) });
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.Property(role => role.Name).HasMaxLength(50);
            entity.HasIndex(role => role.Name).IsUnique();
            entity.HasData(
                new Role { RoleId = 1, Name = "Admin" },
                new Role { RoleId = 2, Name = "Manager" },
                new Role { RoleId = 3, Name = "Employee" },
                new Role { RoleId = 4, Name = "Finance" });
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(user => user.FirstName).HasMaxLength(50);
            entity.Property(user => user.LastName).HasMaxLength(50);
            entity.Property(user => user.Email).HasMaxLength(100);
            entity.Property(user => user.PasswordHash).HasMaxLength(255);
            entity.Property(user => user.EntraTenantId).HasMaxLength(36);
            entity.Property(user => user.EntraObjectId).HasMaxLength(36);
            entity.Property(user => user.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(user => user.Email).IsUnique();
            entity.HasIndex(user => new { user.EntraTenantId, user.EntraObjectId })
                .IsUnique()
                .HasFilter("[EntraTenantId] IS NOT NULL AND [EntraObjectId] IS NOT NULL");
            entity.HasData(SeedAdminUser);
            entity.HasOne(user => user.Role)
                .WithMany(role => role.Users)
                .HasForeignKey(user => user.RoleId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(user => user.Department)
                .WithMany(department => department.Users)
                .HasForeignKey(user => user.DepartmentId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.Property(category => category.Name).HasMaxLength(50);
            entity.Property(category => category.Description).HasMaxLength(255);
            entity.HasIndex(category => category.Name).IsUnique();
            entity.HasData(
                new Category { CategoryId = 1, Name = "Travel", Description = "Flights, taxis, and vehicle fuel" },
                new Category { CategoryId = 2, Name = "Meals & Entertainment", Description = "Client lunches and team dinners" },
                new Category { CategoryId = 3, Name = "Office Supplies", Description = "Hardware, software licenses, stationery" },
                new Category { CategoryId = 4, Name = "Utilities", Description = "Internet, phone, and power bills" });
        });

        modelBuilder.Entity<Budget>(entity =>
        {
            entity.Property(budget => budget.AllocatedAmount).HasPrecision(18, 2);
            entity.Property(budget => budget.RemainingAmount).HasPrecision(18, 2);
            entity.Property(budget => budget.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(budget => budget.RowVersion).IsRowVersion();
            entity.HasIndex(budget => new { budget.DepartmentId, budget.FiscalYear, budget.FiscalQuarter }).IsUnique();
            entity.HasIndex(budget => budget.DepartmentId);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Budgets_FiscalQuarter", "FiscalQuarter BETWEEN 1 AND 4");
                table.HasCheckConstraint("CK_Budgets_AllocatedAmount", "AllocatedAmount >= 0");
            });
            entity.HasOne(budget => budget.Department)
                .WithMany(department => department.Budgets)
                .HasForeignKey(budget => budget.DepartmentId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.Property(expense => expense.Title).HasMaxLength(150);
            entity.Property(expense => expense.Description).HasMaxLength(500);
            entity.Property(expense => expense.Amount).HasPrecision(18, 2);
            entity.Property(expense => expense.ExpenseDate).HasColumnType("date");
            entity.Property(expense => expense.ReceiptUrl).HasMaxLength(2083);
            entity.Property(expense => expense.Status).HasMaxLength(20).HasDefaultValue("Pending");
            entity.Property(expense => expense.RowVersion).IsRowVersion();
            entity.Property(expense => expense.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(expense => new { expense.DepartmentId, expense.Status });
            entity.HasIndex(expense => expense.UserId);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Expenses_Amount", "Amount > 0"));
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Expenses_Status", "Status IN ('Pending', 'Approved', 'Rejected')"));
            entity.HasOne(expense => expense.User)
                .WithMany(user => user.Expenses)
                .HasForeignKey(expense => expense.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(expense => expense.Category)
                .WithMany(category => category.Expenses)
                .HasForeignKey(expense => expense.CategoryId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(expense => expense.Department)
                .WithMany(department => department.Expenses)
                .HasForeignKey(expense => expense.DepartmentId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ApprovalLog>(entity =>
        {
            entity.Property(log => log.Action).HasMaxLength(20);
            entity.Property(log => log.Comments).HasMaxLength(500);
            entity.Property(log => log.ActionedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_ApprovalLogs_Action", "Action IN ('Approved', 'Rejected')"));
            entity.HasOne(log => log.Expense)
                .WithMany(expense => expense.ApprovalLogs)
                .HasForeignKey(log => log.ExpenseId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(log => log.ReviewedByUser)
                .WithMany(user => user.ApprovalLogs)
                .HasForeignKey(log => log.ReviewedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}