using HabitsTracker.Identity.Adapters.Storage;
using HabitsTracker.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HabitsTracker.Host.Tests;

// The host in tests migrates its database on start (research R2), so it must never reach the developer database.
public class HostStartTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Host_migrates_the_identity_schema_on_start()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new HostFactory(postgres);
        await host.InitializeAsync();
        using var client = host.CreateClient();

        await using var scope = host.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database;
        Assert.NotEmpty(await database.GetAppliedMigrationsAsync(ct));
        Assert.Empty(await database.GetPendingMigrationsAsync(ct));
    }

    [Fact]
    public async Task Host_without_a_test_database_does_not_start()
    {
        await using var host = new HostFactory(postgres);

        var exception = Assert.Throws<InvalidOperationException>(() => host.CreateClient());

        Assert.Contains("ConnectionStrings:Habits", exception.Message, StringComparison.Ordinal);
    }
}
