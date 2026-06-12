namespace MonitorApi.Data.Entities;

public class DataQualityRecord
{
    public long Id { get; set; }
    public string RuleName { get; set; } = default!;
    public string Environment { get; set; } = default!;
    public string Severity { get; set; } = default!;
    public int ViolationCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CheckedAt { get; set; }
}
