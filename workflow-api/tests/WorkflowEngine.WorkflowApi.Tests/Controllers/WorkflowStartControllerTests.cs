using WorkflowEngine.WorkflowApi.Controllers;
using WorkflowEngine.WorkflowApi.Models;
using WorkflowEngine.WorkflowApi.Services;

namespace WorkflowEngine.WorkflowApi.Tests.Controllers;

public sealed class WorkflowStartControllerTests
{
    [Fact]
    public void StartWorkflowReturnsWorkflowStartResultEnvelope()
    {
        var expectedResponse = new WorkflowStartApiResponse(
            new WorkflowStartApiResult(
                new()
                {
                    Success = true,
                    ResultCode = "Success",
                    WorkflowInstanceId = "WF-PT-1001"
                },
                new()));
        var workflowStartService = new RecordingWorkflowStartService(expectedResponse);
        var controller = new WorkflowStartController(workflowStartService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var request = new WorkflowStartRequest
        {
            WorkflowKey = "PortfolioTrade",
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-1001"
        };

        var response = controller.StartWorkflow(request);

        var ok = Assert.IsType<OkObjectResult>(response);
        var body = Assert.IsType<WorkflowStartApiResponse>(ok.Value);
        Assert.Same(expectedResponse, body);
        Assert.True(body.Result.ActionResult.Success);
        Assert.Same(controller.HttpContext, workflowStartService.LastHttpContext);
        Assert.Same(request, workflowStartService.LastRequest);
    }

    [Fact]
    public void StartWorkflowReturnsCustom500ForWorkflowFailures()
    {
        var expectedResponse = new WorkflowStartApiResponse(
            new WorkflowStartApiResult(
                new()
                {
                    Success = false,
                    ResultCode = "AlreadyExists",
                    WorkflowInstanceId = "WF-PT-1001"
                },
                WorkflowContext: null));
        var controller = new WorkflowStartController(new RecordingWorkflowStartService(expectedResponse))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = controller.StartWorkflow(new WorkflowStartRequest
        {
            WorkflowKey = "PortfolioTrade",
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-1001"
        });

        var objectResult = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        Assert.Same(expectedResponse, objectResult.Value);
    }

    private sealed class RecordingWorkflowStartService(WorkflowStartApiResponse response) : IWorkflowStartService
    {
        public HttpContext? LastHttpContext { get; private set; }

        public WorkflowStartRequest? LastRequest { get; private set; }

        public WorkflowStartApiResponse StartWorkflow(HttpContext httpContext, WorkflowStartRequest? request)
        {
            LastHttpContext = httpContext;
            LastRequest = request;

            return response;
        }
    }
}
