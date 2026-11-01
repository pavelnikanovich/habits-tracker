using HabitsTracker.Identity.Adapters.CodeEmail;
using HabitsTracker.Identity.Adapters.Http;
using HabitsTracker.Identity.Adapters.Storage;
using HabitsTracker.Identity.Adapters.Translations;
using System.Security.Cryptography;
using HabitsTracker.Identity.Core;
using HabitsTracker.Identity.Core.UseCases;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
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

        services.AddSingleton(CreateOptions(configuration, environment));
        services.AddScoped<RequestCode>();
        services.AddScoped<SignIn>();
        services.AddScoped<AuthenticateSession>();
        services.AddScoped<GetCurrentUser>();
        services.AddScoped<SignOut>();
        services.AddScoped<SignOutEverywhere>();
        services.AddScoped<ChangeLocale>();
        AddSessionAuthentication(services);

        // The code email sender reads the time; a host that registers its own clock (tests) keeps it.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(new CodeEmailText(translationFiles.Text));
        AddCodeEmailSender(services, configuration, environment);

        return services;
    }

    // The code hash key is a secret of the deployment (constitution VIII): outside Development a missing key stops the
    // start; Development makes a new one at each start, so a code of an earlier run does not verify (research R5).
    private static IdentityContextOptions CreateOptions(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["Identity:CodeHashKey"];
        byte[] key;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            try
            {
                key = Convert.FromBase64String(configured);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException("Identity:CodeHashKey is not base64: the 32-byte key of the code hash.", exception);
            }
        }
        else if (environment.IsDevelopment())
        {
            key = RandomNumberGenerator.GetBytes(32);
        }
        else
        {
            throw new InvalidOperationException("Identity:CodeHashKey is not configured: the base64 of the 32-byte key of the code hash.");
        }

        var section = configuration.GetSection("Identity:CodeRequestLimits");
        var defaults = CodeRequestLimits.Default;
        var limits = CodeRequestLimits.Create(
            section.GetValue<int?>("PerEmail") ?? defaults.PerEmail,
            section.GetValue<int?>("PerIpAddress") ?? defaults.PerIpAddress,
            section.GetValue<TimeSpan?>("Window") ?? defaults.Window);

        return IdentityContextOptions.Create(key, limits);
    }

    // The session scheme runs only where an endpoint asks for it with RequireAuthorization(): the default policy names
    // it, and it is not the default scheme, so the authentication middleware reads no session for a public request.
    private static void AddSessionAuthentication(IServiceCollection services)
    {
        // ASP.NET Core makes the only registered scheme the default, and the authentication middleware would then
        // read the session, and extend it, on every request. The switch is ASP.NET Core's own way to turn that off; it
        // is process-wide, which is what it should be in a process that hosts this one application. Revisit when a
        // second scheme is registered: the switch is then no longer needed.
        AppContext.SetSwitch("Microsoft.AspNetCore.Authentication.SuppressAutoDefaultScheme", isEnabled: true);
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, configureOptions: null);
        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder(SessionAuthenticationHandler.SchemeName).RequireAuthenticatedUser().Build());
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
