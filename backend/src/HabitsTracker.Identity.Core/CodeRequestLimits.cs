namespace HabitsTracker.Identity.Core;

// How many codes one email and one IP address prefix may request in a sliding window (FR-004).
public sealed record CodeRequestLimits
{
    private CodeRequestLimits(int perEmail, int perIpAddress, TimeSpan window)
    {
        PerEmail = perEmail;
        PerIpAddress = perIpAddress;
        Window = window;
    }

    public static CodeRequestLimits Default { get; } = Create(5, 10, TimeSpan.FromHours(24));

    public int PerEmail { get; }

    public int PerIpAddress { get; }

    public TimeSpan Window { get; }

    public static CodeRequestLimits Create(int perEmail, int perIpAddress, TimeSpan window)
    {
        if (perEmail <= 0 || perIpAddress <= 0 || window <= TimeSpan.Zero)
            throw new DomainException(DomainError.InvalidConfiguration, "The code request limits are not all above zero.");

        return new CodeRequestLimits(perEmail, perIpAddress, window);
    }
}
