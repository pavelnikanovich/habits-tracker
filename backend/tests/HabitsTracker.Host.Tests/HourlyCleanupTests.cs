using System.Globalization;
using HabitsTracker.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace HabitsTracker.Host.Tests;

// FR-033: an hourly job deletes expired codes, old code requests and idle sessions through the core's use case, and the
// host's own request timings older than 13 months. The job runs on the host's fake clock.
public class HourlyCleanupTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-11-02T10:20:00Z", CultureInfo.InvariantCulture);

    [Fact]
    public async Task Cleanup_runs_every_hour()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        _ = host.Services; // starts the host, and with it the job's first run, on an empty database
        var user = Guid.NewGuid();
        await ExecuteAsync(host, $"""
            insert into identity.users (id, email, time_zone, time_zone_recognized, locale, created_at)
            values ('{user}', 'ann@example.com', 'UTC', true, 'en', '2026-01-01Z');
            -- Valid now, expired or old an hour later.
            insert into identity.one_time_codes (email, code_hash, issued_at, expires_at, entry_attempts)
            values ('gone@example.com', '\x00', '{Start.AddMinutes(-5):O}', '{Start.AddMinutes(30):O}', 0),
                   ('kept@example.com', '\x00', '{Start.AddMinutes(-5):O}', '{Start.AddHours(3):O}', 0);
            insert into identity.code_requests (email, ip_prefix, requested_at)
            values ('gone@example.com', '203.0.113.7/32', '{Start.AddHours(-23).AddMinutes(-30):O}'),
                   ('kept@example.com', '203.0.113.7/32', '{Start.AddHours(-1):O}');
            insert into identity.sessions (id, user_id, remembered, created_at, last_activity_at)
            values ('\x01', '{user}', false, '2026-01-01Z', '{Start.AddDays(-30).AddMinutes(30):O}'),
                   ('\x02', '{user}', false, '2026-01-01Z', '{Start:O}');
            """, ct);

        Assert.Equal((2L, 2L, 2L), await CountsAsync(host, ct));

        host.Time.Advance(TimeSpan.FromHours(1));

        await WaitForAsync(async () => await CountsAsync(host, ct) == (1L, 1L, 1L), "the expired records to be deleted", ct);
        Assert.Equal("kept@example.com", await ScalarAsync(host, "select email from identity.one_time_codes", ct));
        Assert.Equal("kept@example.com", await ScalarAsync(host, "select email from identity.code_requests", ct));
        Assert.Equal("02", await ScalarAsync(host, "select encode(id, 'hex') from identity.sessions", ct));
    }

    [Fact]
    public async Task Cleanup_runs_once_when_the_host_starts()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await postgres.CreateDatabaseAsync();
        void OnDatabase(HostFactory host)
        {
            AtStart(host);
            host.WithSetting("ConnectionStrings:Habits", connectionString);
        }
        await using (var migrating = await HostFactory.StartAsync(postgres, OnDatabase))
        {
            _ = migrating.Services; // migrates the database
            await ExecuteAsync(migrating, """
                insert into identity.one_time_codes (email, code_hash, issued_at, expires_at, entry_attempts)
                values ('old@example.com', '\x00', '2020-01-01Z', '2020-01-01Z', 0)
                """, ct);
        }

        await using var host = await HostFactory.StartAsync(postgres, OnDatabase);
        _ = host.Services;

        await WaitForAsync(async () => await CountsAsync(host, ct) == (0L, 0L, 0L), "the first run to delete the expired code", ct);
    }

    [Fact]
    public async Task Request_timings_older_than_13_months_are_deleted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        _ = host.Services;
        // At the next run the cutoff is Start + 1 hour - 13 months: a row 13 months and 1 hour old at that run is older.
        var now = Start.AddHours(1);
        await ExecuteAsync(host, $"""
            insert into operations.request_timings (hour, endpoint, bucket_upper_ms, count)
            values ('{now.AddMonths(-13).AddHours(-1):O}', 'GET /old', 10, 1),
                   ('{now.AddMonths(-12):O}', 'GET /recent', 10, 1);
            """, ct);

        host.Time.Advance(TimeSpan.FromHours(1));

        await WaitForAsync(async () => await ScalarAsync(host, "select count(*) from operations.request_timings", ct) == "1", "the old timing to be deleted", ct);
        Assert.Equal("GET /recent", await ScalarAsync(host, "select endpoint from operations.request_timings", ct));
    }

    private static void AtStart(HostFactory host) => host.Time.SetUtcNow(Start);

    private static async Task<(long Codes, long Requests, long Sessions)> CountsAsync(HostFactory host, CancellationToken ct) =>
        (long.Parse(await ScalarAsync(host, "select count(*) from identity.one_time_codes", ct), CultureInfo.InvariantCulture),
         long.Parse(await ScalarAsync(host, "select count(*) from identity.code_requests", ct), CultureInfo.InvariantCulture),
         long.Parse(await ScalarAsync(host, "select count(*) from identity.sessions", ct), CultureInfo.InvariantCulture));

    // The job runs on the host's fake clock and writes a moment after the clock moves, so poll.
    private static async Task WaitForAsync(Func<Task<bool>> done, string what, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!await done())
        {
            Assert.True(DateTime.UtcNow < deadline, $"Timed out waiting for {what}.");
            await Task.Delay(50, ct);
        }
    }

    private static async Task ExecuteAsync(HostFactory host, string sql, CancellationToken ct)
    {
        await using var connection = await OpenAsync(host, ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<string> ScalarAsync(HostFactory host, string sql, CancellationToken ct)
    {
        await using var connection = await OpenAsync(host, ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToString(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture)!;
    }

    private static async Task<NpgsqlConnection> OpenAsync(HostFactory host, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(host.Services.GetRequiredService<IConfiguration>().GetConnectionString("Habits"));
        await connection.OpenAsync(ct);
        return connection;
    }
}
