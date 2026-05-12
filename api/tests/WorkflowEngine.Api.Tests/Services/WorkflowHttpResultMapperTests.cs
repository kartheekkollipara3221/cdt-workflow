using WorkflowEngine.Api.Services;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Api.Tests.Services;

public sealed class WorkflowHttpResultMapperTests
{
    private readonly WorkflowHttpResultMapper mapper = new();

    [Theory]
    [InlineData("Success", StatusCodes.Status200OK)]
    [InlineData("ValidationFailed", StatusCodes.Status400BadRequest)]
    [InlineData("WorkflowInstanceNotFound", StatusCodes.Status404NotFound)]
    [InlineData("Forbidden", StatusCodes.Status403Forbidden)]
    [InlineData("AmbiguousRoleMatch", StatusCodes.Status403Forbidden)]
    [InlineData("InvalidTransition", StatusCodes.Status409Conflict)]
    [InlineData("ConcurrencyConflict", StatusCodes.Status409Conflict)]
    [InlineData("WorkflowDefinitionVersionMismatch", StatusCodes.Status409Conflict)]
    [InlineData("IdempotencyConflict", StatusCodes.Status409Conflict)]
    [InlineData("AlreadyExists", StatusCodes.Status409Conflict)]
    [InlineData("UnexpectedCode", StatusCodes.Status500InternalServerError)]
    public void MapsWorkflowResultCodesToHttpStatusCodes(string resultCode, int expectedStatusCode)
    {
        var envelope = new WorkflowActionResultEnvelope
        {
            Result = new WorkflowActionResult { ResultCode = resultCode }
        };

        var statusCode = mapper.MapStatusCode(envelope);

        Assert.Equal(expectedStatusCode, statusCode);
    }
}
