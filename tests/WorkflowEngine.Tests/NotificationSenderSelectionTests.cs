using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Infrastructure.DependencyInjection;
using WorkflowEngine.Infrastructure.Notifications;

namespace WorkflowEngine.Tests;

public sealed class NotificationSenderSelectionTests
{
    [Theory]
    [InlineData(null, typeof(ConsoleNotificationSender))]
    [InlineData("Console", typeof(ConsoleNotificationSender))]
    [InlineData("console", typeof(ConsoleNotificationSender))]
    [InlineData("  NoOp  ", typeof(NoOpNotificationSender))]
    [InlineData("Smtp", typeof(SmtpNotificationSender))]
    public void ConfiguredSenderResolvesExpectedImplementation(string? senderValue, Type expectedType)
    {
        using var provider = BuildProvider(senderValue, smtpHost: "localhost", smtpFrom: "workflow@example.test");

        var sender = provider.GetRequiredService<INotificationSender>();

        Assert.IsType(expectedType, sender);
    }

    [Fact]
    public void UnknownSenderFailsFastAtResolution()
    {
        using var provider = BuildProvider("Unknown", smtpHost: "localhost", smtpFrom: "workflow@example.test");

        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<INotificationSender>());

        Assert.Contains("Allowed values: Console, NoOp, Smtp", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SmtpSenderRequiresHostAtResolution()
    {
        using var provider = BuildProvider("Smtp", smtpHost: null, smtpFrom: "workflow@example.test");

        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<INotificationSender>());

        Assert.Contains("Smtp:Host", exception.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildProvider(string? senderValue, string? smtpHost, string? smtpFrom)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=DOLLY;Database=burrito_sender_selection_tests;Integrated Security=true;TrustServerCertificate=True;Encrypt=False"
        };
        if (senderValue is not null)
        {
            values["Notifications:Sender"] = senderValue;
        }

        if (smtpHost is not null)
        {
            values["Smtp:Host"] = smtpHost;
        }

        if (smtpFrom is not null)
        {
            values["Smtp:From"] = smtpFrom;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddWorkflowInfrastructure(configuration)
            .BuildServiceProvider();
    }
}
