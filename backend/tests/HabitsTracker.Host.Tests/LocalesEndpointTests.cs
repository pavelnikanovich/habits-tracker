using System.Net;
using HabitsTracker.Tests;

namespace HabitsTracker.Host.Tests;

// GET /api/locales lists the translation files, so a new file is a new language (contracts/http-api.md, FR-020).
public class LocalesEndpointTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Locales_lists_the_translation_files()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new HostFactory(postgres);
        await host.InitializeAsync();
        using var client = host.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/locales", UriKind.Relative), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            """[{"code":"en","name":"English"},{"code":"pl","name":"Polski"},{"code":"ru","name":"Русский"}]""",
            await response.Content.ReadAsStringAsync(ct));
    }
}
