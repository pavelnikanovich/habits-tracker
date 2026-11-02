using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace HabitsTracker.Identity.Core;

public sealed partial record Locale
{
    public const string Pattern = "^[a-z]{2}$";

    private Locale(string code)
    {
        Code = code;
    }

    public string Code { get; }

    public static Locale Create(string? code, ILocales locales)
    {
        ArgumentNullException.ThrowIfNull(locales);

        if (!IsCode(code) || !locales.IsAvailable(code))
            throw new DomainException(DomainError.InvalidLocale, "The locale is not an available locale.");

        return new Locale(code);
    }

    public static Locale FromStorage(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        if (!IsCode(code))
            throw new DomainException(DomainError.CorruptRecord, "A stored locale breaks the locale rules.");

        return new Locale(code);
    }

    // The form of a locale code, without asking ILocales; the translation files check their names with it.
    public static bool IsCode([NotNullWhen(true)] string? code) =>
        code is not null && WholeMatch.IsMatch(PatternRegex(), code);

    [GeneratedRegex(Pattern, RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex PatternRegex();
}
