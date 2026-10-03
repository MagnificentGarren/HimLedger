using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Security.Claims;
using HimLedger.Domain.Entities;
using HimLedger.Infrastructure;
using HimLedger.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace HimLedger.Api.IntegrationTests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly string _jwtSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private readonly WebApplicationFactory<Program> _testFactory;
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = Guid.NewGuid().ToString();
        _testFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("JwtSettings:Secret", _jwtSecret);
            builder.UseSetting("JwtSettings:Issuer", "HimLedger.Tests");
            builder.UseSetting("JwtSettings:Audience", "HimLedger.Tests");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=(localdb)\\mssqllocaldb;Database=HimLedgerTests;Trusted_Connection=True;");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<IReceiptStorage>();
                services.AddSingleton<IReceiptStorage, TestReceiptStorage>();
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(databaseName)
                    .Options;
                services.AddSingleton(options);
                services.AddScoped<ApplicationDbContext>(_ => new ApplicationDbContext(options));
            });
        });
        _client = _testFactory.CreateClient();
        SetUserToken(1);

        using var scope = _testFactory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Categories.AddRange(
            new Category { CategoryId = 1, Name = "Alpha" },
            new Category { CategoryId = 2, Name = "Beta" },
            new Category { CategoryId = 3, Name = "Gamma" });
        context.Departments.AddRange(
            new Department { DepartmentId = 1, Name = "North", Code = "N" },
            new Department { DepartmentId = 2, Name = "South", Code = "S" });
        context.Roles.AddRange(
            new Role { RoleId = 1, Name = "Admin" },
            new Role { RoleId = 2, Name = "Manager" },
            new Role { RoleId = 3, Name = "Employee" },
            new Role { RoleId = 4, Name = "Finance" });
        context.Users.AddRange(
            new User { UserId = 1, FirstName = "System", LastName = "Admin", Email = "admin@example.com", RoleId = 1 },
            new User { UserId = 10, FirstName = "Mina", LastName = "Manager", Email = "manager@example.com", RoleId = 2, DepartmentId = 1 },
            new User { UserId = 11, FirstName = "Eli", LastName = "North", Email = "north@example.com", RoleId = 3, DepartmentId = 1 },
            new User { UserId = 12, FirstName = "Ari", LastName = "South", Email = "south@example.com", RoleId = 3, DepartmentId = 2 },
            new User { UserId = 13, FirstName = "Fin", LastName = "Analyst", Email = "finance@example.com", RoleId = 4 },
            new User { UserId = 14, FirstName = "Una", LastName = "Assigned", Email = "unassigned@example.com", RoleId = 2 });
        context.Expenses.AddRange(
            new Expense { ExpenseId = 101, UserId = 11, CategoryId = 1, DepartmentId = 1, Title = "North claim", Amount = 10, ExpenseDate = DateTime.UtcNow.Date, Status = ClaimStatuses.PendingApproval, RowVersion = new byte[8] },
            new Expense { ExpenseId = 202, UserId = 12, CategoryId = 1, DepartmentId = 2, Title = "South claim", Amount = 20, ExpenseDate = DateTime.UtcNow.Date, Status = ClaimStatuses.PendingApproval, RowVersion = new byte[8] },
            new Expense { ExpenseId = 303, UserId = 10, CategoryId = 1, DepartmentId = 1, Title = "Delegated claim", Amount = 30, ExpenseDate = DateTime.UtcNow.Date, Status = ClaimStatuses.PendingApproval, RowVersion = new byte[8] });
        context.ClaimStatusHistory.AddRange(
            new ClaimStatusHistory { ExpenseId = 101, ActorUserId = 11, ToStatus = ClaimStatuses.PendingApproval, Decision = "Imported" },
            new ClaimStatusHistory { ExpenseId = 202, ActorUserId = 12, ToStatus = ClaimStatuses.PendingApproval, Decision = "Imported" },
            new ClaimStatusHistory { ExpenseId = 303, ActorUserId = 10, ToStatus = ClaimStatuses.PendingApproval, Decision = "Imported" });
        context.Budgets.AddRange(
            new Budget
            {
                DepartmentId = 1,
                FiscalYear = DateTime.UtcNow.Year,
                FiscalQuarter = ((DateTime.UtcNow.Month - 1) / 3) + 1,
                AllocatedAmount = 1000,
                RemainingAmount = 1000,
                RowVersion = new byte[8]
            },
            new Budget
            {
                DepartmentId = 2,
                FiscalYear = DateTime.UtcNow.Year,
                FiscalQuarter = ((DateTime.UtcNow.Month - 1) / 3) + 1,
                AllocatedAmount = 1000,
                RemainingAmount = 1000,
                RowVersion = new byte[8]
            });
        context.SaveChanges();
    }

    [Fact]
    public async Task Health_endpoint_reports_the_api_as_healthy()
    {
        using var response = await _client.GetAsync("/health");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Readiness_endpoint_reports_database_availability()
    {
        using var response = await _client.GetAsync("/ready");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Liveness_does_not_depend_on_database_readiness()
    {
        using var unhealthyFactory = _testFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
                services.Configure<HealthCheckServiceOptions>(options =>
                    options.Registrations.Add(new HealthCheckRegistration(
                        "test-database-failure",
                        _ => new FailingReadinessHealthCheck(),
                        HealthStatus.Unhealthy,
                        ["ready"]))));
        });
        using var client = unhealthyFactory.CreateClient();

        using var livenessResponse = await client.GetAsync("/healthz");
        using var readinessResponse = await client.GetAsync("/ready");

        Assert.Equal(System.Net.HttpStatusCode.OK, livenessResponse.StatusCode);
        Assert.Equal("Healthy", await livenessResponse.Content.ReadAsStringAsync());
        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, readinessResponse.StatusCode);
        Assert.Equal("Unhealthy", await readinessResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_routes_return_problem_details()
    {
        using var response = await _client.GetAsync("/route-that-does-not-exist");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.StartsWith(
            "application/problem+json",
            response.Content.Headers.ContentType?.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invalid_request_models_return_validation_problem_details()
    {
        using var response = await _client.PostAsJsonAsync("/api/Auth/register", new
        {
            firstName = "",
            lastName = "Lee",
            email = "not-an-email",
            password = "short",
            roleId = 1
        });

        Assert.True(
            response.StatusCode == System.Net.HttpStatusCode.BadRequest,
            await response.Content.ReadAsStringAsync());
        Assert.StartsWith(
            "application/problem+json",
            response.Content.Headers.ContentType?.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Categories_endpoint_returns_the_requested_page_and_total_count()
    {
        using var response = await _client.GetAsync("/api/Categories?page=2&pageSize=1");
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(1, body.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, body.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal("Beta", body.RootElement.GetProperty("items")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Employee_cannot_create_a_claim_for_a_client_selected_department()
    {
        SetUserToken(11);
        using var response = await _client.PostAsJsonAsync("/api/Expenses", new
        {
            categoryId = 1,
            departmentId = 2,
            title = "Wrong department",
            amount = 42,
            expenseDate = DateTime.UtcNow
        });

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Employee_claims_use_the_department_assigned_to_the_internal_account()
    {
        SetUserToken(11, new Claim("DepartmentId", "2"));
        using var response = await _client.PostAsJsonAsync("/api/Expenses", new
        {
            categoryId = 1,
            departmentId = 1,
            title = "Assigned department",
            amount = 42,
            expenseDate = DateTime.UtcNow
        });
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(11, body.RootElement.GetProperty("userId").GetInt32());
        Assert.Equal(1, body.RootElement.GetProperty("departmentId").GetInt32());
    }

    [Fact]
    public async Task Manager_cannot_read_or_approve_a_claim_outside_their_department()
    {
        SetUserToken(10, new Claim("DepartmentId", "2"), new Claim(ClaimTypes.Role, "Admin"));
        using var getResponse = await _client.GetAsync("/api/Expenses/202");
        using var updateResponse = await _client.PutAsJsonAsync("/api/Expenses/202/status", new
        {
            status = "Approved",
            comments = "cross-department attempt",
            rowVersion = Convert.ToBase64String(new byte[8])
        });

        Assert.Equal(System.Net.HttpStatusCode.NotFound, getResponse.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, updateResponse.StatusCode);
    }

    [Fact]
    public async Task Manager_without_a_department_cannot_approve_a_claim()
    {
        SetUserToken(14);
        using var response = await _client.PutAsJsonAsync("/api/Expenses/101/status", new
        {
            status = "Approved",
            rowVersion = Convert.ToBase64String(new byte[8])
        });

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Client_role_and_department_claims_do_not_override_internal_account_assignments()
    {
        SetUserToken(
            11,
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("DepartmentId", "2"));

        using var userResponse = await _client.GetAsync("/api/Users");
        using var expensesResponse = await _client.GetAsync("/api/Expenses");
        using var expenses = await expensesResponse.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, userResponse.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, expensesResponse.StatusCode);
        Assert.NotNull(expenses);
        Assert.Equal(1, expenses.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(101, expenses.RootElement.GetProperty("items")[0].GetProperty("expenseId").GetInt32());
    }

    [Fact]
    public async Task Local_token_must_map_to_an_internal_account()
    {
        SetUserToken(999);
        using var response = await _client.GetAsync("/api/Categories");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Public_registration_cannot_assign_an_elevated_role()
    {
        using var response = await _client.PostAsJsonAsync("/api/Auth/register", new
        {
            firstName = "Sam",
            lastName = "Lee",
            email = "sam@example.com",
            password = "a-strong-password",
            roleId = 1,
            departmentId = 2
        });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_failures_do_not_disclose_whether_the_account_exists_or_is_active()
    {
        using (var scope = _testFactory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await context.Users.SingleAsync(item => item.UserId == 11);
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "correct-password");
            await context.SaveChangesAsync();
        }

        using var wrongPasswordResponse = await _client.PostAsJsonAsync("/api/Auth/login", new
        {
            email = "north@example.com",
            password = "incorrect-password"
        });
        using var unknownUserResponse = await _client.PostAsJsonAsync("/api/Auth/login", new
        {
            email = "unknown@example.com",
            password = "incorrect-password"
        });
        using var wrongPassword = await wrongPasswordResponse.Content.ReadFromJsonAsync<JsonDocument>();
        using var unknownUser = await unknownUserResponse.Content.ReadFromJsonAsync<JsonDocument>();

        using (var scope = _testFactory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await context.Users.SingleAsync(item => item.UserId == 11);
            user.IsActive = false;
            await context.SaveChangesAsync();
        }
        using var inactiveUserResponse = await _client.PostAsJsonAsync("/api/Auth/login", new
        {
            email = "north@example.com",
            password = "correct-password"
        });
        using var inactiveUser = await inactiveUserResponse.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, wrongPasswordResponse.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, unknownUserResponse.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, inactiveUserResponse.StatusCode);
        var expectedDetail = "We couldn't sign you in. Check your work email and password, or contact your administrator.";
        Assert.Equal(expectedDetail, wrongPassword!.RootElement.GetProperty("detail").GetString());
        Assert.Equal(expectedDetail, unknownUser!.RootElement.GetProperty("detail").GetString());
        Assert.Equal(expectedDetail, inactiveUser!.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Claim_submission_records_each_lifecycle_transition_and_outbox_event()
    {
        SetUserToken(11);
        using var createResponse = await _client.PostAsJsonAsync("/api/Expenses", new
        {
            categoryId = 1,
            departmentId = 1,
            title = "Lifecycle test",
            amount = 18,
            expenseDate = DateTime.UtcNow
        });
        using var draft = await createResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(System.Net.HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(draft);
        var expenseId = draft.RootElement.GetProperty("expenseId").GetInt32();
        Assert.Equal(ClaimStatuses.Draft, draft.RootElement.GetProperty("status").GetString());

        using var submitResponse = await _client.PostAsJsonAsync($"/api/Expenses/{expenseId}/submit", new { notes = "Ready" });
        using var currentResponse = await _client.GetAsync($"/api/Expenses/{expenseId}");
        using var current = await currentResponse.Content.ReadFromJsonAsync<JsonDocument>();
        using var historyResponse = await _client.GetAsync($"/api/Expenses/{expenseId}/history");
        using var history = await historyResponse.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.Equal(System.Net.HttpStatusCode.NoContent, submitResponse.StatusCode);
        using var duplicateSubmitResponse = await _client.PostAsJsonAsync(
            $"/api/Expenses/{expenseId}/submit", new { notes = "Retry" });
        Assert.Equal(ClaimStatuses.PendingApproval, current!.RootElement.GetProperty("status").GetString());
        Assert.Equal(System.Net.HttpStatusCode.Conflict, duplicateSubmitResponse.StatusCode);
        Assert.Equal(
            new[] { ClaimStatuses.Draft, ClaimStatuses.Submitted, ClaimStatuses.PendingApproval },
            history!.RootElement.EnumerateArray().Select(item => item.GetProperty("toStatus").GetString()).ToArray());
        using var scope = _testFactory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await context.NotificationOutboxMessages.CountAsync(item =>
            item.Payload.Contains($"\"expenseId\":{expenseId}", StringComparison.Ordinal)));

        SetUserToken(11);
        using var employeeNotificationsResponse = await _client.GetAsync("/api/Notifications");
        using var employeeNotifications = await employeeNotificationsResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(0, employeeNotifications!.RootElement.GetProperty("unreadCount").GetInt32());

        SetUserToken(10);
        using var managerNotificationsResponse = await _client.GetAsync("/api/Notifications");
        using var managerNotifications = await managerNotificationsResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Contains(
            managerNotifications!.RootElement.GetProperty("notifications").EnumerateArray(),
            item => item.GetProperty("expenseId").GetInt32() == expenseId);

        SetUserToken(13);
        using var financeNotificationsResponse = await _client.GetAsync("/api/Notifications");
        using var financeNotifications = await financeNotificationsResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Contains(
            financeNotifications!.RootElement.GetProperty("notifications").EnumerateArray(),
            item => item.GetProperty("expenseId").GetInt32() == expenseId);
    }

    [Fact]
    public async Task Notifications_are_private_to_the_recipient_and_can_be_marked_read()
    {
        long ownNotificationId;
        long anotherUsersNotificationId;
        using (var scope = _testFactory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ownNotification = new UserNotification
            {
                UserId = 11,
                ExpenseId = 101,
                Title = "Your claim was rejected",
                Message = "Please review the comment."
            };
            var anotherUsersNotification = new UserNotification
            {
                UserId = 12,
                ExpenseId = 202,
                Title = "A different claim",
                Message = "This notice belongs to another employee."
            };
            context.UserNotifications.AddRange(ownNotification, anotherUsersNotification);
            await context.SaveChangesAsync();
            ownNotificationId = ownNotification.UserNotificationId;
            anotherUsersNotificationId = anotherUsersNotification.UserNotificationId;
        }

        SetUserToken(11);
        using var response = await _client.GetAsync("/api/Notifications");
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        using var unauthorizedRead = await _client.PostAsync(
            $"/api/Notifications/{anotherUsersNotificationId}/read",
            content: null);
        using var markRead = await _client.PostAsync($"/api/Notifications/{ownNotificationId}/read", content: null);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, body!.RootElement.GetProperty("unreadCount").GetInt32());
        Assert.Single(body.RootElement.GetProperty("notifications").EnumerateArray());
        Assert.Equal(System.Net.HttpStatusCode.NotFound, unauthorizedRead.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, markRead.StatusCode);

        using var scopeAfterRead = _testFactory.Services.CreateScope();
        var contextAfterRead = scopeAfterRead.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.NotNull((await contextAfterRead.UserNotifications.SingleAsync(item =>
            item.UserNotificationId == ownNotificationId)).ReadAt);
    }

    [Fact]
    public async Task Missing_budget_returns_a_specific_reason_and_keeps_the_claim_pending()
    {
        using (var scope = _testFactory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var budget = await context.Budgets.SingleAsync(item => item.DepartmentId == 1);
            context.Budgets.Remove(budget);
            await context.SaveChangesAsync();
        }

        SetUserToken(10);
        using var response = await _client.PutAsJsonAsync("/api/Expenses/101/status", new
        {
            status = ClaimStatuses.Approved,
            comments = "Eligible review attempt",
            rowVersion = Convert.ToBase64String(new byte[8])
        });
        using var problem = await response.Content.ReadFromJsonAsync<JsonDocument>();
        using var scopeAfterDecision = _testFactory.Services.CreateScope();
        var contextAfterDecision = scopeAfterDecision.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("No budget is allocated", problem!.RootElement.GetProperty("detail").GetString());
        Assert.Contains("Ask Finance or an administrator", problem.RootElement.GetProperty("detail").GetString());
        Assert.Equal(ClaimStatuses.PendingApproval, (await contextAfterDecision.Expenses
            .SingleAsync(item => item.ExpenseId == 101)).Status);
    }

    [Fact]
    public async Task Admin_can_deactivate_accounts_without_deleting_their_audit_history()
    {
        SetUserToken(1);
        using var response = await _client.PutAsJsonAsync("/api/Users/11/active", new { isActive = false });

        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
        using var scope = _testFactory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False((await context.Users.SingleAsync(item => item.UserId == 11)).IsActive);
        var accessLog = await context.UserAccessAuditLogs.SingleAsync();
        Assert.Equal(11, accessLog.UserId);
        Assert.Equal(1, accessLog.ActorUserId);
        Assert.False(accessLog.IsActive);

        SetUserToken(11);
        using var inactiveResponse = await _client.GetAsync("/api/Categories");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, inactiveResponse.StatusCode);
    }

    [Fact]
    public async Task Invalid_lifecycle_transitions_are_rejected()
    {
        SetUserToken(11);
        using var response = await _client.PostAsJsonAsync("/api/Expenses/101/resubmit", new { notes = "Not requested" });

        Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Changes_requested_claim_can_be_resubmitted_to_pending_approval()
    {
        SetUserToken(10);
        using var reviewResponse = await _client.PutAsJsonAsync("/api/Expenses/101/status", new
        {
            status = ClaimStatuses.ChangesRequested,
            comments = "Please add a business purpose.",
            rowVersion = Convert.ToBase64String(new byte[8])
        });
        Assert.Equal(System.Net.HttpStatusCode.NoContent, reviewResponse.StatusCode);

        SetUserToken(11);
        using var resubmitResponse = await _client.PostAsJsonAsync("/api/Expenses/101/resubmit", new { notes = "Added purpose." });
        using var claimResponse = await _client.GetAsync("/api/Expenses/101");
        using var claim = await claimResponse.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.Equal(System.Net.HttpStatusCode.NoContent, resubmitResponse.StatusCode);
        Assert.Equal(ClaimStatuses.PendingApproval, claim!.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Active_department_delegation_allows_another_user_to_approve()
    {
        var now = DateTimeOffset.UtcNow;
        SetUserToken(10);
        using var delegationResponse = await _client.PostAsJsonAsync("/api/ApprovalDelegations", new
        {
            delegateUserId = 11,
            startsAt = now.AddMinutes(-1),
            endsAt = now.AddDays(1)
        });
        Assert.Equal(System.Net.HttpStatusCode.Created, delegationResponse.StatusCode);

        SetUserToken(11);
        using var reviewResponse = await _client.PutAsJsonAsync("/api/Expenses/303/status", new
        {
            status = ClaimStatuses.Approved,
            comments = "Delegated approval",
            rowVersion = Convert.ToBase64String(new byte[8])
        });

        Assert.Equal(System.Net.HttpStatusCode.NoContent, reviewResponse.StatusCode);
    }

    [Fact]
    public async Task Approved_claim_can_be_reimbursed_by_finance()
    {
        SetUserToken(10);
        using var approveResponse = await _client.PutAsJsonAsync("/api/Expenses/101/status", new
        {
            status = ClaimStatuses.Approved,
            comments = "Within budget",
            rowVersion = Convert.ToBase64String(new byte[8])
        });
        Assert.Equal(System.Net.HttpStatusCode.NoContent, approveResponse.StatusCode);

        SetUserToken(13);
        using var reimburseResponse = await _client.PostAsJsonAsync("/api/Expenses/101/reimburse", new
        {
            notes = "Paid"
        });
        using var claimResponse = await _client.GetAsync("/api/Expenses/101");
        using var claim = await claimResponse.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.Equal(System.Net.HttpStatusCode.NoContent, reimburseResponse.StatusCode);
        Assert.Equal(ClaimStatuses.Reimbursed, claim!.RootElement.GetProperty("status").GetString());

        SetUserToken(13);
        var year = DateTime.UtcNow.Year;
        var quarter = ((DateTime.UtcNow.Month - 1) / 3) + 1;
        using var budgetResponse = await _client.GetAsync($"/api/Budgets?fiscalYear={year}&fiscalQuarter={quarter}");
        using var budgets = await budgetResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var departmentBudget = budgets!.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("departmentId").GetInt32() == 1);
        Assert.Equal(10m, departmentBudget.GetProperty("approvedAmount").GetDecimal());
        Assert.Equal(10m, departmentBudget.GetProperty("reimbursedAmount").GetDecimal());
        Assert.Equal(960m, departmentBudget.GetProperty("remainingAmount").GetDecimal());
    }

    [Fact]
    public async Task Employee_cannot_approve_their_own_claim_through_delegation()
    {
        var now = DateTimeOffset.UtcNow;
        SetUserToken(10);
        using var delegationResponse = await _client.PostAsJsonAsync("/api/ApprovalDelegations", new
        {
            delegateUserId = 11,
            startsAt = now.AddMinutes(-1),
            endsAt = now.AddDays(1)
        });
        Assert.Equal(System.Net.HttpStatusCode.Created, delegationResponse.StatusCode);

        SetUserToken(11);
        using var reviewResponse = await _client.PutAsJsonAsync("/api/Expenses/101/status", new
        {
            status = ClaimStatuses.Approved,
            comments = "Self approval",
            rowVersion = Convert.ToBase64String(new byte[8])
        });

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, reviewResponse.StatusCode);
    }

    [Fact]
    public async Task Receipt_upload_rejects_content_that_does_not_match_an_allowed_file_type()
    {
        SetUserToken(11);
        using var createResponse = await _client.PostAsJsonAsync("/api/Expenses", new
        {
            categoryId = 1,
            departmentId = 1,
            title = "Invalid receipt test",
            amount = 15,
            expenseDate = DateTime.UtcNow
        });
        using var draft = await createResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(System.Net.HttpStatusCode.Created, createResponse.StatusCode);
        var expenseId = draft!.RootElement.GetProperty("expenseId").GetInt32();

        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("not a receipt")), "file", "receipt.png");

        using var response = await _client.PostAsync($"/api/Expenses/{expenseId}/attachments", multipart);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Valid_pdf_receipt_is_saved_and_served_with_a_temporary_read_url()
    {
        SetUserToken(11);
        using var createResponse = await _client.PostAsJsonAsync("/api/Expenses", new
        {
            categoryId = 1,
            departmentId = 1,
            title = "Valid receipt test",
            amount = 15,
            expenseDate = DateTime.UtcNow
        });
        using var draft = await createResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var expenseId = draft!.RootElement.GetProperty("expenseId").GetInt32();
        using var multipart = new MultipartFormDataContent();
        multipart.Add(
            new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.7\nreceipt")),
            "file",
            "receipt.pdf");

        using var uploadResponse = await _client.PostAsync($"/api/Expenses/{expenseId}/attachments", multipart);
        using var uploadBody = await uploadResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var attachmentId = uploadBody!.RootElement.GetProperty("expenseAttachmentId").GetInt32();
        using var downloadResponse = await _client.GetAsync(
            $"/api/Expenses/{expenseId}/attachments/{attachmentId}");
        using var downloadBody = await downloadResponse.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.Equal(System.Net.HttpStatusCode.Created, uploadResponse.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.Equal(300, downloadBody!.RootElement.GetProperty("expiresInSeconds").GetInt32());
        Assert.StartsWith("https://example.test/", downloadBody.RootElement.GetProperty("url").GetString());
    }

    [Fact]
    public async Task Budgets_report_committed_approved_and_reimbursed_claims_from_expenses()
    {
        SetUserToken(1);
        var year = DateTime.UtcNow.Year;
        var quarter = ((DateTime.UtcNow.Month - 1) / 3) + 1;
        using var response = await _client.GetAsync($"/api/Budgets?fiscalYear={year}&fiscalQuarter={quarter}");
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var departmentBudget = body!.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("departmentId").GetInt32() == 1);

        Assert.Equal(40m, departmentBudget.GetProperty("committedAmount").GetDecimal());
        Assert.Equal(0m, departmentBudget.GetProperty("approvedAmount").GetDecimal());
        Assert.Equal(0m, departmentBudget.GetProperty("reimbursedAmount").GetDecimal());
        Assert.Equal(960m, departmentBudget.GetProperty("remainingAmount").GetDecimal());
    }

    [Fact]
    public async Task Approval_is_rejected_when_commitment_would_exceed_the_department_budget()
    {
        using (var scope = _testFactory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var budget = await context.Budgets.SingleAsync(item => item.DepartmentId == 1);
            budget.AllocatedAmount = 35;
            await context.SaveChangesAsync();
        }

        SetUserToken(10);
        using var response = await _client.PutAsJsonAsync("/api/Expenses/101/status", new
        {
            status = ClaimStatuses.Approved,
            comments = "Test budget limit",
            rowVersion = Convert.ToBase64String(new byte[8])
        });

        Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Reconciliation_csv_filters_line_items_and_includes_the_audit_trace()
    {
        SetUserToken(1);
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        using var response = await _client.GetAsync(
            $"/api/Expenses/reconciliation.csv?fromDate={date}&toDate={date}&departmentId=1&categoryId=1");
        var csv = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("ExpenseId,UserId,Title,Amount", csv, StringComparison.Ordinal);
        Assert.Contains("\"101\"", csv, StringComparison.Ordinal);
        Assert.Contains("actor=11", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"202\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Claim_status_history_rejects_update_and_delete_operations()
    {
        using (var scope = _testFactory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var history = await context.ClaimStatusHistory.FirstAsync();
            history.Notes = "tampered";
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        }

        using (var scope = _testFactory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var history = await context.ClaimStatusHistory.FirstAsync();
            context.ClaimStatusHistory.Remove(history);
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        }
    }

    private void SetUserToken(int userId, params Claim[] extraClaims)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        claims.AddRange(extraClaims);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_jwtSecret, claims));
    }

    private static string CreateToken(string jwtSecret, IEnumerable<Claim> claims)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "HimLedger.Tests",
            audience: "HimLedger.Tests",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public void Dispose()
    {
        _client.Dispose();
        _testFactory.Dispose();
    }

    private sealed class FailingReadinessHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Unhealthy());
    }

    private sealed class TestReceiptStorage : IReceiptStorage
    {
        public Task StoreAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<Uri> CreateReadSasAsync(string blobName, CancellationToken cancellationToken) =>
            Task.FromResult(new Uri("https://example.test/receipt?sig=test"));

        public Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
