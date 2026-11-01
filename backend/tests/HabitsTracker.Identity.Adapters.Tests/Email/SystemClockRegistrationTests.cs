using HabitsTracker.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace HabitsTracker.Identity.Adapters.Tests.CodeEmail;

// The registration brings its own clock: a host that registers none still resolves the code email sender, which reads the time.
public class SystemClockRegistrationTests
{
    [Fact]
    public void Production_registration_resolves_the_sender_with_the_system_clock()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Translations:Path"] = Repository.PathTo("frontend/public/i18n"),
            ["ConnectionStrings:Habits"] = "Host=127.0.0.1;Database=unused",
            ["Resend:ApiKey"] = "re_test_key",
        }).Build();
        var services = new ServiceCollection();

        services.AddIdentityContext(configuration, new ProductionEnvironment());

        using var provider = services.BuildServiceProvider();
        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
        Assert.NotNull(provider.GetRequiredService<HabitsTracker.Identity.Core.ICodeEmailSender>());
    }

    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
