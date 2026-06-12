using MonitorApi.Data.Entities;
using MonitorApi.Models;

namespace MonitorApi.Services;

public class TopologyBuilderService
{
    // Dependency map: service → downstream dependencies
    private static readonly Dictionary<string, string[]> DependencyMap = new()
    {
        ["core-insurance"]    = ["mssql", "user-service", "audit-log-service", "notification-service"],
        ["core-oic"]          = ["mssql", "audit-log-service"],
        ["user-service"]      = ["mssql", "audit-log-service"],
        ["audit-log-service"] = ["mssql"],
        ["assessment-service"]= ["mssql", "user-service", "notification-service"],
        ["notification-service"] = ["smtp"],
        ["report-service"]    = ["mssql"],
        ["dashboard-service"] = ["core-insurance", "core-oic"],
        ["oes-web-oic"]       = ["core-oic", "user-service"],
        ["oes-web-insurance"]  = ["core-insurance", "user-service"],
    };

    public TopologyGraph Build(List<ServiceHealthRecord> records)
    {
        var graph = new TopologyGraph();
        var healthMap = records.ToDictionary(r => r.ServiceName, r => r);

        // Infrastructure nodes
        graph.Nodes.Add(new TopologyNode
        {
            Id = "mssql", Title = "MSSQL", SubTitle = "Database",
            ArcSuccess = 1, ArcFailed = 0, Color = "blue"
        });
        graph.Nodes.Add(new TopologyNode
        {
            Id = "smtp", Title = "SMTP", SubTitle = "Mail Server",
            ArcSuccess = 1, ArcFailed = 0, Color = "blue"
        });

        // Service nodes
        foreach (var (svcName, deps) in DependencyMap)
        {
            var isHealthy = healthMap.TryGetValue(svcName, out var rec) && rec.IsHealthy;
            var responseMs = rec?.ResponseTimeMs;

            graph.Nodes.Add(new TopologyNode
            {
                Id = svcName,
                Title = svcName,
                SubTitle = rec?.OverallStatus ?? "Unknown",
                MainStat = responseMs.HasValue ? $"{responseMs}ms" : null,
                ArcSuccess = isHealthy ? 1 : 0,
                ArcFailed = isHealthy ? 0 : 1,
                Color = isHealthy ? "green" : "red",
            });

            // Edges for each dependency
            foreach (var dep in deps)
            {
                // Edge health: check if the dependency entry is healthy
                var edgeHealthy = dep is "mssql" or "smtp"
                    ? (healthMap.TryGetValue(svcName, out var r) && (r.DbConnected ?? true))
                    : (healthMap.TryGetValue(dep, out var depRec) && depRec.IsHealthy);

                graph.Edges.Add(new TopologyEdge
                {
                    Id = $"{svcName}--{dep}",
                    Source = svcName,
                    Target = dep,
                    MainStat = edgeHealthy ? "OK" : "FAILED",
                    Color = edgeHealthy ? "green" : "red",
                });
            }
        }

        return graph;
    }
}
