using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Contracts;

// Testing only: CommitmentAmountRequested was made NULL by setup.testing.sql in an isolated database.
var actor = new SqmsActor("seller-user", "Seller User", [SqmsKeys.Roles.Seller]);
var recipients = await recipientResolver.ResolveForSqmsAsync(cancellationToken);
var result = await sqmsWorkflowService.ExecuteBidActionAsync(
    new SqmsExecuteRequest(20000, SqmsKeys.BidActions.Accept, 4, actor, recipients),
    cancellationToken);

if (result.Success || result.ResultCode != "ValidationFailed")
{
    throw new InvalidOperationException("Expected ValidationFailed (recommended HTTP 422).");
}

// recipientResolver, targetErrorMapper, target repository, and shared transaction are target-owned placeholders.
// Roll back once; do not persist Bid state and do not send email outside the outbox.
