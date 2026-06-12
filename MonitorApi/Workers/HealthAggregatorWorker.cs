using MonitorApi.Models;
using MonitorApi.Services;

namespace MonitorApi.Workers;

public class HealthAggregatorWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<HealthAggregatorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = configuration.GetSection("Monitoring").Get<MonitoringOptions>()
                   ?? new MonitoringOptions();
        var interval = TimeSpan.FromSeconds(opts.PollIntervalSeconds);

        logger.LogInformation("HealthAggregatorWorker started — polling every {Interval}s", opts.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var poller = scope.ServiceProvider.GetRequiredService<HealthPollerService>();
                var services = configuration.GetSection("Services").Get<List<ServiceConfig>>()
                               ?? [];

                await poller.PollAllAsync(services, opts, stoppingToken);
                logger.LogDebug("Health poll completed for {Count} services", services.Count);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Health poll cycle failed");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
