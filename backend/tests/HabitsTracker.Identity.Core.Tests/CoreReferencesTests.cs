using System.Xml.Linq;
using HabitsTracker.Tests;

namespace HabitsTracker.Identity.Core.Tests;

// The dependency rule (constitution IV, research R1): the core compiles against the .NET base library only.
public class CoreReferencesTests
{
    private const string CoreFolder = "backend/src/HabitsTracker.Identity.Core";

    [Fact]
    public void Core_project_references_nothing()
    {
        var csproj = XDocument.Load(Repository.PathTo($"{CoreFolder}/HabitsTracker.Identity.Core.csproj"));

        foreach (var element in new[] { "PackageReference", "FrameworkReference", "ProjectReference" })
            Assert.Empty(csproj.Descendants(element));
    }

    [Fact]
    public void Core_sources_carry_no_storage_or_serialization_attributes()
    {
        var offending = Directory
            .EnumerateFiles(Repository.PathTo(CoreFolder), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
            .Where(file =>
            {
                var source = File.ReadAllText(file);
                return source.Contains("System.ComponentModel.DataAnnotations", StringComparison.Ordinal)
                    || source.Contains("System.Text.Json", StringComparison.Ordinal);
            });

        Assert.Empty(offending);
    }

    private static bool IsBuildOutput(string file) =>
        file.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj");
}
