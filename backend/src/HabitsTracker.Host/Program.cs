using HabitsTracker.Host.Health;
using HabitsTracker.Host.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OperationsDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("Habits"),
    npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, OperationsDbContext.Schema)));

builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();

public partial class Program;
