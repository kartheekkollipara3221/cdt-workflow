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

/// <summary>
/// Proves that an action whose transition keeps the state runs, queues its notification and lets the
/// instance continue. A caller can use such an action to send a second email after a state change.
/// </summary>
[Collection("SqlServerWorkflowExecution")]
public sealed class WorkflowStateKeepingActionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2030-01-15T15:30:00Z");
    private const string WorkflowKey = "StateKeepingTest";
    private const string ObjectType = "StateKeepingObject";
    private const string InstanceId = "WF-STATE-KEEPING-1";

    [Fact]
    public void StateKeepingActionQueuesNotificationAndInstanceContinues()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        Seed(dbContext);

        var inform = Request("Inform", expectedVersion: 1, "idem-state-keeping-inform", "info@example.test");
        var informResult = CreateService(dbContext).Execute(inform);

        Assert.True(informResult.Result.Success, $"{informResult.Result.ResultCode}: {informResult.Result.Message}");
        dbContext.ChangeTracker.Clear();
        var afterInform = dbContext.WorkflowInstances.Single(row => row.WorkflowInstanceId == InstanceId);
        Assert.Equal("Submitted", afterInform.CurrentStateKey);
        Assert.Equal("Active", afterInform.Status);
        Assert.Equal(2, afterInform.Version);
        var informRow = Assert.Single(dbContext.NotificationOutbox.Where(row => row.WorkflowInstanceId == InstanceId));
        Assert.Equal("StateKeepingInformational", informRow.NotificationType);
        Assert.Equal("Informational: Account A", informRow.Subject);
        Assert.Equal("Pending", informRow.Status);
        Assert.Contains("info@example.test", informRow.NotificationRecipientsJson, StringComparison.Ordinal);

        var replay = CreateService(dbContext).Execute(inform);

        Assert.True(replay.Result.Success);
        dbContext.ChangeTracker.Clear();
        Assert.Single(dbContext.NotificationOutbox.Where(row => row.WorkflowInstanceId == InstanceId));
        Assert.Equal(2, dbContext.WorkflowInstances.Single(row => row.WorkflowInstanceId == InstanceId).Version);

        var confirm = Request("Confirm", expectedVersion: 2, "idem-state-keeping-confirm", "done@example.test");
        var confirmResult = CreateService(dbContext).Execute(confirm);

        Assert.True(confirmResult.Result.Success, $"{confirmResult.Result.ResultCode}: {confirmResult.Result.Message}");
        dbContext.ChangeTracker.Clear();
        var afterConfirm = dbContext.WorkflowInstances.Single(row => row.WorkflowInstanceId == InstanceId);
        Assert.Equal("Done", afterConfirm.CurrentStateKey);
        Assert.Equal("Completed", afterConfirm.Status);
        Assert.Equal(2, dbContext.NotificationOutbox.Count(row => row.WorkflowInstanceId == InstanceId));
    }

    private static void Seed(WorkflowDbContext dbContext)
    {
        var definition = new WorkflowDefinition
        {
            WorkflowKey = WorkflowKey,
            DisplayName = "State keeping test",
            BusinessObjectType = ObjectType,
            IsActive = true
        };
        dbContext.WorkflowDefinitions.Add(definition);
        dbContext.SaveChanges();

        var version = new WorkflowDefinitionVersion
        {
            WorkflowDefinitionId = definition.Id,
            VersionNumber = 1,
            IsActive = true,
            CreatedAtUtc = Now.AddDays(-1)
        };
        dbContext.WorkflowDefinitionVersions.Add(version);
        dbContext.SaveChanges();

        dbContext.WorkflowStateDefinitions.AddRange(
            State(version.Id, "Submitted", terminal: false),
            State(version.Id, "Done", terminal: true));
        dbContext.WorkflowActionDefinitions.AddRange(
            Action(version.Id, "Inform", 10),
            Action(version.Id, "Confirm", 20));
        dbContext.WorkflowTransitionDefinitions.AddRange(
            Transition(version.Id, "Inform", "Submitted", "RouteInformational"),
            Transition(version.Id, "Confirm", "Done", "RouteDone"));
        var informRoute = Route(version.Id, "RouteInformational", "StateKeepingInformational", "Informational: {{accountName}}");
        var doneRoute = Route(version.Id, "RouteDone", "StateKeepingDone", "Done: {{accountName}}");
        dbContext.NotificationRoutes.AddRange(informRoute, doneRoute);
        dbContext.WorkflowInstances.Add(new WorkflowInstance
        {
            WorkflowInstanceId = InstanceId,
            WorkflowKey = WorkflowKey,
            WorkflowDefinitionVersionId = version.Id,
            BusinessObjectType = ObjectType,
            BusinessObjectId = "1",
            CurrentStateKey = "Submitted",
            Status = "Active",
            StartedAtUtc = Now.AddMinutes(-30),
            StartedByUserId = "writer1",
            Version = 1
        });
        dbContext.SaveChanges();

        dbContext.NotificationRecipientRules.AddRange(
            RequestInputRule(informRoute.Id),
            RequestInputRule(doneRoute.Id));
        dbContext.SaveChanges();
    }

    private static WorkflowStateDefinition State(long versionId, string key, bool terminal) => new()
    {
        WorkflowDefinitionVersionId = versionId,
        StateKey = key,
        DisplayName = key,
        IsTerminal = terminal,
        IsActive = true
    };

    private static WorkflowActionDefinition Action(long versionId, string key, int sortOrder) => new()
    {
        WorkflowDefinitionVersionId = versionId,
        ActionKey = key,
        DisplayName = key,
        SectionKey = "Main",
        ButtonKey = key + "Button",
        ButtonLabel = key,
        Intent = key,
        SortOrder = sortOrder,
        IsActive = true
    };

    private static WorkflowTransitionDefinition Transition(long versionId, string actionKey, string toState, string routeKey) => new()
    {
        WorkflowDefinitionVersionId = versionId,
        FromStateKey = "Submitted",
        ActionKey = actionKey,
        AllowedRoleKey = "Write",
        ToStateKey = toState,
        NotificationRouteKey = routeKey,
        AuditLabel = actionKey,
        IsActive = true
    };

    private static NotificationRoute Route(long versionId, string routeKey, string type, string subject) => new()
    {
        WorkflowDefinitionVersionId = versionId,
        RouteKey = routeKey,
        NotificationType = type,
        SubjectTemplate = subject,
        BodyTemplate = "Account {{accountName}}",
        HtmlBodyTemplate = "<p>Account {{accountName}}</p>",
        IsActive = true
    };

    private static NotificationRecipientRule RequestInputRule(long routeId) => new()
    {
        NotificationRouteId = routeId,
        RecipientKind = RecipientKinds.To,
        SourceType = RecipientSourceTypes.RequestInput,
        SourceKey = "lists",
        SortOrder = 10,
        IsRequired = true,
        IsActive = true
    };

    private static WorkflowExecutionRequest Request(string actionKey, int expectedVersion, string idempotencyKey, string address)
    {
        var templateData = new Dictionary<string, string?>(StringComparer.Ordinal) { ["accountName"] = "Account A" };
        var notificationInput = new WorkflowNotificationInput
        {
            RecipientInputs = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
            {
                ["lists"] = [new(null, address, null)]
            },
            TemplateData = templateData
        };

        var envelope = WorkflowActionEnvelopeTestData.DeserializeValid();
        envelope.BusinessObject = new WorkflowBusinessObject { ObjectType = ObjectType, ObjectId = "1" };
        envelope.Actor!.UserId = "writer1";
        envelope.Actor.Roles = ["Write"];
        envelope.UiAction!.SectionKey = "Main";
        envelope.UiAction.ButtonKey = actionKey + "Button";
        envelope.DataSave!.SectionKey = "Main";
        envelope.DataSave.Payload = JsonDocument.Parse("""{"note":"state keeping"}""").RootElement.Clone();
        envelope.Workflow!.WorkflowInstanceId = InstanceId;
        envelope.Workflow.WorkflowKey = WorkflowKey;
        envelope.Workflow.WorkflowDefinitionVersion = 1;
        envelope.Workflow.ActionKey = actionKey;
        envelope.Workflow.ExpectedCurrentState = "Submitted";
        envelope.Workflow.ExpectedInstanceVersion = expectedVersion;
        envelope.Workflow.ExpectedCurrentTaskId = null;
        envelope.Workflow.ExpectedCurrentTaskKey = null;
        envelope.Workflow.IdempotencyKey = idempotencyKey;
        // The engine renders the templates of an executed action from the notification context.
        envelope.Workflow.NotificationContext = new Dictionary<string, string?>(templateData, StringComparer.Ordinal);
        envelope.Workflow.NotificationRecipients = null;

        var normalized = new WorkflowActionEnvelopeNormalizer().Normalize(envelope, null, null);
        return new WorkflowExecutionRequest(
            normalized,
            new CanonicalRequestHasher().ComputeHash(normalized.Envelope, notificationInput),
            AdminOverride: false)
        {
            NotificationInput = notificationInput
        };
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
}
