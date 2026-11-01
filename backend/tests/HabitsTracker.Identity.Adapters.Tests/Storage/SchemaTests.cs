using System.Text.RegularExpressions;
using HabitsTracker.Tests;

namespace HabitsTracker.Identity.Adapters.Tests.Storage;

// The identity schema is exactly data-model.md, "Schema identity".
public sealed partial class SchemaTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Timestamptz = "timestamp with time zone";

    private IdentityDatabase database = null!;

    public async ValueTask InitializeAsync() => database = await IdentityDatabase.CreateAsync(postgres);

    public ValueTask DisposeAsync() => database.DisposeAsync();

    [Fact]
    public async Task Migration_creates_the_tables_of_the_data_model()
    {
        var columns = await database.QueryAsync<string>($"""
            SELECT table_name || '.' || column_name || ' ' || data_type
                   || CASE WHEN is_identity = 'YES' THEN ' identity' ELSE '' END AS "Value"
            FROM information_schema.columns
            WHERE table_schema = 'identity' AND table_name <> '__EFMigrationsHistory'
            ORDER BY table_name, ordinal_position
            """);

        // The one-time code is stored only as its hash: no other column of one_time_codes holds it.
        Assert.Equal(
            [
                "code_requests.id bigint identity",
                "code_requests.email text",
                "code_requests.ip_prefix cidr",
                $"code_requests.requested_at {Timestamptz}",
                "one_time_codes.email text",
                "one_time_codes.code_hash bytea",
                $"one_time_codes.issued_at {Timestamptz}",
                $"one_time_codes.expires_at {Timestamptz}",
                "one_time_codes.entry_attempts smallint",
                "sessions.id bytea",
                "sessions.user_id uuid",
                "sessions.remembered boolean",
                $"sessions.created_at {Timestamptz}",
                $"sessions.last_activity_at {Timestamptz}",
                "users.id uuid",
                "users.email text",
                "users.time_zone text",
                "users.time_zone_recognized boolean",
                "users.locale text",
                $"users.created_at {Timestamptz}",
            ],
            columns);
    }

    [Fact]
    public async Task Migration_creates_the_keys_and_indexes_of_the_data_model()
    {
        var indexes = await database.QueryAsync<string>($"""
            SELECT indexdef AS "Value"
            FROM pg_indexes
            WHERE schemaname = 'identity' AND tablename <> '__EFMigrationsHistory'
            """);
        var foreignKeys = await database.QueryAsync<string>($"""
            SELECT conrelid::regclass::text || ' ' || pg_get_constraintdef(oid) AS "Value"
            FROM pg_constraint
            WHERE contype = 'f' AND connamespace = 'identity'::regnamespace
            """);

        Assert.Equal(
            [
                "code_requests(email, requested_at)",
                "code_requests(ip_prefix, requested_at)",
                "code_requests(requested_at)",
                "one_time_codes(expires_at)",
                "sessions(last_activity_at)",
                "sessions(user_id)",
                "unique code_requests(id)",
                "unique one_time_codes(email)",
                "unique sessions(id)",
                "unique users(email)",
                "unique users(id)",
            ],
            indexes.Select(Describe).Order(StringComparer.Ordinal));
        Assert.Equal(["identity.sessions FOREIGN KEY (user_id) REFERENCES identity.users(id)"], foreignKeys);
    }

    // "CREATE UNIQUE INDEX "IX_users_email" ON identity.users USING btree (email)" -> "unique users(email)".
    private static string Describe(string indexDefinition)
    {
        var match = IndexDefinition().Match(indexDefinition);
        Assert.True(match.Success, indexDefinition);
        return $"{(match.Groups["unique"].Success ? "unique " : "")}{match.Groups["table"].Value}({match.Groups["columns"].Value})";
    }

    [GeneratedRegex("""^CREATE (?<unique>UNIQUE )?INDEX \S+ ON "?identity"?\.(?<table>\w+) USING btree \((?<columns>[^)]+)\)$""")]
    private static partial Regex IndexDefinition();
}
