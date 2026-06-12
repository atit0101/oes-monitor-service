using Microsoft.AspNetCore.Mvc;
using MonitorApi.Services;

namespace MonitorApi.Controllers;

[ApiController]
[Route("api/db-verification")]
public class DbVerificationController(HealthPollerService poller) : ControllerBase
{
    // GET /api/db-verification?env=DEV
    // ใช้ใน Grafana Table panel: Service | DB Server | Actual DB | Expected DB | Match
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string env = "DEV",
        CancellationToken ct = default)
    {
        var records = await poller.GetLatestAsync(env.ToUpper(), ct);

        var dbRecords = records
            .Where(r => r.DbServer is not null || r.ActualDatabase is not null)
            .Select(r => new
            {
                service      = r.ServiceName,
                environment  = r.Environment,
                dbServer     = r.DbServer ?? "-",
                actualDb     = r.ActualDatabase ?? "-",
                expectedDb   = r.ExpectedDatabase ?? "-",
                isConnected  = r.DbConnected ?? false,
                dbMatch      = r.DbMatch ?? false,
                status       = GetDbStatus(r.DbConnected, r.DbMatch),
                checkedAt    = r.CheckedAt,
            });

        return Ok(dbRecords);
    }

    // GET /api/db-verification/all — ทั้ง DEV และ UAT
    [HttpGet("all")]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var dev = await poller.GetLatestAsync("DEV", ct);
        var uat = await poller.GetLatestAsync("UAT", ct);
        var all = dev.Concat(uat)
            .Where(r => r.DbServer is not null || r.ActualDatabase is not null)
            .Select(r => new
            {
                service      = r.ServiceName,
                environment  = r.Environment,
                dbServer     = r.DbServer ?? "-",
                actualDb     = r.ActualDatabase ?? "-",
                expectedDb   = r.ExpectedDatabase ?? "-",
                isConnected  = r.DbConnected ?? false,
                dbMatch      = r.DbMatch ?? false,
                status       = GetDbStatus(r.DbConnected, r.DbMatch),
                checkedAt    = r.CheckedAt,
            });

        return Ok(all);
    }

    private static string GetDbStatus(bool? connected, bool? match) =>
        (connected, match) switch
        {
            (false, _)   => "NOT_CONNECTED",
            (true, false) => "WRONG_DB",
            (true, true)  => "OK",
            _             => "UNKNOWN",
        };
}
