using Microsoft.Data.Sqlite;
using MonitorApi.Data;
using MonitorApi.Data.Entities;
using MonitorApi.Models;

namespace MonitorApi.Workers;

public class DataQualityWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DataQualityWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = configuration.GetSection("Monitoring").Get<MonitoringOptions>()
                   ?? new MonitoringOptions();
        var interval = TimeSpan.FromSeconds(opts.DataQualityIntervalSeconds);

        logger.LogInformation("DataQualityWorker started — running every {Interval}s", opts.DataQualityIntervalSeconds);

        // รอให้ระบบพร้อมก่อน
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunRulesAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "DataQuality check cycle failed");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task RunRulesAsync(CancellationToken ct)
    {
        var rules = configuration.GetSection("DataQualityRules").Get<List<DataQualityRule>>() ?? [];
        if (rules.Count == 0) return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MonitorDbContext>();
        var mssqlOpts = configuration.GetSection("Mssql").Get<MssqlOptions>() ?? new MssqlOptions();

        foreach (var rule in rules)
        {
            var connStr = rule.Database == "UAT"
                ? mssqlOpts.BuildUatConnectionString()
                : mssqlOpts.BuildDevConnectionString();

            var record = new DataQualityRecord
            {
                RuleName = rule.RuleName,
                Environment = rule.Database,
                Severity = rule.Severity,
                CheckedAt = DateTime.UtcNow,
            };

            try
            {
                record.ViolationCount = await RunSqlAsync(connStr, rule.SqlQuery, ct);
                logger.LogDebug("DataQuality {Rule}: {Count} violations", rule.RuleName, record.ViolationCount);
            }
            catch (Exception ex)
            {
                record.ViolationCount = -1;
                record.ErrorMessage = ex.Message;
                logger.LogWarning("DataQuality rule {Rule} failed: {Error}", rule.RuleName, ex.Message);
            }

            db.DataQualityRecords.Add(record);
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task<int> RunSqlAsync(string connStr, string sql, CancellationToken ct)
    {
        // ใช้ Microsoft.Data.SqlClient สำหรับ MSSQL จริง
        // ตัวอย่างนี้ใช้ SqlConnection pattern
        using var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr);
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 30;
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }
}

public class MssqlOptions
{
    public string HostDev { get; set; } = "";
    public int PortDev { get; set; } = 1433;
    public string UserDev { get; set; } = "sa";
    public string PasswordDev { get; set; } = "";

    public string HostUat { get; set; } = "";
    public int PortUat { get; set; } = 1433;
    public string UserUat { get; set; } = "sa";
    public string PasswordUat { get; set; } = "";

    public string BuildDevConnectionString() =>
        $"Server={HostDev},{PortDev};User Id={UserDev};Password={PasswordDev};TrustServerCertificate=true;";

    public string BuildUatConnectionString() =>
        $"Server={HostUat},{PortUat};User Id={UserUat};Password={PasswordUat};TrustServerCertificate=true;";
}
