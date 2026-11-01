using System.Diagnostics.CodeAnalysis;

namespace HabitsTracker.Identity.Core;

// The user's IANA time zone, checked against the machine's IANA database through TimeZoneInfo.
public sealed record TimeZone
{
    private const string Utc = "UTC";

    private TimeZone(string id, bool recognized)
    {
        Id = id;
        Recognized = recognized;
    }

    public string Id { get; }

    public bool Recognized { get; }

    public static TimeZone FromBrowser(string? ianaId) =>
        IsKnown(ianaId) ? new TimeZone(ianaId, recognized: true) : new TimeZone(Utc, recognized: false);

    public static TimeZone FromStorage(string id, bool recognized)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (!IsKnown(id) || (!recognized && id != Utc))
            throw new DomainException(DomainError.CorruptRecord, "A stored time zone breaks the time zone rules.");

        return new TimeZone(id, recognized);
    }

    // TimeZoneInfo also finds Windows ids; only an IANA id is known here.
    private static bool IsKnown([NotNullWhen(true)] string? id) =>
        !string.IsNullOrEmpty(id)
        && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone)
        && zone.HasIanaId;
}
