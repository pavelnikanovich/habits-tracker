using System.Net;
using HabitsTracker.Tests;

namespace HabitsTracker.Host.Tests;

// HTTP caching (research R21): a deployment reaches the browser at once, hashed build files are kept for a year,
// and no answer of the API or of the health check is stored (research R19, I6).
public class CachingHeadersTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Each_kind_of_response_has_its_cache_rule()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new HostFactory(postgres).WithStubBuild();
        await host.InitializeAsync();
        using var client = host.CreateClient();

        (string Path, string CacheControl)[] rules =
        [
            ("/", "no-cache"),
            ("/privacy", "no-cache"),
            ($"/{HostFactory.StubHashedFile}", "public, max-age=31536000, immutable"),
            ("/i18n/en.json", "no-cache"),
            ("/api/locales", "no-store"),
            ("/health", "no-store"),
        ];

        foreach (var (path, cacheControl) in rules)
        {
            using var response = await client.GetAsync(new Uri(path, UriKind.Relative), ct);

            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {response.StatusCode}");
            Assert.True(
                response.Headers.CacheControl?.ToString() == cacheControl,
                $"{path}: Cache-Control '{response.Headers.CacheControl}', expected '{cacheControl}'");
        }
    }
}
