namespace HabitsTracker.Identity.Core.Tests.Fakes;

// ICodeEmailSender that sends nothing: it records each message and answers with Next, Sent unless a test sets it,
// or fails with Failure.
public sealed class RecordingCodeEmailSender : ICodeEmailSender
{
    private readonly List<Message> messages = [];
    private readonly CallLog? calls;
    private CodeEmailOutcome next = new CodeEmailOutcome.Sent();

    // calls: null when a test does not check the order of calls.
    public RecordingCodeEmailSender(CallLog? calls = null)
    {
        this.calls = calls;
    }

    public CodeEmailOutcome Next
    {
        get => next;
        set => next = value ?? throw new ArgumentNullException(nameof(value));
    }

    // When set, SendAsync fails with it, as a sender whose request to the email service fails.
    public Exception? Failure { get; set; }

    public IReadOnlyList<Message> Messages => messages;

    public Task<CodeEmailOutcome> SendAsync(Email email, OneTimeCodeValue code, Locale locale, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(locale);

        calls?.Record(this);
        messages.Add(new Message(email, code, locale));
        return Failure is null ? Task.FromResult(Next) : Task.FromException<CodeEmailOutcome>(Failure);
    }

    public sealed record Message(Email Email, OneTimeCodeValue Code, Locale Locale);
}
