using System.Net;
using HabitsTracker.Identity.Core.Tests.Fakes;
using HabitsTracker.Identity.Core.UseCases;
using Microsoft.Extensions.Time.Testing;

namespace HabitsTracker.Identity.Core.Tests;

public class RequestCodeTests
{
    private static readonly DateTimeOffset T = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly Email Ada = Email.Create("ada@example.com");
    private static readonly Locale Polish = Locale.FromStorage("pl");
    private static readonly IpAddressPrefix Prefix = IpAddressPrefix.From(IPAddress.Parse("203.0.113.7"));
    private static readonly byte[] Key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();

    private readonly CallLog calls = new();
    private readonly FakeTimeProvider time = new(T);
    private readonly InMemoryTransaction transaction;
    private readonly InMemoryOneTimeCodes oneTimeCodes;
    private readonly InMemoryCodeRequests codeRequests;
    private readonly RecordingCodeEmailSender sender;

    public RequestCodeTests()
    {
        transaction = new InMemoryTransaction(calls);
        oneTimeCodes = new InMemoryOneTimeCodes(transaction, calls);
        codeRequests = new InMemoryCodeRequests(transaction, calls);
        sender = new RecordingCodeEmailSender(calls);
    }

    [Fact]
    public async Task Sent_stores_the_code_and_the_request()
    {
        var result = await ExecuteAsync();

        Assert.Equal(new CodeRequestResult.Sent(4, 5), result);
        var message = Assert.Single(sender.Messages);
        Assert.Equal(Ada, message.Email);
        Assert.Equal(Polish, message.Locale);
        var stored = await StoredCodeAsync();
        Assert.True(stored.CodeHash.Matches(CodeHash.Compute(Key, Ada, message.Code)));
        Assert.Equal(T, stored.IssuedAt);
        Assert.Equal(new CodeRequestCounts(1, T, 1, T), await CountsAsync());
    }

    [Fact]
    public async Task Unclear_counts_as_sent()
    {
        sender.Next = new CodeEmailOutcome.Unclear();

        var result = await ExecuteAsync();

        Assert.Equal(new CodeRequestResult.Sent(4, 5), result);
        await StoredCodeAsync();
        Assert.Equal(new CodeRequestCounts(1, T, 1, T), await CountsAsync());
    }

    [Fact]
    public async Task Refused_send_keeps_the_earlier_code_and_stores_nothing()
    {
        var earlier = await StoreEarlierCodeAsync();
        sender.Next = new CodeEmailOutcome.Refused(EmailServiceRefusal.DailyLimit, T.AddHours(14));

        var result = await ExecuteAsync();

        Assert.Equal(new CodeRequestResult.RefusedByEmailService(EmailServiceRefusal.DailyLimit, T.AddHours(14)), result);
        var stored = await StoredCodeAsync();
        Assert.Equal(earlier.CodeHash, stored.CodeHash);
        Assert.Equal(earlier.IssuedAt, stored.IssuedAt);
        Assert.Equal(new CodeRequestCounts(0, null, 0, null), await CountsAsync());
    }

    [Fact]
    public async Task A_new_code_replaces_the_earlier_one()
    {
        await ExecuteAsync();
        await ExecuteAsync();

        var first = sender.Messages[0].Code;
        var second = sender.Messages[1].Code;
        var stored = await StoredCodeAsync();
        Assert.True(stored.CodeHash.Matches(CodeHash.Compute(Key, Ada, second)));
        // Two random codes are equal once in a million; then "not the first" cannot be told apart.
        if (first.Digits != second.Digits)
            Assert.False(stored.CodeHash.Matches(CodeHash.Compute(Key, Ada, first)));
    }

    [Fact]
    public async Task A_limit_refusal_sends_nothing_and_stores_nothing()
    {
        for (var hours = 1; hours <= 5; hours++)
            await codeRequests.AddAsync(CodeRequest.Create(Ada, Prefix, T.AddHours(-hours)), Ct);

        var result = await ExecuteAsync();

        Assert.Equal(new CodeRequestResult.RefusedByEmailLimit(T.AddHours(19)), result);
        Assert.Empty(sender.Messages);
        Assert.Null(await oneTimeCodes.FindExclusiveAsync(Ada, Ct));
        Assert.Equal(5, (await CountsAsync()).ForEmail);
    }

    [Fact]
    public async Task A_request_exactly_24_hours_old_has_left_the_window()
    {
        await codeRequests.AddAsync(CodeRequest.Create(Ada, Prefix, T.AddHours(-24)), Ct);
        for (var hours = 1; hours <= 4; hours++)
            await codeRequests.AddAsync(CodeRequest.Create(Ada, Prefix, T.AddHours(-hours)), Ct);

        var result = await ExecuteAsync();

        Assert.Equal(new CodeRequestResult.Sent(0, 5), result);
    }

    [Fact]
    public async Task Locks_before_counting_and_sends_before_storing()
    {
        await ExecuteAsync();

        Assert.Equal(
            ["InMemoryTransaction.Begin", "InMemoryCodeRequests.LockAsync", "InMemoryCodeRequests.CountAsync", "RecordingCodeEmailSender.SendAsync"],
            calls.Entries.Take(4));
        Assert.Equal(
            ["InMemoryCodeRequests.AddAsync", "InMemoryOneTimeCodes.ReplaceAsync"],
            calls.Entries.Skip(4).Take(2).Order(StringComparer.Ordinal));
        Assert.Equal("InMemoryTransaction.Commit", calls.Entries[6]);
        Assert.Equal(7, calls.Entries.Count);
    }

    [Fact]
    public async Task Reads_the_time_once_the_locks_are_taken()
    {
        // A request queued behind another's send waits for the locks; its code's 10 minutes start once it holds them.
        codeRequests.WhileLocking = () => time.Advance(TimeSpan.FromSeconds(10));

        await ExecuteAsync();

        var locked = T.AddSeconds(10);
        Assert.Equal(locked, (await StoredCodeAsync()).IssuedAt);
        Assert.Equal(new CodeRequestCounts(1, locked, 1, locked), await CountsAsync());
    }

    [Fact]
    public async Task Uses_the_configured_limits()
    {
        var limits = CodeRequestLimits.Create(2, 3, TimeSpan.FromHours(1));

        Assert.Equal(new CodeRequestResult.Sent(1, 2), await ExecuteAsync(limits));
        Assert.Equal(new CodeRequestResult.Sent(0, 2), await ExecuteAsync(limits));
        Assert.Equal(new CodeRequestResult.RefusedByEmailLimit(T.AddHours(1)), await ExecuteAsync(limits));
    }

    [Fact]
    public async Task A_refused_send_rolls_back_nothing_it_did_not_write()
    {
        var earlier = await StoreEarlierCodeAsync();
        await codeRequests.AddAsync(CodeRequest.Create(Ada, Prefix, T.AddHours(-1)), Ct);
        sender.Next = new CodeEmailOutcome.Refused(EmailServiceRefusal.Unavailable, null);

        await ExecuteAsync();

        Assert.Equal(earlier.CodeHash, (await StoredCodeAsync()).CodeHash);
        Assert.Equal(new CodeRequestCounts(1, T.AddHours(-1), 1, T.AddHours(-1)), await CountsAsync());

        var other = Email.Create("bob@example.com");
        var failure = new HttpRequestException("The email service failed.");
        sender.Failure = failure;

        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() => ExecuteAsync(email: other));

        Assert.Same(failure, thrown);
        Assert.Equal("InMemoryTransaction.Rollback", calls.Entries[^1]);
        Assert.Null(await oneTimeCodes.FindExclusiveAsync(other, Ct));
        Assert.Equal(0, (await codeRequests.CountAsync(other, Prefix, T.AddDays(-1), Ct)).ForEmail);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<CodeRequestResult> ExecuteAsync(CodeRequestLimits? limits = null, Email? email = null)
    {
        var options = IdentityContextOptions.Create(Key, limits ?? CodeRequestLimits.Default);
        var useCase = new RequestCode(transaction, codeRequests, oneTimeCodes, sender, time, options);
        return useCase.ExecuteAsync(email ?? Ada, Polish, Prefix, Ct);
    }

    private async Task<OneTimeCode> StoreEarlierCodeAsync()
    {
        var earlier = OneTimeCode.Issue(Ada, CodeHash.Compute(Key, Ada, OneTimeCodeValue.Create("123456")), T.AddMinutes(-1));
        await oneTimeCodes.ReplaceAsync(earlier, Ct);
        return earlier;
    }

    private async Task<OneTimeCode> StoredCodeAsync() =>
        await oneTimeCodes.FindExclusiveAsync(Ada, Ct) ?? throw new Xunit.Sdk.XunitException("No code is stored for Ada.");

    private Task<CodeRequestCounts> CountsAsync() => codeRequests.CountAsync(Ada, Prefix, T.AddDays(-1), Ct);
}
