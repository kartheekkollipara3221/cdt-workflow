using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.WorkflowExecution;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;
using WorkflowEngine.Infrastructure.Persistence.Seed;
using WorkflowEngine.Infrastructure.WorkflowContext;
using WorkflowEngine.Infrastructure.WorkflowExecution;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class WorkflowExecutionValidationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2030-01-15T15:30:00Z");

    [Fact]
    public void MatchingIdempotencyRecordReturnsReplayBeforeInstanceLookup()
    {
        using var dbContext = CreateSeededContext();
        var envelope = Envelope();
        var request = Request(envelope);
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Draft", "Active", 1);
        dbContext.IdempotencyRecords.Add(new IdempotencyRecord
        {
            IdempotencyKey = request.Normalized.EffectiveIdempotencyKey,
            WorkflowInstanceId = "WF-PT-1001",
            ActionKey = "SubmitForChecker",
            RequestHash = request.RequestHash,
            ResultHash = "result-hash",
            ResultJson = JsonSerializer.Serialize(new WorkflowActionResultEnvelope
            {
                Result = new WorkflowActionResult
                {
                    Success = true,
                    ResultCode = "Success",
                    WorkflowInstanceId = "WF-PT-1001",
                    Diagnostics = new WorkflowActionDiagnostics()
                }
            }, WorkflowJson.Options),
            CreatedAtUtc = Now
        });
        dbContext.SaveChanges();

        var result = CreateService(dbContext).Execute(request);

        Assert.True(result.Result.Success);
        Assert.True(result.Result.Diagnostics.Replay);
        Assert.Equal(Now, result.Result.Diagnostics.OriginalCreatedAtUtc);
    }

    [Fact]
    public void IdempotencyConflictFiresBeforeInstanceLookup()
    {
        using var dbContext = CreateSeededContext();
        var request = Request(Envelope());
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Draft", "Active", 1);
        dbContext.IdempotencyRecords.Add(new IdempotencyRecord
        {
            IdempotencyKey = request.Normalized.EffectiveIdempotencyKey,
            WorkflowInstanceId = "WF-PT-1001",
            ActionKey = "SubmitForChecker",
            RequestHash = "different-hash",
            ResultHash = "result-hash",
            ResultJson = "{}",
            CreatedAtUtc = Now
        });
        dbContext.SaveChanges();

        var result = CreateService(dbContext).Execute(request);

        Assert.Equal("IdempotencyConflict", result.Result.ResultCode);
        Assert.Equal("different-hash", result.Result.Expected);
        Assert.Equal(request.RequestHash, result.Result.Actual);
    }

    [Fact]
    public void MissingInstanceReturnsWorkflowInstanceNotFound()
    {
        using var dbContext = CreateSeededContext();

        var result = CreateService(dbContext).Execute(Request(Envelope(version: 99)));

        Assert.Equal("WorkflowInstanceNotFound", result.Result.ResultCode);
        Assert.Equal("No workflow instance found for id 'WF-PT-1001'.", result.Result.Message);
    }

    [Fact]
    public void DefinitionVersionMismatchReturnsExpectedActualAndContext()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Draft", "Active", 1);
        dbContext.SaveChanges();

        var result = CreateService(dbContext).Execute(Request(Envelope(version: 2)));

        Assert.Equal("WorkflowDefinitionVersionMismatch", result.Result.ResultCode);
        Assert.Equal("2", result.Result.Expected);
        Assert.Equal("1", result.Result.Actual);
        Assert.NotNull(result.WorkflowContext);
    }

    [Fact]
    public void BusinessObjectIdentityMismatchReturnsValidationFailedBeforeSideEffects()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Draft", "Active", 1);
        dbContext.SaveChanges();
        var envelope = Envelope();
        envelope.BusinessObject!.ObjectId = "PT-9999";

        var result = CreateService(dbContext).Execute(Request(envelope));

        Assert.Equal("ValidationFailed", result.Result.ResultCode);
        Assert.Contains("businessObject.objectId", result.Result.Diagnostics.ValidationErrors!.Select(error => error.Field));
        Assert.Equal("Draft", dbContext.WorkflowInstances.Single().CurrentStateKey);
        Assert.Equal(1, dbContext.WorkflowInstances.Single().Version);
        Assert.Empty(dbContext.MockSectionData);
        Assert.Empty(dbContext.WorkflowAudits);
        Assert.Empty(dbContext.NotificationOutbox);
        Assert.Empty(dbContext.IdempotencyRecords);
    }

    [Fact]
    public void WorkflowKeyMismatchReturnsValidationFailedBeforeSideEffects()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Draft", "Active", 1);
        dbContext.SaveChanges();
        var envelope = Envelope();
        envelope.Workflow!.WorkflowKey = "OtherWorkflow";

        var result = CreateService(dbContext).Execute(Request(envelope));

        Assert.Equal("ValidationFailed", result.Result.ResultCode);
        Assert.Contains("workflow.workflowKey", result.Result.Diagnostics.ValidationErrors!.Select(error => error.Field));
        Assert.Equal("Draft", dbContext.WorkflowInstances.Single().CurrentStateKey);
        Assert.Empty(dbContext.MockSectionData);
        Assert.Empty(dbContext.WorkflowAudits);
    }

    [Fact]
    public void ActionSectionEchoMustMatchActionMetadataBeforeSideEffects()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "CheckerReview", "Active", 2);
        AddTask(dbContext, "WF-PT-1001", "CheckerReviewTask", "Checker");
        dbContext.SaveChanges();
        var envelope = Envelope(
            expectedState: "CheckerReview",
            expectedVersion: 2,
            actionKey: "CheckerSendToTradingDesk",
            roles: ["Checker"]);
        envelope.UiAction!.SectionKey = "Checker";
        envelope.UiAction.ButtonKey = "CheckerPrimary";
        envelope.UiAction.SaveAsDraft = null;
        envelope.DataSave!.SectionKey = "Maker";

        var result = CreateService(dbContext).Execute(Request(envelope));

        Assert.Equal("ValidationFailed", result.Result.ResultCode);
        Assert.Contains("dataSave.sectionKey", result.Result.Diagnostics.ValidationErrors!.Select(error => error.Field));
        Assert.Equal("CheckerReview", dbContext.WorkflowInstances.Single().CurrentStateKey);
        Assert.Single(dbContext.WorkflowTasks.Where(row => row.Status == "Pending"));
        Assert.Empty(dbContext.MockSectionData);
        Assert.Empty(dbContext.WorkflowAudits);
        Assert.Empty(dbContext.NotificationOutbox);
    }

    [Fact]
    public void DynamicAssignmentMissingRoleReturnsValidationFailedBeforeSideEffects()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "CheckerReview", "Active", 2);
        AddTask(dbContext, "WF-PT-1001", "CheckerReviewTask", "Checker");
        dbContext.SaveChanges();
        var envelope = Envelope(
            expectedState: "CheckerReview",
            expectedVersion: 2,
            actionKey: "CheckerSendToTradingDesk",
            roles: ["Checker"]);
        envelope.Workflow!.ExpectedCurrentTaskKey = "CheckerReviewTask";

        var result = CreateService(dbContext).Execute(Request(envelope));

        Assert.Equal("ValidationFailed", result.Result.ResultCode);
        Assert.Contains("workflow.assignedRoleKey", result.Result.Diagnostics.ValidationErrors!.Select(error => error.Field));
        Assert.Equal("CheckerReview", dbContext.WorkflowInstances.Single().CurrentStateKey);
        Assert.Single(dbContext.WorkflowTasks.Where(row => row.Status == "Pending" && row.TaskKey == "CheckerReviewTask"));
        Assert.Empty(dbContext.NotificationOutbox);
        Assert.Empty(dbContext.WorkflowAudits);
        Assert.Empty(dbContext.MockSectionData);
    }

    [Fact]
    public void DynamicAssignmentInvalidRoleReturnsValidationFailedBeforeSideEffects()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "CheckerReview", "Active", 2);
        AddTask(dbContext, "WF-PT-1001", "CheckerReviewTask", "Checker");
        dbContext.SaveChanges();
        var envelope = Envelope(
            expectedState: "CheckerReview",
            expectedVersion: 2,
            actionKey: "CheckerSendToTradingDesk",
            roles: ["Checker"]);
        envelope.Workflow!.ExpectedCurrentTaskKey = "CheckerReviewTask";
        envelope.Workflow.AssignedRoleKey = "TradingDeskMunicipal";

        var result = CreateService(dbContext).Execute(Request(envelope));

        Assert.Equal("ValidationFailed", result.Result.ResultCode);
        Assert.Contains("workflow.assignedRoleKey", result.Result.Diagnostics.ValidationErrors!.Select(error => error.Field));
        Assert.Equal("CheckerReview", dbContext.WorkflowInstances.Single().CurrentStateKey);
        Assert.Single(dbContext.WorkflowTasks.Where(row => row.Status == "Pending" && row.TaskKey == "CheckerReviewTask"));
        Assert.Empty(dbContext.NotificationOutbox);
        Assert.Empty(dbContext.WorkflowAudits);
        Assert.Empty(dbContext.MockSectionData);
    }

    [Fact]
    public void AssignmentRoleOnNonDynamicTransitionReturnsValidationFailedBeforeSideEffects()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Draft", "Active", 1);
        dbContext.SaveChanges();
        var envelope = Envelope();
        envelope.Workflow!.AssignedRoleKey = "TradingDeskTaxable";

        var result = CreateService(dbContext).Execute(Request(envelope));

        Assert.Equal("ValidationFailed", result.Result.ResultCode);
        Assert.Contains("workflow.assignedRoleKey", result.Result.Diagnostics.ValidationErrors!.Select(error => error.Field));
        Assert.Equal("Draft", dbContext.WorkflowInstances.Single().CurrentStateKey);
        Assert.Empty(dbContext.WorkflowTasks);
        Assert.Empty(dbContext.NotificationOutbox);
        Assert.Empty(dbContext.WorkflowAudits);
        Assert.Empty(dbContext.MockSectionData);
    }

    [Fact]
    public void CompletedInstanceReturnsInvalidTransitionBeforeActionChecks()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Completed", "Completed", 8, completedAtUtc: Now);
        dbContext.SaveChanges();

        var result = CreateService(dbContext).Execute(Request(Envelope(actionKey: "NotARealAction")));

        Assert.Equal("InvalidTransition", result.Result.ResultCode);
        Assert.Equal("Workflow is Completed; no actions are permitted.", result.Result.Message);
    }

    [Fact]
    public void MissingActionReturnsInvalidTransition()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Draft", "Active", 1);
        dbContext.SaveChanges();

        var result = CreateService(dbContext).Execute(Request(Envelope(actionKey: "NotARealAction")));

        Assert.Equal("InvalidTransition", result.Result.ResultCode);
        Assert.Equal("Action 'NotARealAction' is not defined for workflow definition version 1.", result.Result.Message);
    }

    [Fact]
    public void MissingTransitionReturnsInvalidTransition()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Draft", "Active", 1);
        dbContext.SaveChanges();

        var result = CreateService(dbContext).Execute(Request(Envelope(actionKey: "CheckerReturnToMaker")));

        Assert.Equal("InvalidTransition", result.Result.ResultCode);
        Assert.Equal("No transition exists for state 'Draft' with action 'CheckerReturnToMaker'.", result.Result.Message);
    }

    [Fact]
    public void StateConcurrencyConflictWinsBeforeVersionConflict()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "CheckerReview", "Active", 3);
        dbContext.SaveChanges();

        var result = CreateService(dbContext).Execute(Request(Envelope(expectedState: "Draft", expectedVersion: 1, actionKey: "CheckerSendToTradingDesk", roles: ["Checker"])));

        Assert.Equal("ConcurrencyConflict", result.Result.ResultCode);
        Assert.Equal("expectedCurrentState", result.Result.FailedGuard);
        Assert.Equal("Draft", result.Result.Expected);
        Assert.Equal("CheckerReview", result.Result.Actual);
        Assert.Empty(dbContext.WorkflowTasks);
        Assert.Empty(dbContext.NotificationOutbox);
        Assert.Empty(dbContext.WorkflowAudits);
        Assert.Empty(dbContext.MockSectionData);
        Assert.Empty(dbContext.IdempotencyRecords);
    }

    [Fact]
    public void SaveDraftVersionConcurrencyConflictDoesNotSaveSectionData()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Draft", "Active", 2);
        dbContext.SaveChanges();

        var envelope = Envelope(actionKey: "SaveDraft", expectedState: "Draft", expectedVersion: 1);
        envelope.UiAction!.SaveAsDraft = true;
        var result = CreateService(dbContext).Execute(Request(envelope));

        Assert.Equal("ConcurrencyConflict", result.Result.ResultCode);
        Assert.Equal("expectedInstanceVersion", result.Result.FailedGuard);
        Assert.Equal("1", result.Result.Expected);
        Assert.Equal("2", result.Result.Actual);
        Assert.Equal("Draft", dbContext.WorkflowInstances.Single().CurrentStateKey);
        Assert.Equal(2, dbContext.WorkflowInstances.Single().Version);
        Assert.Empty(dbContext.WorkflowTasks);
        Assert.Empty(dbContext.NotificationOutbox);
        Assert.Empty(dbContext.WorkflowAudits);
        Assert.Empty(dbContext.MockSectionData);
        Assert.Empty(dbContext.IdempotencyRecords);
    }

    [Fact]
    public void WrongRoleReturnsForbidden()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Draft", "Active", 1);
        dbContext.SaveChanges();

        var result = CreateService(dbContext).Execute(Request(Envelope(roles: ["Checker"])));

        Assert.Equal("Forbidden", result.Result.ResultCode);
        Assert.Contains("None of the actor's roles (Checker) are authorized", result.Result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminOverrideBypassesOnlyRoleAuthorization()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Draft", "Active", 1);
        dbContext.SaveChanges();

        var result = CreateService(dbContext).Execute(Request(Envelope(roles: ["Checker"]), adminOverride: true));

        Assert.True(result.Result.Success);
        Assert.Equal("Success", result.Result.ResultCode);
    }

    [Fact]
    public void AmbiguousRoleMatchRequiresMultipleMatchingTransitionRoles()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Draft", "Active", 1);
        dbContext.WorkflowTransitionDefinitions.Add(new WorkflowTransitionDefinition
        {
            WorkflowDefinitionVersionId = PortfolioTradeVersionId(dbContext),
            FromStateKey = "Draft",
            ActionKey = "SubmitForChecker",
            AllowedRoleKey = "Checker",
            ToStateKey = "CheckerReview",
            AuditLabel = "Injected ambiguous transition",
            IsActive = true
        });
        dbContext.SaveChanges();

        var result = CreateService(dbContext).Execute(Request(Envelope(roles: ["Maker", "Checker"])));

        Assert.Equal("AmbiguousRoleMatch", result.Result.ResultCode);
        Assert.Contains("Multiple actor roles (Maker, Checker)", result.Result.Message, StringComparison.Ordinal);
    }

    internal static WorkflowDbContext CreateSeededContext()
    {
        var dbContext = CreateDbContext();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.Migrate();
        new WorkflowSeedService(dbContext, new FixedTimeProvider(Now)).SeedPortfolioTrade();
        return dbContext;
    }

    private static WorkflowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(TestSqlConnectionFactory.ConnectionString("burrito_phase4_execution_tests"))
            .Options;

        return new WorkflowDbContext(options);
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

    private static WorkflowExecutionRequest Request(WorkflowActionEnvelope envelope, bool adminOverride = false)
    {
        var normalized = new WorkflowActionEnvelopeNormalizer().Normalize(envelope, null, null);
        return new WorkflowExecutionRequest(normalized, new CanonicalRequestHasher().ComputeHash(normalized.Envelope), adminOverride);
    }

    private static WorkflowActionEnvelope Envelope(
        int version = 1,
        string expectedState = "Draft",
        int expectedVersion = 1,
        string actionKey = "SubmitForChecker",
        string[]? roles = null)
    {
        var envelope = WorkflowActionEnvelopeTestData.DeserializeValid();
        envelope.Workflow!.WorkflowDefinitionVersion = version;
        envelope.Workflow.ExpectedCurrentState = expectedState;
        envelope.Workflow.ExpectedInstanceVersion = expectedVersion;
        envelope.Workflow.ActionKey = actionKey;
        envelope.Actor!.Roles = roles?.ToList() ?? ["Maker"];
        var (sectionKey, buttonKey) = ActionUi(actionKey);
        envelope.UiAction!.SectionKey = sectionKey;
        envelope.UiAction.ButtonKey = buttonKey;
        envelope.DataSave!.SectionKey = sectionKey;
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

    private static (string SectionKey, string ButtonKey) ActionUi(string actionKey)
    {
        return actionKey switch
        {
            "CheckerReturnToMaker" or "CheckerSendToTradingDesk" => ("Checker", "CheckerPrimary"),
            "TradingReturnToMaker" or "TradingTradeExecuted" => ("TradingDesk", "TradingPrimary"),
            "BdrmTradeFulfilled" or "BdrmTradeNotExecutedProperly" => ("BDRMClientService", "BdrmPrimary"),
            _ => ("Maker", "MakerPrimary")
        };
    }

    private static void AddInstance(
        WorkflowDbContext dbContext,
        string workflowInstanceId,
        string businessObjectId,
        string currentState,
        string status,
        int version,
        DateTimeOffset? completedAtUtc = null)
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
            CompletedAtUtc = completedAtUtc,
            Version = version
        });
    }

    private static void AddTask(WorkflowDbContext dbContext, string workflowInstanceId, string taskKey, string assignedRole)
    {
        dbContext.WorkflowTasks.Add(new WorkflowTask
        {
            WorkflowInstanceId = workflowInstanceId,
            TaskKey = taskKey,
            AssignedRoleKey = assignedRole,
            Status = "Pending",
            CreatedAtUtc = Now
        });
    }

    private static long PortfolioTradeVersionId(WorkflowDbContext dbContext)
    {
        var definition = dbContext.WorkflowDefinitions.Single(row => row.WorkflowKey == "PortfolioTrade");
        return dbContext.WorkflowDefinitionVersions.Single(row => row.WorkflowDefinitionId == definition.Id && row.IsActive).Id;
    }
}

[CollectionDefinition("SqlServerWorkflowExecution")]
public sealed class SqlServerWorkflowExecutionCollection
{
}
