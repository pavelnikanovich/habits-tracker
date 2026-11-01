using HabitsTracker.Tests;

namespace HabitsTracker.Host.Tests;

// Every response carries the security headers (research R11, R19 T2 and T3; FR-027): only the origin's scripts,
// styles, images and connections, no framing, no MIME sniffing, no referrer.
public class SecurityHeadersTests(PostgresFixture postgres)
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

    [Theory]
    [InlineData("/")]
    [InlineData("/api/locales")]
    public async Task Every_response_carries_the_security_headers(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new HostFactory(postgres).WithStubBuild();
        await host.InitializeAsync();
        using var client = host.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), ct);

        Assert.Equal([ContentSecurityPolicy], response.Headers.GetValues("Content-Security-Policy"));
        Assert.Equal(["nosniff"], response.Headers.GetValues("X-Content-Type-Options"));
        Assert.Equal(["no-referrer"], response.Headers.GetValues("Referrer-Policy"));
    }
}
