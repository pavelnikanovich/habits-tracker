using System.Runtime.CompilerServices;

namespace HabitsTracker.Identity.Core;

// Instants are UTC (data-model.md, "Domain model rules"). An instant never set (default, 0001-01-01) is a
// forgotten argument, not a moment. Both are programming errors.
internal static class Instant
{
    // The rule itself, for a check that raises its own error (a stored value breaking it is CorruptRecord).
    public static bool IsSetUtc(DateTimeOffset instant) => instant != default && instant.Offset == TimeSpan.Zero;

    public static void Require(DateTimeOffset instant, [CallerArgumentExpression(nameof(instant))] string? name = null)
    {
        if (IsSetUtc(instant))
            return;

        throw new ArgumentException(instant == default ? "The instant is not set." : "The instant is not UTC.", name);
    }
}
