using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Security.Claims;
using HimLedger.Domain.Entities;
using HimLedger.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
            new Expense { ExpenseId = 101, UserId = 11, CategoryId = 1, DepartmentId = 1, Title = "North claim", Amount = 10, ExpenseDate = DateTime.UtcNow, Status = "Pending", RowVersion = new byte[8] },
            new Expense { ExpenseId = 202, UserId = 12, CategoryId = 1, DepartmentId = 2, Title = "South claim", Amount = 20, ExpenseDate = DateTime.UtcNow, Status = "Pending", RowVersion = new byte[8] });
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
}
