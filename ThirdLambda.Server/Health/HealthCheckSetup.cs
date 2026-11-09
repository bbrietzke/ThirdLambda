using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ThirdLambda.Server.Data;

namespace ThirdLambda.Server.Health;

public static class HealthCheckSetup
{
    public const string LiveTag = "live";
    public const string ReadyTag = "ready";

    /// <summary>
    /// Liveness  = "is the process stuck?"     If it fails, Kubernetes restarts the pod.
    /// Readiness = "can it serve traffic now?"  If it fails, the pod is removed from the Service.
    /// Dependencies like the database belong ONLY in readiness. Putting them in liveness means
    /// a database outage restarts every pod in a loop and makes things worse.
    /// </summary>
    public static IHealthChecksBuilder AddThirdLambdaHealthChecks(this IServiceCollection services)
    {
        services.AddSingleton<WorkerHeartbeat>();

        return services.AddHealthChecks()
            .AddCheck<WorkerHeartbeatHealthCheck>("workers", tags: [LiveTag])
            .AddDbContextCheck<AppDbContext>("database", tags: [ReadyTag])
            .AddCheck<PendingMigrationsHealthCheck>("migrations", tags: [ReadyTag]);
    }

    /// <summary>
    /// Maps /healthz/live, /healthz/ready and /healthz (all checks, detailed JSON).
    /// Pass requireHost (e.g. "*:8081") to serve them only on the management port.
    /// </summary>
    public static void MapThirdLambdaHealthChecks(this IEndpointRouteBuilder app, string? requireHost = null)
    {
        var group = app.MapGroup("/healthz");

        group.MapHealthChecks("/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(LiveTag),
        });

        group.MapHealthChecks("/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = WriteJson,
        });

        group.MapHealthChecks("", new HealthCheckOptions
        {
            ResponseWriter = WriteJson,   // every check; for humans and dashboards
        });

        if (requireHost is not null)
            group.RequireHost(requireHost);
    }

    // Healthy/Degraded => 200, Unhealthy => 503 (framework defaults); the body explains why.
    private static Task WriteJson(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            durationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => new
                {
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    error = e.Value.Exception?.Message,
                    durationMs = e.Value.Duration.TotalMilliseconds,
                    data = e.Value.Data,
                }),
        });
}