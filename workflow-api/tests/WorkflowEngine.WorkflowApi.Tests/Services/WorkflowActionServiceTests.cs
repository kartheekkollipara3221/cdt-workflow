using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.Sla;
using WorkflowEngine.Application.WorkflowExecution;
using WorkflowEngine.WorkflowApi.Models;
using WorkflowEngine.WorkflowApi.Services;

namespace WorkflowEngine.WorkflowApi.Tests.Services;

public sealed class WorkflowActionServiceTests
{
    [Fact]
    public void ExecuteWorkflowActionPreservesNotificationRecipientsAndUsesTrustedActor()
    {
        var executionService = new RecordingWorkflowExecutionService(new WorkflowActionResultEnvelope
        {
            Result = new WorkflowActionResult
            {
                Success = true,
                ResultCode = "Success",
                WorkflowInstanceId = "WF-PT-1001",
                ActionKey = "SubmitForChecker",
                CorrelationId = "api-action-recipients"
            },
            WorkflowContext = new WorkflowContext()
        });
        var recipients = new NotificationRecipients
        {
            ToEmailAddresses =
            [
                " primary@example.com ",
                "duplicate@example.com",
                "duplicate@example.com"
            ],
            CcEmailAddresses =
            [
                " copied-first@example.com ",
                "copied-second@example.com"
            ]
        };
        var service = CreateService(
            new StubActorResolver(new WorkflowActorSnapshot("trusted-user", "Trusted User", ["Maker"])),
            executionService);

        service.ExecuteWorkflowAction(new DefaultHttpContext(), new WorkflowActionRequest
        {
            WorkflowInstanceId = "WF-PT-1001",
            ActionKey = "SubmitForChecker",
            NotificationRecipients = recipients
        });

        var envelope = Assert.IsType<WorkflowActionEnvelope>(
            executionService.LastRequest?.Normalized.Envelope);
        Assert.Same(recipients, envelope.Workflow?.NotificationRecipients);
        Assert.Equal(
            [" primary@example.com ", "duplicate@example.com", "duplicate@example.com"],
            envelope.Workflow?.NotificationRecipients?.ToEmailAddresses);
        Assert.Equal(
            [" copied-first@example.com ", "copied-second@example.com"],
            envelope.Workflow?.NotificationRecipients?.CcEmailAddresses);
        Assert.Equal("trusted-user", envelope.Actor?.UserId);
        Assert.Equal("Trusted User", envelope.Actor?.DisplayName);
        Assert.Equal(["Maker"], envelope.Actor?.Roles);
        Assert.Null(envelope.Workflow?.NotificationContext);
    }

    [Fact]
    public void ExecuteWorkflowActionBuildsWorkflowOnlyExecutionRequest()
    {
        var executionService = new RecordingWorkflowExecutionService(new WorkflowActionResultEnvelope
        {
            Result = new WorkflowActionResult
            {
                Success = true,
                ResultCode = "Success",
                WorkflowInstanceId = "WF-PT-1001",
                ActionKey = "CheckerSendToTradingDesk",
                CorrelationId = "api-action-1",
                Notification = new WorkflowNotificationSummary
                {
                    Queued = true,
                    NotificationType = "WorkflowTaskAssigned",
                    RecipientRole = "TradingDesk"
                }
            },
            WorkflowContext = new WorkflowContext()
        });
        var dispatcher = new RecordingWorkflowActionNotificationDispatcher(
            WorkflowActionNotificationDispatchResult.Delivered([]));
        var service = CreateService(
            new StubActorResolver(new WorkflowActorSnapshot("checker1", "Checker One", ["Checker"])),
            executionService,
            dispatcher);

        var response = service.ExecuteWorkflowAction(new DefaultHttpContext(), new WorkflowActionRequest
        {
            WorkflowInstanceId = " WF-PT-1001 ",
            ActionKey = " CheckerSendToTradingDesk ",
            AssignedRoleKey = " TradingDeskTaxable ",
            IdempotencyKey = " application-action-1 ",
            SlaInput = new WorkflowSlaInput { TargetDate = new DateOnly(2030, 1, 20) },
            NotificationContext = new Dictionary<string, string?>
            {
                ["submitterDisplayName"] = " Kollipara, Kartheek ",
                ["deskName"] = "Taxable",
                ["emptyValue"] = " "
            }
        });

        Assert.True(response.Result.ActionResult.Success);
        Assert.Same(executionService.Response.Result, response.Result.ActionResult);
        Assert.Same(executionService.Response.WorkflowContext, response.Result.WorkflowContext);
        Assert.NotNull(executionService.LastRequest);
        Assert.False(executionService.LastRequest.AdminOverride);
        Assert.True(executionService.LastRequest.DeriveIdentityFromPersistence);
        Assert.False(executionService.LastRequest.PersistSectionData);
        Assert.False(executionService.LastRequest.ValidateActionEcho);

        var envelope = executionService.LastRequest.Normalized.Envelope;
        Assert.Equal("WF-PT-1001", envelope.Workflow?.WorkflowInstanceId);
        Assert.Equal("CheckerSendToTradingDesk", envelope.Workflow?.ActionKey);
        Assert.Equal("TradingDeskTaxable", envelope.Workflow?.AssignedRoleKey);
        Assert.Equal(new DateOnly(2030, 1, 20), envelope.Workflow?.SlaInput?.TargetDate);
        Assert.Equal("Kollipara, Kartheek", envelope.Workflow?.NotificationContext?["submitterDisplayName"]);
        Assert.Equal("Taxable", envelope.Workflow?.NotificationContext?["deskName"]);
        Assert.False(envelope.Workflow?.NotificationContext?.ContainsKey("emptyValue") ?? true);
        Assert.Equal("checker1", envelope.Actor?.UserId);
        Assert.Equal("Checker One", envelope.Actor?.DisplayName);
        Assert.Equal(["Checker"], envelope.Actor?.Roles);
        Assert.Null(envelope.BusinessObject);
        Assert.Null(envelope.UiAction);
        Assert.Null(envelope.DataSave);
        Assert.Null(envelope.Workflow?.WorkflowKey);
        Assert.Null(envelope.Workflow?.WorkflowDefinitionVersion);
        Assert.Null(envelope.Workflow?.ExpectedCurrentState);
        Assert.Null(envelope.Workflow?.ExpectedCurrentTaskId);
        Assert.Null(envelope.Workflow?.ExpectedCurrentTaskKey);
        Assert.Null(envelope.Workflow?.ExpectedInstanceVersion);
        Assert.Null(envelope.Workflow?.ActionReason);
        Assert.False(string.IsNullOrWhiteSpace(envelope.Request?.ClientActionId));
        Assert.Equal(envelope.Request?.ClientActionId, envelope.Request?.CorrelationId);
        Assert.Equal("application-action-1", envelope.Workflow?.IdempotencyKey);
        Assert.Equal(
            new CanonicalRequestHasher().ComputeHash(envelope),
            executionService.LastRequest.RequestHash);
        Assert.Equal("WF-PT-1001", dispatcher.LastWorkflowInstanceId);
        Assert.Equal("api-action-1", dispatcher.LastCorrelationId);
        Assert.Equal(1, dispatcher.DispatchCount);
    }

    [Fact]
    public void ActionlessRequestSynchronizesSlaThroughTheSameServicePath()
    {
        var executionService = new RecordingWorkflowExecutionService(new WorkflowActionResultEnvelope());
        var synchronizationService = new RecordingWorkflowSlaSynchronizationService(
            new WorkflowSlaSynchronizationResult(
                Changed: true,
                ScheduleVersion: 2,
                TargetDate: new DateOnly(2030, 1, 25),
                DueAtUtc: DateTimeOffset.Parse("2030-01-26T14:00:00Z"),
                NextReminderAtUtc: DateTimeOffset.Parse("2030-01-25T14:00:00Z")));
        var contextResponse = new WorkflowContextResponse();
        contextResponse.WorkflowContext.Workflow.WorkflowInstanceId = "WF-PT-1001";
        contextResponse.WorkflowContext.Workflow.WorkflowInstanceVersion = 3;
        contextResponse.WorkflowContext.Workflow.CurrentState = "TradingDeskReview";
        contextResponse.WorkflowContext.Sla.ScheduleVersion = 2;
        var contextReader = new RecordingWorkflowContextReader(contextResponse);
        var service = CreateService(
            new StubActorResolver(new WorkflowActorSnapshot("maker1", "Maker One", ["Maker"])),
            executionService,
            synchronizationService: synchronizationService,
            contextReader: contextReader);

        var response = service.ExecuteWorkflowAction(new DefaultHttpContext(), new WorkflowActionRequest
        {
            WorkflowInstanceId = "WF-PT-1001",
            ActionKey = null,
            IdempotencyKey = "application-save-2",
            SlaInput = new WorkflowSlaInput
            {
                TargetDate = new DateOnly(2030, 1, 25)
            }
        });

        Assert.Null(executionService.LastRequest);
        Assert.NotNull(synchronizationService.LastRequest);
        Assert.Equal("WF-PT-1001", synchronizationService.LastRequest.WorkflowInstanceId);
        Assert.Null(synchronizationService.LastRequest.WorkflowTaskId);
        Assert.Equal(new DateOnly(2030, 1, 25), synchronizationService.LastRequest.TargetDate);
        Assert.Equal("application-save-2", synchronizationService.LastRequest.IdempotencyKey);
        Assert.Equal("maker1", synchronizationService.LastRequest.ActorUserId);
        Assert.Equal("SlaSynchronized", response.Result.ActionResult.ResultCode);
        Assert.Equal(string.Empty, response.Result.ActionResult.ActionKey);
        Assert.Same(contextResponse.WorkflowContext, response.Result.WorkflowContext);
        Assert.Equal(3, response.Result.WorkflowContext?.Workflow.WorkflowInstanceVersion);
        Assert.Equal(2, response.Result.WorkflowContext?.Sla.ScheduleVersion);
        Assert.Equal(0, synchronizationService.CompleteCount);
    }

    [Fact]
    public void ExecuteWorkflowActionDoesNotDispatchNotificationsWhenActionQueuedNone()
    {
        var executionService = new RecordingWorkflowExecutionService(new WorkflowActionResultEnvelope
        {
            Result = new WorkflowActionResult
            {
                Success = true,
                ResultCode = "Success",
                WorkflowInstanceId = "WF-PT-1001",
                ActionKey = "SaveDraft",
                CorrelationId = "api-action-2",
                Notification = new WorkflowNotificationSummary { Queued = false }
            },
            WorkflowContext = new WorkflowContext()
        });
        var dispatcher = new RecordingWorkflowActionNotificationDispatcher(
            WorkflowActionNotificationDispatchResult.Delivered([]));
        var service = CreateService(
            new StubActorResolver(new WorkflowActorSnapshot("maker1", "Maker One", ["Maker"])),
            executionService,
            dispatcher);

        var response = service.ExecuteWorkflowAction(new DefaultHttpContext(), new WorkflowActionRequest
        {
            WorkflowInstanceId = "WF-PT-1001",
            ActionKey = "SaveDraft"
        });

        Assert.True(response.Result.ActionResult.Success);
        Assert.Equal(0, dispatcher.DispatchCount);
    }

    [Fact]
    public void ExecuteWorkflowActionReturnsFailureWhenCreatedNotificationDeliveryFails()
    {
        var executionService = new RecordingWorkflowExecutionService(new WorkflowActionResultEnvelope
        {
            Result = new WorkflowActionResult
            {
                Success = true,
                ResultCode = "Success",
                WorkflowInstanceId = "WF-PT-1001",
                ActionKey = "SubmitForChecker",
                CorrelationId = "api-action-3",
                Notification = new WorkflowNotificationSummary
                {
                    Queued = true,
                    NotificationType = "WorkflowTaskAssigned",
                    RecipientRole = "Checker"
                }
            },
            WorkflowContext = new WorkflowContext()
        });
        var dispatcher = new RecordingWorkflowActionNotificationDispatcher(
            WorkflowActionNotificationDispatchResult.Failed("SMTP delivery failed for notification 42."));
        var service = CreateService(
            new StubActorResolver(new WorkflowActorSnapshot("maker1", "Maker One", ["Maker"])),
            executionService,
            dispatcher);

        var response = service.ExecuteWorkflowAction(new DefaultHttpContext(), new WorkflowActionRequest
        {
            WorkflowInstanceId = "WF-PT-1001",
            ActionKey = "SubmitForChecker"
        });

        Assert.False(response.Result.ActionResult.Success);
        Assert.Equal("NotificationDeliveryFailed", response.Result.ActionResult.ResultCode);
        Assert.Equal("SMTP delivery failed for notification 42.", response.Result.ActionResult.Message);
        Assert.Equal("WF-PT-1001", dispatcher.LastWorkflowInstanceId);
        Assert.Equal("api-action-3", dispatcher.LastCorrelationId);
    }

    [Fact]
    public void ExecuteWorkflowActionThrowsRequestExceptionWhenRequestIsMissing()
    {
        var service = CreateService(
            new StubActorResolver(new WorkflowActorSnapshot("checker1", "Checker One", ["Checker"])),
            new RecordingWorkflowExecutionService(new WorkflowActionResultEnvelope()));

        var exception = Assert.Throws<WorkflowApiRequestException>(() =>
            service.ExecuteWorkflowAction(new DefaultHttpContext(), request: null));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal("Invalid workflow action request.", exception.Title);
        Assert.Equal("Workflow action request body is required.", exception.Detail);
    }

    [Theory]
    [InlineData(null, "SubmitForChecker", "workflowInstanceId is required.")]
    [InlineData(" ", "SubmitForChecker", "workflowInstanceId is required.")]
    [InlineData("WF-PT-1001", null, "actionKey is required.")]
    [InlineData("WF-PT-1001", " ", "actionKey is required.")]
    public void ExecuteWorkflowActionThrowsRequestExceptionWhenRequiredFieldsAreMissing(
        string? workflowInstanceId,
        string? actionKey,
        string expectedDetail)
    {
        var executionService = new RecordingWorkflowExecutionService(new WorkflowActionResultEnvelope());
        var service = CreateService(
            new StubActorResolver(new WorkflowActorSnapshot("checker1", "Checker One", ["Checker"])),
            executionService);

        var exception = Assert.Throws<WorkflowApiRequestException>(() =>
            service.ExecuteWorkflowAction(new DefaultHttpContext(), new WorkflowActionRequest
            {
                WorkflowInstanceId = workflowInstanceId,
                ActionKey = actionKey
            }));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal("Invalid workflow action request.", exception.Title);
        Assert.Equal(expectedDetail, exception.Detail);
        Assert.Null(executionService.LastRequest);
    }

    [Fact]
    public void ExecuteWorkflowActionTrimsBlankAssignedRoleKeyToNull()
    {
        var executionService = new RecordingWorkflowExecutionService(new WorkflowActionResultEnvelope
        {
            Result = new WorkflowActionResult
            {
                Success = true,
                ResultCode = "Success",
                WorkflowInstanceId = "WF-PT-1001",
                ActionKey = "SaveDraft",
                CorrelationId = "api-action-blank-assignment",
                Notification = new WorkflowNotificationSummary { Queued = false }
            },
            WorkflowContext = new WorkflowContext()
        });
        var service = CreateService(
            new StubActorResolver(new WorkflowActorSnapshot("maker1", "Maker One", ["Maker"])),
            executionService);

        service.ExecuteWorkflowAction(new DefaultHttpContext(), new WorkflowActionRequest
        {
            WorkflowInstanceId = "WF-PT-1001",
            ActionKey = "SaveDraft",
            AssignedRoleKey = " "
        });

        Assert.NotNull(executionService.LastRequest);
        Assert.Null(executionService.LastRequest.Normalized.Envelope.Workflow?.AssignedRoleKey);
    }

    [Fact]
    public void ExecuteWorkflowActionMergesDuplicateNotificationContextKeysAfterTrim()
    {
        var executionService = new RecordingWorkflowExecutionService(new WorkflowActionResultEnvelope
        {
            Result = new WorkflowActionResult
            {
                Success = true,
                ResultCode = "Success",
                WorkflowInstanceId = "WF-PT-1001",
                ActionKey = "SaveDraft",
                CorrelationId = "api-action-duplicate-context",
                Notification = new WorkflowNotificationSummary { Queued = false }
            },
            WorkflowContext = new WorkflowContext()
        });
        var service = CreateService(
            new StubActorResolver(new WorkflowActorSnapshot("maker1", "Maker One", ["Maker"])),
            executionService);

        service.ExecuteWorkflowAction(new DefaultHttpContext(), new WorkflowActionRequest
        {
            WorkflowInstanceId = "WF-PT-1001",
            ActionKey = "SaveDraft",
            NotificationContext = new Dictionary<string, string?>
            {
                ["deskName"] = "Original",
                [" deskName "] = " Taxable ",
                ["emptyValue"] = " "
            }
        });

        var context = Assert.IsType<Dictionary<string, string?>>(
            executionService.LastRequest?.Normalized.Envelope.Workflow?.NotificationContext);
        var item = Assert.Single(context);
        Assert.Equal("deskName", item.Key);
        Assert.Equal("Taxable", item.Value);
        Assert.False(context.ContainsKey("emptyValue"));
    }

    private static WorkflowActionService CreateService(
        IWorkflowActorResolver actorResolver,
        IWorkflowExecutionService workflowExecutionService,
        IWorkflowActionNotificationDispatcher? notificationDispatcher = null,
        IWorkflowSlaSynchronizationService? synchronizationService = null,
        IWorkflowContextReader? contextReader = null)
    {
        return new WorkflowActionService(
            actorResolver,
            new WorkflowActionEnvelopeNormalizer(),
            new CanonicalRequestHasher(),
            workflowExecutionService,
            synchronizationService ?? new RecordingWorkflowSlaSynchronizationService(
                new WorkflowSlaSynchronizationResult(false, 0, null, null, null)),
            contextReader ?? new RecordingWorkflowContextReader(new WorkflowContextResponse()),
            new WorkflowActionResultBuilder(),
            notificationDispatcher ?? new RecordingWorkflowActionNotificationDispatcher(
                WorkflowActionNotificationDispatchResult.Delivered([])));
    }

    private sealed class RecordingWorkflowSlaSynchronizationService(
        WorkflowSlaSynchronizationResult response) : IWorkflowSlaSynchronizationService
    {
        public WorkflowSlaSynchronizationRequest? LastRequest { get; private set; }

        public int CompleteCount { get; private set; }

        public WorkflowSlaSynchronizationResult Synchronize(WorkflowSlaSynchronizationRequest request)
        {
            LastRequest = request;
            return response;
        }

        public WorkflowSlaSynchronizationResult Complete(WorkflowSlaCompletionRequest request)
        {
            CompleteCount++;
            return response;
        }
    }

    private sealed class RecordingWorkflowContextReader(
        WorkflowContextResponse response) : IWorkflowContextReader
    {
        public WorkflowContextReadRequest? LastRequest { get; private set; }

        public WorkflowContextResponse Read(WorkflowContextReadRequest request)
        {
            LastRequest = request;
            return response;
        }
    }

    private sealed class StubActorResolver(WorkflowActorSnapshot actor) : IWorkflowActorResolver
    {
        public WorkflowActorSnapshot Resolve(HttpContext httpContext)
        {
            return actor;
        }
    }

    private sealed class RecordingWorkflowExecutionService(WorkflowActionResultEnvelope response) : IWorkflowExecutionService
    {
        public WorkflowActionResultEnvelope Response { get; } = response;

        public WorkflowExecutionRequest? LastRequest { get; private set; }

        public WorkflowActionResultEnvelope Execute(WorkflowExecutionRequest request)
        {
            LastRequest = request;

            return Response;
        }
    }

    private sealed class RecordingWorkflowActionNotificationDispatcher(
        WorkflowActionNotificationDispatchResult result) : IWorkflowActionNotificationDispatcher
    {
        public int DispatchCount { get; private set; }

        public string? LastWorkflowInstanceId { get; private set; }

        public string? LastCorrelationId { get; private set; }

        public WorkflowActionNotificationDispatchResult DispatchCreatedNotifications(
            string workflowInstanceId,
            string correlationId)
        {
            DispatchCount++;
            LastWorkflowInstanceId = workflowInstanceId;
            LastCorrelationId = correlationId;

            return result;
        }
    }
}
