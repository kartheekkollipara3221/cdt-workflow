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
    public void UnknownSenderFailsFastAtStartup()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => BuildProvider("Unknown", smtpHost: "localhost", smtpFrom: "workflow@example.test"));

        Assert.Contains("Allowed values: Console, NoOp, Smtp", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SmtpSenderRequiresHostAtStartup()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => BuildProvider("Smtp", smtpHost: null, smtpFrom: "workflow@example.test"));

        Assert.Contains("Smtp:Host", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SmtpSenderRequiresFromAtStartup()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => BuildProvider("Smtp", smtpHost: "localhost", smtpFrom: null));

        Assert.Contains("Smtp:From", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Console")]
    [InlineData("NoOp")]
    public void NonSmtpSendersDoNotRequireSmtpSettings(string senderValue)
    {
        using var provider = BuildProvider(senderValue, smtpHost: null, smtpFrom: null);

        var sender = provider.GetRequiredService<INotificationSender>();

        Assert.NotNull(sender);
    }

    private static ServiceProvider BuildProvider(string? senderValue, string? smtpHost, string? smtpFrom)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = TestSqlConnectionFactory.ConnectionString("burrito_sender_selection_tests")
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
