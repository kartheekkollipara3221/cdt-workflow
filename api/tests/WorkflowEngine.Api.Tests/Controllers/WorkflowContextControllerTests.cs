using Microsoft.AspNetCore.Mvc;
using WorkflowEngine.Api.Controllers;
using WorkflowEngine.Api.Services;
using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Api.Tests.Controllers;

public sealed class WorkflowContextControllerTests
{
    [Fact]
    public void GetContextCallsReaderUsingActorRolesFromResolver()
    {
        var reader = new RecordingWorkflowContextReader();
        var controller = new WorkflowContextController(
            reader,
            new StubActorResolver(new WorkflowActorSnapshot("checker1", "Checker One", ["Checker"])))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = controller.Get("WF-PT-1001");

        Assert.NotNull(response.Value);
        Assert.NotNull(reader.LastRequest);
        Assert.Equal("WF-PT-1001", reader.LastRequest.WorkflowInstanceId);
        Assert.Equal("checker1", reader.LastRequest.UserId);
        Assert.Equal(["Checker"], reader.LastRequest.Roles);
    }

    private sealed class RecordingWorkflowContextReader : IWorkflowContextReader
    {
        public WorkflowContextReadRequest? LastRequest { get; private set; }

        public WorkflowContextResponse Read(WorkflowContextReadRequest request)
        {
            LastRequest = request;
            return new WorkflowContextResponse();
        }
    }

    private sealed class StubActorResolver(WorkflowActorSnapshot actor) : IWorkflowActorResolver
    {
        public WorkflowActorSnapshot Resolve(HttpContext httpContext)
        {
            return actor;
        }
    }
}
