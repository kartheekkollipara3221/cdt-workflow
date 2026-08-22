namespace WorkflowEngine.Sqms.Contracts;

public interface ISqmsScheduledTransitionService
{
    Task<SqmsOperationResult> ExecuteScheduledOpportunityActionAsync(
        SqmsExecuteRequest request,
        CancellationToken cancellationToken);

    Task<SqmsOperationResult> ExecuteScheduledBidActionAsync(
        SqmsExecuteRequest request,
        CancellationToken cancellationToken);
}
