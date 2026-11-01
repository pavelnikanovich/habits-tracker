namespace HabitsTracker.Identity.Core.Tests.Fakes;

// ILocales over a fixed set of codes; by default the epic's three translation files: en, pl and ru.
public sealed class FixedLocales : ILocales
{
    private static readonly string[] EpicLocales = ["en", "pl", "ru"];

    private readonly HashSet<string> codes;

    public FixedLocales(params string[] codes)
    {
        ArgumentNullException.ThrowIfNull(codes);
        this.codes = new HashSet<string>(codes.Length == 0 ? EpicLocales : codes, StringComparer.Ordinal);
    }

    public bool IsAvailable(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return codes.Contains(code);
    }
}
