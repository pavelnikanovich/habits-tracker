using HabitsTracker.Tests;

namespace HabitsTracker.Host.Tests;

// FR-032: logs are kept 7 days. The drop-in is installed on the server by the deployment (Task 20); this checks its content.
public class JournaldRetentionTests
{
    [Fact]
    public async Task Logs_are_kept_seven_days()
    {
        var lines = (await File.ReadAllLinesAsync(Repository.PathTo("ops/journald/habits.conf"), TestContext.Current.CancellationToken))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();

        Assert.Equal(["[Journal]", "MaxRetentionSec=7day", "MaxFileSec=1day"], lines);
    }
}
