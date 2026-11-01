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

        if (!Follows(code) || !locales.IsAvailable(code))
            throw new DomainException(DomainError.InvalidLocale, "The locale is not an available locale.");

        return new Locale(code);
    }

    public static Locale FromStorage(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        if (!Follows(code))
            throw new DomainException(DomainError.CorruptRecord, "A stored locale breaks the locale rules.");

        return new Locale(code);
    }

    // In .NET "$" also matches before a final "\n", in JavaScript it does not (see Email).
    private static bool Follows([NotNullWhen(true)] string? code) =>
        code is not null
        && !code.EndsWith('\n')
        && PatternRegex().IsMatch(code);

    [GeneratedRegex(Pattern, RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex PatternRegex();
}
