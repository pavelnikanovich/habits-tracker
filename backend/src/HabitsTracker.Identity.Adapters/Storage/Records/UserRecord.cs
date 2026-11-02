namespace HabitsTracker.Identity.Adapters.Storage.Records;

/// <summary>A row of <c>identity.users</c>; <see cref="EfUsers"/> turns it into a <c>User</c> and back.</summary>
public sealed class UserRecord
{
    public required Guid Id { get; set; }

    public required string Email { get; set; }

    public required string TimeZone { get; set; }

    public required bool TimeZoneRecognized { get; set; }

    public required string Locale { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }
}
