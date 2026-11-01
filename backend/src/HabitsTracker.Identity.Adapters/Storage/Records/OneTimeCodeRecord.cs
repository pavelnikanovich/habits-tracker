namespace HabitsTracker.Identity.Adapters.Storage.Records;

/// <summary>A row of <c>identity.one_time_codes</c>; <see cref="EfOneTimeCodes"/> turns it into a <c>OneTimeCode</c> and back.</summary>
public sealed class OneTimeCodeRecord
{
    public required string Email { get; set; }

    public required byte[] CodeHash { get; set; }

    public required DateTimeOffset IssuedAt { get; set; }

    public required DateTimeOffset ExpiresAt { get; set; }

    public required short EntryAttempts { get; set; }
}
