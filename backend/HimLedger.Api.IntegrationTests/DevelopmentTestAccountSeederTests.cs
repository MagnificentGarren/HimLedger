using HimLedger.Api.Services;
using HimLedger.Domain.Entities;
using HimLedger.Infrastructure;
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
            .SingleAsync(user => user.Email == "finance.test@himledger.local");
        Assert.Equal(financeRole.RoleId, financeAccount.RoleId);
        Assert.Equal("Finance", financeAccount.Role.Name);
    }
}
