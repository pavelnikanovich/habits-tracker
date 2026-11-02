using HabitsTracker.Identity.Core;

namespace HabitsTracker.Tests.PortContracts;

// The contract of ICodeEmailSender (data-model.md, "Driven ports"): the recording fake and the code email senders
// of Task 8 pass it.
public abstract class CodeEmailSenderContract
{
    protected static readonly Email Ada = Email.Create("ada@example.com");
    protected static readonly Locale Polish = Locale.FromStorage("pl");

    // A sender set up so that its message is delivered.
    protected abstract ICodeEmailSender DeliveringSender { get; }

    [Fact]
    public async Task A_deliverable_message_is_sent()
    {
        var ct = TestContext.Current.CancellationToken;

        var outcome = await DeliveringSender.SendAsync(Ada, OneTimeCodeValue.Generate(), Polish, ct);

        Assert.Equal(new CodeEmailOutcome.Sent(), outcome);
    }
}

// The contract of a sender behind an email service that can refuse; the development log has none, so it passes
// only the contract above.
public abstract class RefusingCodeEmailSenderContract : CodeEmailSenderContract
{
    // A sender set up so that its email service refuses, with the refusal it reports.
    protected abstract (ICodeEmailSender Sender, CodeEmailOutcome.Refused Refusal) Refusing { get; }

    [Fact]
    public async Task A_refusal_is_an_outcome_not_an_exception()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sender, refusal) = Refusing;

        var outcome = await sender.SendAsync(Ada, OneTimeCodeValue.Generate(), Polish, ct);

        Assert.Equal(refusal, outcome);
    }
}
