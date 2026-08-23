using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Contracts;
var actor = new SqmsActor("seller-user", "Seller User", [SqmsKeys.Roles.Seller]);
var recipients = new Dictionary<string,IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
{
    ["relationshipManager"] = [new("relationshipManager", "relationship.manager@example.test", "Relationship manager")],
    ["buyerRmTeam"] = [new("buyerRmTeam", "buyer.team@example.test", "Buyer RM team")]
};
var result = await sqmsWorkflowService.ExecuteBidActionAsync(new(20001, SqmsKeys.BidActions.PartiallyAccept, 4, actor, recipients), cancellationToken);
if (!result.Success) throw new InvalidOperationException(result.ResultCode + ": " + result.Message);
var replay = result.WorkflowResult?.Result.Diagnostics.Replay ?? false;
// Target implementation required: persist returned state only when replay is false.
