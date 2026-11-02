namespace HabitsTracker.Host.Metrics;

/// <summary>
/// A row of <c>operations.request_timings</c> (FR-039): how many requests of one route, in one hour, took no longer than
/// one latency bucket's upper bound. It holds no IP address, user, session, query string or path value.
/// </summary>
/// <param name="Hour">The start of the hour of the request, UTC.</param>
/// <param name="Endpoint"><c>"&lt;METHOD&gt; &lt;route pattern&gt;"</c>, or <c>static</c> for files and the application fallback.</param>
/// <param name="BucketUpperMs">The bucket's upper bound in ms; <see cref="int.MaxValue"/> for the +∞ bucket.</param>
/// <param name="Count">The number of requests.</param>
public sealed record RequestTiming(DateTimeOffset Hour, string Endpoint, int BucketUpperMs, long Count);
