using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.WorkflowApi.Services;

namespace WorkflowEngine.WorkflowApi.Tests.Services;

public sealed class WorkflowContextQueryServiceTests
{
    [Fact]
    public void GetWorkflowContextResolvesActorAndReadsTrimmedWorkflowContext()
    {
        var expectedResponse = new WorkflowContextResponse();
        var reader = new RecordingWorkflowContextReader(expectedResponse);
        var service = new WorkflowContextQueryService(
            reader,
            new StubActorResolver(new WorkflowActorSnapshot("checker1", "Checker One", ["Checker"])));

        var response = service.GetWorkflowContext(new DefaultHttpContext(), " WF-PT-1001 ");

        Assert.Same(expectedResponse, response);
        Assert.NotNull(reader.LastRequest);
        Assert.Equal("WF-PT-1001", reader.LastRequest.WorkflowInstanceId);
        Assert.Equal("checker1", reader.LastRequest.UserId);
        Assert.Equal(["Checker"], reader.LastRequest.Roles);
    }

    [Fact]
    public void GetWorkflowContextThrowsRequestExceptionWhenWorkflowInstanceIdIsBlank()
    {
        var service = new WorkflowContextQueryService(
            new RecordingWorkflowContextReader(new WorkflowContextResponse()),
            new StubActorResolver(new WorkflowActorSnapshot("checker1", "Checker One", ["Checker"])));

        var exception = Assert.Throws<WorkflowApiRequestException>(() =>
            service.GetWorkflowContext(new DefaultHttpContext(), " "));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal("Invalid workflow context request.", exception.Title);
        Assert.Equal("workflowInstanceId is required.", exception.Detail);
    }

    [Fact]
    public void GetWorkflowContextLetsActorResolutionExceptionFlowToGlobalHandler()
    {
        var expectedException = new WorkflowActorResolutionException(
            StatusCodes.Status401Unauthorized,
            "Workflow actor is required.",
            "Actor headers were not provided.");
        var service = new WorkflowContextQueryService(
            new RecordingWorkflowContextReader(new WorkflowContextResponse()),
            new ThrowingActorResolver(expectedException));

        var exception = Assert.Throws<WorkflowActorResolutionException>(() =>
            service.GetWorkflowContext(new DefaultHttpContext(), "WF-PT-1001"));

        Assert.Same(expectedException, exception);
    }

    [Fact]
    public void GetWorkflowContextLetsReaderExceptionsFlowToGlobalHandler()
    {
        var expectedException = new KeyNotFoundException("Workflow instance 'WF-MISSING' was not found.");
        var service = new WorkflowContextQueryService(
            new ThrowingWorkflowContextReader(expectedException),
            new StubActorResolver(new WorkflowActorSnapshot("checker1", "Checker One", ["Checker"])));

        var exception = Assert.Throws<KeyNotFoundException>(() =>
            service.GetWorkflowContext(new DefaultHttpContext(), "WF-MISSING"));

        Assert.Same(expectedException, exception);
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

    private sealed class ThrowingWorkflowContextReader(Exception exception) : IWorkflowContextReader
    {
        public WorkflowContextResponse Read(WorkflowContextReadRequest request)
        {
            throw exception;
        }
    }

    private sealed class StubActorResolver(WorkflowActorSnapshot actor) : IWorkflowActorResolver
    {
        public WorkflowActorSnapshot Resolve(HttpContext httpContext)
        {
            return actor;
        }
    }

    private sealed class ThrowingActorResolver(WorkflowActorResolutionException exception) : IWorkflowActorResolver
    {
        public WorkflowActorSnapshot Resolve(HttpContext httpContext)
        {
            throw exception;
        }
    }
}
