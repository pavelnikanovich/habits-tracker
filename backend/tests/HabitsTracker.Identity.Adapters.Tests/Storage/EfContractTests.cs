using HabitsTracker.Identity.Core;
using HabitsTracker.Tests;
using HabitsTracker.Tests.PortContracts;

namespace HabitsTracker.Identity.Adapters.Tests.Storage;

// The port contracts over the EF Core stores on PostgreSQL. Each test gets a fresh migrated database, and all
// stores of a test come from one scope, as in a request.
public static class EfContractTests
{
    public sealed class UsersOnPostgres(PostgresFixture postgres) : UsersContract, IAsyncLifetime
    {
        private IdentityDatabase database = null!;

        protected override IUsers Users => database.Get<IUsers>();

        public async ValueTask InitializeAsync() => database = await IdentityDatabase.CreateAsync(postgres);

        public ValueTask DisposeAsync() => database.DisposeAsync();
    }

    public sealed class OneTimeCodesOnPostgres(PostgresFixture postgres) : OneTimeCodesContract, IAsyncLifetime
    {
        private IdentityDatabase database = null!;

        protected override IOneTimeCodes OneTimeCodes => database.Get<IOneTimeCodes>();

        public async ValueTask InitializeAsync() => database = await IdentityDatabase.CreateAsync(postgres);

        public ValueTask DisposeAsync() => database.DisposeAsync();
    }

    public sealed class CodeRequestsOnPostgres(PostgresFixture postgres) : CodeRequestsContract, IAsyncLifetime
    {
        private IdentityDatabase database = null!;

        protected override ICodeRequests CodeRequests => database.Get<ICodeRequests>();

        public async ValueTask InitializeAsync() => database = await IdentityDatabase.CreateAsync(postgres);

        public ValueTask DisposeAsync() => database.DisposeAsync();
    }

    public sealed class SessionsOnPostgres(PostgresFixture postgres) : SessionsContract, IAsyncLifetime
    {
        private IdentityDatabase database = null!;

        protected override IUsers Users => database.Get<IUsers>();

        protected override ISessions Sessions => database.Get<ISessions>();

        public async ValueTask InitializeAsync() => database = await IdentityDatabase.CreateAsync(postgres);

        public ValueTask DisposeAsync() => database.DisposeAsync();
    }

    public sealed class TransactionOnPostgres(PostgresFixture postgres) : TransactionContract, IAsyncLifetime
    {
        private IdentityDatabase database = null!;

        protected override IIdentityTransaction Transaction => database.Get<IIdentityTransaction>();

        protected override IUsers Users => database.Get<IUsers>();

        protected override IOneTimeCodes OneTimeCodes => database.Get<IOneTimeCodes>();

        protected override ICodeRequests CodeRequests => database.Get<ICodeRequests>();

        public async ValueTask InitializeAsync() => database = await IdentityDatabase.CreateAsync(postgres);

        public ValueTask DisposeAsync() => database.DisposeAsync();
    }
}
