using Microsoft.AspNetCore.Mvc;
using MonitorApi.Models;
using MonitorApi.Services;

namespace MonitorApi.Controllers;

[ApiController]
[Route("api/health")]
public class HealthSummaryController(
    HealthPollerService poller,
    IConfiguration configuration) : ControllerBase
{
    // GET /api/health?env=DEV
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string env = "DEV",
        CancellationToken ct = default)
    {
        var records = await poller.GetLatestAsync(env.ToUpper(), ct);
        var services = configuration.GetSection("Services").Get<List<ServiceConfig>>() ?? [];

        var result = services.Select(svc =>
        {
            var rec = records.FirstOrDefault(r => r.ServiceName == svc.Name);
            return new ServiceHealthSummary
            {
                ServiceName = svc.Name,
                DisplayName = svc.DisplayName,
                Environment = env.ToUpper(),
                Status = rec?.OverallStatus ?? "Unknown",
                IsHealthy = rec?.IsHealthy ?? false,
                ResponseTimeMs = rec?.ResponseTimeMs,
                CheckedAt = rec?.CheckedAt ?? DateTime.UtcNow,
                Database = rec?.DbServer is not null ? new DbVerificationInfo
                {
                    Server = rec.DbServer,
                    ActualDatabase = rec.ActualDatabase,
                    ExpectedDatabase = rec.ExpectedDatabase,
                    IsConnected = rec.DbConnected ?? false,
                    DbMatch = rec.DbMatch ?? false,
                } : null,
            };
        });

        return Ok(result);
    }

    // GET /api/health/{serviceName}/history?env=DEV&hours=24
    [HttpGet("{serviceName}/history")]
    public async Task<IActionResult> GetHistory(
        string serviceName,
        [FromQuery] string env = "DEV",
        [FromQuery] int hours = 24,
        CancellationToken ct = default)
    {
        var records = await poller.GetHistoryAsync(serviceName, env.ToUpper(), hours, ct);
        return Ok(records.Select(r => new
        {
            r.CheckedAt,
            r.IsHealthy,
            r.OverallStatus,
            r.ResponseTimeMs,
            UptimeValue = r.IsHealthy ? 1 : 0,
        }));
    }
}
