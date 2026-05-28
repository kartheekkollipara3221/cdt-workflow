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

    private static ApiWorkflowStartService CreateService(
        IWorkflowActorResolver actorResolver,
        IStartWorkflowService startWorkflowService,
        IWorkflowContextReader workflowContextReader)
    {
        return new ApiWorkflowStartService(actorResolver, startWorkflowService, workflowContextReader);
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
}
