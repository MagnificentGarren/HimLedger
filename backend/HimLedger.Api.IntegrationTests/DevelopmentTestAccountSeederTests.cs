using HimLedger.Api.Services;
using HimLedger.Domain.Entities;
using HimLedger.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HimLedger.Api.IntegrationTests;

public sealed class DevelopmentTestAccountSeederTests
{
    [Fact]
    public async Task SeedAsync_creates_missing_role_before_seeding_test_accounts()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        context.Roles.AddRange(
            new Role { RoleId = 1, Name = "Admin" },
            new Role { RoleId = 2, Name = "Manager" },
            new Role { RoleId = 3, Name = "Employee" });
        context.Departments.Add(new Department { DepartmentId = 1, Name = "Operations", Code = "OPS" });
        await context.SaveChangesAsync();

        var addedCount = await DevelopmentTestAccountSeeder.SeedAsync(context);

        Assert.Equal(4, addedCount);
        var financeRole = await context.Roles.SingleAsync(role => role.Name == "Finance");
        var financeAccount = await context.Users
            .Include(user => user.Role)
            .SingleAsync(user => user.Email == "finance@himledger.test");
        Assert.Equal(financeRole.RoleId, financeAccount.RoleId);
        Assert.Equal("Finance", financeAccount.Role.Name);
        Assert.Equal(
            PasswordVerificationResult.Success,
            new PasswordHasher<User>().VerifyHashedPassword(
                financeAccount,
                financeAccount.PasswordHash,
                DevelopmentTestAccountSeeder.Password));
    }

    [Fact]
    public async Task SeedAsync_resets_passwords_for_existing_test_accounts_and_is_idempotent()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        context.Roles.AddRange(
            new Role { RoleId = 1, Name = "Admin" },
            new Role { RoleId = 2, Name = "Manager" },
            new Role { RoleId = 3, Name = "Employee" },
            new Role { RoleId = 4, Name = "Finance" });
        context.Departments.Add(new Department { DepartmentId = 1, Name = "Operations", Code = "OPS" });
        var existingAccount = new User
        {
            UserId = 1,
            FirstName = "Old",
            LastName = "Employee",
            Email = "employee@himledger.test",
            RoleId = 3,
            DepartmentId = 1
        };
        var passwordHasher = new PasswordHasher<User>();
        existingAccount.PasswordHash = passwordHasher.HashPassword(existingAccount, "oldpass1");
        context.Users.Add(existingAccount);
        await context.SaveChangesAsync();

        var changedCount = await DevelopmentTestAccountSeeder.SeedAsync(context);

        Assert.Equal(4, changedCount);
        Assert.Equal(
            PasswordVerificationResult.Success,
            passwordHasher.VerifyHashedPassword(
                existingAccount,
                existingAccount.PasswordHash,
                DevelopmentTestAccountSeeder.Password));
        Assert.Equal(0, await DevelopmentTestAccountSeeder.SeedAsync(context));
    }
}
