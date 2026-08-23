using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Contracts;

// Testing only: construct SqmsWorkflowService with an ISqmsDataStore test double whose
// ProjectOpportunityStateAsync throws. Do not add a public API flag or production route.
var actor = new SqmsActor("system", "System", [SqmsKeys.Roles.System]);
var result = await scheduledTransitionService.ExecuteScheduledOpportunityActionAsync(
    new SqmsExecuteRequest(
        10000,
        SqmsKeys.OpportunityActions.CloseBidding,
        4,
        actor,
        new Dictionary<string, IReadOnlyList<WorkflowEngine.Application.Contracts.WorkflowRecipientInput>>(StringComparer.Ordinal)),
    cancellationToken);

// The projection exception is expected. The owned transaction rolls workflow and target work back.
