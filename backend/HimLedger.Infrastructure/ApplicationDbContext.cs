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
    public DbSet<ClaimStatusHistory> ClaimStatusHistory => Set<ClaimStatusHistory>();
    public DbSet<ApprovalDelegation> ApprovalDelegations => Set<ApprovalDelegation>();
    public DbSet<ExpenseAttachment> ExpenseAttachments => Set<ExpenseAttachment>();
    public DbSet<NotificationOutboxMessage> NotificationOutboxMessages => Set<NotificationOutboxMessage>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<UserAccessAuditLog> UserAccessAuditLogs => Set<UserAccessAuditLog>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureAppendOnlyHistory();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureAppendOnlyHistory();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

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
            entity.Property(user => user.IsActive).HasDefaultValue(true);
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
            entity.Property(expense => expense.Status).HasMaxLength(30).HasDefaultValue(ClaimStatuses.Draft);
            entity.Property(expense => expense.RowVersion).IsRowVersion();
            entity.Property(expense => expense.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(expense => new { expense.DepartmentId, expense.Status });
            entity.HasIndex(expense => expense.UserId);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Expenses_Amount", "Amount > 0"));
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Expenses_Status", "Status IN ('Draft', 'Submitted', 'Pending Approval', 'Approved', 'Rejected', 'Changes Requested', 'Resubmitted', 'Reimbursed')"));
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
            entity.Property(log => log.Action).HasMaxLength(40);
            entity.Property(log => log.Comments).HasMaxLength(500);
            entity.Property(log => log.ActionedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_ApprovalLogs_Action", "Action IN ('Approved', 'Rejected', 'Changes Requested', 'Reimbursed')"));
            entity.HasOne(log => log.Expense)
                .WithMany(expense => expense.ApprovalLogs)
                .HasForeignKey(log => log.ExpenseId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(log => log.ReviewedByUser)
                .WithMany(user => user.ApprovalLogs)
                .HasForeignKey(log => log.ReviewedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ClaimStatusHistory>(entity =>
        {
            entity.HasKey(history => history.ClaimStatusHistoryId);
            entity.Property(history => history.FromStatus).HasMaxLength(30);
            entity.Property(history => history.ToStatus).HasMaxLength(30);
            entity.Property(history => history.Decision).HasMaxLength(40);
            entity.Property(history => history.Notes).HasMaxLength(1000);
            entity.Property(history => history.OccurredAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(history => new { history.ExpenseId, history.OccurredAt });
            entity.HasOne(history => history.Expense)
                .WithMany(expense => expense.StatusHistory)
                .HasForeignKey(history => history.ExpenseId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(history => history.Actor)
                .WithMany()
                .HasForeignKey(history => history.ActorUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ApprovalDelegation>(entity =>
        {
            entity.Property(delegation => delegation.StartsAt).HasColumnType("datetimeoffset");
            entity.Property(delegation => delegation.EndsAt).HasColumnType("datetimeoffset");
            entity.Property(delegation => delegation.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(delegation => new
            {
                delegation.DepartmentId,
                delegation.DelegateUserId,
                delegation.StartsAt,
                delegation.EndsAt
            });
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_ApprovalDelegations_DateRange", "StartsAt < EndsAt"));
            entity.HasOne(delegation => delegation.Department)
                .WithMany()
                .HasForeignKey(delegation => delegation.DepartmentId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(delegation => delegation.Delegator)
                .WithMany()
                .HasForeignKey(delegation => delegation.DelegatorUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(delegation => delegation.Delegate)
                .WithMany()
                .HasForeignKey(delegation => delegation.DelegateUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ExpenseAttachment>(entity =>
        {
            entity.Property(attachment => attachment.BlobName).HasMaxLength(200);
            entity.Property(attachment => attachment.FileName).HasMaxLength(255);
            entity.Property(attachment => attachment.ContentType).HasMaxLength(100);
            entity.Property(attachment => attachment.UploadedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(attachment => attachment.BlobName).IsUnique();
            entity.HasOne(attachment => attachment.Expense)
                .WithMany(expense => expense.Attachments)
                .HasForeignKey(attachment => attachment.ExpenseId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(attachment => attachment.UploadedBy)
                .WithMany()
                .HasForeignKey(attachment => attachment.UploadedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<NotificationOutboxMessage>(entity =>
        {
            entity.Property(message => message.IdempotencyKey).HasMaxLength(100);
            entity.Property(message => message.EventType).HasMaxLength(100);
            entity.Property(message => message.Payload).HasColumnType("nvarchar(max)");
            entity.Property(message => message.LastError).HasMaxLength(2000);
            entity.HasIndex(message => message.IdempotencyKey).IsUnique();
            entity.HasIndex(message => new { message.DispatchedAt, message.NextAttemptAt, message.LockedUntil });
        });

        modelBuilder.Entity<UserNotification>(entity =>
        {
            entity.Property(notification => notification.Title).HasMaxLength(120);
            entity.Property(notification => notification.Message).HasMaxLength(700);
            entity.HasIndex(notification => new { notification.UserId, notification.ReadAt, notification.CreatedAt });
            entity.HasOne(notification => notification.User)
                .WithMany()
                .HasForeignKey(notification => notification.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(notification => notification.Expense)
                .WithMany()
                .HasForeignKey(notification => notification.ExpenseId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<UserAccessAuditLog>(entity =>
        {
            entity.HasIndex(log => new { log.OccurredAt, log.UserAccessAuditLogId });
            entity.HasOne(log => log.User)
                .WithMany()
                .HasForeignKey(log => log.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(log => log.Actor)
                .WithMany()
                .HasForeignKey(log => log.ActorUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private void EnsureAppendOnlyHistory()
    {
        if (ChangeTracker.Entries<ClaimStatusHistory>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Claim status history is append-only.");
        }
    }
}