namespace HabitsTracker.Identity.Core;

// Outcomes reached with valid values: results of the use cases and of the email sender, not exceptions
// (data-model.md, "Error model"). Every switch over one ends with `_ => throw new UnreachableException()`.
// Positional records without checks, unlike the value objects: R23 bans positional syntax there because it
// would bypass their factories, and results have none; the core builds them only from values already checked.

public enum CodeVerification
{
    SignIn,
    Incorrect,
    NewCodeRequired,
}

// The read model "Code request counts" of data-model.md: counted requests in the window and the oldest of each.
public sealed record CodeRequestCounts(
    int ForEmail, DateTimeOffset? OldestForEmail, int ForIpAddressPrefix, DateTimeOffset? OldestForIpAddressPrefix);

// What CodeRequestPolicy decides. The cases are nested: CodeRequestResult has cases of the same names.
public abstract record CodeRequestDecision
{
    private CodeRequestDecision()
    {
    }

    public sealed record Allowed(int RemainingAfterSend) : CodeRequestDecision;

    public sealed record RefusedByEmailLimit(DateTimeOffset RetryAt) : CodeRequestDecision;

    public sealed record RefusedByIpLimit(DateTimeOffset RetryAt) : CodeRequestDecision;
}

public enum EmailServiceRefusal
{
    DailyLimit,
    Limit,
    Unavailable,
}

public abstract record CodeEmailOutcome
{
    private CodeEmailOutcome()
    {
    }

    public sealed record Sent : CodeEmailOutcome;

    public sealed record Unclear : CodeEmailOutcome;

    public sealed record Refused(EmailServiceRefusal Reason, DateTimeOffset? RetryAt) : CodeEmailOutcome;
}

public abstract record CodeRequestResult
{
    private CodeRequestResult()
    {
    }

    public sealed record Sent(int Remaining, int Limit) : CodeRequestResult;

    public sealed record RefusedByEmailLimit(DateTimeOffset RetryAt) : CodeRequestResult;

    public sealed record RefusedByIpLimit(DateTimeOffset RetryAt) : CodeRequestResult;

    public sealed record RefusedByEmailService(EmailServiceRefusal Reason, DateTimeOffset? RetryAt) : CodeRequestResult;
}

public abstract record SignInResult
{
    private SignInResult()
    {
    }

    public sealed record SignedIn(User User, Session Session, SessionToken Token) : SignInResult;

    public sealed record CodeIncorrect : SignInResult;

    public sealed record NewCodeRequired : SignInResult;
}

public sealed record AuthenticatedSession(User User, Session Session);

public sealed record DeletedRecords(int OneTimeCodes, int CodeRequests, int Sessions);
