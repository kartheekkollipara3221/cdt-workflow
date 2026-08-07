using System.Text.Json;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Tests;

public sealed class WorkflowContextSerializationTests
{
    [Fact]
    public void WorkflowContextSerializesRequiredBlocksAndNullFields()
    {
        var response = new WorkflowContextResponse
        {
            WorkflowContext = new WorkflowContext
            {
                Workflow = new WorkflowContextWorkflow
                {
                    WorkflowKey = "PortfolioTrade",
                    WorkflowInstanceId = "WF-PT-CTX",
                    WorkflowDefinitionVersion = 1,
                    WorkflowInstanceVersion = 7,
                    CurrentState = "CheckerReview",
                    CurrentStateLabel = "Checker Review",
                    Status = "Active",
                    StartedAtUtc = DateTimeOffset.Parse("2026-05-07T12:00:00Z")
                },
                BusinessObject = new WorkflowContextBusinessObject
                {
                    ObjectType = "PortfolioTrade",
                    ObjectId = "PT-CTX"
                },
                ActorAccess = new WorkflowContextActorAccess
                {
                    UserId = "checker1",
                    DisplayName = "checker1",
                    EffectiveRoles = ["Checker"],
                    CanAct = true
                },
                Sla = new WorkflowContextSla
                {
                    Status = "OnTrack",
                    DueAtUtc = DateTimeOffset.Parse("2030-01-21T14:00:00Z"),
                    CalculationMode = "ApplicationTargetDate",
                    TargetDate = new DateOnly(2030, 1, 20),
                    ScheduleVersion = 2,
                    NextReminderAtUtc = DateTimeOffset.Parse("2030-01-20T14:00:00Z")
                },
                AvailableActions =
                [
                    new AvailableWorkflowAction
                    {
                        ActionKey = "CheckerSendToTradingDesk",
                        SectionKey = "Checker",
                        ButtonKey = "CheckerPrimary",
                        ButtonLabel = "Send to Trading Desk",
                        Intent = "Approve",
                        RequestDefaults = new WorkflowActionRequestDefaults
                        {
                            WorkflowKey = "PortfolioTrade",
                            WorkflowInstanceId = "WF-PT-CTX",
                            WorkflowDefinitionVersion = 1,
                            ExpectedCurrentState = "CheckerReview",
                            ExpectedCurrentTaskId = "11",
                            ExpectedCurrentTaskKey = "CheckerReviewTask",
                            ExpectedInstanceVersion = 7
                        }
                    }
                ],
                ServerTimeUtc = DateTimeOffset.Parse("2026-05-07T12:01:00Z")
            }
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response, WorkflowJson.Options));
        var context = document.RootElement.GetProperty("workflowContext");

        Assert.True(context.TryGetProperty("workflow", out _));
        Assert.True(context.TryGetProperty("businessObject", out _));
        Assert.True(context.TryGetProperty("actorAccess", out _));
        Assert.True(context.TryGetProperty("currentTask", out var currentTask));
        Assert.Equal(JsonValueKind.Null, currentTask.ValueKind);
        Assert.Equal("OnTrack", context.GetProperty("sla").GetProperty("status").GetString());
        Assert.Equal(
            "ApplicationTargetDate",
            context.GetProperty("sla").GetProperty("calculationMode").GetString());
        Assert.Equal("2030-01-20", context.GetProperty("sla").GetProperty("targetDate").GetString());
        Assert.Equal(2, context.GetProperty("sla").GetProperty("scheduleVersion").GetInt32());
        Assert.Equal(
            DateTimeOffset.Parse("2030-01-20T14:00:00Z"),
            context.GetProperty("sla").GetProperty("nextReminderAtUtc").GetDateTimeOffset());
        Assert.False(context.GetProperty("availableActions")[0].TryGetProperty("enabledByWorkflow", out _));
        Assert.False(context.GetProperty("availableActions")[0].TryGetProperty("disabledReason", out _));
    }

    [Fact]
    public void WorkflowContextCanRepresentNoSlaAndNoActions()
    {
        var response = new WorkflowContextResponse
        {
            WorkflowContext = new WorkflowContext
            {
                ActorAccess = new WorkflowContextActorAccess
                {
                    UserId = "maker1",
                    DisplayName = "maker1",
                    EffectiveRoles = ["Maker"],
                    CanAct = false,
                    ReadOnlyReason = "Workflow is completed."
                },
                Sla = new WorkflowContextSla(),
                ServerTimeUtc = DateTimeOffset.Parse("2026-05-07T12:01:00Z")
            }
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response, WorkflowJson.Options));
        var context = document.RootElement.GetProperty("workflowContext");

        Assert.Equal("NoSla", context.GetProperty("sla").GetProperty("status").GetString());
        Assert.Equal(0, context.GetProperty("availableActions").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, context.GetProperty("currentTask").ValueKind);
    }
}
