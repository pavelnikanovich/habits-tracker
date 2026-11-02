using System.Text.RegularExpressions;
using HabitsTracker.Identity.Core;
using HabitsTracker.Tests;

namespace HabitsTracker.Host.Tests;

// The page checks an email and a code before it sends them, with its own copy of the core's rules (research R18).
public class ValidationPatternParityTests
{
    [Fact]
    public void The_page_uses_the_cores_rules()
    {
        var source = File.ReadAllText(Repository.PathTo("frontend/src/app/validation/patterns.ts"));

        Assert.Equal(Email.Pattern, PatternOf(source, "EMAIL_PATTERN"));
        Assert.Equal(OneTimeCodeValue.Pattern, PatternOf(source, "ONE_TIME_CODE_PATTERN"));
        Assert.Equal(Email.MaxLength.ToString(System.Globalization.CultureInfo.InvariantCulture), ConstantOf(source, "EMAIL_MAX_LENGTH"));
    }

    // `export const EMAIL_PATTERN = /…/;` : the source of the regular expression literal.
    private static string PatternOf(string source, string name)
    {
        var match = Regex.Match(source, $@"export const {name}\s*=\s*/(?<pattern>.+)/\s*;", RegexOptions.None, TimeSpan.FromSeconds(1));
        Assert.True(match.Success, $"{name} is not a regular expression literal in patterns.ts");
        return match.Groups["pattern"].Value.Replace("\\/", "/", StringComparison.Ordinal);
    }

    private static string ConstantOf(string source, string name)
    {
        var match = Regex.Match(source, $@"export const {name}\s*=\s*(?<value>\d+)\s*;", RegexOptions.None, TimeSpan.FromSeconds(1));
        Assert.True(match.Success, $"{name} is not a number constant in patterns.ts");
        return match.Groups["value"].Value;
    }
}
