using HabitsTracker.Identity.Core;

namespace HabitsTracker.Host.Tests;

/// <summary>
/// The code email sender of the endpoint tests: remembers every code email and answers <see cref="Next"/>. A test
/// opts in with <see cref="HostFactory.WithCapturingSender"/>; by default the host keeps its environment's sender.
/// </summary>
public sealed class CapturingCodeEmailSender : ICodeEmailSender
{
    private readonly Lock gate = new();
    private readonly List<(string Email, string Code, string Locale)> sent = [];

    /// <summary>What the next sends answer.</summary>
    public CodeEmailOutcome Next { get; set; } = new CodeEmailOutcome.Sent();

    /// <summary>
    /// When set, a send records its email, completes <see cref="Started"/>, waits for the gate and then throws if its
    /// token was cancelled meanwhile, as an HTTP send cut off by the token does: the email has left by then.
    /// </summary>
    public Task? Gate { get; set; }

    /// <summary>Completes when a send that waits for <see cref="Gate"/> has started.</summary>
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int Count
    {
        get
        {
            lock (gate)
                return sent.Count;
        }
    }

    public async Task<CodeEmailOutcome> SendAsync(Email email, OneTimeCodeValue code, Locale locale, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(locale);

        lock (gate)
            sent.Add((email.Value, code.Digits, locale.Code));
        if (Gate is { } held)
        {
            Started.TrySetResult();
            await held;
            ct.ThrowIfCancellationRequested();
        }

        return Next;
    }

    /// <summary>The code of the newest email to <paramref name="email"/>, or null.</summary>
    public string? LastCode(string email) => Last(email)?.Code;

    /// <summary>The locale of the newest email to <paramref name="email"/>, or null.</summary>
    public string? LastLocale(string email) => Last(email)?.Locale;

    private (string Email, string Code, string Locale)? Last(string email)
    {
        lock (gate)
            return sent.FindLast(entry => entry.Email == email);
    }
}
