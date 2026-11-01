using HabitsTracker.Host.Health;
using HabitsTracker.Host.Operations;
using HabitsTracker.Identity.Adapters;
using HabitsTracker.Identity.Adapters.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OperationsDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("Habits"),
    npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, OperationsDbContext.Schema)));

builder.Services.AddIdentityContext(builder.Configuration, builder.Environment);

builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();

// Development and tests only; in Production the deployment migrates after the backup (research R2).
if (app.Configuration.GetValue<bool>("Database:MigrateOnStart"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
}

app.MapHealthChecks("/health");

app.Run();

public partial class Program;
