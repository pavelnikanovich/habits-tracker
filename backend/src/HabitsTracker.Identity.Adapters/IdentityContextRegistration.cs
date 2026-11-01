using HabitsTracker.Identity.Adapters.CodeEmail;
using HabitsTracker.Identity.Adapters.Storage;
using HabitsTracker.Identity.Adapters.Translations;
using HabitsTracker.Identity.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        // Read here, while the host starts, so a translation file that is not named after a locale code stops the
        // start (research R11). A relative path is taken from the content root.
        var translationsPath = configuration["Translations:Path"]
            ?? throw new InvalidOperationException("Translations:Path is not configured: the folder of the translation files.");
        var translationFiles = new TranslationFiles(Path.Combine(environment.ContentRootPath, translationsPath));
        services.AddSingleton(translationFiles);
        services.AddSingleton<ILocales>(translationFiles);
        services.AddSingleton<Func<IReadOnlyList<LocaleResponse>>>(translationFiles.List);

        // The code email sender reads the time; a host that registers its own clock (tests) keeps it.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(new CodeEmailText(translationFiles.Text));
        AddCodeEmailSender(services, configuration, environment);

        return services;
    }

    // Development writes the code email to the log; any other environment sends it through Resend and does not
    // start without the key.
    private static void AddCodeEmailSender(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            services.AddSingleton<ICodeEmailSender, DevelopmentLogCodeEmailSender>();
            return;
        }

        var apiKey = configuration["Resend:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Resend:ApiKey is not configured: the API key of the email service that sends the code email.");

        services.AddHttpClient<ICodeEmailSender, ResendCodeEmailSender>(client => ResendCodeEmailSender.ConfigureClient(client, apiKey));
    }
}
