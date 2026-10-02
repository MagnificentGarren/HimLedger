using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
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
    private const string JwtSecret = "integration-test-secret-at-least-32-bytes";
    private readonly WebApplicationFactory<Program> _testFactory;
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = Guid.NewGuid().ToString();
        _testFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("JwtSettings:Secret", JwtSecret);
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
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken());

        using var scope = _testFactory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Categories.AddRange(
            new Category { CategoryId = 1, Name = "Alpha" },
            new Category { CategoryId = 2, Name = "Beta" },
            new Category { CategoryId = 3, Name = "Gamma" });
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

    private static string CreateToken()
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "HimLedger.Tests",
            audience: "HimLedger.Tests",
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
