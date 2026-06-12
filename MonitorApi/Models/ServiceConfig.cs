namespace MonitorApi.Models;

public class ServiceConfig
{
    public string Name { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string Type { get; set; } = "API";  // API | Web | DB
    public Dictionary<string, EnvironmentConfig> Environments { get; set; } = new();
}

public class EnvironmentConfig
{
    public string? HealthDetailUrl { get; set; }
    public string? HealthUrl { get; set; }
    public string? ExpectedDb { get; set; }
}

public class MonitoringOptions
{
    public int PollIntervalSeconds { get; set; } = 30;
    public int DataQualityIntervalSeconds { get; set; } = 300;
    public string HostDev { get; set; } = "";
    public string HostUat { get; set; } = "";
}

public class DataQualityRule
{
    public string RuleName { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Database { get; set; } = "DEV";
    public string ConnectionStringKey { get; set; } = default!;
    public string Severity { get; set; } = "warning";   // warning | critical
    public string SqlQuery { get; set; } = default!;
}
