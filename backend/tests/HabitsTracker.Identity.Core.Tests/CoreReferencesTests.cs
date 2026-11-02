using System.Runtime.InteropServices;
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

    // What the core is compiled against, wherever a reference comes from (Directory.Build.props included): only the
    // .NET base library, Microsoft.NETCore.App. An analyzer, such as SonarAnalyzer.CSharp there, leaves no reference.
    [Fact]
    public void Core_assembly_references_only_the_base_library()
    {
        var runtime = RuntimeEnvironment.GetRuntimeDirectory();
        var outside = typeof(Email).Assembly.GetReferencedAssemblies()
            .Select(name => name.Name!)
            .Where(name => !File.Exists(Path.Combine(runtime, $"{name}.dll")));

        Assert.Empty(outside);
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
