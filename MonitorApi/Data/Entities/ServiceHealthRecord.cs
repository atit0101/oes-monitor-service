namespace MonitorApi.Data.Entities;

public class ServiceHealthRecord
{
    public long Id { get; set; }
    public string ServiceName { get; set; } = default!;
    public string Environment { get; set; } = default!;
    public string OverallStatus { get; set; } = default!;
    public bool IsHealthy { get; set; }
    public int? ResponseTimeMs { get; set; }

    // DB verification snapshot
    public string? DbServer { get; set; }
    public string? ActualDatabase { get; set; }
    public string? ExpectedDatabase { get; set; }
    public bool? DbMatch { get; set; }
    public bool? DbConnected { get; set; }

    // Full JSON snapshot จาก /health/detail
    public string? RawJson { get; set; }

    public DateTime CheckedAt { get; set; }
}
