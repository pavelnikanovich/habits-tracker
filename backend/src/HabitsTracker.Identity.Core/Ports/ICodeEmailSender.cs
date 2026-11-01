namespace HabitsTracker.Identity.Core;

public interface ICodeEmailSender
{
    Task<CodeEmailOutcome> SendAsync(Email email, OneTimeCodeValue code, Locale locale, CancellationToken ct);
}
