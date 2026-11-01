namespace HabitsTracker.Identity.Core;

// An invalid value. The message names the value, never its content.
public sealed class DomainException : Exception
{
    public DomainException(DomainError error, string message)
        : base(message)
    {
        Error = error;
    }

    public DomainError Error { get; }
}
