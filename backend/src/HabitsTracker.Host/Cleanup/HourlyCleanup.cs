using HabitsTracker.Host.Metrics;
using HabitsTracker.Host.Operations;
using HabitsTracker.Identity.Core.UseCases;
using Microsoft.EntityFrameworkCore;

namespace HabitsTracker.Host.Cleanup;

/// <summary>
/// Once when the host starts and then every hour, on a <see cref="PeriodicTimer"/> over <see cref="TimeProvider"/>:
/// runs the retention cleanup of the Identity context (<see cref="DeleteExpiredRecords"/>, FR-033) and deletes the
/// host's own <c>operations.request_timings</c> rows of hours before now minus 13 months (FR-039).
/// A failed run is logged and left to the next one: every delete is idempotent.
/// </summary>
public sealed partial class HourlyCleanup(
    IServiceScopeFactory scopes,
    TimeProvider time,
    ILogger<HourlyCleanup> log) : BackgroundService
{
    public static readonly TimeSpan Period = TimeSpan.FromHours(1);

    /// <summary>How long request timings are kept (FR-039).</summary>
    public const int TimingsKeptMonths = 13;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Created before the first await, so a test that moves the clock right after the host starts finds the timer.
        using var timer = new PeriodicTimer(Period, time);
        try
        {
            await RunAsync(stoppingToken);
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RunAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // The host is stopping.
        }
    }

    /// <summary>One run; a failure is logged, not thrown.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var identity = await scope.ServiceProvider.GetRequiredService<DeleteExpiredRecords>().ExecuteAsync(ct);

            var cutoff = time.GetUtcNow().AddMonths(-TimingsKeptMonths);
            var db = scope.ServiceProvider.GetRequiredService<OperationsDbContext>();
            var timings = await db.Set<RequestTiming>().Where(timing => timing.Hour < cutoff).ExecuteDeleteAsync(ct);

            LogRun(identity.OneTimeCodes, identity.CodeRequests, identity.Sessions, timings);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // Any failure leaves the records for the next hour; it must not stop the job.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogRunFailed(exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cleanup deleted {Codes} codes, {CodeRequests} code requests, {Sessions} sessions and {Timings} request timings.")]
    private partial void LogRun(int codes, int codeRequests, int sessions, int timings);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The hourly cleanup failed; the next run repeats it.")]
    private partial void LogRunFailed(Exception exception);
}
