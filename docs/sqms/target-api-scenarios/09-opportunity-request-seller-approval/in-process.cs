using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Contracts;

var actor = new SqmsActor("ipg-user", "IPG User", [SqmsKeys.Roles.Ipg]);
var recipients = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
{
    ["relationshipManager"] = [new("relationshipManager", "relationship.manager@example.test", "Relationship manager")],
    ["sellerRmTeam"] = [new("sellerRmTeam", "seller.team@example.test", "Seller RM team")]
};
var result = await sqmsWorkflowService.ExecuteOpportunityActionAsync(
    new SqmsExecuteRequest(10000, SqmsKeys.OpportunityActions.RequestSellerApproval, 5, actor, recipients),
    cancellationToken);
if (!result.Success) throw new InvalidOperationException(result.ResultCode + ": " + result.Message);
var state = result.WorkflowResult?.Result.NewState ?? result.Context?.WorkflowContext.Workflow.CurrentState;
var replay = result.WorkflowResult?.Result.Diagnostics.Replay ?? false;
// Target implementation required: save Status/WorkflowState only when replay is false; commit once.
