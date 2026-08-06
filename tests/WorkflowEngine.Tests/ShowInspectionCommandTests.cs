using WorkflowEngine.Application.Commands;
using WorkflowEngine.Application.Inspection;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;
using WorkflowEngine.Infrastructure.WorkflowInspection;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowContext")]
public sealed class ShowInspectionCommandTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-07T12:00:00Z");

    [Fact]
    public void ShowReturnsWorkflowSummary()
    {
        using var dbContext = CreateReturnToMakerContext();
        var handler = new ShowCommandHandler(new WorkflowInspectionReader(dbContext));

        var result = handler.Execute(["WF-PT-SHOW"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Workflow Instance: WF-PT-SHOW", result.Message, StringComparison.Ordinal);
        Assert.Contains("Current State: ReturnedToMaker", result.Message, StringComparison.Ordinal);
        Assert.Contains("Current Task: MakerReworkTask (Maker)", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShowTasksReturnsClosedAndPendingTasks()
    {
        using var dbContext = CreateReturnToMakerContext();
        var handler = new ShowTasksCommandHandler(new WorkflowInspectionReader(dbContext));

        var result = handler.Execute(["WF-PT-SHOW"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("CheckerReviewTask", result.Message, StringComparison.Ordinal);
        Assert.Contains("status=Completed", result.Message, StringComparison.Ordinal);
        Assert.Contains("closedBy=CheckerReturnToMaker", result.Message, StringComparison.Ordinal);
        Assert.Contains("MakerReworkTask", result.Message, StringComparison.Ordinal);
        Assert.Contains("status=Pending", result.Message, StringComparison.Ordinal);
        Assert.Contains("due=None", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShowNotificationsReturnsOutboxRows()
    {
        using var dbContext = CreateReturnToMakerContext();
        var handler = new ShowNotificationsCommandHandler(new WorkflowInspectionReader(dbContext));

        var result = handler.Execute(["WF-PT-SHOW"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("WorkflowReturnedToMaker", result.Message, StringComparison.Ordinal);
        Assert.Contains("to=[maker.one@example.test, maker.two@example.test]", result.Message, StringComparison.Ordinal);
        Assert.Contains("cc=[manager@example.test, operations@example.test]", result.Message, StringComparison.Ordinal);
        Assert.Contains("status=Pending", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("recipientSnapshot=invalid", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("""{"toEmailAddresses":null,"ccEmailAddresses":[]}""")]
    [InlineData("""{"toEmailAddresses":[],"ccEmailAddresses":null}""")]
    public void ShowNotificationsMarksInvalidRecipientSnapshots(string notificationRecipientsJson)
    {
        using var dbContext = CreateReturnToMakerContext();
        var row = Assert.Single(dbContext.NotificationOutbox);
        row.NotificationRecipientsJson = notificationRecipientsJson;
        dbContext.SaveChanges();
        var reader = new WorkflowInspectionReader(dbContext);
        var handler = new ShowNotificationsCommandHandler(reader);

        var snapshot = reader.Read("WF-PT-SHOW");
        var result = handler.Execute(["WF-PT-SHOW"]);

        var notification = Assert.Single(snapshot.Notifications);
        Assert.True(notification.HasInvalidRecipientSnapshot);
        Assert.Empty(notification.ToEmailAddresses);
        Assert.Empty(notification.CcEmailAddresses);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("recipientSnapshot=invalid", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("toEmailAddresses", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShowNotificationsDistinguishesValidEmptyRecipientSnapshot()
    {
        using var dbContext = CreateReturnToMakerContext();
        var row = Assert.Single(dbContext.NotificationOutbox);
        row.NotificationRecipientsJson = """{"toEmailAddresses":[],"ccEmailAddresses":[]}""";
        dbContext.SaveChanges();
        var reader = new WorkflowInspectionReader(dbContext);
        var handler = new ShowNotificationsCommandHandler(reader);

        var snapshot = reader.Read("WF-PT-SHOW");
        var result = handler.Execute(["WF-PT-SHOW"]);

        var notification = Assert.Single(snapshot.Notifications);
        Assert.False(notification.HasInvalidRecipientSnapshot);
        Assert.Empty(notification.ToEmailAddresses);
        Assert.Empty(notification.CcEmailAddresses);
        Assert.Contains("to=[] | cc=[]", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("recipientSnapshot=invalid", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShowNotificationsDisplaysTypedInvalidRecipientSnapshotMarker()
    {
        var snapshot = new WorkflowInspectionSnapshot(
            new WorkflowInstanceInspection(
                "WF-INVALID-RECIPIENTS",
                "PortfolioTrade",
                1,
                "PortfolioTrade",
                "PT-INVALID-RECIPIENTS",
                "Draft",
                "Active",
                Now,
                "maker1",
                null,
                1),
            [],
            [],
            [new NotificationInspection(
                1,
                null,
                "WorkflowTaskAssigned",
                [],
                [],
                true,
                "Invalid recipients",
                "Pending",
                "corr-invalid-recipients",
                Now,
                null)]);
        var handler = new ShowNotificationsCommandHandler(new StubInspectionReader(snapshot));

        var result = handler.Execute(["WF-INVALID-RECIPIENTS"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("to=[] | cc=[] | recipientSnapshot=invalid", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShowHistoryReturnsAuditRows()
    {
        using var dbContext = CreateReturnToMakerContext();
        var handler = new ShowHistoryCommandHandler(new WorkflowInspectionReader(dbContext));

        var result = handler.Execute(["WF-PT-SHOW"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("CheckerReturnToMaker", result.Message, StringComparison.Ordinal);
        Assert.Contains("CheckerReview -> ReturnedToMaker", result.Message, StringComparison.Ordinal);
        Assert.Contains("by=checker1/Checker", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("show")]
    [InlineData("show-tasks")]
    [InlineData("show-history")]
    [InlineData("show-notifications")]
    public void MissingWorkflowReturnsNonZero(string command)
    {
        using var dbContext = WorkflowContextReaderTests.CreateSeededContext();
        var reader = new WorkflowInspectionReader(dbContext);
        var result = command switch
        {
            "show" => new ShowCommandHandler(reader).Execute(["WF-PT-MISSING"]),
            "show-tasks" => new ShowTasksCommandHandler(reader).Execute(["WF-PT-MISSING"]),
            "show-history" => new ShowHistoryCommandHandler(reader).Execute(["WF-PT-MISSING"]),
            "show-notifications" => new ShowNotificationsCommandHandler(reader).Execute(["WF-PT-MISSING"]),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, null)
        };

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("was not found", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CommandRegistryDispatchesInspectionHandlers()
    {
        using var dbContext = CreateReturnToMakerContext();
        var reader = new WorkflowInspectionReader(dbContext);
        var registry = new CommandRegistry(
            showCommandHandler: new ShowCommandHandler(reader),
            showTasksCommandHandler: new ShowTasksCommandHandler(reader),
            showHistoryCommandHandler: new ShowHistoryCommandHandler(reader),
            showNotificationsCommandHandler: new ShowNotificationsCommandHandler(reader));

        Assert.Contains("ReturnedToMaker", registry.Dispatch(["show", "WF-PT-SHOW"]).Message, StringComparison.Ordinal);
        Assert.Contains("CheckerReviewTask", registry.Dispatch(["show-tasks", "WF-PT-SHOW"]).Message, StringComparison.Ordinal);
        Assert.Contains("CheckerReturnToMaker", registry.Dispatch(["show-history", "WF-PT-SHOW"]).Message, StringComparison.Ordinal);
        Assert.Contains("WorkflowReturnedToMaker", registry.Dispatch(["show-notifications", "WF-PT-SHOW"]).Message, StringComparison.Ordinal);
    }

    private static WorkflowDbContext CreateReturnToMakerContext()
    {
        var dbContext = WorkflowContextReaderTests.CreateSeededContext();
        WorkflowContextReaderTests.AddInstance(dbContext, "WF-PT-SHOW", "PT-SHOW", "ReturnedToMaker", "Active", 3);
        dbContext.WorkflowTasks.Add(new WorkflowTask
        {
            WorkflowInstanceId = "WF-PT-SHOW",
            TaskKey = "CheckerReviewTask",
            AssignedRoleKey = "Checker",
            Status = "Completed",
            CreatedAtUtc = Now.AddMinutes(-20),
            DueAtUtc = Now.AddMinutes(40),
            CompletedAtUtc = Now.AddMinutes(-10),
            CompletedByUserId = "checker1",
            ClosedByActionKey = "CheckerReturnToMaker",
            NotificationRecipientsJson = """{"toEmailAddresses":["maker.one@example.test","maker.two@example.test"],"ccEmailAddresses":["manager@example.test","operations@example.test"]}"""
        });
        dbContext.WorkflowTasks.Add(new WorkflowTask
        {
            WorkflowInstanceId = "WF-PT-SHOW",
            TaskKey = "MakerReworkTask",
            AssignedRoleKey = "Maker",
            Status = "Pending",
            CreatedAtUtc = Now.AddMinutes(-10),
            NotificationRecipientsJson = """{"toEmailAddresses":["maker.one@example.test","maker.two@example.test"],"ccEmailAddresses":["manager@example.test","operations@example.test"]}"""
        });
        dbContext.WorkflowAudits.Add(new WorkflowAudit
        {
            WorkflowInstanceId = "WF-PT-SHOW",
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-SHOW",
            ActionKey = "CheckerReturnToMaker",
            FromStateKey = "CheckerReview",
            ToStateKey = "ReturnedToMaker",
            PerformedByUserId = "checker1",
            PerformedByRoleKey = "Checker",
            Reason = "Needs correction",
            CorrelationId = "corr-show",
            CreatedAtUtc = Now.AddMinutes(-10)
        });
        dbContext.NotificationOutbox.Add(new NotificationOutbox
        {
            WorkflowInstanceId = "WF-PT-SHOW",
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-SHOW",
            NotificationType = "WorkflowReturnedToMaker",
            NotificationRecipientsJson = """{"toEmailAddresses":["maker.one@example.test","maker.two@example.test"],"ccEmailAddresses":["manager@example.test","operations@example.test"]}""",
            Subject = "Returned to Maker",
            PayloadJson = "{}",
            Status = "Pending",
            CorrelationId = "corr-show",
            CreatedAtUtc = Now.AddMinutes(-10)
        });
        dbContext.SaveChanges();

        return dbContext;
    }

    private sealed class StubInspectionReader(WorkflowInspectionSnapshot snapshot) : IWorkflowInspectionReader
    {
        public WorkflowInspectionSnapshot Read(string workflowInstanceId)
        {
            return snapshot;
        }
    }
}
