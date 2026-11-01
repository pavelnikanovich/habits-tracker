using System.Net;
using HabitsTracker.Tests;

namespace HabitsTracker.Host.Tests;

// One origin serves the application and the API: any path outside /api and /health gets the application,
// which shows its own "not found" (contracts/http-api.md, "Pages").
public class SpaHostingTests(PostgresFixture postgres)
{
    [Fact]
    public async Task A_page_route_gets_the_application()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new HostFactory(postgres).WithStubBuild();
        await host.InitializeAsync();
        using var client = host.CreateClient();

        using var response = await client.GetAsync(new Uri("/privacy", UriKind.Relative), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HostFactory.StubIndexHtml, await response.Content.ReadAsStringAsync(ct));
    }

    [Fact]
    public async Task An_unknown_api_path_is_not_the_application()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new HostFactory(postgres).WithStubBuild();
        await host.InitializeAsync();
        using var client = host.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/unknown", UriKind.Relative), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("<app-root>", await response.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Health_is_still_the_health_answer()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new HostFactory(postgres).WithStubBuild();
        await host.InitializeAsync();
        using var client = host.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(ct));
    }
}
