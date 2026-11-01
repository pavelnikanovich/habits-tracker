using HabitsTracker.Host.Operations;
using Microsoft.EntityFrameworkCore;

namespace HabitsTracker.Host.Metrics;

/// <summary>
/// Once a minute, on a <see cref="PeriodicTimer"/> over <see cref="TimeProvider"/>, adds the histogram's counts to
/// <c>operations.request_timings</c> with one upsert per hour, endpoint and bucket (research R9). It writes once more
/// when the host stops, so a deployment loses no minute. Counts it cannot write stay in the histogram for the next run.
/// </summary>
public sealed partial class RequestTimingFlusher(
    RequestTimingHistogram histogram,
    IServiceScopeFactory scopes,
    TimeProvider time,
    ILogger<RequestTimingFlusher> log) : BackgroundService
{
    public static readonly TimeSpan Period = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Created before the first await, so a test that moves the clock right after the host starts finds the timer.
        using var timer = new PeriodicTimer(Period, time);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await FlushAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // The host is stopping; StopAsync writes the rest.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await FlushAsync(CancellationToken.None);
    }

    /// <summary>Writes the counts taken from the histogram; on failure puts them back and logs it.</summary>
    public async Task FlushAsync(CancellationToken ct)
    {
        var timings = histogram.Drain();
        if (timings.Count == 0)
            return;

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OperationsDbContext>();
            foreach (var timing in timings)
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    insert into operations.request_timings (hour, endpoint, bucket_upper_ms, count)
                    values ({timing.Hour}, {timing.Endpoint}, {timing.BucketUpperMs}, {timing.Count})
                    on conflict (hour, endpoint, bucket_upper_ms)
                    do update set count = operations.request_timings.count + excluded.count
                    """,
                    ct);
            }
        }
#pragma warning disable CA1031 // Any failure keeps the counts for the next minute; a lost minute is not worth a crash.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            histogram.Add([.. timings]);
            LogFlushFailed(exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Request timings could not be written; they are kept for the next minute.")]
    private partial void LogFlushFailed(Exception exception);
}
