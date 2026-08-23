using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Contracts;
var actor = new SqmsActor("mfo-user", "MFO User", [SqmsKeys.Roles.Mfo]);
var recipients = new Dictionary<string,IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal);
var result = await sqmsWorkflowService.ExecuteBidActionAsync(new(20000, SqmsKeys.BidActions.Submit, 3, actor, recipients), cancellationToken);
if (!result.Success) throw new InvalidOperationException(result.ResultCode + ": " + result.Message);
var replay = result.WorkflowResult?.Result.Diagnostics.Replay ?? false;
// Target implementation required: persist returned state only when replay is false.
