using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Contracts;

var actor = new SqmsActor("ipg-user", "IPG User", [SqmsKeys.Roles.Ipg]);
var result = await sqmsWorkflowService.GetBidContextAsync(20000, actor, cancellationToken);
if (!result.Success) throw new InvalidOperationException(result.ResultCode + ": " + result.Message);
var context = result.Context!.WorkflowContext;
