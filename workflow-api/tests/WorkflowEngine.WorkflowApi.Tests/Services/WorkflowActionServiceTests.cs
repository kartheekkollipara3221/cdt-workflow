using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.WorkflowExecution;
using WorkflowEngine.WorkflowApi.Models;
using WorkflowEngine.WorkflowApi.Services;

namespace WorkflowEngine.WorkflowApi.Tests.Services;

public sealed class WorkflowActionServiceTests
{
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
                ActionKey = "CheckerSendToTradingDesk"
            },
            WorkflowContext = new WorkflowContext()
        });
        var service = CreateService(
            new StubActorResolver(new WorkflowActorSnapshot("checker1", "Checker One", ["Checker"])),
            executionService);

        var response = service.ExecuteWorkflowAction(new DefaultHttpContext(), new WorkflowActionRequest
        {
            WorkflowInstanceId = " WF-PT-1001 ",
            ActionKey = " CheckerSendToTradingDesk "
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
        Assert.Equal(envelope.Request?.ClientActionId, envelope.Workflow?.IdempotencyKey);
        Assert.Equal(
            new CanonicalRequestHasher().ComputeHash(envelope),
            executionService.LastRequest.RequestHash);
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

    private static WorkflowActionService CreateService(
        IWorkflowActorResolver actorResolver,
        IWorkflowExecutionService workflowExecutionService)
    {
        return new WorkflowActionService(
            actorResolver,
            new WorkflowActionEnvelopeNormalizer(),
            new CanonicalRequestHasher(),
            workflowExecutionService);
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
}
