using HabitsTracker.Identity.Adapters.Storage;
using HabitsTracker.Identity.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HabitsTracker.Identity.Adapters;

/// <summary>Every registration of the Identity context, and the only place that chooses an adapter by environment.</summary>
public static class IdentityContextRegistration
{
    public static IServiceCollection AddIdentityContext(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        // One context per request, shared by the stores and the transaction.
        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(
            configuration.GetConnectionString("Habits"),
            npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, IdentityDbContext.Schema)));
        services.AddScoped<IUsers, EfUsers>();
        services.AddScoped<IOneTimeCodes, EfOneTimeCodes>();
        services.AddScoped<ICodeRequests, EfCodeRequests>();
        services.AddScoped<ISessions, EfSessions>();
        services.AddScoped<IIdentityTransaction, EfIdentityTransaction>();

        return services;
    }
}
