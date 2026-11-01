using System.Reflection;
using System.Text.RegularExpressions;
using HabitsTracker.Identity.Adapters.Http;
using HabitsTracker.Tests;

namespace HabitsTracker.Host.Tests;

// The page shows a code it does not know as error.network, so it keeps its own list of the API's codes
// (frontend/src/app/api/problem.ts, knownCodes); the list holds exactly the codes of ProblemCodes.
public class ProblemCodeParityTests
{
    [Fact]
    public void The_page_knows_every_code_the_api_answers()
    {
        var source = File.ReadAllText(Repository.PathTo("frontend/src/app/api/problem.ts"));

        string[] api = [.. typeof(ProblemCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)];
        string[] page = [.. KnownCodesOf(source)];

        Assert.True(api.Length > 0, "ProblemCodes holds no code");
        string[] unknownToPage = [.. api.Except(page, StringComparer.Ordinal)];
        Assert.True(unknownToPage.Length == 0, $"knownCodes lacks {string.Join(", ", unknownToPage)}");
        string[] unknownToApi = [.. page.Except(api, StringComparer.Ordinal)];
        Assert.True(unknownToApi.Length == 0, $"ProblemCodes lacks {string.Join(", ", unknownToApi)}");
        Assert.True(page.Length == page.Distinct(StringComparer.Ordinal).Count(), "knownCodes names a code twice");
    }

    // `export const knownCodes = ['…', …] as const;`: the string literals of the array.
    private static IEnumerable<string> KnownCodesOf(string source)
    {
        var array = Regex.Match(source, @"export const knownCodes = \[(?<items>[^\]]*)\] as const;", RegexOptions.None, TimeSpan.FromSeconds(1));
        Assert.True(array.Success, "knownCodes is not a const array literal in problem.ts");
        return Regex.Matches(array.Groups["items"].Value, "'(?<code>[^']*)'", RegexOptions.None, TimeSpan.FromSeconds(1))
            .Select(match => match.Groups["code"].Value);
    }
}
