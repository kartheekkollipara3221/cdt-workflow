namespace WorkflowEngine.Sqms.Contracts;

public sealed record SqmsScheduledFailure(
    string EntityType,
    string EntityId,
    string Operation,
    string Error);

public sealed record SqmsScheduledRunResult(
    DateTimeOffset Now,
    int OpportunitiesClosed,
    int BidsMovedToIpgReview,
    int BidsNotAllocated,
    int OpenNotificationsQueued,
    int ReminderNotificationsQueued,
    int AlreadyQueued,
    IReadOnlyList<SqmsScheduledFailure> Failures)
{
    public bool Success => Failures.Count == 0;
}

public interface ISqmsScheduledService
{
    Task<SqmsScheduledRunResult> RunAsync(CancellationToken cancellationToken);
}
