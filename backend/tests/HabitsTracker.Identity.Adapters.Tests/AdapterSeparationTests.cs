using System.Text.RegularExpressions;
using HabitsTracker.Tests;

namespace HabitsTracker.Identity.Adapters.Tests;

// Adapters meet only in the core (constitution IV, research R1): the adapter folders name no type of one another;
// only IdentityContextRegistration, at the project's root, names them all. A driving adapter calls only use cases, so
// the HTTP adapter names no driven port either.
public class AdapterSeparationTests
{
    private const string AdaptersNamespace = "HabitsTracker.Identity.Adapters";

    // Each folder and its namespace; the Email folder's is CodeEmail, because a namespace Email would hide the core's
    // Email type (R-50).
    private static readonly (string Folder, string Namespace)[] Adapters =
    [
        ("Http", "Http"),
        ("Storage", "Storage"),
        ("Email", "CodeEmail"),
        ("Translations", "Translations"),
    ];

    private static readonly string[] DrivenPorts =
        ["IUsers", "ISessions", "IOneTimeCodes", "ICodeRequests", "ILocales", "IIdentityTransaction", "ICodeEmailSender"];

    [Fact]
    public void Adapters_meet_only_in_the_core()
    {
        var crossings = new List<string>();

        foreach (var (folder, _) in Adapters)
        {
            foreach (var (file, source) in SourcesOf(folder))
            {
                crossings.AddRange(Adapters
                    .Where(other => other.Folder != folder && Names(source, other.Namespace))
                    .Select(other => $"{file} in {folder} names {other.Folder}"));
            }
        }

        Assert.Empty(crossings);
    }

    [Fact]
    public void The_http_adapter_names_no_driven_port()
    {
        var named = new List<string>();

        foreach (var (file, source) in SourcesOf("Http"))
        {
            named.AddRange(DrivenPorts
                .Where(port => Regex.IsMatch(source, $@"\b{port}\b", RegexOptions.None, TimeSpan.FromSeconds(1)))
                .Select(port => $"{file} in Http names {port}"));
        }

        Assert.Empty(named);
    }

    // The .cs files of a folder of the adapters project, relative to it. A folder that is gone fails: a test that
    // reads no file would pass whatever the adapters name.
    private static IEnumerable<(string File, string Source)> SourcesOf(string folder)
    {
        var path = Repository.PathTo($"backend/src/{AdaptersNamespace}/{folder}");
        Assert.True(Directory.Exists(path), $"no folder {folder} in {AdaptersNamespace}");

        return Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories)
            .Select(file => (Path.GetRelativePath(path, file), File.ReadAllText(file)));
    }

    // Whether the source names the adapter whose namespace is `ns`: fully qualified; by a shorter name that the
    // enclosing namespace resolves (`Storage.IdentityDbContext`, `Adapters.Storage.…`, not `System.Net.Http.…`); or by
    // a using directive (`using Adapters.Storage;`, `using Storage;` after a file-scoped namespace).
    private static bool Names(string source, string ns) =>
        Regex.IsMatch(source, $@"\b{AdaptersNamespace.Replace(".", @"\.", StringComparison.Ordinal)}\.{ns}\b", RegexOptions.None, TimeSpan.FromSeconds(1))
        || Regex.IsMatch(source, $@"(?<![\w.])(?:Identity\.)?(?:Adapters\.)?{ns}\.", RegexOptions.None, TimeSpan.FromSeconds(1))
        || Regex.IsMatch(source, $@"\busing\s+(?:static\s+)?(?:\w+\s*=\s*)?(?:[\w.]*\bAdapters\.)?{ns}\s*;", RegexOptions.None, TimeSpan.FromSeconds(1));
}
