namespace HabitsTracker.Identity.Core;

// The languages the application has a translation file for (FR-020).
public interface ILocales
{
    bool IsAvailable(string code);
}
