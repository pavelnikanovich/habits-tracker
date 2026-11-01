using HabitsTracker.Identity.Core;

namespace HabitsTracker.Tests.PortContracts;

// The contract of ICodeEmailSender (data-model.md, "Driven ports"): the recording fake and the code email senders
// of Task 8 pass it.
public abstract class CodeEmailSenderContract
{
    private static readonly Email Ada = Email.Create("ada@example.com");
    private static readonly Locale Polish = Locale.FromStorage("pl");

    // A sender set up so that its message is delivered.
    protected abstract ICodeEmailSender DeliveringSender { get; }

    // A sender set up so that its email service refuses, with the refusal it reports; null for a sender with no
    // email service to refuse (the development log).
    protected abstract (ICodeEmailSender Sender, CodeEmailOutcome.Refused Refusal)? Refusing { get; }

    [Fact]
    public async Task A_deliverable_message_is_sent()
    {
        var ct = TestContext.Current.CancellationToken;

        var outcome = await DeliveringSender.SendAsync(Ada, OneTimeCodeValue.Generate(), Polish, ct);

        Assert.Equal(new CodeEmailOutcome.Sent(), outcome);
    }

    [Fact]
    public async Task A_refusal_is_an_outcome_not_an_exception()
    {
        var ct = TestContext.Current.CancellationToken;
        if (Refusing is not ({ } sender, { } refusal))
        {
            Assert.Skip("This sender has no email service to refuse.");
            return;
        }

        var outcome = await sender.SendAsync(Ada, OneTimeCodeValue.Generate(), Polish, ct);

        Assert.Equal(refusal, outcome);
    }
}
