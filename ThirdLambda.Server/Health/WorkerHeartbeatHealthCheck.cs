using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ThirdLambda.Server.Health;

/// <summary>Liveness: unhealthy if any registered worker has stopped beating (hung or deadlocked).</summary>
public sealed class WorkerHeartbeatHealthCheck(WorkerHeartbeat heartbeat) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var data = new Dictionary<string, object>();
        var stalled = new List<string>();

        foreach (var worker in heartbeat.Snapshot())
        {
            var age = now - worker.LastBeat;
            data[worker.Name] = $"last beat {age.TotalSeconds:F0}s ago (limit {worker.Timeout.TotalSeconds:F0}s)";
            if (age > worker.Timeout) stalled.Add(worker.Name);
        }

        var result = stalled.Count == 0
            ? HealthCheckResult.Healthy($"{data.Count} worker(s) reporting", data)
            : HealthCheckResult.Unhealthy($"Stalled worker(s): {string.Join(", ", stalled)}", data: data);

        return Task.FromResult(result);
    }
}