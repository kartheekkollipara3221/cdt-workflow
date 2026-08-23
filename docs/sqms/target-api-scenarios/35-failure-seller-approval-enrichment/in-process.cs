using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Contracts;

// Testing only: EffectiveDateForSale was made NULL by setup.testing.sql in an isolated database.
var actor = new SqmsActor("ipg-user", "IPG User", [SqmsKeys.Roles.Ipg]);
var recipients = await recipientResolver.ResolveForSqmsAsync(cancellationToken);
var result = await sqmsWorkflowService.ExecuteOpportunityActionAsync(
    new SqmsExecuteRequest(10000, SqmsKeys.OpportunityActions.RequestSellerApproval, 5, actor, recipients),
    cancellationToken);

if (result.Success || result.ResultCode != "ValidationFailed")
{
    throw new InvalidOperationException("Expected ValidationFailed (recommended HTTP 422).");
}

// recipientResolver, targetErrorMapper, target repository, and shared transaction are target-owned placeholders.
// Roll back the outer transaction. Do not persist target state and do not retry with browser-supplied enrichment.
