using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Contracts;

// Target-owned integration placeholder: actorFactory derives identity from authentication.
var actor = actorFactory.CreateFromAuthenticatedUser();
var result = await sqmsWorkflowService.StartOpportunityAsync(
    999999,
    actor,
    cancellationToken);

if (result.Success || result.ResultCode != "TargetNotFound")
{
    throw new InvalidOperationException("Expected TargetNotFound (recommended HTTP 404).");
}

// No target repository save and no fallback/lazy Start are permitted.
