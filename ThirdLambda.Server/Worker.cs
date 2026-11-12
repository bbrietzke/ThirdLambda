using ThirdLambda.Server.Health;
namespace ThirdLambda.Server;

public class Worker(IServiceScopeFactory scopes, WorkerHeartbeat heartbeat, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        heartbeat.Register(nameof(Worker), TimeSpan.FromMinutes(2));

        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }

            await using (var scope = scopes.CreateAsyncScope())
            {
                // ... do work ...
            }

            heartbeat.Beat(nameof(Worker));

            await Task.Delay(15000, stoppingToken);
        }
    }
}
