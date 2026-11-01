using HabitsTracker.Identity.Adapters.Storage;
using HabitsTracker.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HabitsTracker.Host.Tests;

// The host in tests migrates its database on start (research R2), so it must never reach the developer database.
public class HostStartTests(PostgresFixture postgres)
{
    private const string MigrateOnStartKey = "Database:MigrateOnStart";

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

    // Development migrates at start; Production never does, the deployment migrates after the backup (research R2).
    [Fact]
    public void Only_Development_migrates_on_start()
    {
        Assert.True(ReadSettings("appsettings.json", "appsettings.Development.json").GetValue<bool>(MigrateOnStartKey));
        Assert.Null(ReadSettings("appsettings.json")[MigrateOnStartKey]);
    }

    [Fact]
    public async Task Host_without_a_test_database_does_not_start()
    {
        await using var host = new HostFactory(postgres);

        var exception = Assert.Throws<InvalidOperationException>(() => host.CreateClient());

        Assert.Contains("ConnectionStrings:Habits", exception.Message, StringComparison.Ordinal);
    }

    // The host's settings files as an environment layers them, read from the source tree.
    private static IConfiguration ReadSettings(params string[] files)
    {
        var builder = new ConfigurationBuilder();
        foreach (var file in files)
            builder.AddJsonFile(Repository.PathTo($"backend/src/HabitsTracker.Host/{file}"), optional: false);
        return builder.Build();
    }
}
