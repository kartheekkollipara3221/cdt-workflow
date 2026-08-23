using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Contracts;

var mfoActor = new SqmsActor("mfo-user", "MFO User", [SqmsKeys.Roles.Mfo]);
var ipgActor = new SqmsActor("ipg-user", "IPG User", [SqmsKeys.Roles.Ipg]);
var recipients = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal);

var forbidden = await sqmsWorkflowService.ExecuteOpportunityActionAsync(
    new SqmsExecuteRequest(10000, SqmsKeys.OpportunityActions.MarkPending, 2, mfoActor, recipients),
    cancellationToken);
var invalid = await sqmsWorkflowService.ExecuteOpportunityActionAsync(
    new SqmsExecuteRequest(10000, SqmsKeys.OpportunityActions.OpenForBidding, 2, ipgActor, recipients),
    cancellationToken);

if (forbidden.ResultCode != "Forbidden" || invalid.ResultCode != "InvalidTransition")
{
    throw new InvalidOperationException("Expected Forbidden (403) and InvalidTransition (422).");
}

// targetErrorMapper and transaction handling are target-owned integration placeholders.
// Neither failure is followed by a target repository save.
