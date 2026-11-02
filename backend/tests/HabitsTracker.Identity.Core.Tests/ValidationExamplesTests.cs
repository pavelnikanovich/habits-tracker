using System.Text.Json;
using HabitsTracker.Tests;

namespace HabitsTracker.Identity.Core.Tests;

// The examples the core and the pages share (research R18); Vitest runs the same file (Task 10).
public class ValidationExamplesTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string[] Rules = ["email", "locale-code", "one-time-code"];

    [Fact]
    public void Every_shared_example_holds()
    {
        var examples = JsonSerializer.Deserialize<Example[]>(
            File.ReadAllText(Repository.PathTo("testdata/validation-examples.json")), JsonOptions)!;

        Assert.Equal(Rules, examples.Select(e => e.Rule).Distinct().Order());

        var failures = examples
            .Where(example => IsAccepted(example) != example.Valid)
            .Select(example => $"{example.Rule} {Describe(example.Input)} valid={example.Valid}")
            .ToList();

        Assert.Empty(failures);
    }

    private static bool IsAccepted(Example example)
    {
        Action check = example.Rule switch
        {
            "email" => () => Email.Create(example.Input),
            "one-time-code" => () => OneTimeCodeValue.Create(example.Input),
            // The pattern only, as the pages check it; FromStorage also refuses a final "\n" (see Email).
            "locale-code" => () => Locale.FromStorage(example.Input),
            _ => throw new InvalidOperationException($"Unknown rule {example.Rule}."),
        };

        try
        {
            check();
            return true;
        }
        catch (DomainException)
        {
            return false;
        }
    }

    // A long input would flood the failure message: its start and its length are enough.
    private static string Describe(string input) =>
        input.Length > 40 ? $"{JsonSerializer.Serialize(input[..20])}… ({input.Length} characters)" : JsonSerializer.Serialize(input);

    private sealed record Example(string Rule, string Input, bool Valid);
}
