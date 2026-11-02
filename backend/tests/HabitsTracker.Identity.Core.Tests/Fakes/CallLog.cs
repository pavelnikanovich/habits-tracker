using System.Runtime.CompilerServices;

namespace HabitsTracker.Identity.Core.Tests.Fakes;

// The order of the fakes' calls, as "<fake>.<method>", for the tests of a use case's sequence.
public sealed class CallLog
{
    private readonly List<string> entries = [];

    public IReadOnlyList<string> Entries => entries;

    public void Record(object source, [CallerMemberName] string method = "")
    {
        ArgumentNullException.ThrowIfNull(source);
        entries.Add($"{source.GetType().Name}.{method}");
    }
}
