namespace HabitsTracker.Host.Metrics;

/// <summary>
/// The counts of requests not yet written to <c>operations.request_timings</c>, per hour, endpoint and latency bucket
/// (research R9). The middleware adds to it; the flusher takes the counts out once a minute.
/// </summary>
public sealed class RequestTimingHistogram
{
    /// <summary>The stored upper bound of the +∞ bucket (data-model.md, "Schema <c>operations</c>").</summary>
    public const int UnboundedBucketUpperMs = int.MaxValue;

    // The bucket upper bounds in ms (research R9); a duration over the last one falls into the +∞ bucket.
    private static readonly int[] Bounds = [10, 25, 50, 75, 100, 150, 200, 250, 300, 400, 500, 750, 1000, 2000, 5000];

    private readonly Lock gate = new();
    private Dictionary<(DateTimeOffset Hour, string Endpoint, int BucketUpperMs), long> counts = [];

    /// <summary>The upper bound in ms of the bucket that holds <paramref name="duration"/>; <c>null</c> for +∞.</summary>
    public static int? Bucket(TimeSpan duration)
    {
        foreach (var bound in Bounds)
        {
            if (duration <= TimeSpan.FromMilliseconds(bound))
                return bound;
        }

        return null;
    }

    /// <summary>Counts one request of <paramref name="endpoint"/> that took <paramref name="duration"/> in the hour of <paramref name="hour"/>.</summary>
    public void Record(DateTimeOffset hour, string endpoint, TimeSpan duration) =>
        Add(new RequestTiming(hour, endpoint, Bucket(duration) ?? UnboundedBucketUpperMs, 1));

    /// <summary>Adds the counts of <paramref name="timings"/>; the flusher puts back what it could not write.</summary>
    public void Add(params RequestTiming[] timings)
    {
        ArgumentNullException.ThrowIfNull(timings);
        lock (gate)
        {
            foreach (var timing in timings)
            {
                var key = (timing.Hour, timing.Endpoint, timing.BucketUpperMs);
                counts[key] = counts.GetValueOrDefault(key) + timing.Count;
            }
        }
    }

    /// <summary>Takes out every count and starts again from zero.</summary>
    public IReadOnlyList<RequestTiming> Drain()
    {
        Dictionary<(DateTimeOffset Hour, string Endpoint, int BucketUpperMs), long> taken;
        lock (gate)
        {
            taken = counts;
            counts = [];
        }

        return [.. taken.Select(entry => new RequestTiming(entry.Key.Hour, entry.Key.Endpoint, entry.Key.BucketUpperMs, entry.Value))];
    }
}
