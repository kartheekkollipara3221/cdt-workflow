using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Contracts;

var actor = new SqmsActor("mfo-user", "MFO User", [SqmsKeys.Roles.Mfo]);
var beforeRequest = await targetRepository.ReadCompleteOpportunitySnapshotAsync(10000, cancellationToken);
var requestResult = await sqmsWorkflowService.ExecuteOpportunityActionAsync(
    new SqmsExecuteRequest(
        10000,
        SqmsKeys.OpportunityActions.Submit,
        3,
        actor,
        emptyRecipients),
    cancellationToken);
if (!requestResult.Success) throw new InvalidOperationException(requestResult.ResultCode + ": " + requestResult.Message);
var afterAdapter = await targetRepository.ReadCompleteOpportunitySnapshotAsync(10000, cancellationToken);
if (!beforeRequest.SequenceEqual(afterAdapter))
    throw new InvalidOperationException("Request adapter modified the target row.");

// Target implementation required: save returned Status/WorkflowState and commit the shared transaction.
await targetRepository.SaveReturnedStateAsync(
    10000,
    requestResult.WorkflowResult!.Result.NewState!,
    cancellationToken);

var beforeScheduled = await targetRepository.ReadCompleteOpportunitySnapshotAsync(10001, cancellationToken);
var scheduledResult = await scheduledTransitionService.ExecuteScheduledOpportunityActionAsync(
    new SqmsExecuteRequest(
        10001,
        SqmsKeys.OpportunityActions.CloseBidding,
        4,
        new SqmsActor("system", "System", [SqmsKeys.Roles.System]),
        emptyRecipients),
    cancellationToken);
if (!scheduledResult.Success) throw new InvalidOperationException(scheduledResult.ResultCode + ": " + scheduledResult.Message);
var afterScheduled = await targetRepository.ReadCompleteOpportunitySnapshotAsync(10001, cancellationToken);

// Testing only: this target-test helper compares every column and fails unless the changed-column
// set is exactly Status, WorkflowState, ModifiedBy, and ModifiedDate.
AssertOnlyColumnsChanged(
    beforeScheduled,
    afterScheduled,
    ["Status", "WorkflowState", "ModifiedBy", "ModifiedDate"]);
