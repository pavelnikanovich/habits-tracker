using HabitsTracker.Identity.Core;

namespace HabitsTracker.Identity.Adapters.CodeEmail;

/// <summary>
/// The text of the code email, composed from the translation file of the page's locale (FR-020). The keys
/// <c>email.subject</c> and <c>email.body</c> take <c>{code}</c> by plain substitution, not by MessageFormat
/// (research R11). The lookup is a function, so this folder does not name the translation adapter.
/// </summary>
public sealed class CodeEmailText(Func<Locale, string, string> text)
{
    private const string Placeholder = "{code}";

    public (string Subject, string Body) Compose(Locale locale, OneTimeCodeValue code)
    {
        ArgumentNullException.ThrowIfNull(locale);
        ArgumentNullException.ThrowIfNull(code);

        return (Fill("email.subject"), Fill("email.body"));

        string Fill(string key) => text(locale, key).Replace(Placeholder, code.Digits, StringComparison.Ordinal);
    }
}
