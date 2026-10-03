using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HimLedger.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(HealthCheckService healthCheckService) : ControllerBase
{
    [HttpGet]
    [HttpGet("/ready")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetReadiness(CancellationToken cancellationToken)
    {
        var report = await healthCheckService.CheckHealthAsync(
            check => check.Tags.Contains("ready"),
            cancellationToken);
        return report.Status == HealthStatus.Healthy
            ? Ok(report.Status.ToString())
            : StatusCode(StatusCodes.Status503ServiceUnavailable, report.Status.ToString());
    }

    [HttpGet("/healthz")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public IActionResult GetLiveness() => Ok(HealthStatus.Healthy.ToString());
}
