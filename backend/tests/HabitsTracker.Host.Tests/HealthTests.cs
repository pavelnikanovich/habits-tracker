using System.Net;
using HabitsTracker.Tests;

namespace HabitsTracker.Host.Tests;

// GET /health: 200 "Healthy" when the database is reachable, 503 "Unhealthy" otherwise (FR-037).
public class HealthTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Health_is_healthy_when_the_database_is_reachable()
    {
        await using var host = new HostFactory(postgres);
        await host.InitializeAsync();
        using var client = host.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Health_is_unhealthy_when_the_database_is_unreachable()
    {
        await using var host = new HostFactory(postgres)
            .WithSetting("ConnectionStrings:Habits", "Host=127.0.0.1;Port=1;Database=habits;Username=habits;Timeout=2")
            .WithSetting("Database:MigrateOnStart", "false");
        await host.InitializeAsync();
        using var client = host.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
