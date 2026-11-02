using System.Reflection;
using System.Text.RegularExpressions;
using HabitsTracker.Identity.Adapters;
using HabitsTracker.Identity.Core;

namespace HabitsTracker.Host.Tests;

// A match timeout counts elapsed time, not CPU time: under a load average of 17 a valid address timed out at
// 100 ms and answered 500. Every generated regex runs on the non-backtracking engine instead, whose time is
// linear in the input, so it needs no timeout and still cannot be made to work hard (research R19, D5).
public class RegexEngineTests
{
    public static TheoryData<string> Assemblies() =>
    [
        typeof(Email).Assembly.GetName().Name!,
        typeof(IdentityContextRegistration).Assembly.GetName().Name!,
        typeof(Program).Assembly.GetName().Name!,
    ];

    [Theory]
    [MemberData(nameof(Assemblies))]
    public void Every_generated_regex_runs_without_backtracking_and_without_a_timeout(string assemblyName)
    {
        var offending = GeneratedRegexes(Assembly.Load(assemblyName))
            .Where(found => !found.Regex.Options.HasFlag(RegexOptions.NonBacktracking)
                || found.Regex.MatchTimeout != Regex.InfiniteMatchTimeout)
            .Select(found => found.Name);

        Assert.Empty(offending);
    }

    [Fact]
    public void The_known_generated_regexes_are_found()
    {
        var names = new[] { typeof(Email), typeof(Program) }
            .SelectMany(type => GeneratedRegexes(type.Assembly))
            .Select(found => found.Name);

        Assert.Superset(
            new HashSet<string>
            {
                "HabitsTracker.Identity.Core.Email.PatternRegex",
                "HabitsTracker.Identity.Core.OneTimeCodeValue.PatternRegex",
                "HabitsTracker.Identity.Core.Locale.PatternRegex",
                "HabitsTracker.Host.Web.CachingHeaders.HashedFileName",
            },
            names.ToHashSet());
    }

    private static IEnumerable<(string Name, Regex Regex)> GeneratedRegexes(Assembly assembly) =>
        assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttribute<GeneratedRegexAttribute>() is not null)
            .Select(method => ($"{method.DeclaringType!.FullName}.{method.Name}", (Regex)method.Invoke(null, null)!));
}
