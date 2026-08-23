using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Contracts;

var actor = new SqmsActor("mfo-user", "MFO User", [SqmsKeys.Roles.Mfo]);
var result = await sqmsWorkflowService.StartOpportunityAsync(10000, actor, cancellationToken);
if (!result.Success) throw new InvalidOperationException(result.ResultCode + ": " + result.Message);
var authoritativeState = result.WorkflowResult?.Result.NewState ?? result.Context?.WorkflowContext.Workflow.CurrentState;
// Target implementation required: persist returned Status/WorkflowState and commit shared transaction.
