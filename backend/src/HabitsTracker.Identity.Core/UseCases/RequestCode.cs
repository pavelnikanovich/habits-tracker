using System.Diagnostics;

namespace HabitsTracker.Identity.Core.UseCases;

// Request a code (research R5). Inside one transaction: take the locks, count, decide, send, and only when the
// email was sent, or may have been, store the new code and its counted request. A refused send stores nothing,
// so the earlier code stays valid and the refusal is not counted (FR-002, FR-004). The send runs inside the
// transaction, holding the locks; the sender's 10-second timeout bounds the wait of a concurrent request.
public sealed class RequestCode
{
    private readonly IIdentityTransaction transaction;
    private readonly ICodeRequests codeRequests;
    private readonly IOneTimeCodes oneTimeCodes;
    private readonly ICodeEmailSender sender;
    private readonly TimeProvider time;
    private readonly IdentityContextOptions options;

    public RequestCode(
        IIdentityTransaction transaction,
        ICodeRequests codeRequests,
        IOneTimeCodes oneTimeCodes,
        ICodeEmailSender sender,
        TimeProvider time,
        IdentityContextOptions options)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(codeRequests);
        ArgumentNullException.ThrowIfNull(oneTimeCodes);
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(options);

        this.transaction = transaction;
        this.codeRequests = codeRequests;
        this.oneTimeCodes = oneTimeCodes;
        this.sender = sender;
        this.time = time;
        this.options = options;
    }

    public Task<CodeRequestResult> ExecuteAsync(
        Email email, Locale locale, IpAddressPrefix ipAddressPrefix, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(locale);
        ArgumentNullException.ThrowIfNull(ipAddressPrefix);

        return transaction.RunAsync(work => RequestAsync(email, locale, ipAddressPrefix, work), ct);
    }

    private async Task<CodeRequestResult> RequestAsync(
        Email email, Locale locale, IpAddressPrefix ipAddressPrefix, CancellationToken ct)
    {
        // One moment for the window, the code and the request, so all three agree on it.
        var now = time.GetUtcNow();
        var limits = options.CodeRequestLimits;

        await codeRequests.LockAsync(email, ipAddressPrefix, ct);
        var counts = await codeRequests.CountAsync(email, ipAddressPrefix, now - limits.Window, ct);

        int remaining;
        switch (CodeRequestPolicy.Decide(counts, limits))
        {
            case CodeRequestDecision.Allowed allowed:
                remaining = allowed.RemainingAfterSend;
                break;
            case CodeRequestDecision.RefusedByEmailLimit refused:
                return new CodeRequestResult.RefusedByEmailLimit(refused.RetryAt);
            case CodeRequestDecision.RefusedByIpLimit refused:
                return new CodeRequestResult.RefusedByIpLimit(refused.RetryAt);
            default:
                throw new UnreachableException();
        }

        var code = OneTimeCodeValue.Generate();
        switch (await sender.SendAsync(email, code, locale, ct))
        {
            case CodeEmailOutcome.Refused refused:
                return new CodeRequestResult.RefusedByEmailService(refused.Reason, refused.RetryAt);
            // An unclear send counts as sent: the email may have arrived, so its code must work (FR-004).
            case CodeEmailOutcome.Sent or CodeEmailOutcome.Unclear:
                break;
            default:
                throw new UnreachableException();
        }

        await oneTimeCodes.ReplaceAsync(
            OneTimeCode.Issue(email, CodeHash.Compute(options.CodeHashKey.Span, email, code), now), ct);
        await codeRequests.AddAsync(CodeRequest.Create(email, ipAddressPrefix, now), ct);

        return new CodeRequestResult.Sent(remaining, limits.PerEmail);
    }
}
