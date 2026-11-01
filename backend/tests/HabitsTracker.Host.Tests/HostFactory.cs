using System.Net;
using HabitsTracker.Tests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace HabitsTracker.Host.Tests;

/// <summary>
/// The host in process (research R12): a fresh database, migrated on start, a <see cref="FakeTimeProvider"/> in place of
/// <see cref="TimeProvider"/>, environment Development unless set, the translation files of the frontend,
/// and the client's address taken from the test header <c>Test-Remote-Address</c> (default 127.0.0.1).
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
            services.Insert(0, ServiceDescriptor.Transient<IStartupFilter, TestRemoteAddressStartupFilter>());
        });
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
