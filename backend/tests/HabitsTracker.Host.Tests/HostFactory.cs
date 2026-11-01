using System.Net;
using System.Net.Http.Json;
using HabitsTracker.Identity.Core;
using HabitsTracker.Tests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Net.Http.Headers;
using Npgsql;

namespace HabitsTracker.Host.Tests;

/// <summary>
/// The host in process (research R12): a fresh database, migrated on start, a <see cref="FakeTimeProvider"/> in place of
/// <see cref="TimeProvider"/>, environment Development unless set, the translation files of the frontend,
/// the client's address taken from the test header <c>Test-Remote-Address</c> (default 127.0.0.1),
/// and the test-only endpoints under <c>/api/test/</c>, mapped inside the host's own pipeline.
/// </summary>
public sealed class HostFactory(PostgresFixture postgres) : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string RemoteAddressHeader = "Test-Remote-Address";
    private const string ConnectionStringKey = "ConnectionStrings:Habits";

    private readonly Dictionary<string, string?> settings = new()
    {
        ["Translations:Path"] = Repository.PathTo("frontend/public/i18n"),
        ["Database:MigrateOnStart"] = "true",
    };

    // A stub of the frontend build: what the SPA tests need of the web root, without building the frontend.
    public const string StubIndexHtml = "<!doctype html><html><body><app-root></app-root></body></html>";
    public const string StubHashedFile = "main-ABC123.js";

    private DirectoryInfo? webRoot;

    public FakeTimeProvider Time { get; } = new();

    /// <summary>The host's environment; set it before the first client is created.</summary>
    public string EnvironmentName { get; set; } = Environments.Development;

    /// <summary>The sender <see cref="WithCapturingSender"/> puts in place of the environment's.</summary>
    public CapturingCodeEmailSender Sender { get; } = new();

    private bool capturesCodeEmails;

    /// <summary>
    /// Replaces the code email sender with <see cref="Sender"/>. An opt-in, so the registration tests keep observing
    /// the environment's own sender; call it before the first client is created.
    /// </summary>
    public HostFactory WithCapturingSender()
    {
        capturesCodeEmails = true;
        return this;
    }

    /// <summary>Overrides one configuration key; call it before the first client is created.</summary>
    public HostFactory WithSetting(string key, string? value)
    {
        settings[key] = value;
        return this;
    }

    /// <summary>
    /// Serves a stub build from a temporary web root: <c>index.html</c>, <see cref="StubHashedFile"/> and a translation
    /// file, which the build copies from <c>public/i18n</c>.
    /// </summary>
    public HostFactory WithStubBuild()
    {
        webRoot = Directory.CreateTempSubdirectory("web-root-");
        File.WriteAllText(Path.Combine(webRoot.FullName, "index.html"), StubIndexHtml);
        File.WriteAllText(Path.Combine(webRoot.FullName, StubHashedFile), "console.log('stub');");
        Directory.CreateDirectory(Path.Combine(webRoot.FullName, "i18n"));
        File.WriteAllText(Path.Combine(webRoot.FullName, "i18n", "en.json"), """{ "language": { "name": "English" } }""");
        return this;
    }

    public async ValueTask InitializeAsync()
    {
        if (!settings.ContainsKey(ConnectionStringKey))
            settings[ConnectionStringKey] = await postgres.CreateDatabaseAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Without it the host would fall back to the developer database of appsettings.Development.json and
        // migrate it on start.
        if (settings.GetValueOrDefault(ConnectionStringKey) is null)
            throw new InvalidOperationException(
                $"No {ConnectionStringKey} for the host: call InitializeAsync() or WithSetting(\"{ConnectionStringKey}\", ...) before creating a client.");

        builder.UseEnvironment(EnvironmentName);
        if (webRoot is not null)
            builder.UseWebRoot(webRoot.FullName);
        foreach (var (key, value) in settings)
            builder.UseSetting(key, value);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            if (capturesCodeEmails)
            {
                services.RemoveAll<ICodeEmailSender>();
                services.AddSingleton<ICodeEmailSender>(Sender);
            }

            services.Insert(0, ServiceDescriptor.Transient<IStartupFilter, TestRemoteAddressStartupFilter>());
            // Program maps every Action<IEndpointRouteBuilder> after its own endpoints, so these run behind the
            // exception handler and the /api request rules; a startup filter would put them in front of both.
            services.AddSingleton<Action<IEndpointRouteBuilder>>(MapTestEndpoints);
        });
    }

    /// <summary>
    /// A host on a fresh database of <paramref name="postgres"/>, with <paramref name="configure"/> applied before it
    /// starts (the clock, the sender, settings, the environment).
    /// </summary>
    public static async Task<HostFactory> StartAsync(PostgresFixture postgres, Action<HostFactory>? configure = null)
    {
        var host = new HostFactory(postgres);
        configure?.Invoke(host);
        await host.InitializeAsync();
        return host;
    }

    /// <summary>A client that keeps no cookie, so a test sets and reads the session cookie itself.</summary>
    public HttpClient CreateCookielessClient() => CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    /// <summary>
    /// Requests a code for <paramref name="email"/> in <paramref name="locale"/> through <paramref name="client"/>,
    /// asserts that it was sent (<c>202</c>) and returns it, read from <see cref="Sender"/>. Needs
    /// <see cref="WithCapturingSender"/>.
    /// </summary>
    public async Task<string> RequestCodeAsync(HttpClient client, string email, string locale = "en")
    {
        ArgumentNullException.ThrowIfNull(client);
        if (!capturesCodeEmails)
            throw new InvalidOperationException("RequestCodeAsync reads the code from Sender: call WithCapturingSender() first.");

        var ct = TestContext.Current.CancellationToken;
        using var requested = await client.PostAsJsonAsync("/api/code-requests", new { email, locale }, ct);
        Assert.True(
            requested.StatusCode == HttpStatusCode.Accepted,
            $"The code request answered {(int)requested.StatusCode}: {await requested.Content.ReadAsStringAsync(ct)}");
        return Sender.LastCode(email.ToLowerInvariant())!;
    }

    /// <summary>
    /// Signs <paramref name="email"/> in through the API, as a browser does: requests a code (from 127.0.0.1), reads it
    /// from <see cref="Sender"/> and enters it, with <paramref name="timeZone"/> and, for both requests,
    /// <paramref name="locale"/>. Needs <see cref="WithCapturingSender"/>. Each call counts one code request against
    /// the email's limit of 5 and 127.0.0.1's limit of 10 in 24 hours. The client sends the session's cookie on every
    /// request and keeps no cookie of its own, so a cookie a response sets or clears does not change what it sends.
    /// </summary>
    public async Task<SignedInClient> SignInAsync(string email, bool rememberMe = false, string? timeZone = "UTC", string locale = "en")
    {
        var ct = TestContext.Current.CancellationToken;
        using var browser = CreateCookielessClient();
        var code = await RequestCodeAsync(browser, email, locale);

        using var signedIn = await browser.PostAsJsonAsync("/api/sessions", new { email, code, rememberMe, timeZone, locale }, ct);
        Assert.True(
            signedIn.StatusCode == HttpStatusCode.OK,
            $"The sign-in answered {(int)signedIn.StatusCode}: {await signedIn.Content.ReadAsStringAsync(ct)}");

        string[] setCookie = [.. signedIn.Headers.GetValues(HeaderNames.SetCookie)];
        var token = setCookie.Select(header => SetCookieHeaderValue.Parse(header))
            .Single(cookie => cookie.Name == "session").Value.ToString();
        return SignedInClient.WithCookie(this, token) with { SetCookie = setCookie };
    }

    // The test-only endpoints of the error format (Task 9), of the log test (Task 12) and of the timing test (Task 17).
    private void MapTestEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/test/echo", (EchoBody body) => body);
        endpoints.MapGet("/api/test/throw", Throw);
        endpoints.MapGet("/api/test/domain/{error}", (DomainError error) => ThrowDomain(error));
        // A request that takes 5000.1 ms on the host's clock, the first beyond the last bucket bound (Task 17).
        endpoints.MapGet("/api/test/slow", () =>
        {
            Time.Advance(TimeSpan.FromMilliseconds(5000.1));
            return Results.NoContent();
        });
    }

    private static IResult Throw() => throw new InvalidOperationException("secret detail");

    private static IResult ThrowDomain(DomainError error) => throw new DomainException(error, "Raised by the test endpoint.");

    /// <summary>The body of <c>POST /api/test/echo</c>: two required fields.</summary>
    public sealed record EchoBody
    {
        public required string Text { get; init; }

        public required int Number { get; init; }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        // Each host has a database of its own, hence a pool of its own; idle connections would stay open for minutes
        // and, with the host count of this assembly, reach the server's max_connections.
        NpgsqlConnection.ClearAllPools();
        webRoot?.Delete(recursive: true);
    }

    private sealed class TestRemoteAddressStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                var header = context.Request.Headers[RemoteAddressHeader];
                context.Connection.RemoteIpAddress = IPAddress.Parse(header.Count > 0 ? header[0]! : "127.0.0.1");
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
