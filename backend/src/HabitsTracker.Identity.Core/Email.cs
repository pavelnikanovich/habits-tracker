using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace HabitsTracker.Identity.Core;

public sealed partial record Email
{
    public const int MaxLength = 254;

    // The HTML standard's "valid e-mail address" (WHATWG), research R18.
    public const string Pattern =
        "^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$";

    private Email(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Email Create(string? input)
    {
        if (!Follows(input))
            throw new DomainException(DomainError.InvalidEmail, "The email is not a valid email address.");

        return new Email(input.ToLowerInvariant());
    }

    public static Email FromStorage(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!Follows(value) || !string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal))
            throw new DomainException(DomainError.CorruptRecord, "A stored email breaks the email rules.");

        return new Email(value);
    }

    // The length check runs before the pattern (research R19, D5). In .NET "$" also matches before a final
    // "\n", in JavaScript it does not: the explicit check keeps the rule one for both engines.
    private static bool Follows([NotNullWhen(true)] string? input) =>
        input is { Length: <= MaxLength }
        && !input.EndsWith('\n')
        && PatternRegex().IsMatch(input);

    [GeneratedRegex(Pattern, RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex PatternRegex();
}
