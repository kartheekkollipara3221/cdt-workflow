using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WorkflowEngine.Api.Controllers;
using WorkflowEngine.Api.Services;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.Validation;
using WorkflowEngine.Application.WorkflowExecution;

namespace WorkflowEngine.Api.Tests.Controllers;

public sealed class WorkflowActionsControllerTests
{
    [Fact]
    public void ExecuteRejectsNullBodyWithBadRequest()
    {
        var controller = CreateController();

        var response = controller.Execute("WF-PT-1001", null);

        var objectResult = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public void ExecuteRejectsRouteBodyWorkflowInstanceMismatchWithBadRequest()
    {
        var controller = CreateController();
        var envelope = ValidEnvelope();
        envelope.Workflow!.WorkflowInstanceId = "WF-OTHER";

        var response = controller.Execute("WF-PT-1001", envelope);

        var objectResult = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public void ExecuteOverwritesBodyActorFromResolverBeforeExecution()
    {
        var executionService = new RecordingWorkflowExecutionService("Success");
        var controller = CreateController(
            executionService,
            new StubActorResolver(new WorkflowActorSnapshot("maker1", "Maker One", ["Maker"])));
        var envelope = ValidEnvelope();
        envelope.Actor = new WorkflowActor
        {
            UserId = "spoofed",
            DisplayName = "Spoofed",
            Roles = ["AdminDemoOverride", "TradingDesk"]
        };

        var response = controller.Execute("WF-PT-1001", envelope);

        var objectResult = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status200OK, objectResult.StatusCode);
        Assert.NotNull(executionService.LastRequest);
        Assert.Equal("maker1", executionService.LastRequest.Normalized.Envelope.Actor!.UserId);
        Assert.Equal("Maker One", executionService.LastRequest.Normalized.Envelope.Actor.DisplayName);
        Assert.Equal(["Maker"], executionService.LastRequest.Normalized.Envelope.Actor.Roles);
    }

    [Fact]
    public void ExecuteStampsMissingBodyWorkflowInstanceIdFromRoute()
    {
        var executionService = new RecordingWorkflowExecutionService("Success");
        var controller = CreateController(executionService);
        var envelope = ValidEnvelope();
        envelope.Workflow!.WorkflowInstanceId = null;

        controller.Execute("WF-PT-1001", envelope);

        Assert.NotNull(executionService.LastRequest);
        Assert.Equal("WF-PT-1001", executionService.LastRequest.Normalized.Envelope.Workflow!.WorkflowInstanceId);
    }

    [Fact]
    public void ExecuteMapsValidationFailureToBadRequestWorkflowEnvelope()
    {
        var executionService = new RecordingWorkflowExecutionService("Success");
        var controller = CreateController(executionService);
        var envelope = ValidEnvelope();
        envelope.Request!.ClientActionId = "";

        var response = controller.Execute("WF-PT-1001", envelope);

        var objectResult = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        var envelopeResult = Assert.IsType<WorkflowActionResultEnvelope>(objectResult.Value);
        Assert.Equal("ValidationFailed", envelopeResult.Result.ResultCode);
        Assert.Null(executionService.LastRequest);
    }

    [Theory]
    [InlineData("Forbidden", StatusCodes.Status403Forbidden)]
    [InlineData("InvalidTransition", StatusCodes.Status409Conflict)]
    [InlineData("ConcurrencyConflict", StatusCodes.Status409Conflict)]
    [InlineData("IdempotencyConflict", StatusCodes.Status409Conflict)]
    public void ExecuteMapsWorkflowResultCodesToHttpStatuses(string resultCode, int expectedStatusCode)
    {
        var executionService = new RecordingWorkflowExecutionService(resultCode);
        var controller = CreateController(executionService);

        var response = controller.Execute("WF-PT-1001", ValidEnvelope());

        var objectResult = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(expectedStatusCode, objectResult.StatusCode);
        var envelopeResult = Assert.IsType<WorkflowActionResultEnvelope>(objectResult.Value);
        Assert.Equal(resultCode, envelopeResult.Result.ResultCode);
    }

    private static WorkflowActionsController CreateController(
        RecordingWorkflowExecutionService? executionService = null,
        IWorkflowActorResolver? actorResolver = null)
    {
        var controller = new WorkflowActionsController(
            executionService ?? new RecordingWorkflowExecutionService("Success"),
            actorResolver ?? new StubActorResolver(new WorkflowActorSnapshot("maker1", "Maker One", ["Maker"])),
            new WorkflowActionEnvelopeNormalizer(),
            new WorkflowActionEnvelopeValidator(),
            new CanonicalRequestHasher(),
            new WorkflowActionResultBuilder(),
            new WorkflowHttpResultMapper())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        return controller;
    }

    private static WorkflowActionEnvelope ValidEnvelope()
    {
        using var payload = JsonDocument.Parse("{}");
        return new WorkflowActionEnvelope
        {
            Request = new WorkflowRequest
            {
                CommandType = "WorkflowAction",
                CommandVersion = 1,
                ClientActionId = "client-1",
                CorrelationId = "corr-1"
            },
            BusinessObject = new WorkflowBusinessObject
            {
                ObjectType = "PortfolioTrade",
                ObjectId = "PT-1001"
            },
            Actor = new WorkflowActor
            {
                UserId = "body-user",
                DisplayName = "Body User",
                Roles = ["TradingDesk"]
            },
            UiAction = new WorkflowUiAction
            {
                SectionKey = "Maker",
                ButtonKey = "MakerPrimary",
                SaveAsDraft = false
            },
            DataSave = new WorkflowDataSave
            {
                SectionKey = "Maker",
                Payload = payload.RootElement.Clone()
            },
            Workflow = new WorkflowActionBlock
            {
                WorkflowKey = "PortfolioTrade",
                WorkflowInstanceId = "WF-PT-1001",
                WorkflowDefinitionVersion = 1,
                ExpectedCurrentState = "Draft",
                ExpectedInstanceVersion = 1,
                ActionKey = "SubmitForChecker"
            }
        };
    }

    private sealed class RecordingWorkflowExecutionService(string resultCode) : IWorkflowExecutionService
    {
        public WorkflowExecutionRequest? LastRequest { get; private set; }

        public WorkflowActionResultEnvelope Execute(WorkflowExecutionRequest request)
        {
            LastRequest = request;
            return new WorkflowActionResultEnvelope
            {
                Result = new WorkflowActionResult
                {
                    Success = resultCode == "Success",
                    ResultCode = resultCode,
                    WorkflowInstanceId = request.Normalized.Envelope.Workflow?.WorkflowInstanceId ?? string.Empty,
                    ActionKey = request.Normalized.Envelope.Workflow?.ActionKey ?? string.Empty,
                    Diagnostics = new WorkflowActionDiagnostics()
                }
            };
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
