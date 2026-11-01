using HabitsTracker.Identity.Adapters.CodeEmail;
using HabitsTracker.Identity.Core;
using HabitsTracker.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HabitsTracker.Host.Tests;

// The environment chooses the code email sender: Development writes to the log, anything else sends through Resend.
public class CodeEmailSenderRegistrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Development_uses_only_the_log()
    {
        await using var host = new HostFactory(postgres);
        await host.InitializeAsync();
        using var client = host.CreateClient();

        await using var scope = host.Services.CreateAsyncScope();
        var sender = Assert.Single(scope.ServiceProvider.GetServices<ICodeEmailSender>());
        Assert.IsType<DevelopmentLogCodeEmailSender>(sender);
    }

    [Fact]
    public async Task Production_uses_only_Resend()
    {
        await using var host = Production().WithSetting("Resend:ApiKey", "re_test_key");
        await host.InitializeAsync();
        using var client = host.CreateClient();

        await using var scope = host.Services.CreateAsyncScope();
        var sender = Assert.Single(scope.ServiceProvider.GetServices<ICodeEmailSender>());
        Assert.IsType<ResendCodeEmailSender>(sender);
    }

    [Fact]
    public async Task Resend_client_has_the_timeout_and_the_key()
    {
        await using var host = Production().WithSetting("Resend:ApiKey", "re_test_key");
        await host.InitializeAsync();
        using var client = host.CreateClient();

        // A typed client is named after its service type.
        using var resend = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(ICodeEmailSender));

        Assert.Equal(TimeSpan.FromSeconds(10), resend.Timeout);
        Assert.Equal("Bearer", resend.DefaultRequestHeaders.Authorization?.Scheme);
        Assert.Equal("re_test_key", resend.DefaultRequestHeaders.Authorization?.Parameter);
    }

    [Fact]
    public async Task Production_without_a_Resend_key_does_not_start()
    {
        await using var host = Production();
        await host.InitializeAsync();

        var exception = Assert.ThrowsAny<Exception>(() => host.CreateClient());

        Assert.Contains("Resend:ApiKey", exception.ToString(), StringComparison.Ordinal);
    }

    // Production lacks the settings Development provides: the key of the codes; the translations, the database and
    // the migration come from HostFactory.
    private HostFactory Production() => new HostFactory(postgres) { EnvironmentName = Environments.Production }
        .WithSetting("Identity:CodeHashKey", Convert.ToBase64String(new byte[32]));
}
