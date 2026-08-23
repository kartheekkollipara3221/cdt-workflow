using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Contracts;

// Target-owned integration placeholders: authenticated actor construction and recipients.
var mfoActor = new SqmsActor("mfo-user", "MFO User", [SqmsKeys.Roles.Mfo]);
var ipgActor = new SqmsActor("ipg-user", "IPG User", [SqmsKeys.Roles.Ipg]);
var recipients = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal);

var context = await sqmsWorkflowService.GetOpportunityContextAsync(10000, mfoActor, cancellationToken);
var action = await sqmsWorkflowService.ExecuteOpportunityActionAsync(
    new SqmsExecuteRequest(10000, SqmsKeys.OpportunityActions.MarkPending, 1, ipgActor, recipients),
    cancellationToken);

if (context.ResultCode != "NotWorkflowManaged" || action.ResultCode != "NotWorkflowManaged")
{
    throw new InvalidOperationException("Context and Action must return NotWorkflowManaged (recommended HTTP 409).");
}

// Do not call Start as a fallback. Start is an explicit target-owned route/use case.
