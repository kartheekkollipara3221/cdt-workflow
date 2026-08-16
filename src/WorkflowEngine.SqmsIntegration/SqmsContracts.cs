using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.SqmsIntegration;

public sealed record SqmsActor(
    string UserId,
    string DisplayName,
    IReadOnlyList<string> Roles);

public sealed record SqmsActionRequest(
    string ActionKey,
    string? Reason = null);

public sealed record SqmsWorkflowResult(
    int EntityId,
    string WorkflowState,
    string Status,
    WorkflowContext WorkflowContext);

public sealed class SqmsIntegrationException : Exception
{
    public SqmsIntegrationException(string message)
        : base(message)
    {
    }

    public SqmsIntegrationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal enum SqmsEntityKind
{
    Opportunity,
    Bid
}

internal sealed record SqmsApplicationState(
    string? WorkflowState,
    string? Status);

internal sealed record SqmsSaleCycleCandidate(
    DateOnly EffectiveDateForSale,
    DateTimeOffset NoticeAt,
    DateTimeOffset ReminderAt,
    DateOnly ResponseDueDate);

public sealed record SqmsScheduleRunResult(
    int CyclesScanned,
    int NoticesQueued,
    int RemindersQueued,
    int RemindersBlocked,
    int CyclesCompleted,
    IReadOnlyList<string> Errors);

public sealed class SqmsIntegrationOptions
{
    public string OpenBiddingViewUrl { get; set; }
        = "https://example.test/sqms/opportunities/open-to-bidding";

    public int MaxCyclesPerRun { get; set; } = 100;
}
