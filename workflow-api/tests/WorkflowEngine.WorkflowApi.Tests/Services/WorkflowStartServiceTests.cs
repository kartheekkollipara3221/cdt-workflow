using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.WorkflowStart;
using WorkflowEngine.WorkflowApi.Models;
using WorkflowEngine.WorkflowApi.Services;
using ApiWorkflowStartService = WorkflowEngine.WorkflowApi.Services.WorkflowStartService;

namespace WorkflowEngine.WorkflowApi.Tests.Services;

public sealed class WorkflowStartServiceTests
{
    [Fact]
    public void StartWorkflowPreservesNotificationRecipientsAndUsesTrustedActor()
    {
        var applicationStart = new RecordingStartWorkflowService(SuccessResult());
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
            applicationStart,
            new RecordingWorkflowContextReader(new WorkflowContextResponse()));

        service.StartWorkflow(new DefaultHttpContext(), new WorkflowStartRequest
        {
            WorkflowKey = "PortfolioTrade",
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-1001",
            InitialActionKey = "SubmitForChecker",
            NotificationRecipients = recipients
        });

        Assert.NotNull(applicationStart.LastRequest);
        Assert.Same(recipients, applicationStart.LastRequest.NotificationRecipients);
        Assert.Equal(
            [" primary@example.com ", "duplicate@example.com", "duplicate@example.com"],
            applicationStart.LastRequest.NotificationRecipients?.ToEmailAddresses);
        Assert.Equal(
            [" copied-first@example.com ", "copied-second@example.com"],
            applicationStart.LastRequest.NotificationRecipients?.CcEmailAddresses);
        Assert.Equal("trusted-user", applicationStart.LastRequest.StartedByUserId);
        Assert.Equal("Trusted User", applicationStart.LastRequest.StartedByDisplayName);
        Assert.Equal(["Maker"], applicationStart.LastRequest.StartedByRoles);
        Assert.Null(applicationStart.LastRequest.NotificationContext);
    }

    [Fact]
    public void StartWorkflowUsesTrustedActorAndReturnsRefreshedContext()
    {
        var expectedContext = new WorkflowContextResponse();
        expectedContext.WorkflowContext.Workflow.WorkflowInstanceId = "WF-PT-1001";
        var applicationStart = new RecordingStartWorkflowService(new StartWorkflowResult(
            new WorkflowActionResultEnvelope
            {
                Result = new WorkflowActionResult
                {
                    Success = true,
                    ResultCode = "Success",
                    WorkflowInstanceId = "WF-PT-1001"
                }
            },
            ResetCounts: null));
        var contextReader = new RecordingWorkflowContextReader(expectedContext);
        var service = CreateService(
            new StubActorResolver(new WorkflowActorSnapshot("maker1", "Maker One", ["Maker"])),
            applicationStart,
            contextReader);

        var response = service.StartWorkflow(new DefaultHttpContext(), new WorkflowStartRequest
        {
            WorkflowKey = " PortfolioTrade ",
            BusinessObjectType = " PortfolioTrade ",
            BusinessObjectId = " PT-1001 "
        });

        Assert.True(response.Result.ActionResult.Success);
        Assert.Same(applicationStart.Response.Envelope.Result, response.Result.ActionResult);
        Assert.Same(expectedContext.WorkflowContext, response.Result.WorkflowContext);
        Assert.NotNull(applicationStart.LastRequest);
        Assert.Equal("PortfolioTrade", applicationStart.LastRequest.WorkflowKey);
        Assert.Equal("PortfolioTrade", applicationStart.LastRequest.BusinessObjectType);
        Assert.Equal("PT-1001", applicationStart.LastRequest.BusinessObjectId);
        Assert.Equal("maker1", applicationStart.LastRequest.StartedByUserId);
        Assert.False(applicationStart.LastRequest.Reset);
        Assert.NotNull(contextReader.LastRequest);
        Assert.Equal("WF-PT-1001", contextReader.LastRequest.WorkflowInstanceId);
        Assert.Equal("maker1", contextReader.LastRequest.UserId);
        Assert.Equal(["Maker"], contextReader.LastRequest.Roles);
    }

    [Fact]
    public void StartWorkflowPassesInitialActionAndActorEvidenceToApplicationStart()
    {
        var expectedContext = new WorkflowContextResponse();
        expectedContext.WorkflowContext.Workflow.WorkflowInstanceId = "WF-PT-1001";
        var applicationStart = new RecordingStartWorkflowService(new StartWorkflowResult(
            new WorkflowActionResultEnvelope
            {
                Result = new WorkflowActionResult
                {
                    Success = true,
                    ResultCode = "Success",
                    WorkflowInstanceId = "WF-PT-1001"
                }
            },
            ResetCounts: null));
        var service = CreateService(
            new StubActorResolver(new WorkflowActorSnapshot("maker1", "Maker One", ["Maker"])),
            applicationStart,
            new RecordingWorkflowContextReader(expectedContext));

        var response = service.StartWorkflow(new DefaultHttpContext(), new WorkflowStartRequest
        {
            WorkflowKey = " PortfolioTrade ",
            BusinessObjectType = " PortfolioTrade ",
            BusinessObjectId = " PT-1001 ",
            InitialActionKey = " SubmitForChecker ",
            NotificationContext = new Dictionary<string, string?>
            {
                ["submitterDisplayName"] = " Kollipara, Kartheek ",
                ["deskName"] = "Taxable",
                ["emptyValue"] = " "
            }
        });

        Assert.True(response.Result.ActionResult.Success);
        Assert.NotNull(applicationStart.LastRequest);
        Assert.Equal("SubmitForChecker", applicationStart.LastRequest.InitialActionKey);
        Assert.Equal("Maker One", applicationStart.LastRequest.StartedByDisplayName);
        Assert.Equal(["Maker"], applicationStart.LastRequest.StartedByRoles);
        Assert.NotNull(applicationStart.LastRequest.NotificationContext);
        Assert.Equal("Kollipara, Kartheek", applicationStart.LastRequest.NotificationContext["submitterDisplayName"]);
        Assert.Equal("Taxable", applicationStart.LastRequest.NotificationContext["deskName"]);
        Assert.False(applicationStart.LastRequest.NotificationContext.ContainsKey("emptyValue"));
    }

    [Fact]
    public void StartWorkflowDispatchesInitialActionCreatedNotifications()
    {
        var applicationStart = new RecordingStartWorkflowService(new StartWorkflowResult(
            new WorkflowActionResultEnvelope
            {
                Result = new WorkflowActionResult
                {
                    Success = true,
                    ResultCode = "Success",
                    WorkflowInstanceId = "WF-PT-1001",
                    ActionKey = "SubmitForChecker",
                    CorrelationId = "start-action-1",
                    Notification = new WorkflowNotificationSummary
                    {
                        Queued = true,
                        NotificationType = "WorkflowTaskAssigned",
                        RecipientRole = "Checker"
                    }
                },
                WorkflowContext = new WorkflowContext()
            },
            ResetCounts: null));
        var dispatcher = new RecordingWorkflowActionNotificationDispatcher(
            WorkflowActionNotificationDispatchResult.Delivered([]));
        var service = CreateService(
            new StubActorResolver(new WorkflowActorSnapshot("maker1", "Maker One", ["Maker"])),
            applicationStart,
            new RecordingWorkflowContextReader(new WorkflowContextResponse()),
            dispatcher);

        var response = service.StartWorkflow(new DefaultHttpContext(), new WorkflowStartRequest
        {
            WorkflowKey = "PortfolioTrade",
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-1001",
            InitialActionKey = "SubmitForChecker"
        });

        Assert.True(response.Result.ActionResult.Success);
        Assert.Equal("WF-PT-1001", dispatcher.LastWorkflowInstanceId);
        Assert.Equal("start-action-1", dispatcher.LastCorrelationId);
        Assert.Equal(1, dispatcher.DispatchCount);
    }

    [Fact]
    public void StartWorkflowDoesNotDispatchNotificationsForStartOnly()
    {
        var applicationStart = new RecordingStartWorkflowService(SuccessResult());
        var dispatcher = new RecordingWorkflowActionNotificationDispatcher(
            WorkflowActionNotificationDispatchResult.Delivered([]));
        var service = CreateService(
            new StubActorResolver(new WorkflowActorSnapshot("maker1", "Maker One", ["Maker"])),
            applicationStart,
            new RecordingWorkflowContextReader(new WorkflowContextResponse()),
            dispatcher);

        var response = service.StartWorkflow(new DefaultHttpContext(), new WorkflowStartRequest
        {
            WorkflowKey = "PortfolioTrade",
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-1001"
        });

        Assert.True(response.Result.ActionResult.Success);
        Assert.Equal(0, dispatcher.DispatchCount);
    }

    [Fact]
    public void StartWorkflowReturnsFailureWhenInitialActionNotificationDeliveryFails()
    {
        var applicationStart = new RecordingStartWorkflowService(new StartWorkflowResult(
            new WorkflowActionResultEnvelope
            {
                Result = new WorkflowActionResult
                {
                    Success = true,
                    ResultCode = "Success",
                    WorkflowInstanceId = "WF-PT-1001",
                    ActionKey = "SubmitForChecker",
                    CorrelationId = "start-action-2",
                    Notification = new WorkflowNotificationSummary
                    {
                        Queued = true,
                        NotificationType = "WorkflowTaskAssigned",
                        RecipientRole = "Checker"
                    }
                },
                WorkflowContext = new WorkflowContext()
            },
            ResetCounts: null));
        var dispatcher = new RecordingWorkflowActionNotificationDispatcher(
            WorkflowActionNotificationDispatchResult.Failed("SMTP delivery failed for notification 99."));
        var service = CreateService(
            new StubActorResolver(new WorkflowActorSnapshot("maker1", "Maker One", ["Maker"])),
            applicationStart,
            new RecordingWorkflowContextReader(new WorkflowContextResponse()),
            dispatcher);

        var response = service.StartWorkflow(new DefaultHttpContext(), new WorkflowStartRequest
        {
            WorkflowKey = "PortfolioTrade",
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-1001",
            InitialActionKey = "SubmitForChecker"
        });

        Assert.False(response.Result.ActionResult.Success);
        Assert.Equal("NotificationDeliveryFailed", response.Result.ActionResult.ResultCode);
        Assert.Equal("SMTP delivery failed for notification 99.", response.Result.ActionResult.Message);
        Assert.Equal("WF-PT-1001", dispatcher.LastWorkflowInstanceId);
        Assert.Equal("start-action-2", dispatcher.LastCorrelationId);
    }

    [Fact]
    public void StartWorkflowDoesNotReadContextForFailedStart()
    {
        var applicationStart = new RecordingStartWorkflowService(new StartWorkflowResult(
            new WorkflowActionResultEnvelope
            {
                Result = new WorkflowActionResult
                {
                    Success = false,
                    ResultCode = "AlreadyExists",
                    WorkflowInstanceId = "WF-PT-1001"
                }
            },
            ResetCounts: null));
        var contextReader = new RecordingWorkflowContextReader(new WorkflowContextResponse());
        var service = CreateService(
            new StubActorResolver(new WorkflowActorSnapshot("maker1", "Maker One", ["Maker"])),
            applicationStart,
            contextReader);

        var response = service.StartWorkflow(new DefaultHttpContext(), new WorkflowStartRequest
        {
            WorkflowKey = "PortfolioTrade",
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-1001"
        });

        Assert.False(response.Result.ActionResult.Success);
        Assert.Null(response.Result.WorkflowContext);
        Assert.Null(contextReader.LastRequest);
    }

    [Fact]
    public void StartWorkflowThrowsRequestExceptionWhenRequestIsMissing()
    {
        var service = CreateService(
            new StubActorResolver(new WorkflowActorSnapshot("maker1", "Maker One", ["Maker"])),
            new RecordingStartWorkflowService(SuccessResult()),
            new RecordingWorkflowContextReader(new WorkflowContextResponse()));

        var exception = Assert.Throws<WorkflowApiRequestException>(() =>
            service.StartWorkflow(new DefaultHttpContext(), request: null));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal("Invalid workflow start request.", exception.Title);
        Assert.Equal("Workflow start request body is required.", exception.Detail);
    }

    [Theory]
    [InlineData(null, "PortfolioTrade", "PT-1001", "workflowKey is required.")]
    [InlineData(" ", "PortfolioTrade", "PT-1001", "workflowKey is required.")]
    [InlineData("PortfolioTrade", null, "PT-1001", "businessObjectType is required.")]
    [InlineData("PortfolioTrade", " ", "PT-1001", "businessObjectType is required.")]
    [InlineData("PortfolioTrade", "PortfolioTrade", null, "businessObjectId is required.")]
    [InlineData("PortfolioTrade", "PortfolioTrade", " ", "businessObjectId is required.")]
    public void StartWorkflowThrowsRequestExceptionWhenRequiredFieldsAreMissing(
        string? workflowKey,
        string? businessObjectType,
        string? businessObjectId,
        string expectedDetail)
    {
        var applicationStart = new RecordingStartWorkflowService(SuccessResult());
        var service = CreateService(
            new StubActorResolver(new WorkflowActorSnapshot("maker1", "Maker One", ["Maker"])),
            applicationStart,
            new RecordingWorkflowContextReader(new WorkflowContextResponse()));

        var exception = Assert.Throws<WorkflowApiRequestException>(() =>
            service.StartWorkflow(new DefaultHttpContext(), new WorkflowStartRequest
            {
                WorkflowKey = workflowKey,
                BusinessObjectType = businessObjectType,
                BusinessObjectId = businessObjectId
            }));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal("Invalid workflow start request.", exception.Title);
        Assert.Equal(expectedDetail, exception.Detail);
        Assert.Null(applicationStart.LastRequest);
    }

    [Fact]
    public void StartWorkflowMergesDuplicateNotificationContextKeysAfterTrim()
    {
        var expectedContext = new WorkflowContextResponse();
        expectedContext.WorkflowContext.Workflow.WorkflowInstanceId = "WF-PT-1001";
        var applicationStart = new RecordingStartWorkflowService(SuccessResult());
        var service = CreateService(
            new StubActorResolver(new WorkflowActorSnapshot("maker1", "Maker One", ["Maker"])),
            applicationStart,
            new RecordingWorkflowContextReader(expectedContext));

        service.StartWorkflow(new DefaultHttpContext(), new WorkflowStartRequest
        {
            WorkflowKey = "PortfolioTrade",
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-1001",
            InitialActionKey = "SubmitForChecker",
            NotificationContext = new Dictionary<string, string?>
            {
                ["deskName"] = "Original",
                [" deskName "] = " Taxable ",
                ["emptyValue"] = " "
            }
        });

        var context = Assert.IsType<Dictionary<string, string?>>(
            applicationStart.LastRequest?.NotificationContext);
        var item = Assert.Single(context);
        Assert.Equal("deskName", item.Key);
        Assert.Equal("Taxable", item.Value);
        Assert.False(context.ContainsKey("emptyValue"));
    }

    private static ApiWorkflowStartService CreateService(
        IWorkflowActorResolver actorResolver,
        IStartWorkflowService startWorkflowService,
        IWorkflowContextReader workflowContextReader,
        IWorkflowActionNotificationDispatcher? notificationDispatcher = null)
    {
        return new ApiWorkflowStartService(
            actorResolver,
            startWorkflowService,
            workflowContextReader,
            notificationDispatcher ?? new RecordingWorkflowActionNotificationDispatcher(
                WorkflowActionNotificationDispatchResult.Delivered([])));
    }

    private static StartWorkflowResult SuccessResult()
    {
        return new StartWorkflowResult(
            new WorkflowActionResultEnvelope
            {
                Result = new WorkflowActionResult
                {
                    Success = true,
                    ResultCode = "Success",
                    WorkflowInstanceId = "WF-PT-1001"
                }
            },
            ResetCounts: null);
    }

    private sealed class StubActorResolver(WorkflowActorSnapshot actor) : IWorkflowActorResolver
    {
        public WorkflowActorSnapshot Resolve(HttpContext httpContext)
        {
            return actor;
        }
    }

    private sealed class RecordingStartWorkflowService(StartWorkflowResult response) : IStartWorkflowService
    {
        public StartWorkflowResult Response { get; } = response;

        public StartWorkflowRequest? LastRequest { get; private set; }

        public StartWorkflowResult Start(StartWorkflowRequest request)
        {
            LastRequest = request;

            return Response;
        }
    }

    private sealed class RecordingWorkflowContextReader(WorkflowContextResponse response) : IWorkflowContextReader
    {
        public WorkflowContextReadRequest? LastRequest { get; private set; }

        public WorkflowContextResponse Read(WorkflowContextReadRequest request)
        {
            LastRequest = request;

            return response;
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
