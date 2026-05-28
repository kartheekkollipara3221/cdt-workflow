using WorkflowEngine.WorkflowApi.Controllers;
using WorkflowEngine.WorkflowApi.Models;
using WorkflowEngine.WorkflowApi.Services;

namespace WorkflowEngine.WorkflowApi.Tests.Controllers;

public sealed class WorkflowActionControllerTests
{
    [Fact]
    public void ExecuteWorkflowActionReturnsWorkflowActionResultEnvelope()
    {
        var expectedResponse = new WorkflowActionApiResponse(
            new WorkflowActionApiResult(
                new()
                {
                    Success = true,
                    ResultCode = "Success",
                    WorkflowInstanceId = "WF-PT-1001",
                    ActionKey = "SubmitForChecker"
                },
                new()));
        var workflowActionService = new RecordingWorkflowActionService(expectedResponse);
        var controller = new WorkflowActionController(workflowActionService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var request = new WorkflowActionRequest
        {
            WorkflowInstanceId = "WF-PT-1001",
            ActionKey = "SubmitForChecker"
        };

        var response = controller.ExecuteWorkflowAction(request);

        var ok = Assert.IsType<OkObjectResult>(response);
        var body = Assert.IsType<WorkflowActionApiResponse>(ok.Value);
        Assert.Same(expectedResponse, body);
        Assert.True(body.Result.ActionResult.Success);
        Assert.Same(controller.HttpContext, workflowActionService.LastHttpContext);
        Assert.Same(request, workflowActionService.LastRequest);
    }

    [Fact]
    public void ExecuteWorkflowActionReturnsCustom500ForWorkflowFailures()
    {
        var expectedResponse = new WorkflowActionApiResponse(
            new WorkflowActionApiResult(
                new()
                {
                    Success = false,
                    ResultCode = "InvalidTransition",
                    WorkflowInstanceId = "WF-PT-1001",
                    ActionKey = "SubmitForChecker"
                },
                new()));
        var controller = new WorkflowActionController(new RecordingWorkflowActionService(expectedResponse))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = controller.ExecuteWorkflowAction(new WorkflowActionRequest
        {
            WorkflowInstanceId = "WF-PT-1001",
            ActionKey = "SubmitForChecker"
        });

        var objectResult = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        Assert.Same(expectedResponse, objectResult.Value);
    }

    private sealed class RecordingWorkflowActionService(WorkflowActionApiResponse response) : IWorkflowActionService
    {
        public HttpContext? LastHttpContext { get; private set; }

        public WorkflowActionRequest? LastRequest { get; private set; }

        public WorkflowActionApiResponse ExecuteWorkflowAction(HttpContext httpContext, WorkflowActionRequest? request)
        {
            LastHttpContext = httpContext;
            LastRequest = request;

            return response;
        }
    }
}
