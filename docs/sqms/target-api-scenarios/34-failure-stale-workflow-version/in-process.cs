using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Contracts;

var actor = new SqmsActor("ipg-user", "IPG User", [SqmsKeys.Roles.Ipg]);
var recipients = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal);
var result = await sqmsWorkflowService.ExecuteOpportunityActionAsync(
    new SqmsExecuteRequest(10000, SqmsKeys.OpportunityActions.MarkPending, 1, actor, recipients),
    cancellationToken);

if (result.Success || result.ResultCode != "ConcurrencyConflict")
{
    throw new InvalidOperationException("Expected ConcurrencyConflict (recommended HTTP 409).");
}

var refreshed = result.Context ?? throw new InvalidOperationException("Current context is required for refresh.");
// The target response mapper returns refreshed context; it performs no target repository save.
