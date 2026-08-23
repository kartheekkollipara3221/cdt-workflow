using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Contracts;

var actor = new SqmsActor("mfo-user", "MFO User", [SqmsKeys.Roles.Mfo]);
var result = await sqmsWorkflowService.StartBidAsync(20000, actor, cancellationToken);
if (!result.Success) throw new InvalidOperationException(result.ResultCode + ": " + result.Message);
var state = result.WorkflowResult?.Result.NewState ?? result.Context?.WorkflowContext.Workflow.CurrentState;
// Target implementation required: save returned Status/WorkflowState and commit the shared transaction.
