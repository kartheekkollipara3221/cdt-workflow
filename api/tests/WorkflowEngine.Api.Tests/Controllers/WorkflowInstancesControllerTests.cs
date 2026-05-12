using Microsoft.AspNetCore.Mvc;
using WorkflowEngine.Api.Contracts;
using WorkflowEngine.Api.Controllers;
using WorkflowEngine.Api.Services;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.WorkflowStart;

namespace WorkflowEngine.Api.Tests.Controllers;

public sealed class WorkflowInstancesControllerTests
{
    [Fact]
    public void StartWorkflowRequestDtoDoesNotExposeResetAdminUserOrRoles()
    {
        var propertyNames = typeof(StartWorkflowApiRequest).GetProperties().Select(property => property.Name).ToList();

        Assert.Equal(["BusinessObjectType", "BusinessObjectId"], propertyNames);
    }

    [Fact]
    public void StartCallsStartWorkflowServiceWithResetDisabled()
    {
        var service = new RecordingStartWorkflowService("Success");
        var controller = new WorkflowInstancesController(
            service,
            new StubActorResolver(new WorkflowActorSnapshot("maker1", "Maker One", ["Maker"])),
            new WorkflowHttpResultMapper())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = controller.Start("PortfolioTrade", new StartWorkflowApiRequest
        {
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-1001"
        });

        var objectResult = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status200OK, objectResult.StatusCode);
        Assert.NotNull(service.LastRequest);
        Assert.False(service.LastRequest.Reset);
        Assert.Equal("maker1", service.LastRequest.StartedByUserId);
    }

    private sealed class RecordingStartWorkflowService(string resultCode) : IStartWorkflowService
    {
        public StartWorkflowRequest? LastRequest { get; private set; }

        public StartWorkflowResult Start(StartWorkflowRequest request)
        {
            LastRequest = request;
            return new StartWorkflowResult(
                new WorkflowActionResultEnvelope
                {
                    Result = new WorkflowActionResult
                    {
                        Success = resultCode == "Success",
                        ResultCode = resultCode,
                        WorkflowInstanceId = $"WF-{request.BusinessObjectId}"
                    }
                },
                ResetCounts: null);
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
