using WorkflowEngine.Application.Contracts;
using WorkflowEngine.WorkflowApi.Controllers;
using WorkflowEngine.WorkflowApi.Models;
using WorkflowEngine.WorkflowApi.Services;

namespace WorkflowEngine.WorkflowApi.Tests.Controllers;

public sealed class WorkflowContextControllerTests
{
    [Fact]
    public void GetWorkflowContextReturnsWorkflowContextInResultEnvelope()
    {
        var expectedResponse = ContextResponse(canAct: true, availableActionKey: "CheckerSendToTradingDesk");
        var queryService = new RecordingWorkflowContextQueryService(expectedResponse);
        var controller = new WorkflowContextController(queryService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = controller.GetWorkflowContext(" WF-PT-1001 ");

        var ok = Assert.IsType<OkObjectResult>(response);
        var body = Assert.IsType<WorkflowContextApiResponse>(ok.Value);
        Assert.Same(expectedResponse.WorkflowContext, body.Result.WorkflowContext);
        Assert.Same(controller.HttpContext, queryService.LastHttpContext);
        Assert.Equal(" WF-PT-1001 ", queryService.LastWorkflowInstanceId);
    }

    [Fact]
    public void GetWorkflowContextReturnsReadOnlyContextInResultEnvelope()
    {
        var expectedResponse = ContextResponse(canAct: false, availableActionKey: null);
        expectedResponse.WorkflowContext.ActorAccess.ReadOnlyReason = "Current task is assigned to role Checker.";
        var controller = new WorkflowContextController(new RecordingWorkflowContextQueryService(expectedResponse))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = controller.GetWorkflowContext("WF-PT-1001");

        var ok = Assert.IsType<OkObjectResult>(response);
        var body = Assert.IsType<WorkflowContextApiResponse>(ok.Value);
        Assert.False(body.Result.WorkflowContext.ActorAccess.CanAct);
        Assert.Empty(body.Result.WorkflowContext.AvailableActions);
        Assert.Contains("Checker", body.Result.WorkflowContext.ActorAccess.ReadOnlyReason, StringComparison.Ordinal);
    }

    private static WorkflowContextResponse ContextResponse(bool canAct, string? availableActionKey)
    {
        var response = new WorkflowContextResponse
        {
            WorkflowContext =
            {
                Workflow =
                {
                    WorkflowKey = "PortfolioTrade",
                    WorkflowInstanceId = "WF-PT-1001",
                    WorkflowDefinitionVersion = 1,
                    WorkflowInstanceVersion = 2,
                    CurrentState = "CheckerReview",
                    Status = "Active"
                },
                ActorAccess =
                {
                    UserId = canAct ? "checker1" : "maker1",
                    DisplayName = canAct ? "Checker One" : "Maker One",
                    EffectiveRoles = canAct ? ["Checker"] : ["Maker"],
                    CanAct = canAct
                }
            }
        };

        if (availableActionKey is not null)
        {
            response.WorkflowContext.AvailableActions.Add(new AvailableWorkflowAction
            {
                ActionKey = availableActionKey
            });
        }

        return response;
    }

    private sealed class RecordingWorkflowContextQueryService(WorkflowContextResponse response) : IWorkflowContextQueryService
    {
        public HttpContext? LastHttpContext { get; private set; }

        public string? LastWorkflowInstanceId { get; private set; }

        public WorkflowContextResponse GetWorkflowContext(HttpContext httpContext, string workflowInstanceId)
        {
            LastHttpContext = httpContext;
            LastWorkflowInstanceId = workflowInstanceId;

            return response;
        }
    }
}
