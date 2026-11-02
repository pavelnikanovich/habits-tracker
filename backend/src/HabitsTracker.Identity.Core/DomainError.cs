namespace HabitsTracker.Identity.Core;

// The error model of data-model.md: one value per invalid value; the adapters translate it at the edge.
public enum DomainError
{
    InvalidEmail,
    InvalidLocale,
    InvalidOneTimeCode,
    InvalidSessionToken,
    CorruptRecord,
    InvalidConfiguration,
}
