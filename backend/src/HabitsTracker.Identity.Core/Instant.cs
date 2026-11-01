using System.Runtime.CompilerServices;

namespace HabitsTracker.Identity.Core;

// Instants are UTC (data-model.md, "Domain model rules"). An instant never set (default, 0001-01-01) is a
// forgotten argument, not a moment. Both are programming errors.
internal static class Instant
{
    public static void Require(DateTimeOffset instant, [CallerArgumentExpression(nameof(instant))] string? name = null)
    {
        if (instant == default)
            throw new ArgumentException("The instant is not set.", name);

        if (instant.Offset != TimeSpan.Zero)
            throw new ArgumentException("The instant is not UTC.", name);
    }
}
