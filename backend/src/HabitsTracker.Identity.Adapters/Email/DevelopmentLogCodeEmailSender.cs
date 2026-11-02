using HabitsTracker.Identity.Core;
using Microsoft.Extensions.Logging;

namespace HabitsTracker.Identity.Adapters.CodeEmail;

/// <summary>
/// The code email sender of Development: writes the email to the log instead of sending it, in the line the
/// end-to-end tests read the code from (research R17). It never reaches production, where logs hold no email
/// and no code (research R20).
/// </summary>
public sealed partial class DevelopmentLogCodeEmailSender(CodeEmailText text, ILogger<DevelopmentLogCodeEmailSender> logger)
    : ICodeEmailSender
{
    public Task<CodeEmailOutcome> SendAsync(Email email, OneTimeCodeValue code, Locale locale, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(locale);

        var (subject, body) = text.Compose(locale, code);
        LogCodeEmail(email.Value, locale.Code, code.Digits, subject, body);
        return Task.FromResult<CodeEmailOutcome>(new CodeEmailOutcome.Sent());
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "code-email to={To} locale={Locale} code={Code} subject={Subject}\n{Body}")]
    private partial void LogCodeEmail(string to, string locale, string code, string subject, string body);
}
