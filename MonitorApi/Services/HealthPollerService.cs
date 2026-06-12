using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MonitorApi.Data;
using MonitorApi.Data.Entities;
using MonitorApi.Models;

namespace MonitorApi.Services;

public class HealthPollerService(
    IHttpClientFactory httpClientFactory,
    MonitorDbContext db,
    ILogger<HealthPollerService> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task PollAllAsync(
        IEnumerable<ServiceConfig> services,
        MonitoringOptions options,
        CancellationToken ct)
    {
        var tasks = services
            .SelectMany(svc => svc.Environments.Select(env =>
                PollServiceAsync(svc, env.Key, env.Value, options, ct)));

        await Task.WhenAll(tasks);
    }

    private async Task PollServiceAsync(
        ServiceConfig svc,
        string envName,
        EnvironmentConfig envCfg,
        MonitoringOptions options,
        CancellationToken ct)
    {
        var url = ResolveUrl(envCfg.HealthDetailUrl ?? envCfg.HealthUrl, envName, options);
        if (string.IsNullOrEmpty(url)) return;

        var client = httpClientFactory.CreateClient("health");
        var sw = Stopwatch.StartNew();
        var record = new ServiceHealthRecord
        {
            ServiceName = svc.Name,
            Environment = envName,
            ExpectedDatabase = envCfg.ExpectedDb,
            CheckedAt = DateTime.UtcNow,
        };

        try
        {
            var resp = await client.GetAsync(url, ct);
            sw.Stop();
            record.ResponseTimeMs = (int)sw.ElapsedMilliseconds;

            var json = await resp.Content.ReadAsStringAsync(ct);
            record.RawJson = json;

            if (resp.IsSuccessStatusCode || resp.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            {
                var detail = JsonSerializer.Deserialize<HealthDetailResponse>(json, JsonOpts);
                record.OverallStatus = detail?.Status ?? (resp.IsSuccessStatusCode ? "Healthy" : "Unhealthy");
                record.IsHealthy = record.OverallStatus == "Healthy";

                ExtractDbInfo(detail, record);
            }
            else
            {
                record.OverallStatus = "Unhealthy";
                record.IsHealthy = false;
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            record.OverallStatus = "Unreachable";
            record.IsHealthy = false;
            logger.LogWarning("Poll failed for {Service}/{Env}: {Error}", svc.Name, envName, ex.Message);
        }

        db.ServiceHealthRecords.Add(record);
        await db.SaveChangesAsync(ct);
    }

    private static void ExtractDbInfo(HealthDetailResponse? detail, ServiceHealthRecord record)
    {
        if (detail?.Entries is null) return;

        var dbEntry = detail.Entries
            .FirstOrDefault(e => e.Key.Equals("database", StringComparison.OrdinalIgnoreCase));

        if (dbEntry.Value?.Data is null) return;

        var data = dbEntry.Value.Data;
        record.DbConnected = dbEntry.Value.Status == "Healthy";

        if (data.TryGetValue("server", out var server))
            record.DbServer = server?.ToString();

        if (data.TryGetValue("database", out var db))
            record.ActualDatabase = db?.ToString();

        if (!string.IsNullOrEmpty(record.ActualDatabase) && !string.IsNullOrEmpty(record.ExpectedDatabase))
            record.DbMatch = string.Equals(
                record.ActualDatabase, record.ExpectedDatabase,
                StringComparison.OrdinalIgnoreCase);
    }

    public async Task<List<ServiceHealthRecord>> GetLatestAsync(string environment, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-2);
        return await db.ServiceHealthRecords
            .Where(r => r.Environment == environment && r.CheckedAt >= cutoff)
            .GroupBy(r => r.ServiceName)
            .Select(g => g.OrderByDescending(r => r.CheckedAt).First())
            .ToListAsync(ct);
    }

    public async Task<List<ServiceHealthRecord>> GetHistoryAsync(
        string serviceName, string environment, int hours, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddHours(-hours);
        return await db.ServiceHealthRecords
            .Where(r => r.ServiceName == serviceName
                     && r.Environment == environment
                     && r.CheckedAt >= cutoff)
            .OrderByDescending(r => r.CheckedAt)
            .Take(500)
            .ToListAsync(ct);
    }

    private static string? ResolveUrl(string? url, string env, MonitoringOptions opts)
    {
        if (string.IsNullOrEmpty(url)) return null;
        return url
            .Replace("{HOST_DEV}", opts.HostDev)
            .Replace("{HOST_UAT}", opts.HostUat);
    }
}
