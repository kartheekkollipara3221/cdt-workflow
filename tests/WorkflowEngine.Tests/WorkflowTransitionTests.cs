using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.WorkflowExecution;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;
using WorkflowEngine.Infrastructure.WorkflowContext;
using WorkflowEngine.Infrastructure.WorkflowExecution;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class WorkflowTransitionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2030-01-15T15:30:00Z");

    [Fact]
    public void SaveDraftWritesDataAndAuditWithoutMovingWorkflow()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Draft", "Active", 1);
        dbContext.SaveChanges();

        var result = CreateService(dbContext).Execute(Request(Envelope(actionKey: "SaveDraft", saveAsDraft: true)));

        Assert.True(result.Result.Success);
        var instance = dbContext.WorkflowInstances.Single();
        Assert.Equal("Draft", instance.CurrentStateKey);
        Assert.Equal(1, instance.Version);
        Assert.Empty(dbContext.WorkflowTasks);
        Assert.Empty(dbContext.NotificationOutbox);
        Assert.Single(dbContext.MockSectionData);
        var audit = Assert.Single(dbContext.WorkflowAudits);
        Assert.Equal("SaveDraft", audit.ActionKey);
        Assert.Equal(audit.FromStateKey, audit.ToStateKey);
        Assert.Single(dbContext.IdempotencyRecords);
        Assert.NotNull(result.WorkflowContext);
    }

    [Fact]
    public void SubmitForCheckerMovesDraftAndCreatesCheckerTaskNotificationAuditAndIdempotency()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Draft", "Active", 1);
        dbContext.SaveChanges();

        var result = CreateService(dbContext).Execute(Request(Envelope()));

        Assert.True(result.Result.Success);
        Assert.Equal("Draft", result.Result.OldState);
        Assert.Equal("CheckerReview", result.Result.NewState);
        var instance = dbContext.WorkflowInstances.Single();
        Assert.Equal("CheckerReview", instance.CurrentStateKey);
        Assert.Equal(2, instance.Version);
        var task = Assert.Single(dbContext.WorkflowTasks);
        Assert.Equal("CheckerReviewTask", task.TaskKey);
        Assert.Equal("Pending", task.Status);
        Assert.NotNull(task.DueAtUtc);
        var notification = Assert.Single(dbContext.NotificationOutbox);
        Assert.Equal("Directed Trade Pending Checker Approval", notification.Subject);
        Assert.Contains("Kollipara, Kartheek", notification.Body, StringComparison.Ordinal);
        Assert.Contains("Taxable desk", notification.Body, StringComparison.Ordinal);
        Assert.Contains("Sharepoint link = https://sharepoint.example.test/trades/1050", notification.Body, StringComparison.Ordinal);
        Assert.Contains("\"submitterDisplayName\":\"Kollipara, Kartheek\"", notification.PayloadJson, StringComparison.Ordinal);
        var createdTask = Assert.Single(dbContext.WorkflowTasks);
        Assert.Equal("RouteCheckerAssignedOnSubmit", createdTask.NotificationRouteKey);
        Assert.Contains("\"deskName\":\"Taxable\"", createdTask.NotificationContextJson, StringComparison.Ordinal);
        Assert.Equal("WorkflowTaskAssigned", notification.NotificationType);
        Assert.Equal("Checker", notification.RecipientRoleKey);
        Assert.Single(dbContext.WorkflowAudits);
        Assert.Single(dbContext.MockSectionData);
        Assert.Single(dbContext.IdempotencyRecords);
        Assert.NotNull(result.WorkflowContext);
    }

    [Fact]
    public void SubmitForCheckerWithMissingNotificationTokenReturnsValidationFailedAndRollsBackSideEffects()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Draft", "Active", 1);
        dbContext.SaveChanges();
        var envelope = Envelope();
        envelope.Workflow!.NotificationContext!.Remove("tradeLink");

        var result = CreateService(dbContext).Execute(Request(envelope));

        Assert.False(result.Result.Success);
        Assert.Equal("ValidationFailed", result.Result.ResultCode);
        Assert.Contains("workflow.notificationContext.tradeLink", result.Result.Diagnostics.ValidationErrors!.Select(error => error.Field));
        var instance = dbContext.WorkflowInstances.Single();
        Assert.Equal("Draft", instance.CurrentStateKey);
        Assert.Equal(1, instance.Version);
        Assert.Empty(dbContext.WorkflowTasks);
        Assert.Empty(dbContext.NotificationOutbox);
        Assert.Empty(dbContext.WorkflowAudits);
        Assert.Empty(dbContext.MockSectionData);
        Assert.Empty(dbContext.IdempotencyRecords);
    }

    [Fact]
    public void CheckerReturnToMakerClosesCheckerTaskAndCreatesMakerReworkTask()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "CheckerReview", "Active", 2);
        dbContext.WorkflowTasks.Add(new WorkflowTask
        {
            WorkflowInstanceId = "WF-PT-1001",
            TaskKey = "CheckerReviewTask",
            AssignedRoleKey = "Checker",
            Status = "Pending",
            CreatedAtUtc = Now,
            DueAtUtc = Now.AddMinutes(2)
        });
        dbContext.SaveChanges();

        var result = CreateService(dbContext).Execute(Request(Envelope(
            actionKey: "CheckerReturnToMaker",
            sectionKey: "Checker",
            buttonKey: "CheckerPrimary",
            expectedState: "CheckerReview",
            expectedVersion: 2,
            expectedTaskKey: "CheckerReviewTask",
            roles: ["Checker"],
            userId: "checker1")));

        Assert.True(result.Result.Success);
        Assert.Equal("ReturnedToMaker", dbContext.WorkflowInstances.Single().CurrentStateKey);
        Assert.Contains(dbContext.WorkflowTasks, row => row.TaskKey == "CheckerReviewTask" && row.Status == "Completed");
        var rework = Assert.Single(dbContext.WorkflowTasks.Where(row => row.TaskKey == "MakerReworkTask"));
        Assert.Equal("Pending", rework.Status);
        Assert.Null(rework.DueAtUtc);
        Assert.Equal("WorkflowReturnedToMaker", dbContext.NotificationOutbox.Single().NotificationType);
        Assert.Equal("Maker", dbContext.NotificationOutbox.Single().RecipientRoleKey);
    }

    [Theory]
    [InlineData("TradingDeskTaxable")]
    [InlineData("TradingDeskTaxExempt")]
    [InlineData("TradingDeskEquity")]
    public void CheckerSendToTradingDeskAssignsSpecificDeskTaskAndNotification(string assignedRoleKey)
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "CheckerReview", "Active", 2);
        dbContext.WorkflowTasks.Add(new WorkflowTask
        {
            WorkflowInstanceId = "WF-PT-1001",
            TaskKey = "CheckerReviewTask",
            AssignedRoleKey = "Checker",
            Status = "Pending",
            CreatedAtUtc = Now,
            DueAtUtc = Now.AddMinutes(2)
        });
        dbContext.SaveChanges();

        var envelope = Envelope(
            actionKey: "CheckerSendToTradingDesk",
            sectionKey: "Checker",
            buttonKey: "CheckerPrimary",
            expectedState: "CheckerReview",
            expectedVersion: 2,
            expectedTaskKey: "CheckerReviewTask",
            roles: ["Checker"],
            userId: "checker1");
        envelope.Workflow!.AssignedRoleKey = assignedRoleKey;

        var result = CreateService(dbContext).Execute(Request(envelope));

        Assert.True(result.Result.Success);
        Assert.Equal("TradingDeskReview", dbContext.WorkflowInstances.Single().CurrentStateKey);
        Assert.Contains(dbContext.WorkflowTasks, row => row.TaskKey == "CheckerReviewTask" && row.Status == "Completed");
        var tradingTask = Assert.Single(dbContext.WorkflowTasks.Where(row => row.TaskKey == "TradingDeskReviewTask"));
        Assert.Equal("Pending", tradingTask.Status);
        Assert.Equal(assignedRoleKey, tradingTask.AssignedRoleKey);
        var notification = Assert.Single(dbContext.NotificationOutbox);
        Assert.Equal("WorkflowTaskAssigned", notification.NotificationType);
        Assert.Equal(assignedRoleKey, notification.RecipientRoleKey);
        Assert.Equal(assignedRoleKey, result.Result.CreatedTask?.AssignedRole);
        Assert.Equal(assignedRoleKey, result.Result.Notification?.RecipientRole);
    }

    [Fact]
    public void BroadTradingDeskRoleCanExecuteTaskAssignedToSpecificDesk()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "TradingDeskReview", "Active", 3);
        dbContext.WorkflowTasks.Add(new WorkflowTask
        {
            WorkflowInstanceId = "WF-PT-1001",
            TaskKey = "TradingDeskReviewTask",
            AssignedRoleKey = "TradingDeskTaxable",
            Status = "Pending",
            CreatedAtUtc = Now,
            DueAtUtc = Now.AddMinutes(2)
        });
        dbContext.SaveChanges();

        var result = CreateService(dbContext).Execute(Request(Envelope(
            actionKey: "TradingTradeExecuted",
            sectionKey: "TradingDesk",
            buttonKey: "TradingPrimary",
            expectedState: "TradingDeskReview",
            expectedVersion: 3,
            expectedTaskKey: "TradingDeskReviewTask",
            roles: ["TradingDesk"],
            userId: "trader1")));

        Assert.True(result.Result.Success);
        Assert.Equal("BdrmClientServiceReview", dbContext.WorkflowInstances.Single().CurrentStateKey);
        Assert.Contains(dbContext.WorkflowTasks, row => row.TaskKey == "TradingDeskReviewTask" && row.Status == "Completed");
        Assert.Contains(dbContext.WorkflowTasks, row => row.TaskKey == "BdrmClientServiceReviewTask" && row.AssignedRoleKey == "BDRMClientService" && row.Status == "Pending");
    }

    [Fact]
    public void IdempotentReplayDoesNotDuplicateSideEffects()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Draft", "Active", 1);
        dbContext.SaveChanges();
        var request = Request(Envelope());
        var service = CreateService(dbContext);

        var first = service.Execute(request);
        var second = service.Execute(request);

        Assert.True(first.Result.Success);
        Assert.True(second.Result.Success);
        Assert.True(second.Result.Diagnostics.Replay);
        Assert.Single(dbContext.WorkflowTasks);
        Assert.Single(dbContext.NotificationOutbox);
        Assert.Single(dbContext.WorkflowAudits);
        Assert.Single(dbContext.MockSectionData);
        Assert.Single(dbContext.IdempotencyRecords);
        Assert.Equal(2, dbContext.WorkflowInstances.Single().Version);
    }

    [Fact]
    public void StoredIdempotencyResultReplaysOriginalSuccess()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Draft", "Active", 1);
        dbContext.SaveChanges();
        var request = Request(Envelope());
        var service = CreateService(dbContext);

        var first = service.Execute(request);
        dbContext.ChangeTracker.Clear();
        var second = service.Execute(request);

        Assert.True(first.Result.Success);
        Assert.True(second.Result.Success);
        Assert.True(second.Result.Diagnostics.Replay);
        Assert.Equal(first.Result.OldState, second.Result.OldState);
        Assert.Equal(first.Result.NewState, second.Result.NewState);
        Assert.Equal(first.Result.CreatedTask?.TaskKey, second.Result.CreatedTask?.TaskKey);
        var record = Assert.Single(dbContext.IdempotencyRecords);
        Assert.Contains("\"resultCode\":\"Success\"", record.ResultJson, StringComparison.Ordinal);
        Assert.Contains("\"newState\":\"CheckerReview\"", record.ResultJson, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(record.ResultHash));
    }

    [Fact]
    public void TaskClosingActionWithoutCurrentTaskReturnsConcurrencyConflictWithoutSideEffects()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "CheckerReview", "Active", 2);
        dbContext.SaveChanges();

        var result = CreateService(dbContext).Execute(Request(Envelope(
            actionKey: "CheckerReturnToMaker",
            sectionKey: "Checker",
            buttonKey: "CheckerPrimary",
            expectedState: "CheckerReview",
            expectedVersion: 2,
            roles: ["Checker"],
            userId: "checker1")));

        Assert.Equal("ConcurrencyConflict", result.Result.ResultCode);
        Assert.Equal("expectedCurrentTaskKey", result.Result.FailedGuard);
        Assert.Equal("CheckerReviewTask", result.Result.Expected);
        Assert.Equal(string.Empty, result.Result.Actual);
        Assert.Equal("CheckerReview", dbContext.WorkflowInstances.Single().CurrentStateKey);
        Assert.Empty(dbContext.WorkflowTasks);
        Assert.Empty(dbContext.NotificationOutbox);
        Assert.Empty(dbContext.WorkflowAudits);
        Assert.Empty(dbContext.MockSectionData);
        Assert.Empty(dbContext.IdempotencyRecords);
    }

    private static WorkflowExecutionService CreateService(WorkflowDbContext dbContext)
    {
        return new WorkflowExecutionService(
            dbContext,
            new WorkflowContextReader(dbContext, new FixedTimeProvider(Now)),
            new FixedTimeProvider(Now),
            new WorkflowActionResultBuilder(),
            new CanonicalWorkflowResultSerializer(),
            new NotificationTemplateRenderer());
    }

    private static WorkflowExecutionRequest Request(WorkflowActionEnvelope envelope)
    {
        var normalized = new WorkflowActionEnvelopeNormalizer().Normalize(envelope, null, null);
        return new WorkflowExecutionRequest(normalized, new CanonicalRequestHasher().ComputeHash(normalized.Envelope), AdminOverride: false);
    }

    private static WorkflowActionEnvelope Envelope(
        string actionKey = "SubmitForChecker",
        bool saveAsDraft = false,
        string sectionKey = "Maker",
        string buttonKey = "MakerPrimary",
        string expectedState = "Draft",
        int expectedVersion = 1,
        string? expectedTaskKey = null,
        string[]? roles = null,
        string userId = "maker1")
    {
        var envelope = WorkflowActionEnvelopeTestData.DeserializeValid();
        envelope.Actor!.UserId = userId;
        envelope.Actor.Roles = roles?.ToList() ?? ["Maker"];
        envelope.UiAction!.SectionKey = sectionKey;
        envelope.UiAction.ButtonKey = buttonKey;
        envelope.UiAction.SaveAsDraft = saveAsDraft;
        envelope.DataSave!.SectionKey = sectionKey;
        envelope.DataSave.Payload = JsonDocument.Parse("""{"comment":"ok"}""").RootElement.Clone();
        envelope.Workflow!.ActionKey = actionKey;
        envelope.Workflow.ExpectedCurrentState = expectedState;
        envelope.Workflow.ExpectedInstanceVersion = expectedVersion;
        envelope.Workflow.ExpectedCurrentTaskKey = expectedTaskKey;
        envelope.Workflow.IdempotencyKey = $"idem-{actionKey}-{expectedState}";
        envelope.Workflow.NotificationContext = NotificationContext();
        return envelope;
    }

    private static Dictionary<string, string?> NotificationContext()
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["referenceNumber"] = "1050",
            ["submitterDisplayName"] = "Kollipara, Kartheek",
            ["returnedByDisplayName"] = "Kollipara, Kartheek",
            ["recipientDisplayName"] = "Kollipara, Kartheek",
            ["traderDisplayName"] = "Kollipara, Kartheek",
            ["confirmerDisplayName"] = "Kollipara, Kartheek",
            ["deskName"] = "Taxable",
            ["securityType"] = "Other",
            ["accountShortName"] = "EFG",
            ["accountDisplayName"] = "INSURANCE-5091003029-EFG INSURANCE, LTD./ASI",
            ["submittedDate"] = "4/24/2026",
            ["tradeLink"] = "https://sharepoint.example.test/trades/1050"
        };
    }

    private static void AddInstance(
        WorkflowDbContext dbContext,
        string workflowInstanceId,
        string businessObjectId,
        string currentState,
        string status,
        int version)
    {
        dbContext.WorkflowInstances.Add(new WorkflowInstance
        {
            WorkflowInstanceId = workflowInstanceId,
            WorkflowKey = "PortfolioTrade",
            WorkflowDefinitionVersionId = PortfolioTradeVersionId(dbContext),
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = businessObjectId,
            CurrentStateKey = currentState,
            Status = status,
            StartedAtUtc = Now.AddMinutes(-30),
            StartedByUserId = "maker1",
            Version = version
        });
    }

    private static long PortfolioTradeVersionId(WorkflowDbContext dbContext)
    {
        var definition = dbContext.WorkflowDefinitions.Single(row => row.WorkflowKey == "PortfolioTrade");
        return dbContext.WorkflowDefinitionVersions.Single(row => row.WorkflowDefinitionId == definition.Id && row.IsActive).Id;
    }
}
