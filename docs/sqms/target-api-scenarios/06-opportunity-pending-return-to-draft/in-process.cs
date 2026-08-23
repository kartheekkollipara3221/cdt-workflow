using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Contracts;

var actor = new SqmsActor("ipg-user", "IPG User", [SqmsKeys.Roles.Ipg]);
var recipients = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal);
var result = await sqmsWorkflowService.ExecuteOpportunityActionAsync(
    new SqmsExecuteRequest(10000, SqmsKeys.OpportunityActions.ReturnToDraft, 3, actor, recipients),
    cancellationToken);
if (!result.Success) throw new InvalidOperationException(result.ResultCode + ": " + result.Message);
var state = result.WorkflowResult?.Result.NewState ?? result.Context?.WorkflowContext.Workflow.CurrentState;
var replay = result.WorkflowResult?.Result.Diagnostics.Replay ?? false;
// Target implementation required: persist Status/WorkflowState only when replay is false; commit once.
