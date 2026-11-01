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

    public string EnvironmentName { get; init; } = Environments.Development;

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
    /// Signs <paramref name="email"/> in through the API, as a browser does: requests a code (locale <c>en</c>, from
    /// 127.0.0.1), reads it from <see cref="Sender"/> and enters it with time zone <c>UTC</c> and locale <c>en</c>.
    /// Needs <see cref="WithCapturingSender"/>. Each call counts one code request against the email's limit of 5 and
    /// 127.0.0.1's limit of 10 in 24 hours. The client sends the session's cookie on every request and keeps no
    /// cookie of its own, so a cookie a response sets or clears does not change what it sends.
    /// </summary>
    public async Task<SignedInClient> SignInAsync(string email, bool rememberMe = false)
    {
        if (!capturesCodeEmails)
            throw new InvalidOperationException("SignInAsync reads the code from Sender: call WithCapturingSender() first.");

        var ct = TestContext.Current.CancellationToken;
        using var browser = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using (var requested = await browser.PostAsJsonAsync("/api/code-requests", new { email, locale = "en" }, ct))
        {
            if (requested.StatusCode != HttpStatusCode.Accepted)
                throw new InvalidOperationException($"The code request answered {(int)requested.StatusCode}: {await requested.Content.ReadAsStringAsync(ct)}");
        }

        var code = Sender.LastCode(email.ToLowerInvariant());
        using var signedIn = await browser.PostAsJsonAsync(
            "/api/sessions", new { email, code, rememberMe, timeZone = "UTC", locale = "en" }, ct);
        if (signedIn.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"The sign-in answered {(int)signedIn.StatusCode}: {await signedIn.Content.ReadAsStringAsync(ct)}");

        var token = signedIn.Headers.GetValues("Set-Cookie")
            .Select(header => Microsoft.Net.Http.Headers.SetCookieHeaderValue.Parse(header))
            .Single(cookie => cookie.Name == "session").Value.ToString();
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", $"session={token}");
        return new SignedInClient(client, token);
    }

    /// <summary>A client of a signed-in browser: <see cref="Client"/> sends the cookie <c>session=</c><see cref="Token"/>.</summary>
    public sealed record SignedInClient(HttpClient Client, string Token) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    // The test-only endpoints of the error format (Task 9) and of the log test (Task 12).
    private static void MapTestEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/test/echo", (EchoBody body) => body);
        endpoints.MapGet("/api/test/throw", Throw);
        endpoints.MapGet("/api/test/domain/{error}", (DomainError error) => ThrowDomain(error));
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
