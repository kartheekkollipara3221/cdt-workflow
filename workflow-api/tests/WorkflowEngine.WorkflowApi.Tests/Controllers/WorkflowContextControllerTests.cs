using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.WorkflowApi.Controllers;
using WorkflowEngine.WorkflowApi.Services;

namespace WorkflowEngine.WorkflowApi.Tests.Controllers;

public sealed class WorkflowContextControllerTests
{
    [Fact]
    public void GetReturnsWorkflowContextUsingResolvedActor()
    {
        var expectedResponse = ContextResponse(canAct: true, availableActionKey: "CheckerSendToTradingDesk");
        var reader = new RecordingWorkflowContextReader(expectedResponse);
        var controller = new WorkflowContextController(
            reader,
            new StubActorResolver(new WorkflowActorSnapshot("checker1", "Checker One", ["Checker"])))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = controller.Get(" WF-PT-1001 ");

        Assert.Same(expectedResponse, response.Value);
        Assert.NotNull(reader.LastRequest);
        Assert.Equal("WF-PT-1001", reader.LastRequest.WorkflowInstanceId);
        Assert.Equal("checker1", reader.LastRequest.UserId);
        Assert.Equal(["Checker"], reader.LastRequest.Roles);
    }

    [Fact]
    public void GetReturnsReadOnlyContextWhenActorHasNoAvailableActions()
    {
        var expectedResponse = ContextResponse(canAct: false, availableActionKey: null);
        expectedResponse.WorkflowContext.ActorAccess.ReadOnlyReason = "Current task is assigned to role Checker.";
        var controller = new WorkflowContextController(
            new RecordingWorkflowContextReader(expectedResponse),
            new StubActorResolver(new WorkflowActorSnapshot("maker1", "Maker One", ["Maker"])))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = controller.Get("WF-PT-1001");

        Assert.NotNull(response.Value);
        Assert.False(response.Value.WorkflowContext.ActorAccess.CanAct);
        Assert.Empty(response.Value.WorkflowContext.AvailableActions);
        Assert.Contains("Checker", response.Value.WorkflowContext.ActorAccess.ReadOnlyReason, StringComparison.Ordinal);
    }

    [Fact]
    public void GetReturnsBadRequestWhenWorkflowInstanceIdIsBlank()
    {
        var controller = new WorkflowContextController(
            new RecordingWorkflowContextReader(new WorkflowContextResponse()),
            new StubActorResolver(new WorkflowActorSnapshot("checker1", "Checker One", ["Checker"])))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = controller.Get(" ");

        var problem = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Invalid workflow context request.", details.Title);
    }

    [Fact]
    public void GetReturnsNotFoundWhenWorkflowInstanceDoesNotExist()
    {
        var controller = new WorkflowContextController(
            new ThrowingWorkflowContextReader(new KeyNotFoundException("Workflow instance 'WF-MISSING' was not found.")),
            new StubActorResolver(new WorkflowActorSnapshot("checker1", "Checker One", ["Checker"])))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = controller.Get("WF-MISSING");

        var problem = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Workflow instance was not found.", details.Title);
    }

    [Fact]
    public void GetReturnsActorProblemWhenActorCannotBeResolved()
    {
        var controller = new WorkflowContextController(
            new RecordingWorkflowContextReader(new WorkflowContextResponse()),
            new ThrowingActorResolver(new WorkflowActorResolutionException(
                StatusCodes.Status401Unauthorized,
                "Workflow actor is required.",
                "Actor headers were not provided.")))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = controller.Get("WF-PT-1001");

        var problem = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Workflow actor is required.", details.Title);
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
