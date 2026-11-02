using Microsoft.Net.Http.Headers;

namespace HabitsTracker.Host.Tests;

/// <summary>
/// A client of a signed-in browser: <see cref="Client"/> sends the cookie <c>session=</c><see cref="Token"/> on every
/// request and keeps no cookie of its own. <see cref="SetCookie"/> holds the <c>Set-Cookie</c> headers of the sign-in's
/// answer; it is empty for a client made with <see cref="WithCookie"/>.
/// </summary>
public sealed record SignedInClient(HttpClient Client, string Token) : IDisposable
{
    public IReadOnlyList<string> SetCookie { get; init; } = [];

    /// <summary>A client of <paramref name="host"/> that sends the cookie <c>session=</c><paramref name="token"/>, whatever the token is.</summary>
    public static SignedInClient WithCookie(HostFactory host, string token)
    {
        ArgumentNullException.ThrowIfNull(host);
        var client = host.CreateCookielessClient();
        client.DefaultRequestHeaders.Add(HeaderNames.Cookie, $"session={token}");
        return new SignedInClient(client, token);
    }

    public void Dispose() => Client.Dispose();
}
