using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Contracts;

var actor = new SqmsActor("ipg-user", "IPG User", [SqmsKeys.Roles.Ipg]);
var recipients = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
{
    ["relationshipManager"] = [new("relationshipManager", "relationship.manager@example.test", "Relationship manager")],
    ["sellerRmTeam"] = [new("sellerRmTeam", "seller.team@example.test", "Seller RM team")]
};
var request = new SqmsExecuteRequest(
    10000,
    SqmsKeys.OpportunityActions.RequestSellerApproval,
    5,
    actor,
    recipients);

var first = await sqmsWorkflowService.ExecuteOpportunityActionAsync(request, cancellationToken);
var replay = await sqmsWorkflowService.ExecuteOpportunityActionAsync(request, cancellationToken);
if (!first.Success || !replay.Success) throw new InvalidOperationException("Expected two successful results.");
if (first.WorkflowResult?.Result.Diagnostics.Replay != false) throw new InvalidOperationException("First call was unexpectedly a replay.");
if (replay.WorkflowResult?.Result.Diagnostics.Replay != true) throw new InvalidOperationException("Second call was not a replay.");

// Target implementation required: persist first.NewState once. For replay, roll back
// prerequisite target writes, reload the committed row, verify its projection, and do not update it.
