namespace WorkflowEngine.Sqms.Persistence;

public sealed record SqmsOpportunityRow(
    int Id,
    string? SellerRmTeam,
    string AccountName,
    string AccountNumber,
    string? RelationshipManager,
    string? FundConsideredSecondarySale,
    string? Vehicle,
    string? CommitmentAmountSale,
    DateTime? EffectiveDateForSale,
    string? Status,
    string? WorkflowState);

public sealed record SqmsBidRow(
    int Id,
    int OpportunityId,
    string? BuyerRmTeam,
    string AccountName,
    string AccountNumber,
    string? RelationshipManager,
    string? CommitmentAmountRequested,
    string? BidExpressedAsFairValue,
    string? Status,
    string? WorkflowState);

public sealed record SqmsOpenOpportunityRow(
    SqmsOpportunityRow Opportunity,
    DateTimeOffset OpenedAtUtc);
