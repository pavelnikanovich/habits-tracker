namespace HabitsTracker.Identity.Adapters.Storage.Records;

/// <summary>A row of <c>identity.sessions</c>; <see cref="EfSessions"/> turns it into a <c>Session</c> and back.</summary>
public sealed class SessionRecord
{
    public required byte[] Id { get; set; }

    public required Guid UserId { get; set; }

    public required bool Remembered { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    public required DateTimeOffset LastActivityAt { get; set; }
}
