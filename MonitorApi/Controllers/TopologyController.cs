using Microsoft.AspNetCore.Mvc;
using MonitorApi.Services;

namespace MonitorApi.Controllers;

[ApiController]
[Route("api/topology")]
public class TopologyController(
    HealthPollerService poller,
    TopologyBuilderService topologyBuilder) : ControllerBase
{
    // GET /api/topology?env=DEV
    // Format ตรงกับ Grafana Node Graph panel
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string env = "DEV",
        CancellationToken ct = default)
    {
        var records = await poller.GetLatestAsync(env.ToUpper(), ct);
        var graph = topologyBuilder.Build(records);

        // Grafana Node Graph format
        return Ok(new
        {
            nodes = graph.Nodes.Select(n => new
            {
                id = n.Id,
                title = n.Title,
                subTitle = n.SubTitle,
                mainStat = n.MainStat,
                arc__success = n.ArcSuccess,
                arc__failed = n.ArcFailed,
                color = n.Color,
            }),
            edges = graph.Edges.Select(e => new
            {
                id = e.Id,
                source = e.Source,
                target = e.Target,
                mainStat = e.MainStat,
            }),
        });
    }
}
