namespace WorkflowEngine.Sqms.Persistence;

public interface ISqmsDataStore
{
    Task<SqmsOpportunityRow?> GetOpportunityAsync(int id, CancellationToken cancellationToken);
    Task<SqmsBidRow?> GetBidAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<SqmsOpenOpportunityRow>> GetOpenOpportunitiesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<SqmsOpportunityRow>> GetClosedOpportunitiesWithSubmittedBidsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<SqmsBidRow>> GetSubmittedBidsAsync(int opportunityId, CancellationToken cancellationToken);
    Task ProjectOpportunityStateAsync(int id, string state, string modifiedBy, DateTime modifiedAtUtc, CancellationToken cancellationToken);
    Task ProjectBidStateAsync(int id, string state, string modifiedBy, DateTime modifiedAtUtc, CancellationToken cancellationToken);
}
