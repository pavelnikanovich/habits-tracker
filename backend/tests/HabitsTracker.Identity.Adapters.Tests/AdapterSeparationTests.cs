using HabitsTracker.Tests;

namespace HabitsTracker.Identity.Adapters.Tests;

// Adapters meet only in the core (constitution IV, research R1): the adapter folders name no type of
// one another; only IdentityContextRegistration names them all.
public class AdapterSeparationTests
{
    private const string AdaptersNamespace = "HabitsTracker.Identity.Adapters";
    private static readonly string[] AdapterFolders = ["Http", "Storage", "Email", "Translations"];

    // The namespace of a folder; the Email folder's is CodeEmail, because a namespace Email would hide the core's Email type.
    private static string NamespaceOf(string folder) => folder == "Email" ? "CodeEmail" : folder;

    [Fact]
    public void Adapters_meet_only_in_the_core()
    {
        var crossings = new List<string>();

        foreach (var folder in AdapterFolders)
        {
            var path = Repository.PathTo($"backend/src/{AdaptersNamespace}/{folder}");
            if (!Directory.Exists(path))
                continue;

            foreach (var file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == "IdentityContextRegistration.cs")
                    continue;

                var source = File.ReadAllText(file);
                crossings.AddRange(AdapterFolders
                    .Where(other => other != folder && source.Contains($"{AdaptersNamespace}.{NamespaceOf(other)}", StringComparison.Ordinal))
                    .Select(other => $"{Path.GetRelativePath(path, file)} in {folder} names {other}"));
            }
        }

        Assert.Empty(crossings);
    }
}
