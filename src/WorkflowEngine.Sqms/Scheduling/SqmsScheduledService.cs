using System.Globalization;
using Microsoft.Extensions.Options;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Sqms.Configuration;
using WorkflowEngine.Sqms.Contracts;
using WorkflowEngine.Sqms.Persistence;

namespace WorkflowEngine.Sqms.Scheduling;

public sealed class SqmsScheduledService(
    ISqmsDataStore dataStore,
    ISqmsWorkflowService workflowService,
    ISqmsScheduledTransitionService scheduledTransitionService,
    IQueueWorkflowNotificationService notificationQueue,
    SqmsLinkBuilder linkBuilder,
    IOptions<SqmsOptions> options,
    TimeProvider timeProvider) : ISqmsScheduledService
{
    private static readonly SqmsActor SystemActor = new("system", "System", [SqmsKeys.Roles.System]);
    private readonly SqmsOptions options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public async Task<SqmsScheduledRunResult> RunAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (!options.Enabled)
        {
            return new SqmsScheduledRunResult(now, 0, 0, 0, 0, 0, 0, []);
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(options.Scheduling.TimeZoneId);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var failures = new List<SqmsScheduledFailure>();
        var opportunitiesClosed = 0;
        var bidsMovedToIpgReview = 0;
        var bidsNotAllocated = 0;
        var openNotificationsQueued = 0;
        var reminderNotificationsQueued = 0;
        var alreadyQueued = 0;

        var openBeforeClose = await dataStore.GetOpenOpportunitiesAsync(cancellationToken);
        foreach (var candidate in openBeforeClose)
        {
            var openedDate = LocalDate(candidate.OpenedAtUtc, zone);
            if (localDate < openedDate.AddDays(options.Scheduling.BiddingDays + 1))
            {
                continue;
            }

            var context = await workflowService.GetOpportunityContextAsync(
                candidate.Opportunity.Id,
                SystemActor,
                cancellationToken);
            if (!TryVersion(context, out var version, out var contextError))
            {
                failures.Add(Failure("Opportunity", candidate.Opportunity.Id, "Close", contextError));
                continue;
            }

            var result = await scheduledTransitionService.ExecuteScheduledOpportunityActionAsync(
                new SqmsExecuteRequest(
                    candidate.Opportunity.Id,
                    SqmsKeys.OpportunityActions.CloseBidding,
                    version,
                    SystemActor,
                    EmptyRecipients()),
                cancellationToken);
            if (result.Success)
            {
                opportunitiesClosed++;
            }
            else
            {
                failures.Add(Failure("Opportunity", candidate.Opportunity.Id, "Close", ResultError(result)));
            }
        }

        var closedOpportunities = await dataStore.GetClosedOpportunitiesWithSubmittedBidsAsync(cancellationToken);
        foreach (var opportunity in closedOpportunities)
        {
            var saleAmount = SqmsValueParser.ParseAmount(opportunity.CommitmentAmountSale);
            if (!saleAmount.Success)
            {
                failures.Add(Failure("Opportunity", opportunity.Id, "ClassifyBids", saleAmount.Error!));
                continue;
            }

            var bids = await dataStore.GetBidClassificationBasisAsync(opportunity.Id, cancellationToken);
            var validBids = new List<ParsedBid>();
            foreach (var bid in bids)
            {
                var amount = SqmsValueParser.ParseAmount(bid.CommitmentAmountRequested);
                var categoryInput = string.IsNullOrWhiteSpace(bid.BidFairValue)
                    ? bid.BidExpressedAsFairValue
                    : bid.BidFairValue;
                var category = SqmsValueParser.ParseBidCategory(categoryInput);
                if (!amount.Success || !category.Success)
                {
                    var error = string.Join(" ", new[] { amount.Error, category.Error }.Where(value => value is not null));
                    failures.Add(Failure("Bid", bid.Id, "Classify", error));
                    continue;
                }

                validBids.Add(new ParsedBid(bid, amount.Value, category.Value));
            }

            var qualifyingSum = validBids
                .Where(candidate => candidate.Category is SqmsBidCategory.FairValue or SqmsBidCategory.Premium)
                .Sum(candidate => candidate.Amount);
            var rejectDiscounts = qualifyingSum > saleAmount.Value;
            foreach (var candidate in validBids)
            {
                if (candidate.Bid.WorkflowState != SqmsKeys.BidStates.Submitted)
                {
                    continue;
                }

                var actionKey = rejectDiscounts && candidate.Category == SqmsBidCategory.Discount
                    ? SqmsKeys.BidActions.MarkNotAllocated
                    : SqmsKeys.BidActions.MoveToIpgReview;
                var context = await workflowService.GetBidContextAsync(
                    candidate.Bid.Id,
                    SystemActor,
                    cancellationToken);
                if (!TryVersion(context, out var version, out var contextError))
                {
                    failures.Add(Failure("Bid", candidate.Bid.Id, "Classify", contextError));
                    continue;
                }

                var result = await scheduledTransitionService.ExecuteScheduledBidActionAsync(
                    new SqmsExecuteRequest(
                        candidate.Bid.Id,
                        actionKey,
                        version,
                        SystemActor,
                        EmptyRecipients()),
                    cancellationToken);
                if (!result.Success)
                {
                    failures.Add(Failure("Bid", candidate.Bid.Id, actionKey, ResultError(result)));
                    continue;
                }

                if (actionKey == SqmsKeys.BidActions.MarkNotAllocated)
                {
                    bidsNotAllocated++;
                }
                else
                {
                    bidsMovedToIpgReview++;
                }
            }
        }

        var dashboardLink = linkBuilder.BuildOpenBiddingDashboardLink();
        var stillOpen = await dataStore.GetOpenOpportunitiesAsync(cancellationToken);
        var notificationCandidates = new List<NotificationCandidate>();
        foreach (var candidate in stillOpen)
        {
            if (candidate.Opportunity.EffectiveDateForSale is null)
            {
                failures.Add(Failure(
                    "Opportunity",
                    candidate.Opportunity.Id,
                    "QueueNotifications",
                    "Opportunity.EffectiveDateForSale is required."));
                continue;
            }

            if (!dashboardLink.Success)
            {
                failures.Add(Failure(
                    "Opportunity",
                    candidate.Opportunity.Id,
                    "QueueNotifications",
                    dashboardLink.Error!));
                continue;
            }

            var openedDate = LocalDate(candidate.OpenedAtUtc, zone);
            var effectiveDate = DateOnly.FromDateTime(candidate.Opportunity.EffectiveDateForSale.Value);
            notificationCandidates.Add(new NotificationCandidate(
                candidate.Opportunity,
                openedDate,
                effectiveDate.Year,
                ((effectiveDate.Month - 1) / 3) + 1));
        }

        (openNotificationsQueued, reminderNotificationsQueued, alreadyQueued) = await QueueGroupsAsync(
            notificationCandidates,
            localDate,
            dashboardLink.Url,
            failures,
            cancellationToken);

        return new SqmsScheduledRunResult(
            now,
            opportunitiesClosed,
            bidsMovedToIpgReview,
            bidsNotAllocated,
            openNotificationsQueued,
            reminderNotificationsQueued,
            alreadyQueued,
            failures);
    }

    private async Task<(int Open, int Reminder, int Replay)> QueueGroupsAsync(
        IReadOnlyList<NotificationCandidate> candidates,
        DateOnly localDate,
        string? dashboardUrl,
        List<SqmsScheduledFailure> failures,
        CancellationToken cancellationToken)
    {
        var open = 0;
        var reminder = 0;
        var replay = 0;
        foreach (var kind in new[] { NotificationKind.Open, NotificationKind.Reminder })
        {
            var offset = kind == NotificationKind.Open ? 1 : options.Scheduling.BiddingDays - 1;
            var due = candidates
                .Select(candidate => new DueCandidate(candidate, candidate.OpenedDate.AddDays(offset)))
                .Where(candidate => localDate >= candidate.OccurrenceDate)
                .GroupBy(candidate => new
                {
                    candidate.Candidate.Year,
                    candidate.Candidate.Quarter,
                    candidate.OccurrenceDate
                })
                .OrderBy(group => group.Key.Year)
                .ThenBy(group => group.Key.Quarter)
                .ThenBy(group => group.Key.OccurrenceDate);
            foreach (var group in due)
            {
                var route = kind == NotificationKind.Open
                    ? SqmsKeys.Routes.OpportunityOpen
                    : SqmsKeys.Routes.OpportunityReminder;
                var kindKey = kind == NotificationKind.Open ? "OpportunityOpen" : "OpportunityReminder";
                var occurrenceKey = string.Create(
                    CultureInfo.InvariantCulture,
                    $"SQMS:{kindKey}:{group.Key.Year}:Q{group.Key.Quarter}:{group.Key.OccurrenceDate:yyyy-MM-dd}");
                var responseDate = group.First().Candidate.OpenedDate.AddDays(options.Scheduling.BiddingDays);
                var result = await notificationQueue.QueueAsync(
                    new QueueWorkflowNotificationRequest(
                        SqmsKeys.OpportunityWorkflow,
                        1,
                        route,
                        occurrenceKey,
                        "SqmsOpportunityGroup",
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"{group.Key.Year}-Q{group.Key.Quarter}:{group.Key.OccurrenceDate:yyyy-MM-dd}"),
                        new WorkflowNotificationInput
                        {
                            TemplateData = new Dictionary<string, string?>(StringComparer.Ordinal)
                            {
                                ["quarter"] = $"Q{group.Key.Quarter}",
                                ["year"] = group.Key.Year.ToString(CultureInfo.InvariantCulture),
                                ["responseDate"] = responseDate.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture),
                                ["dashboardUrl"] = dashboardUrl
                            }
                        },
                        occurrenceKey),
                    cancellationToken);
                if (!result.Success)
                {
                    failures.Add(new SqmsScheduledFailure(
                        "OpportunityGroup",
                        $"{group.Key.Year}-Q{group.Key.Quarter}",
                        route,
                        string.Join(" ", result.Errors)));
                }
                else if (result.Replay)
                {
                    replay++;
                }
                else if (kind == NotificationKind.Open)
                {
                    open++;
                }
                else
                {
                    reminder++;
                }
            }
        }

        return (open, reminder, replay);
    }

    private static bool TryVersion(
        SqmsOperationResult context,
        out int version,
        out string error)
    {
        version = context.Context?.WorkflowContext.Workflow.WorkflowInstanceVersion ?? 0;
        if (context.Success && version > 0)
        {
            error = string.Empty;
            return true;
        }

        error = ResultError(context);
        return false;
    }

    private static string ResultError(SqmsOperationResult result) =>
        string.IsNullOrWhiteSpace(result.Message) ? result.ResultCode : result.Message;

    private static SqmsScheduledFailure Failure(string type, int id, string operation, string error) =>
        new(type, id.ToString(CultureInfo.InvariantCulture), operation, error);

    private static Dictionary<string, IReadOnlyList<WorkflowRecipientInput>> EmptyRecipients() =>
        new(StringComparer.Ordinal);

    private static DateOnly LocalDate(DateTimeOffset value, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, zone).DateTime);

    private sealed record ParsedBid(SqmsBidRow Bid, decimal Amount, SqmsBidCategory Category);
    private sealed record NotificationCandidate(SqmsOpportunityRow Opportunity, DateOnly OpenedDate, int Year, int Quarter);
    private sealed record DueCandidate(NotificationCandidate Candidate, DateOnly OccurrenceDate);
    private enum NotificationKind { Open, Reminder }
}
