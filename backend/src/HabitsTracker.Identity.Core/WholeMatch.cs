using System.Text.RegularExpressions;

namespace HabitsTracker.Identity.Core;

// In .NET "$" also matches before a final "\n", in JavaScript it does not. The patterns are copied to the
// frontend (research R18), so they stay as they are and this check keeps each rule one for both engines.
internal static class WholeMatch
{
    public static bool IsMatch(Regex pattern, string input) =>
        !input.EndsWith('\n') && pattern.IsMatch(input);
}
