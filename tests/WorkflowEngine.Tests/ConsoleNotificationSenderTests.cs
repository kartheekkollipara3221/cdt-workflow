using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Infrastructure.Notifications;

namespace WorkflowEngine.Tests;

public sealed class ConsoleNotificationSenderTests
{
    [Fact]
    public async Task PrintsReadableSummaryAndDoesNotThrowForWellFormedDelivery()
    {
        using var writer = new StringWriter();
        var sender = new ConsoleNotificationSender(writer);
        var delivery = new NotificationDelivery(
            10,
            "WF-PT-1001",
            20,
            "PortfolioTrade",
            "PT-1001",
            "WorkflowTaskAssigned",
            "Checker",
            null,
            "Task assigned",
            "Rendered body",
            "<p>Rendered body</p>",
            """{"referenceNumber":"1050"}""",
            "Pending",
            "corr-1",
            DateTimeOffset.Parse("2030-01-15T16:01:00Z"));

        await sender.SendAsync(delivery, CancellationToken.None);

        var output = writer.ToString();
        Assert.Contains("[Sent] WorkflowTaskAssigned", output, StringComparison.Ordinal);
        Assert.Contains("Checker", output, StringComparison.Ordinal);
        Assert.Contains("Task assigned", output, StringComparison.Ordinal);
        Assert.Contains("WF-PT-1001", output, StringComparison.Ordinal);
    }
}
