using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonitorApi.Data;

namespace MonitorApi.Controllers;

[ApiController]
[Route("api/data-quality")]
public class DataQualityController(MonitorDbContext db) : ControllerBase
{
    // GET /api/data-quality?env=DEV
    [HttpGet]
    public async Task<IActionResult> GetLatest(
        [FromQuery] string env = "DEV",
        CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-10);
        var records = await db.DataQualityRecords
            .Where(r => r.Environment == env.ToUpper() && r.CheckedAt >= cutoff)
            .GroupBy(r => r.RuleName)
            .Select(g => g.OrderByDescending(r => r.CheckedAt).First())
            .ToListAsync(ct);

        return Ok(records.Select(r => new
        {
            r.RuleName,
            r.Environment,
            r.Severity,
            r.ViolationCount,
            HasViolations = r.ViolationCount > 0,
            r.ErrorMessage,
            r.CheckedAt,
            StatusIcon = r.ViolationCount == 0 ? "✅" : r.Severity == "critical" ? "🚨" : "⚠️",
        }));
    }

    // GET /api/data-quality/{ruleName}/history?env=DEV&hours=24
    [HttpGet("{ruleName}/history")]
    public async Task<IActionResult> GetHistory(
        string ruleName,
        [FromQuery] string env = "DEV",
        [FromQuery] int hours = 24,
        CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddHours(-hours);
        var records = await db.DataQualityRecords
            .Where(r => r.RuleName == ruleName
                     && r.Environment == env.ToUpper()
                     && r.CheckedAt >= cutoff)
            .OrderByDescending(r => r.CheckedAt)
            .Select(r => new { r.CheckedAt, r.ViolationCount, r.Severity })
            .ToListAsync(ct);

        return Ok(records);
    }
}
