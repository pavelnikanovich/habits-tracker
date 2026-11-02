using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace HabitsTracker.Identity.Core;

public sealed partial record OneTimeCodeValue
{
    public const string Pattern = "^[0-9]{6}$";

    private OneTimeCodeValue(string digits)
    {
        Digits = digits;
    }

    public string Digits { get; }

    public static OneTimeCodeValue Generate() =>
        new(RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture));

    public static OneTimeCodeValue Create(string? input)
    {
        if (!Follows(input))
            throw new DomainException(DomainError.InvalidOneTimeCode, "The one-time code is not six digits.");

        return new OneTimeCodeValue(input);
    }

    // Secrets do not print (FR-011): the digits are read only through Digits.
    public override string ToString() => "***";

    private static bool Follows([NotNullWhen(true)] string? input) =>
        input is not null && WholeMatch.IsMatch(PatternRegex(), input);

    // Linear time without a timeout, which would count elapsed time and fail under load (RegexEngineTests).
    [GeneratedRegex(Pattern, RegexOptions.NonBacktracking)]
    private static partial Regex PatternRegex();
}
