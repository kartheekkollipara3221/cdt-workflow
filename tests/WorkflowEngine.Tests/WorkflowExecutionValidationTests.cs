using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.Sla;
using WorkflowEngine.Application.WorkflowExecution;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;
using WorkflowEngine.Infrastructure.Persistence.Seed;
using WorkflowEngine.Infrastructure.Sla;
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
    public void ReturnToMakerPersistsExactRecipientSnapshotSeparatelyFromTemplateContext()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "CheckerReview", "Active", 2);
        AddTask(dbContext, "WF-PT-1001", "CheckerReviewTask", "Checker");
        dbContext.SaveChanges();
        var envelope = Envelope(
            expectedState: "CheckerReview",
            expectedVersion: 2,
            actionKey: "CheckerReturnToMaker",
            roles: ["Checker"]);
        envelope.Workflow!.ExpectedCurrentTaskKey = "CheckerReviewTask";
        envelope.Workflow.NotificationRecipients = new NotificationRecipients
        {
            ToEmailAddresses = [" maker.one@example.test ", "maker.one@example.test"],
            CcEmailAddresses = ["manager@example.test", "operations@example.test"]
        };
        envelope.Workflow.NotificationContext!["recipientEmail"] = "template-context-only@example.test";

        var result = CreateService(dbContext).Execute(Request(envelope));

        Assert.True(result.Result.Success);
        var notification = Assert.Single(dbContext.NotificationOutbox);
        Assert.Equal("WorkflowReturnedToMaker", notification.NotificationType);
        var createdTask = dbContext.WorkflowTasks.Single(row => row.TaskKey == "MakerReworkTask");
        var recipients = JsonSerializer.Deserialize<NotificationRecipients>(
            createdTask.NotificationRecipientsJson,
            WorkflowJson.Options);
        Assert.NotNull(recipients);
        Assert.Equal(
            [" maker.one@example.test ", "maker.one@example.test"],
            recipients.ToEmailAddresses);
        Assert.Equal(
            ["manager@example.test", "operations@example.test"],
            recipients.CcEmailAddresses);
        Assert.Equal(createdTask.NotificationRecipientsJson, notification.NotificationRecipientsJson);
        using var payload = JsonDocument.Parse(notification.PayloadJson);
        Assert.Equal(
            "template-context-only@example.test",
            payload.RootElement.GetProperty("recipientEmail").GetString());
    }

    [Fact]
    public void NotificationInputResolvesMetadataParticipantAndRequestSourcesBeforeSideEffects()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "CheckerReview", "Active", 2);
        AddTask(dbContext, "WF-PT-1001", "CheckerReviewTask", "Checker");
        dbContext.WorkflowParticipants.Add(new WorkflowParticipant
        {
            WorkflowInstanceId = "WF-PT-1001",
            ParticipantKey = "originalMaker",
            UserId = "maker1",
            EmailAddress = "maker@example.test",
            DisplayName = "Maker One",
            SortOrder = 1,
            CreatedAtUtc = Now
        });
        dbContext.SaveChanges();
        var envelope = Envelope(
            expectedState: "CheckerReview",
            expectedVersion: 2,
            actionKey: "CheckerReturnToMaker",
            roles: ["Checker"]);
        envelope.Workflow!.NotificationRecipients = new NotificationRecipients
        {
            ToEmailAddresses = ["legacy-must-not-win"],
            CcEmailAddresses = []
        };
        var request = Request(envelope) with
        {
            NotificationInput = new WorkflowNotificationInput
            {
                RecipientInputs = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
                {
                    ["selectedCcUsers"] = [new("manager", "manager@example.test", "Manager")]
                },
                TemplateData = NotificationContext()
            }
        };

        var result = CreateService(dbContext).Execute(request);

        Assert.True(result.Result.Success);
        var task = Assert.Single(dbContext.WorkflowTasks.Where(row => row.Status == "Pending"));
        var recipients = JsonSerializer.Deserialize<NotificationRecipients>(task.NotificationRecipientsJson, WorkflowJson.Options);
        Assert.NotNull(recipients);
        Assert.Equal(["maker@example.test"], recipients.ToEmailAddresses);
        Assert.Equal(
            ["manager@example.test", "workflow-operations@example.test"],
            recipients.CcEmailAddresses);
        Assert.Equal(task.NotificationRecipientsJson, Assert.Single(dbContext.NotificationOutbox).NotificationRecipientsJson);
    }

    [Fact]
    public void SubmitForCheckerResolvesOwnerMakerAndCumulativeAudience()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Draft", "Active", 1);
        AddOriginalMaker(dbContext);
        dbContext.SaveChanges();
        var request = Request(Envelope()) with
        {
            NotificationInput = NotificationInput(
                new("maker-copy", "MAKER@example.test", "Maker Duplicate"),
                new("checker-copy", "CHECKER@example.test", "Checker Duplicate"),
                new("manager1", "Manager@example.test", "Manager One"),
                new("manager-duplicate", "manager@EXAMPLE.TEST", "Manager Duplicate"))
        };

        var result = CreateService(dbContext).Execute(request);

        Assert.True(result.Result.Success);
        var task = Assert.Single(dbContext.WorkflowTasks.Where(row =>
            row.TaskKey == "CheckerReviewTask"
            && row.Status == "Pending"));
        AssertRecipientSnapshot(
            task.NotificationRecipientsJson,
            ["checker@example.test"],
            ["maker@example.test", "Manager@example.test"]);
        var assignment = Assert.Single(dbContext.NotificationOutbox.Where(row =>
            row.WorkflowTaskId == task.Id
            && row.NotificationType == "WorkflowTaskAssigned"));
        Assert.Equal(task.NotificationRecipientsJson, assignment.NotificationRecipientsJson);
    }

    [Fact]
    public void CheckerSendToTradingDeskResolvesOwnerMakerAndCumulativeAudience()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "CheckerReview", "Active", 2);
        AddTask(dbContext, "WF-PT-1001", "CheckerReviewTask", "Checker");
        AddOriginalMaker(dbContext);
        dbContext.SaveChanges();
        var envelope = Envelope(
            expectedState: "CheckerReview",
            expectedVersion: 2,
            actionKey: "CheckerSendToTradingDesk",
            roles: ["Checker"]);
        envelope.Actor!.UserId = "checker1";
        envelope.Workflow!.ExpectedCurrentTaskKey = "CheckerReviewTask";
        envelope.Workflow.AssignedRoleKey = "TradingDeskTaxable";
        envelope.Workflow.SlaInput = new WorkflowSlaInput
        {
            TargetDate = new DateOnly(2030, 1, 20)
        };
        var request = Request(envelope) with
        {
            NotificationInput = NotificationInput(
                new("manager1", "manager@example.test", "Manager One"),
                new("checker1", "checker@example.test", "Checker One"))
        };

        var result = CreateService(dbContext).Execute(request);

        Assert.True(result.Result.Success);
        var task = Assert.Single(dbContext.WorkflowTasks.Where(row =>
            row.TaskKey == "TradingDeskReviewTask"
            && row.Status == "Pending"));
        AssertRecipientSnapshot(
            task.NotificationRecipientsJson,
            ["trading-desk@example.test"],
            ["maker@example.test", "manager@example.test", "checker@example.test"]);
        var assignment = Assert.Single(dbContext.NotificationOutbox.Where(row =>
            row.WorkflowTaskId == task.Id
            && row.NotificationType == "WorkflowTaskAssigned"));
        Assert.Equal(task.NotificationRecipientsJson, assignment.NotificationRecipientsJson);
    }

    [Fact]
    public async Task TradingTradeExecutedResolvesBdrmAudienceAndSlaReusesSnapshot()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "TradingDeskReview", "Active", 3);
        AddTask(dbContext, "WF-PT-1001", "TradingDeskReviewTask", "TradingDesk");
        dbContext.WorkflowParticipants.Add(new WorkflowParticipant
        {
            WorkflowInstanceId = "WF-PT-1001",
            ParticipantKey = "originalMaker",
            UserId = "maker1",
            EmailAddress = "maker@example.test",
            DisplayName = "Maker One",
            SortOrder = 1,
            CreatedAtUtc = Now
        });
        dbContext.SaveChanges();

        var envelope = Envelope(
            expectedState: "TradingDeskReview",
            expectedVersion: 3,
            actionKey: "TradingTradeExecuted",
            roles: ["TradingDesk"]);
        envelope.Workflow!.ExpectedCurrentTaskKey = "TradingDeskReviewTask";
        var request = Request(envelope) with
        {
            NotificationInput = new WorkflowNotificationInput
            {
                RecipientInputs = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
                {
                    ["selectedCcUsers"] =
                    [
                        new("manager1", "manager@example.test", "Manager One"),
                        new("checker1", "checker@example.test", "Checker One"),
                        new("trader1", "trader@example.test", "Trader One")
                    ]
                },
                TemplateData = NotificationContext()
            }
        };

        var result = CreateService(dbContext).Execute(request);

        Assert.True(result.Result.Success);
        var task = Assert.Single(dbContext.WorkflowTasks.Where(row =>
            row.TaskKey == "BdrmClientServiceReviewTask"
            && row.Status == "Pending"));
        var recipients = JsonSerializer.Deserialize<NotificationRecipients>(
            task.NotificationRecipientsJson,
            WorkflowJson.Options);
        Assert.NotNull(recipients);
        Assert.Equal(["bdrm-client-service@example.test"], recipients.ToEmailAddresses);
        Assert.Equal(
            ["maker@example.test", "manager@example.test", "checker@example.test", "trader@example.test"],
            recipients.CcEmailAddresses);
        var assignment = Assert.Single(dbContext.NotificationOutbox.Where(row =>
            row.WorkflowTaskId == task.Id
            && row.NotificationType == "WorkflowTaskAssigned"));
        Assert.Equal(task.NotificationRecipientsJson, assignment.NotificationRecipientsJson);

        var slaResult = await new RunSlaService(
            dbContext,
            new NotificationTemplateRenderer(),
            new SlaScheduleCalculator()).RunAsync(
                new RunSlaRequest(Now.AddMinutes(2)),
                CancellationToken.None);

        var queued = Assert.Single(slaResult.QueuedNotifications);
        Assert.Equal("Overdue", queued.ReminderKind);
        var overdue = dbContext.NotificationOutbox.Single(row =>
            row.Id == queued.NotificationOutboxId);
        Assert.Equal("SlaTaskOverdue", overdue.NotificationType);
        Assert.Equal(task.NotificationRecipientsJson, overdue.NotificationRecipientsJson);
    }

    [Fact]
    public void TradingReturnToMakerCopiesCompleteCumulativeAudience()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "TradingDeskReview", "Active", 3);
        AddTask(dbContext, "WF-PT-1001", "TradingDeskReviewTask", "TradingDeskTaxable");
        AddOriginalMaker(dbContext);
        dbContext.SaveChanges();
        var envelope = Envelope(
            expectedState: "TradingDeskReview",
            expectedVersion: 3,
            actionKey: "TradingReturnToMaker",
            roles: ["TradingDesk"]);
        envelope.Actor!.UserId = "trader1";
        envelope.Workflow!.ExpectedCurrentTaskKey = "TradingDeskReviewTask";
        var request = Request(envelope) with
        {
            NotificationInput = NotificationInput(
                new("manager1", "manager@example.test", "Manager One"),
                new("checker1", "checker@example.test", "Checker One"),
                new("trader1", "trader@example.test", "Trader One"))
        };

        var result = CreateService(dbContext).Execute(request);

        Assert.True(result.Result.Success);
        var task = Assert.Single(dbContext.WorkflowTasks.Where(row =>
            row.TaskKey == "MakerReworkTask"
            && row.Status == "Pending"));
        AssertRecipientSnapshot(
            task.NotificationRecipientsJson,
            ["maker@example.test"],
            ["manager@example.test", "checker@example.test", "trader@example.test"]);
        var notification = Assert.Single(dbContext.NotificationOutbox.Where(row =>
            row.WorkflowTaskId == task.Id
            && row.NotificationType == "WorkflowReturnedToMaker"));
        Assert.Equal(task.NotificationRecipientsJson, notification.NotificationRecipientsJson);
    }

    [Fact]
    public void BdrmReworkAssignmentCopiesCompleteCumulativeAudience()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "BdrmClientServiceReview", "Active", 4);
        AddTask(dbContext, "WF-PT-1001", "BdrmClientServiceReviewTask", "BDRMClientService");
        AddOriginalMaker(dbContext);
        dbContext.SaveChanges();
        var envelope = Envelope(
            expectedState: "BdrmClientServiceReview",
            expectedVersion: 4,
            actionKey: "BdrmTradeNotExecutedProperly",
            roles: ["BDRMClientService"]);
        envelope.Actor!.UserId = "bdrm1";
        envelope.Workflow!.ExpectedCurrentTaskKey = "BdrmClientServiceReviewTask";
        var request = Request(envelope) with
        {
            NotificationInput = NotificationInput(
                new("manager1", "manager@example.test", "Manager One"),
                new("checker1", "checker@example.test", "Checker One"),
                new("trader1", "trader@example.test", "Trader One"),
                new("bdrm1", "bdrm@example.test", "BDRM One"))
        };

        var result = CreateService(dbContext).Execute(request);

        Assert.True(result.Result.Success);
        var task = Assert.Single(dbContext.WorkflowTasks.Where(row =>
            row.TaskKey == "TradingDeskReworkTask"
            && row.Status == "Pending"));
        AssertRecipientSnapshot(
            task.NotificationRecipientsJson,
            ["trading-desk@example.test"],
            [
                "maker@example.test",
                "manager@example.test",
                "checker@example.test",
                "trader@example.test",
                "bdrm@example.test"
            ]);
        var notification = Assert.Single(dbContext.NotificationOutbox.Where(row =>
            row.WorkflowTaskId == task.Id
            && row.NotificationType == "WorkflowTaskAssigned"));
        Assert.Equal(task.NotificationRecipientsJson, notification.NotificationRecipientsJson);
    }

    [Fact]
    public void BdrmCompletionCopiesCompleteCumulativeAudience()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "BdrmClientServiceReview", "Active", 4);
        AddTask(dbContext, "WF-PT-1001", "BdrmClientServiceReviewTask", "BDRMClientService");
        AddOriginalMaker(dbContext);
        dbContext.SaveChanges();
        var envelope = Envelope(
            expectedState: "BdrmClientServiceReview",
            expectedVersion: 4,
            actionKey: "BdrmTradeFulfilled",
            roles: ["BDRMClientService"]);
        envelope.Actor!.UserId = "bdrm1";
        envelope.Workflow!.ExpectedCurrentTaskKey = "BdrmClientServiceReviewTask";
        var request = Request(envelope) with
        {
            NotificationInput = NotificationInput(
                new("manager1", "manager@example.test", "Manager One"),
                new("checker1", "checker@example.test", "Checker One"),
                new("trader1", "trader@example.test", "Trader One"),
                new("bdrm1", "bdrm@example.test", "BDRM One"))
        };

        var result = CreateService(dbContext).Execute(request);

        Assert.True(result.Result.Success);
        Assert.Equal("Completed", dbContext.WorkflowInstances.Single().Status);
        var notification = Assert.Single(dbContext.NotificationOutbox.Where(row =>
            row.NotificationType == "WorkflowCompleted"));
        AssertRecipientSnapshot(
            notification.NotificationRecipientsJson,
            ["maker@example.test"],
            [
                "manager@example.test",
                "checker@example.test",
                "trader@example.test",
                "bdrm@example.test"
            ]);
    }

    [Fact]
    public void UnknownNotificationInputFailsBeforeWorkflowSideEffects()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Draft", "Active", 1);
        dbContext.SaveChanges();
        var request = Request(Envelope()) with
        {
            NotificationInput = new WorkflowNotificationInput
            {
                RecipientInputs = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
                {
                    ["unexpectedUsers"] = [new("user", "opaque", null)]
                }
            }
        };

        var result = CreateService(dbContext).Execute(request);

        Assert.False(result.Result.Success);
        Assert.Equal("ValidationFailed", result.Result.ResultCode);
        Assert.Contains("unexpectedUsers", result.Result.Message, StringComparison.Ordinal);
        Assert.Equal("Draft", Assert.Single(dbContext.WorkflowInstances).CurrentStateKey);
        Assert.Empty(dbContext.WorkflowTasks);
        Assert.Empty(dbContext.NotificationOutbox);
        Assert.Empty(dbContext.WorkflowAudits);
        Assert.Empty(dbContext.IdempotencyRecords);
        Assert.Empty(dbContext.MockSectionData);
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

    [Theory]
    [InlineData("BdrmTradeFulfilled")]
    [InlineData("BdrmTradeNotExecutedProperly")]
    public void BdrmExitActionCompletesRecurringScheduleAndCancelsUndeliveredReminder(string actionKey)
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "BdrmClientServiceReview", "Active", 4);
        AddTask(dbContext, "WF-PT-1001", "BdrmClientServiceReviewTask", "BDRMClientService");
        dbContext.SaveChanges();
        var task = dbContext.WorkflowTasks.Single();
        var schedule = AddBdrmSchedule(dbContext, task);
        AddPendingReminder(dbContext, task, schedule);
        dbContext.SaveChanges();

        var envelope = Envelope(
            expectedState: "BdrmClientServiceReview",
            expectedVersion: 4,
            actionKey: actionKey,
            roles: ["BDRMClientService"]);
        envelope.Workflow!.ExpectedCurrentTaskKey = "BdrmClientServiceReviewTask";

        var result = CreateService(dbContext).Execute(Request(envelope));

        Assert.True(result.Result.Success);
        Assert.Equal("Completed", schedule.Status);
        Assert.Null(schedule.NextReminderAtUtc);
        var reminder = dbContext.NotificationOutbox
            .Single(row => row.WorkflowTaskSlaScheduleId == schedule.Id);
        Assert.Equal("Cancelled", reminder.Status);
        Assert.Equal("WorkflowTaskCompleted", reminder.CancellationReason);
    }

    [Fact]
    public void TradingReexecutionCreatesFreshBdrmRecurringSchedule()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-1001", "PT-1001", "BdrmClientServiceReview", "Active", 4);
        AddTask(dbContext, "WF-PT-1001", "BdrmClientServiceReviewTask", "BDRMClientService");
        dbContext.SaveChanges();
        var oldTask = dbContext.WorkflowTasks.Single();
        var oldSchedule = AddBdrmSchedule(dbContext, oldTask);
        dbContext.SaveChanges();
        var service = CreateService(dbContext);

        var reject = Envelope(
            expectedState: "BdrmClientServiceReview",
            expectedVersion: 4,
            actionKey: "BdrmTradeNotExecutedProperly",
            roles: ["BDRMClientService"]);
        reject.Request!.ClientActionId = "client-bdrm-reject";
        reject.Workflow!.IdempotencyKey = "idem-bdrm-reject";
        reject.Workflow.ExpectedCurrentTaskKey = "BdrmClientServiceReviewTask";
        Assert.True(service.Execute(Request(reject)).Result.Success);

        var reexecute = Envelope(
            expectedState: "TradingDeskRework",
            expectedVersion: 5,
            actionKey: "TradingTradeExecuted",
            roles: ["TradingDesk"]);
        reexecute.Request!.ClientActionId = "client-trading-reexecute";
        reexecute.Workflow!.IdempotencyKey = "idem-trading-reexecute";
        reexecute.Workflow.ExpectedCurrentTaskKey = "TradingDeskReworkTask";
        Assert.True(service.Execute(Request(reexecute)).Result.Success);

        var newTask = dbContext.WorkflowTasks.Single(row =>
            row.TaskKey == "BdrmClientServiceReviewTask"
            && row.Status == "Pending");
        var newSchedule = dbContext.WorkflowTaskSlaSchedules.Single(row =>
            row.WorkflowTaskId == newTask.Id
            && row.Status == "Active");

        Assert.Equal("Completed", oldSchedule.Status);
        Assert.NotEqual(oldSchedule.Id, newSchedule.Id);
        Assert.Equal(Now.AddMinutes(2), newSchedule.OverdueAtUtc);
        Assert.Equal(Now.AddMinutes(2), newSchedule.NextReminderAtUtc);
        Assert.Equal(new TimeOnly(9, 0), newSchedule.ReminderLocalTime);
        Assert.Equal("America/New_York", newSchedule.TimeZoneId);
        Assert.Equal(1, newSchedule.RecurrenceIntervalDays);
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

    private static void AddOriginalMaker(WorkflowDbContext dbContext)
    {
        dbContext.WorkflowParticipants.Add(new WorkflowParticipant
        {
            WorkflowInstanceId = "WF-PT-1001",
            ParticipantKey = "originalMaker",
            UserId = "maker1",
            EmailAddress = "maker@example.test",
            DisplayName = "Maker One",
            SortOrder = 1,
            CreatedAtUtc = Now
        });
    }

    private static WorkflowNotificationInput NotificationInput(params WorkflowRecipientInput[] recipients)
    {
        return new WorkflowNotificationInput
        {
            RecipientInputs = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
            {
                ["selectedCcUsers"] = recipients
            },
            TemplateData = NotificationContext()
        };
    }

    private static void AssertRecipientSnapshot(
        string notificationRecipientsJson,
        string[] expectedTo,
        string[] expectedCc)
    {
        var recipients = JsonSerializer.Deserialize<NotificationRecipients>(
            notificationRecipientsJson,
            WorkflowJson.Options);
        Assert.NotNull(recipients);
        Assert.Equal(expectedTo, recipients.ToEmailAddresses);
        Assert.Equal(expectedCc, recipients.CcEmailAddresses);
    }

    private static WorkflowTaskSlaSchedule AddBdrmSchedule(
        WorkflowDbContext dbContext,
        WorkflowTask task)
    {
        var schedule = new WorkflowTaskSlaSchedule
        {
            WorkflowTaskId = task.Id,
            ScheduleVersion = 1,
            SlaPolicyKey = "BdrmClientServiceReviewSla",
            OverdueAtUtc = Now.AddMinutes(2),
            NextReminderAtUtc = Now.AddMinutes(2),
            CalculationMode = "DurationFromTaskCreation",
            ReminderLocalTime = new TimeOnly(9, 0),
            TimeZoneId = "America/New_York",
            OverdueStartOffsetDays = 0,
            RecurrenceIntervalDays = 1,
            OverdueNotificationRouteKey = "RouteSlaTaskOverdue",
            Status = "Active",
            CreatedAtUtc = Now,
            CreatedByUserId = "system",
            CorrelationId = "corr-bdrm-sla",
            IdempotencyKey = "idem-bdrm-sla"
        };
        dbContext.WorkflowTaskSlaSchedules.Add(schedule);
        dbContext.SaveChanges();
        return schedule;
    }

    private static void AddPendingReminder(
        WorkflowDbContext dbContext,
        WorkflowTask task,
        WorkflowTaskSlaSchedule schedule)
    {
        dbContext.NotificationOutbox.Add(new NotificationOutbox
        {
            WorkflowInstanceId = task.WorkflowInstanceId,
            WorkflowTaskId = task.Id,
            WorkflowTaskSlaScheduleId = schedule.Id,
            SlaOccurrenceDate = new DateOnly(2030, 1, 16),
            SlaReminderKind = "Overdue",
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-1001",
            NotificationType = "SlaTaskOverdue",
            NotificationRecipientsJson = "{\"toEmailAddresses\":[\"bdrm@example.test\"],\"ccEmailAddresses\":[]}",
            Subject = "Overdue",
            Body = "body",
            HtmlBody = "<p>body</p>",
            PayloadJson = "{}",
            Status = "Pending",
            CorrelationId = "corr-pending-bdrm-reminder",
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
