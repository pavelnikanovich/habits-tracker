using System.Net;
using HabitsTracker.Identity.Core.Tests.Fakes;
using HabitsTracker.Tests.PortContracts;

namespace HabitsTracker.Identity.Core.Tests;

// The port contracts over the in-memory fakes: the use case tests rely on fakes that behave as the adapters do.
public static class FakeContractTests
{
    public sealed class LocalesOnFixedLocales : LocalesContract
    {
        protected override ILocales Locales { get; } = new FixedLocales();
    }

    public sealed class CodeEmailSenderOnRecordingFake : CodeEmailSenderContract
    {
        protected override ICodeEmailSender DeliveringSender { get; } = new RecordingCodeEmailSender();

        protected override (ICodeEmailSender Sender, CodeEmailOutcome.Refused Refusal)? Refusing { get; } =
            (new RecordingCodeEmailSender { Next = Refusal }, Refusal);

        private static readonly CodeEmailOutcome.Refused Refusal =
            new(EmailServiceRefusal.DailyLimit, new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero));
    }

    public sealed class UsersOnFakes : UsersContract
    {
        protected override IUsers Users { get; } = new InMemoryUsers(new InMemoryTransaction());
    }

    public sealed class OneTimeCodesOnFakes : OneTimeCodesContract
    {
        protected override IOneTimeCodes OneTimeCodes { get; } = new InMemoryOneTimeCodes(new InMemoryTransaction());
    }

    public sealed class CodeRequestsOnFakes : CodeRequestsContract
    {
        protected override ICodeRequests CodeRequests { get; } = new InMemoryCodeRequests(new InMemoryTransaction());

        // The storage adapter's ConcurrencyTests hold the same case over PostgreSQL.
        [Fact]
        public Task Lock_outside_a_transaction_fails() =>
            Assert.ThrowsAsync<InvalidOperationException>(() => CodeRequests.LockAsync(
                Email.Create("ada@example.com"),
                IpAddressPrefix.From(IPAddress.Parse("192.0.2.1")),
                TestContext.Current.CancellationToken));
    }

    public sealed class SessionsOnFakes : SessionsContract
    {
        public SessionsOnFakes()
        {
            var transaction = new InMemoryTransaction();
            Users = new InMemoryUsers(transaction);
            Sessions = new InMemorySessions(transaction);
        }

        protected override IUsers Users { get; }

        protected override ISessions Sessions { get; }
    }

    public sealed class TransactionOnFakes : TransactionContract
    {
        public TransactionOnFakes()
        {
            var transaction = new InMemoryTransaction();
            Transaction = transaction;
            Users = new InMemoryUsers(transaction);
            OneTimeCodes = new InMemoryOneTimeCodes(transaction);
            CodeRequests = new InMemoryCodeRequests(transaction);
        }

        protected override IIdentityTransaction Transaction { get; }

        protected override IUsers Users { get; }

        protected override IOneTimeCodes OneTimeCodes { get; }

        protected override ICodeRequests CodeRequests { get; }

        // A nested RunAsync would lose the outer transaction's undo actions; EF Core refuses a second transaction too.
        [Fact]
        public Task A_nested_transaction_fails() =>
            Assert.ThrowsAsync<InvalidOperationException>(() => Transaction.RunAsync(
                work => Transaction.RunAsync(_ => Task.FromResult(0), work),
                TestContext.Current.CancellationToken));
    }
}
