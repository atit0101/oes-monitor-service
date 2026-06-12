namespace MonitorApi.Models;

// ── Response จาก /health/detail ─────────────────────────────
public class HealthDetailResponse
{
    public string Status { get; set; } = "Unknown";
    public string? TotalDuration { get; set; }
    public Dictionary<string, HealthEntry> Entries { get; set; } = new();
}

public class HealthEntry
{
    public string Status { get; set; } = "Unknown";
    public string? Duration { get; set; }
    public string? Exception { get; set; }
    public Dictionary<string, object>? Data { get; set; }
}

// ── Summary สำหรับ API response ─────────────────────────────
public class ServiceHealthSummary
{
    public string ServiceName { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string Environment { get; set; } = default!;
    public string Status { get; set; } = "Unknown";
    public bool IsHealthy { get; set; }
    public int? ResponseTimeMs { get; set; }
    public DbVerificationInfo? Database { get; set; }
    public List<DependencyStatus> Dependencies { get; set; } = new();
    public DateTime CheckedAt { get; set; }
}

public class DbVerificationInfo
{
    public string? Server { get; set; }
    public string? ActualDatabase { get; set; }
    public string? ExpectedDatabase { get; set; }
    public bool IsConnected { get; set; }
    public bool DbMatch { get; set; }
    public string? Environment { get; set; }
}

public class DependencyStatus
{
    public string Name { get; set; } = default!;
    public string Status { get; set; } = "Unknown";
    public bool IsHealthy { get; set; }
    public string? Error { get; set; }
}

// ── Topology สำหรับ Grafana Node Graph ──────────────────────
public class TopologyGraph
{
    public List<TopologyNode> Nodes { get; set; } = new();
    public List<TopologyEdge> Edges { get; set; } = new();
}

public class TopologyNode
{
    public string Id { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string SubTitle { get; set; } = default!;
    public string? MainStat { get; set; }
    public double ArcSuccess { get; set; }
    public double ArcFailed { get; set; }
    public string Color { get; set; } = "green";
}

public class TopologyEdge
{
    public string Id { get; set; } = default!;
    public string Source { get; set; } = default!;
    public string Target { get; set; } = default!;
    public string? MainStat { get; set; }
    public string Color { get; set; } = "green";
}

// ── Data Quality ─────────────────────────────────────────────
public class DataQualityResult
{
    public string RuleName { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Environment { get; set; } = default!;
    public string Severity { get; set; } = default!;
    public int ViolationCount { get; set; }
    public bool HasViolations => ViolationCount > 0;
    public DateTime CheckedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
