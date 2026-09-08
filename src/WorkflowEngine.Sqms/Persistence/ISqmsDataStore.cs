namespace WorkflowEngine.Sqms.Persistence;

public interface ISqmsDataStore
{
    Task<SqmsOpportunityRow?> GetOpportunityAsync(int id, CancellationToken cancellationToken);
    Task<SqmsBidRow?> GetBidAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<SqmsOpenOpportunityRow>> GetOpenOpportunitiesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<SqmsOpportunityRow>> GetClosedOpportunitiesWithSubmittedBidsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<SqmsBidRow>> GetSubmittedBidsAsync(int opportunityId, CancellationToken cancellationToken);
    /// <summary>
    /// Returns submitted bids and previously classified bids, including bids that have advanced further.
    /// Custom stores should override this method to keep the classification total stable across retries.
    /// The default preserves compatibility with existing stores and their submitted-only behavior.
    /// </summary>
    Task<IReadOnlyList<SqmsBidRow>> GetBidClassificationBasisAsync(int opportunityId, CancellationToken cancellationToken) =>
        GetSubmittedBidsAsync(opportunityId, cancellationToken);
    Task ProjectOpportunityStateAsync(int id, string state, string modifiedBy, DateTime modifiedAtUtc, CancellationToken cancellationToken);
    Task ProjectBidStateAsync(int id, string state, string modifiedBy, DateTime modifiedAtUtc, CancellationToken cancellationToken);
}
