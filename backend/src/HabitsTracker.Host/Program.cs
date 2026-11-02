using System.Text.Encodings.Web;
using System.Text.Unicode;
using HabitsTracker.Host.Cleanup;
using HabitsTracker.Host.Health;
using HabitsTracker.Host.Metrics;
using HabitsTracker.Host.Operations;
using HabitsTracker.Host.Web;
using HabitsTracker.Identity.Adapters;
using HabitsTracker.Identity.Adapters.Http;
using HabitsTracker.Identity.Adapters.Storage;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OperationsDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("Habits"),
    npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, OperationsDbContext.Schema)));

builder.Services.AddSingleton<RequestTimingHistogram>();
builder.Services.AddHostedService<RequestTimingFlusher>();
builder.Services.AddHostedService<HourlyCleanup>();

builder.Services.AddIdentityContext(builder.Configuration, builder.Environment);

builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

// JSON keeps letters of every script as they are ("Русский", not "\u0420\u0443..."); HTML-sensitive characters stay escaped.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All));

// The error format of the API (contracts/http-api.md): every exception becomes a problem through DomainExceptionHandler.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = Problems.AddTraceId);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
// A body that does not bind answers 400 in every environment, as in Production, which ApiRequestRules turns into
// invalid-request; Development would otherwise throw, and the tests would see a pipeline Production does not have.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

// The client's address comes from X-Forwarded-For only when the request arrives from the proxy's network (research R7).
// The defaults trust loopback; they are cleared, so nothing is trusted unless ForwardedHeaders:KnownNetwork says so.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();
    if (builder.Configuration["ForwardedHeaders:KnownNetwork"] is { Length: > 0 } network)
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
});

var app = builder.Build();

// Development and tests only; in Production the deployment migrates after the backup (research R2).
if (app.Configuration.GetValue<bool>("Database:MigrateOnStart"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<OperationsDbContext>().Database.MigrateAsync();
}

// First, so the timing covers every other middleware (FR-039).
app.UseRequestTiming();

// Without a known network the middleware would trust every sender, so it is left out.
if (!string.IsNullOrEmpty(app.Configuration["ForwardedHeaders:KnownNetwork"]))
    app.UseForwardedHeaders();

app.UseSecurityHeaders();
app.UseCachingHeaders();
app.UseExceptionHandler();
app.UseApiRequestRules();

// The Angular build in the web root, on the same origin as the API (FR-016, FR-027).
app.UseDefaultFiles();
app.UseStaticFiles();

// Placed here, behind the exception handler and the /api rules, rather than where WebApplication would add them, in
// front of both. There is no default scheme, so authentication reads nothing by itself; authorization runs the session
// scheme for an endpoint marked signed in.
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapIdentityEndpoints();

// The one hook for endpoints from outside the application: the tests map their test-only endpoints here, inside this
// pipeline (HostFactory). Nothing registers one in the application, so in Production it maps nothing.
foreach (var mapEndpoints in app.Services.GetServices<Action<IEndpointRouteBuilder>>())
    mapEndpoints(app);

// Any other path outside /api and /health is a page of the application, which shows its own "not found".
app.MapFallbackToFile("{*path:nonfile:regex(^(?!api(/|$)|health(/|$)))}", "index.html")
    .WithMetadata(new ApplicationFallbackMetadata());

app.Run();

public partial class Program;
