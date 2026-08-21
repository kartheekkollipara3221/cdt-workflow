namespace WorkflowEngine.Sqms.Contracts;

public interface ISqmsWorkflowService
{
    Task<SqmsOperationResult> StartOpportunityAsync(int opportunityId, SqmsActor actor, CancellationToken cancellationToken);
    Task<SqmsOperationResult> StartBidAsync(int bidId, SqmsActor actor, CancellationToken cancellationToken);
    Task<SqmsOperationResult> GetOpportunityContextAsync(int opportunityId, SqmsActor actor, CancellationToken cancellationToken);
    Task<SqmsOperationResult> GetBidContextAsync(int bidId, SqmsActor actor, CancellationToken cancellationToken);
    Task<SqmsOperationResult> ExecuteOpportunityActionAsync(SqmsExecuteRequest request, CancellationToken cancellationToken);
    Task<SqmsOperationResult> ExecuteBidActionAsync(SqmsExecuteRequest request, CancellationToken cancellationToken);
}
