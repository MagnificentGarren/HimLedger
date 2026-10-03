using HimLedger.Domain.Entities;
using HimLedger.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HimLedger.Api.Services;

internal static class DevelopmentTestAccountSeeder
{
    public const string Password = "Test123!";

    private static readonly (string Email, string FirstName, string LastName, string RoleName)[] Accounts =
    [
        ("admin.test@himledger.local", "Test", "Admin", "Admin"),
        ("manager.test@himledger.local", "Test", "Manager", "Manager"),
        ("employee.test@himledger.local", "Test", "Employee", "Employee"),
        ("finance.test@himledger.local", "Test", "Finance", "Finance")
    ];

    public static async Task<int> SeedAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var roles = await context.Roles.ToDictionaryAsync(
            role => role.Name,
            role => role.RoleId,
            cancellationToken);
        var missingRoles = Accounts
            .Select(account => account.RoleName)
            .Distinct()
            .Where(roleName => !roles.ContainsKey(roleName))
            .Select(roleName => new Role { Name = roleName })
            .ToArray();

        if (missingRoles.Length > 0)
        {
            context.Roles.AddRange(missingRoles);
            await context.SaveChangesAsync(cancellationToken);

            foreach (var role in missingRoles)
            {
                roles.Add(role.Name, role.RoleId);
            }
        }

        var departmentId = await context.Departments
            .OrderBy(department => department.DepartmentId)
            .Select(department => (int?)department.DepartmentId)
            .FirstOrDefaultAsync(cancellationToken);
        var passwordHasher = new PasswordHasher<User>();
        var addedCount = 0;

        foreach (var account in Accounts)
        {
            var roleId = roles[account.RoleName];

            int? accountDepartmentId = account.RoleName is "Manager" or "Employee"
                ? departmentId ?? throw new InvalidOperationException(
                    $"Cannot seed development test accounts: role '{account.RoleName}' requires a department.")
                : null;

            if (await context.Users.AnyAsync(user => user.Email == account.Email, cancellationToken))
            {
                continue;
            }

            var user = new User
            {
                FirstName = account.FirstName,
                LastName = account.LastName,
                Email = account.Email,
                RoleId = roleId,
                DepartmentId = accountDepartmentId
            };
            user.PasswordHash = passwordHasher.HashPassword(user, Password);
            context.Users.Add(user);
            addedCount++;
        }

        if (addedCount > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return addedCount;
    }
}
