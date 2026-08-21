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
public sealed class WorkflowTerminalStateTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2030-01-15T15:30:00Z");

    [Fact]
    public void NonliteralTerminalStateCompletesRuntimeAndExposesNoFurtherWork()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var definition = new WorkflowDefinition
        {
            WorkflowKey = "TerminalMetadataTest",
            DisplayName = "Terminal metadata test",
            BusinessObjectType = "TerminalMetadataObject",
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
            new WorkflowStateDefinition
            {
                WorkflowDefinitionVersionId = version.Id,
                StateKey = "SUBMITTED",
                DisplayName = "Submitted",
                IsTerminal = false,
                IsActive = true
            },
            new WorkflowStateDefinition
            {
                WorkflowDefinitionVersionId = version.Id,
                StateKey = "NOT ACCEPTED",
                DisplayName = "Not accepted",
                IsTerminal = true,
                IsActive = true
            });
        dbContext.WorkflowActionDefinitions.Add(new WorkflowActionDefinition
        {
            WorkflowDefinitionVersionId = version.Id,
            ActionKey = "Reject",
            DisplayName = "Reject",
            SectionKey = "Decision",
            ButtonKey = "RejectButton",
            ButtonLabel = "Reject",
            Intent = "Reject",
            SortOrder = 10,
            IsActive = true
        });
        dbContext.WorkflowTransitionDefinitions.Add(new WorkflowTransitionDefinition
        {
            WorkflowDefinitionVersionId = version.Id,
            FromStateKey = "SUBMITTED",
            ActionKey = "Reject",
            AllowedRoleKey = "Reviewer",
            ToStateKey = "NOT ACCEPTED",
            AuditLabel = "Rejected",
            IsActive = true
        });
        dbContext.WorkflowInstances.Add(new WorkflowInstance
        {
            WorkflowInstanceId = "WF-TERMINAL-1",
            WorkflowKey = definition.WorkflowKey,
            WorkflowDefinitionVersionId = version.Id,
            BusinessObjectType = definition.BusinessObjectType,
            BusinessObjectId = "1",
            CurrentStateKey = "SUBMITTED",
            Status = "Active",
            StartedAtUtc = Now.AddMinutes(-30),
            StartedByUserId = "reviewer1",
            Version = 1
        });
        dbContext.SaveChanges();

        var envelope = WorkflowActionEnvelopeTestData.DeserializeValid();
        envelope.BusinessObject = new WorkflowBusinessObject
        {
            ObjectType = definition.BusinessObjectType,
            ObjectId = "1"
        };
        envelope.Actor!.UserId = "reviewer1";
        envelope.Actor.Roles = ["Reviewer"];
        envelope.UiAction!.SectionKey = "Decision";
        envelope.UiAction.ButtonKey = "RejectButton";
        envelope.DataSave!.SectionKey = "Decision";
        envelope.DataSave.Payload = JsonDocument.Parse("""{"decision":"reject"}""").RootElement.Clone();
        envelope.Workflow!.WorkflowInstanceId = "WF-TERMINAL-1";
        envelope.Workflow.WorkflowKey = definition.WorkflowKey;
        envelope.Workflow.WorkflowDefinitionVersion = 1;
        envelope.Workflow.ActionKey = "Reject";
        envelope.Workflow.ExpectedCurrentState = "SUBMITTED";
        envelope.Workflow.ExpectedInstanceVersion = 1;
        envelope.Workflow.ExpectedCurrentTaskId = null;
        envelope.Workflow.ExpectedCurrentTaskKey = null;
        envelope.Workflow.IdempotencyKey = "idem-terminal-metadata-test";
        envelope.Workflow.NotificationContext = null;
        envelope.Workflow.NotificationRecipients = new NotificationRecipients();

        var normalized = new WorkflowActionEnvelopeNormalizer().Normalize(envelope, null, null);
        var request = new WorkflowExecutionRequest(
            normalized,
            new CanonicalRequestHasher().ComputeHash(normalized.Envelope),
            AdminOverride: false);

        var result = CreateService(dbContext).Execute(request);

        Assert.True(result.Result.Success);
        dbContext.ChangeTracker.Clear();
        var instance = dbContext.WorkflowInstances.Single(row => row.WorkflowInstanceId == "WF-TERMINAL-1");
        Assert.Equal("NOT ACCEPTED", instance.CurrentStateKey);
        Assert.Equal("Completed", instance.Status);
        Assert.Equal(Now, instance.CompletedAtUtc);
        Assert.Empty(dbContext.WorkflowTasks.Where(row => row.WorkflowInstanceId == instance.WorkflowInstanceId && row.Status == "Pending"));
        Assert.NotNull(result.WorkflowContext);
        Assert.Empty(result.WorkflowContext.AvailableActions);
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
