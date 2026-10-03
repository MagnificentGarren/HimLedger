using HimLedger.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HimLedger.Api.Services;

public sealed class DatabaseHealthCheck(ApplicationDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The database is unavailable.");
    }
}
