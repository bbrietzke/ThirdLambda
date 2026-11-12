using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ThirdLambda.Server.Data;

namespace ThirdLambda.Server.Health;

/// <summary>
/// Readiness: unhealthy until every migration this build knows about has been applied.
/// New pods stay out of the Service until `migrate` has run. Old pods stay ready after
/// a migration, because migrations they don't know about don't count as pending for them.
/// </summary>
public sealed class PendingMigrationsHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        return pending.Count == 0
            ? HealthCheckResult.Healthy("Schema is up to date")
            : HealthCheckResult.Unhealthy(
                $"{pending.Count} pending migration(s): {string.Join(", ", pending)}. Run `migrate`.");
    }
}