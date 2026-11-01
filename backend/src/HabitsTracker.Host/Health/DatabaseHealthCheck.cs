using HabitsTracker.Host.Operations;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HabitsTracker.Host.Health;

/// <summary>Healthy while the database accepts a connection (FR-037).</summary>
public sealed class DatabaseHealthCheck(OperationsDbContext database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await database.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : new HealthCheckResult(context.Registration.FailureStatus);
}
