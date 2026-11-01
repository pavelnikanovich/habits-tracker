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

    public int Count
    {
        get
        {
            lock (gate)
                return sent.Count;
        }
    }

    public Task<CodeEmailOutcome> SendAsync(Email email, OneTimeCodeValue code, Locale locale, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(locale);

        lock (gate)
            sent.Add((email.Value, code.Digits, locale.Code));
        return Task.FromResult(Next);
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
