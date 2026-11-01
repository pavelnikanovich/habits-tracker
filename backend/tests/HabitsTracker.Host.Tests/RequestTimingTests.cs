using System.Globalization;
using System.Net;
using HabitsTracker.Host.Metrics;
using HabitsTracker.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace HabitsTracker.Host.Tests;

// Request timing metrics (FR-039, research R9): each request lands in a per-hour, per-route, per-latency-bucket count
// of operations.request_timings, with no personal data.
public class RequestTimingTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-11-02T10:20:00Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset StartHour = DateTimeOffset.Parse("2026-11-02T10:00:00Z", CultureInfo.InvariantCulture);
    private static readonly Uri Locales = new("/api/locales", UriKind.Relative);

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 10)]
    [InlineData(10.1, 25)]
    [InlineData(300, 300)]
    [InlineData(300.1, 400)]
    [InlineData(5000, 5000)]
    [InlineData(5000.1, null)]
    public void A_duration_falls_into_the_bucket_whose_upper_bound_it_does_not_exceed(double milliseconds, int? expected) =>
        Assert.Equal(expected, RequestTimingHistogram.Bucket(TimeSpan.FromMilliseconds(milliseconds)));

    [Fact]
    public async Task A_request_timing_reaches_the_metrics_without_personal_data()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateCookielessClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, Locales);
        request.Headers.Add(HostFactory.RemoteAddressHeader, "203.0.113.7");
        request.Headers.Add("Cookie", "session=secret-token");

        using var response = await client.SendAsync(request, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        host.Time.Advance(TimeSpan.FromMinutes(1));

        var rows = await RowsWhenAsync(host, rows => rows.Count > 0, ct);
        var row = Assert.Single(rows);
        Assert.Equal(new Row(StartHour, "GET /api/locales", 10, 1), row);

        Assert.Equal(["hour", "endpoint", "bucket_upper_ms", "count"], await ColumnsAsync(host, ct));
        Assert.Equal(["hour", "endpoint", "bucket_upper_ms"], await PrimaryKeyAsync(host, ct));
    }

    [Fact]
    public async Task A_path_value_and_a_query_string_stay_out_of_the_endpoint()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateCookielessClient();

        using var domain = await client.GetAsync(new Uri("/api/test/domain/not-signed-in?email=ann@example.com", UriKind.Relative), ct);
        using var missing = await client.GetAsync(new Uri("/api/no-such-route/ann@example.com", UriKind.Relative), ct);
        host.Time.Advance(TimeSpan.FromMinutes(1));

        var rows = await RowsWhenAsync(host, rows => rows.Sum(row => row.Count) == 2, ct);
        Assert.Equal(
            ["GET /api/test/domain/{error}", "static"],
            rows.Select(row => row.Endpoint).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Counts_add_up_within_an_hour()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateCookielessClient();

        for (var i = 0; i < 2; i++)
            (await client.GetAsync(Locales, ct)).Dispose();
        host.Time.Advance(TimeSpan.FromMinutes(1));
        await RowsWhenAsync(host, rows => rows.Sum(row => row.Count) == 2, ct);

        (await client.GetAsync(Locales, ct)).Dispose();
        host.Time.Advance(TimeSpan.FromMinutes(1));
        var rows = await RowsWhenAsync(host, rows => rows.Sum(row => row.Count) == 3, ct);

        Assert.Equal(new Row(StartHour, "GET /api/locales", 10, 3), Assert.Single(rows));
    }

    [Fact]
    public async Task A_new_hour_is_a_new_row()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateCookielessClient();

        (await client.GetAsync(Locales, ct)).Dispose();
        host.Time.Advance(TimeSpan.FromMinutes(50));
        (await client.GetAsync(Locales, ct)).Dispose();
        host.Time.Advance(TimeSpan.FromMinutes(1));

        var rows = await RowsWhenAsync(host, rows => rows.Sum(row => row.Count) == 2, ct);
        Assert.Equal(
            [new Row(StartHour, "GET /api/locales", 10, 1), new Row(StartHour.AddHours(1), "GET /api/locales", 10, 1)],
            rows.OrderBy(row => row.Hour));
    }

    [Fact]
    public async Task The_unbounded_bucket_is_stored_as_int_max()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateCookielessClient();

        (await client.GetAsync(new Uri("/api/test/slow", UriKind.Relative), ct)).Dispose();
        host.Time.Advance(TimeSpan.FromMinutes(1));

        var rows = await RowsWhenAsync(host, rows => rows.Count > 0, ct);
        Assert.Equal(new Row(StartHour, "GET /api/test/slow", 2147483647, 1), Assert.Single(rows));
    }

    [Fact]
    public async Task A_method_outside_the_fixed_set_is_recorded_as_OTHER()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateCookielessClient();

        using var custom = new HttpRequestMessage(new HttpMethod("FOO-ann"), "/health");
        (await client.SendAsync(custom, ct)).Dispose();
        using var known = new HttpRequestMessage(HttpMethod.Options, "/health");
        (await client.SendAsync(known, ct)).Dispose();
        host.Time.Advance(TimeSpan.FromMinutes(1));

        var rows = await RowsWhenAsync(host, rows => rows.Sum(row => row.Count) == 2, ct);
        Assert.Equal(
            ["OPTIONS /health", "OTHER /health"],
            rows.Select(row => row.Endpoint).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_flush_that_fails_partway_writes_nothing_and_the_next_one_counts_each_request_once()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        var histogram = host.Services.GetRequiredService<RequestTimingHistogram>();
        var flusher = host.Services.GetServices<IHostedService>().OfType<RequestTimingFlusher>().Single();
        RequestTiming[] good =
        [
            new(StartHour, "GET /one", 10, 2),
            new(StartHour, "GET /two", 25, 3),
        ];
        // PostgreSQL refuses a NUL character in text, so this row fails after the two before it were written.
        histogram.Add([.. good, new RequestTiming(StartHour, "GET /bad\0", 10, 1)]);

        await flusher.FlushAsync(ct);

        Assert.Empty(await ReadRowsAsync(host, ct));
        var putBack = histogram.Drain();
        Assert.Equal(3, putBack.Count);
        histogram.Add([.. putBack.Where(timing => !timing.Endpoint.Contains('\0', StringComparison.Ordinal))]);

        await flusher.FlushAsync(ct);

        Assert.Equal(
            good.Select(timing => new Row(timing.Hour, timing.Endpoint, timing.BucketUpperMs, timing.Count)).OrderBy(row => row.Endpoint, StringComparer.Ordinal),
            (await ReadRowsAsync(host, ct)).OrderBy(row => row.Endpoint, StringComparer.Ordinal));
    }

    private static void AtStart(HostFactory host) => host.Time.SetUtcNow(Start);

    private sealed record Row(DateTimeOffset Hour, string Endpoint, int BucketUpperMs, long Count);

    // The flusher runs on the host's fake clock; it writes a moment after the clock moves, so poll until the rows fit.
    private static async Task<List<Row>> RowsWhenAsync(HostFactory host, Func<List<Row>, bool> done, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (true)
        {
            var rows = await ReadRowsAsync(host, ct);
            if (done(rows))
                return rows;
            Assert.True(DateTime.UtcNow < deadline, $"The timings did not arrive; the table holds: {string.Join("; ", rows)}");
            await Task.Delay(50, ct);
        }
    }

    private static async Task<List<Row>> ReadRowsAsync(HostFactory host, CancellationToken ct)
    {
        await using var connection = await OpenAsync(host, ct);
        await using var command = new NpgsqlCommand("select hour, endpoint, bucket_upper_ms, count from operations.request_timings", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<Row>();
        while (await reader.ReadAsync(ct))
            rows.Add(new Row(reader.GetFieldValue<DateTimeOffset>(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt64(3)));
        return rows;
    }

    private static Task<List<string>> ColumnsAsync(HostFactory host, CancellationToken ct) => StringsAsync(
        host,
        "select column_name from information_schema.columns where table_schema = 'operations' and table_name = 'request_timings' order by ordinal_position",
        ct);

    private static Task<List<string>> PrimaryKeyAsync(HostFactory host, CancellationToken ct) => StringsAsync(
        host,
        """
        select a.attname from pg_index i
        join pg_attribute a on a.attrelid = i.indrelid and a.attnum = any(i.indkey)
        where i.indrelid = 'operations.request_timings'::regclass and i.indisprimary
        order by array_position(i.indkey::int2[], a.attnum)
        """,
        ct);

    private static async Task<List<string>> StringsAsync(HostFactory host, string sql, CancellationToken ct)
    {
        await using var connection = await OpenAsync(host, ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var values = new List<string>();
        while (await reader.ReadAsync(ct))
            values.Add(reader.GetString(0));
        return values;
    }

    private static async Task<NpgsqlConnection> OpenAsync(HostFactory host, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(((IConfiguration)host.Services.GetService(typeof(IConfiguration))!).GetConnectionString("Habits"));
        await connection.OpenAsync(ct);
        return connection;
    }
}
