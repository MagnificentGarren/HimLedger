using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HimLedger.Api;
using HimLedger.Infrastructure;
using HimLedger.Infrastructure.Services;
using HimLedger.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services
    .AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
builder.Services.AddProblemDetails();
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
if (!string.IsNullOrWhiteSpace(builder.Configuration["ApplicationInsights:ConnectionString"]))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    });
}
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "HimLedger API",
        Version = "v1",
        Description = "HimLedger Corporate Expense & Budget Management API"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' followed by a space and your JWT token."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IReceiptStorage, AzureReceiptStorage>();
builder.Services.AddHttpClient(nameof(NotificationOutboxDispatcher));
builder.Services.AddHostedService<NotificationOutboxDispatcher>();

var jwtSecret = builder.Configuration["JwtSettings:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) && builder.Environment.IsDevelopment())
{
    jwtSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    builder.Configuration["JwtSettings:Secret"] = jwtSecret;
}
if (jwtSecret is not null && Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    throw new InvalidOperationException("JwtSettings:Secret must be at least 32 bytes.");
}

var entraAuthority = builder.Configuration["EntraId:Authority"]?.TrimEnd('/');
var entraAudience = builder.Configuration["EntraId:Audience"];
if (string.IsNullOrWhiteSpace(entraAuthority) != string.IsNullOrWhiteSpace(entraAudience))
{
    throw new InvalidOperationException("EntraId:Authority and EntraId:Audience must both be configured.");
}
if (jwtSecret is null && string.IsNullOrWhiteSpace(entraAuthority))
{
    throw new InvalidOperationException("Configure Entra ID or a local JWT secret before starting the API.");
}

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "HimLedgerBearer";
        options.DefaultChallengeScheme = "HimLedgerBearer";
    })
    .AddPolicyScheme("HimLedgerBearer", "Entra ID or local JWT", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var token = context.Request.Headers.Authorization.ToString();
            if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var issuer = new JwtSecurityTokenHandler()
                        .ReadJwtToken(token["Bearer ".Length..])
                        .Issuer;
                    if (!string.IsNullOrWhiteSpace(entraAuthority)
                        && issuer.StartsWith(entraAuthority, StringComparison.OrdinalIgnoreCase))
                    {
                        return "Entra";
                    }
                }
                catch (ArgumentException)
                {
                    // Malformed tokens are sent to the local handler and rejected there.
                }
                catch (SecurityTokenException)
                {
                    // Malformed tokens are sent to the local handler and rejected there.
                }
            }

            return "LocalJwt";
        };
    })
    .AddJwtBearer("LocalJwt", options =>
    {
        if (jwtSecret is null)
        {
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    context.Fail("Local JWT authentication is not configured.");
                    return Task.CompletedTask;
                }
            };
            return;
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context => InternalIdentityClaims.ResolveAsync(context, isEntraToken: false)
        };
    });
if (!string.IsNullOrWhiteSpace(entraAuthority))
{
    builder.Services.AddAuthentication()
        .AddJwtBearer("Entra", options =>
        {
            options.Authority = entraAuthority;
            options.Audience = entraAudience;
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context => InternalIdentityClaims.ResolveAsync(context, isEntraToken: true)
            };
        });
}

builder.Services.AddAuthorization();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
if (allowedOrigins is null || allowedOrigins.Length == 0)
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("Configure Cors:AllowedOrigins with the deployed frontend origin.");
    }

    allowedOrigins = ["http://localhost:5173", "http://127.0.0.1:5173"];
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

if (app.Environment.IsDevelopment()
    && app.Configuration.GetValue<bool>("DevelopmentTestAccounts:Enabled"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var changedCount = await DevelopmentTestAccountSeeder.SeedAsync(context);
    app.Logger.LogInformation(
        "Development test account seeding complete; {ChangedCount} account(s) added or had credentials reset.",
        changedCount);
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "HimLedger API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
