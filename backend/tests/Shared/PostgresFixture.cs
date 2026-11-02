using System.Data.Common;
using HabitsTracker.Tests;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace HabitsTracker.Tests;

/// <summary>
/// One PostgreSQL 18.6 container per test assembly (research R12). Each caller gets a new empty database,
/// so test classes never see one another's rows.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18.6").Build();

    public async ValueTask InitializeAsync() => await container.StartAsync();

    public async ValueTask DisposeAsync() => await container.DisposeAsync();

    /// <summary>Creates a new empty database and returns its connection string.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"test_{Guid.NewGuid():N}";
        var result = await container.ExecScriptAsync($"CREATE DATABASE {name};");
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"CREATE DATABASE {name} failed: {result.Stderr}");

        var connectionString = new DbConnectionStringBuilder { ConnectionString = container.GetConnectionString() };
        connectionString["Database"] = name;
        return connectionString.ConnectionString;
    }
}
