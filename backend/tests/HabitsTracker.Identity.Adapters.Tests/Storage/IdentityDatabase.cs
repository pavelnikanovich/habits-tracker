using HabitsTracker.Identity.Adapters;
using HabitsTracker.Identity.Adapters.Storage;
using HabitsTracker.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace HabitsTracker.Identity.Adapters.Tests.Storage;

/// <summary>
/// A fresh database with the identity migration applied, and the Identity registration over it. <see cref="Get{T}"/>
/// resolves from one scope, a test's request; <see cref="OpenScope"/> opens further requests.
/// </summary>
public sealed class IdentityDatabase : IAsyncDisposable
{
    private readonly ServiceProvider provider;
    private readonly AsyncServiceScope scope;

    private IdentityDatabase(ServiceProvider provider)
    {
        this.provider = provider;
        scope = provider.CreateAsyncScope();
    }

    public static async Task<IdentityDatabase> CreateAsync(PostgresFixture postgres)
    {
        ArgumentNullException.ThrowIfNull(postgres);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Habits"] = await postgres.CreateDatabaseAsync(),
                ["Translations:Path"] = Repository.PathTo("frontend/public/i18n"),
            })
            .Build();
        var services = new ServiceCollection()
            .AddIdentityContext(configuration, new HostingEnvironment
            {
                EnvironmentName = Environments.Development,
                ContentRootPath = AppContext.BaseDirectory,
            });
        var database = new IdentityDatabase(services.BuildServiceProvider(validateScopes: true));
        await database.Get<IdentityDbContext>().Database.MigrateAsync(TestContext.Current.CancellationToken);
        return database;
    }

    public T Get<T>()
        where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    public AsyncServiceScope OpenScope() => provider.CreateAsyncScope();

    public Task<int> ExecuteAsync(FormattableString sql) =>
        Get<IdentityDbContext>().Database.ExecuteSqlAsync(sql, TestContext.Current.CancellationToken);

    /// <summary>Runs a query whose one column is named <c>Value</c>.</summary>
    public Task<List<T>> QueryAsync<T>(FormattableString sql) =>
        Get<IdentityDbContext>().Database.SqlQuery<T>(sql).ToListAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        await scope.DisposeAsync();
        await provider.DisposeAsync();
    }
}
