namespace HabitsTracker.Identity.Core;

// The domain service of data-model.md: decides a code request from the requests counted in the window (FR-003, FR-004).
public static class CodeRequestPolicy
{
    public static CodeRequestDecision Decide(CodeRequestCounts counts, CodeRequestLimits limits)
    {
        ArgumentNullException.ThrowIfNull(counts);
        ArgumentNullException.ThrowIfNull(limits);

        RequireConsistent(counts.ForEmail, counts.OldestForEmail);
        RequireConsistent(counts.ForIpAddressPrefix, counts.OldestForIpAddressPrefix);

        // The email limit is checked first: when both limits are reached, the email's refusal is reported
        // (tasks-plan.md, Task 7, last row of the table). A count at a limit is above zero (CodeRequestLimits),
        // so its oldest moment is set.
        if (counts.ForEmail >= limits.PerEmail)
            return new CodeRequestDecision.RefusedByEmailLimit(counts.OldestForEmail!.Value + limits.Window);

        if (counts.ForIpAddressPrefix >= limits.PerIpAddress)
            return new CodeRequestDecision.RefusedByIpLimit(counts.OldestForIpAddressPrefix!.Value + limits.Window);

        return new CodeRequestDecision.Allowed(limits.PerEmail - (counts.ForEmail + 1));
    }

    // The counts come from storage. The plan asks only that a count at a limit has its oldest moment; every count
    // is checked, so a storage adapter's mistake shows here and not as a wrong retryAt or remaining count.
    // An oldest moment exists exactly when something was counted, and it is a set UTC instant.
    private static void RequireConsistent(int count, DateTimeOffset? oldest)
    {
        var consistent = oldest switch
        {
            null => count == 0,
            { } moment => count > 0 && Instant.IsSetUtc(moment),
        };

        if (!consistent)
            throw new DomainException(DomainError.CorruptRecord, "Stored code request counts break their rules.");
    }
}
