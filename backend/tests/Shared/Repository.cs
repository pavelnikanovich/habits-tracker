namespace HabitsTracker.Tests;

/// <summary>Paths into the repository the tests run from, found by walking up to <c>backend/HabitsTracker.slnx</c>.</summary>
public static class Repository
{
    private static readonly Lazy<string> Root = new(FindRoot);

    public static string PathTo(string relativePath) =>
        Path.Combine(Root.Value, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "backend", "HabitsTracker.slnx")))
                return directory.FullName;
        }

        throw new InvalidOperationException($"No backend/HabitsTracker.slnx above {AppContext.BaseDirectory}.");
    }
}
