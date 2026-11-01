using System.Text.Encodings.Web;
using System.Text.Unicode;
using HabitsTracker.Host.Health;
using HabitsTracker.Host.Operations;
using HabitsTracker.Host.Web;
using HabitsTracker.Identity.Adapters;
using HabitsTracker.Identity.Adapters.Http;
using HabitsTracker.Identity.Adapters.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OperationsDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("Habits"),
    npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, OperationsDbContext.Schema)));

builder.Services.AddIdentityContext(builder.Configuration, builder.Environment);

builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

// JSON keeps letters of every script as they are ("Русский", not "\u0420\u0443..."); HTML-sensitive characters stay escaped.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All));

var app = builder.Build();

// Development and tests only; in Production the deployment migrates after the backup (research R2).
if (app.Configuration.GetValue<bool>("Database:MigrateOnStart"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
}

app.UseSecurityHeaders();
app.UseCachingHeaders();

// The Angular build in the web root, on the same origin as the API (FR-016, FR-027).
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthChecks("/health");
app.MapIdentityEndpoints();

// Any other path outside /api and /health is a page of the application, which shows its own "not found".
app.MapFallbackToFile("{*path:nonfile:regex(^(?!api(/|$)|health(/|$)))}", "index.html");

app.Run();

public partial class Program;
