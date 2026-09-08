using Microsoft.Extensions.Options;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Configuration;
using WorkflowEngine.Sqms.Contracts;
using WorkflowEngine.Sqms.Persistence;
using WorkflowEngine.Sqms.Scheduling;

namespace WorkflowEngine.Tests;

public sealed class SqmsScheduledServiceTests
{
    private static readonly DateTimeOffset OpenedAt = DateTimeOffset.Parse("2030-01-01T15:00:00Z");

    [Fact]
    public async Task DisabledIntegrationReturnsSuccessfulZeroWorkWithoutReadingTargetTables()
    {
        var store = new FakeStore();
        store.AddOpen(Opportunity(10000, "$100.00", new DateTime(2030, 3, 31)), OpenedAt);
        var workflow = new FakeWorkflowService(store);
        var queue = new FakeQueueService();

        var result = await CreateService(OpenedAt.AddDays(16), store, workflow, queue, enabled: false)
            .RunAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.OpportunitiesClosed);
        Assert.Empty(workflow.OpportunityActions);
        Assert.Empty(queue.Requests);
    }

    [Theory]
    [InlineData(1, 1, 0, 0)]
    [InlineData(14, 1, 1, 0)]
    [InlineData(15, 1, 1, 0)]
    [InlineData(16, 0, 0, 1)]
    public async Task CalendarCatchUpQueuesDueGroupsAndClosesBeforeObsoleteNotifications(
        int localDayOffset,
        int expectedOpen,
        int expectedReminder,
        int expectedClosed)
    {
        var store = new FakeStore();
        store.AddOpen(Opportunity(10000, "$100.00", new DateTime(2030, 3, 31)), OpenedAt);
        var workflow = new FakeWorkflowService(store);
        var queue = new FakeQueueService();
        var service = CreateService(OpenedAt.AddDays(localDayOffset), store, workflow, queue);

        var result = await service.RunAsync(CancellationToken.None);

        Assert.Equal(expectedOpen, result.OpenNotificationsQueued);
        Assert.Equal(expectedReminder, result.ReminderNotificationsQueued);
        Assert.Equal(expectedClosed, result.OpportunitiesClosed);
        if (expectedClosed == 1)
        {
            Assert.DoesNotContain(queue.Requests, request => request.NotificationRouteKey == SqmsKeys.Routes.OpportunityReminder);
            Assert.Equal(SqmsKeys.OpportunityActions.CloseBidding, Assert.Single(workflow.OpportunityActions).ActionKey);
        }
    }

    [Fact]
    public async Task GroupNotificationsUseStableKeysAndReplayWithoutDuplicates()
    {
        var store = new FakeStore();
        store.AddOpen(Opportunity(10000, "$100.00", new DateTime(2030, 3, 31)), OpenedAt);
        store.AddOpen(Opportunity(10001, "$200.00", new DateTime(2030, 2, 15)), OpenedAt);
        var workflow = new FakeWorkflowService(store);
        var queue = new FakeQueueService();
        var service = CreateService(OpenedAt.AddDays(14), store, workflow, queue);

        var first = await service.RunAsync(CancellationToken.None);
        var second = await service.RunAsync(CancellationToken.None);

        Assert.Equal(1, first.OpenNotificationsQueued);
        Assert.Equal(1, first.ReminderNotificationsQueued);
        Assert.Equal(2, second.AlreadyQueued);
        Assert.Equal(2, queue.Requests.Select(request => request.NotificationOccurrenceKey).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(queue.Requests, request => request.NotificationOccurrenceKey == "SQMS:OpportunityOpen:2030:Q1:2030-01-02");
        Assert.Contains(queue.Requests, request => request.NotificationOccurrenceKey == "SQMS:OpportunityReminder:2030:Q1:2030-01-15");
        Assert.All(queue.Requests, request => Assert.Equal("https://dashboard.example.test/open?static=true", request.NotificationInput.TemplateData["dashboardUrl"]));
        Assert.All(queue.Requests, request => Assert.Equal("01/16/2030", request.NotificationInput.TemplateData["responseDate"]));
    }

    [Theory]
    [InlineData("$60.00", "$50.00", 2, 1)]
    [InlineData("$50.00", "$50.00", 3, 0)]
    [InlineData("$40.00", "$50.00", 3, 0)]
    public async Task BidClassificationUsesFairValuePremiumSumAndStrictGreaterThreshold(
        string fairValueAmount,
        string premiumAmount,
        int expectedReview,
        int expectedNotAllocated)
    {
        var store = new FakeStore();
        store.AddClosed(Opportunity(10000, "$100.00", new DateTime(2030, 3, 31)));
        store.AddBid(Bid(20000, fairValueAmount, "Fair Value"));
        store.AddBid(Bid(20001, premiumAmount, "PREMIUM"));
        store.AddBid(Bid(20002, "$25.00", "discount"));
        var workflow = new FakeWorkflowService(store);
        var service = CreateService(OpenedAt, store, workflow, new FakeQueueService());

        var result = await service.RunAsync(CancellationToken.None);

        Assert.Equal(expectedReview, result.BidsMovedToIpgReview);
        Assert.Equal(expectedNotAllocated, result.BidsNotAllocated);
        Assert.DoesNotContain(workflow.BidActions, request =>
            request.EntityId != 20002 && request.ActionKey == SqmsKeys.BidActions.MarkNotAllocated);
    }

    [Fact]
    public async Task BidClassificationUsesCategoryInsteadOfExpressedPercentage()
    {
        var store = new FakeStore();
        store.AddClosed(Opportunity(10000, "$100.00", new DateTime(2030, 3, 31)));
        store.AddBid(Bid(20000, "$60.00", "100 %") with { BidFairValue = "Yes, Fair Value" });
        store.AddBid(Bid(20001, "$50.00", "5 %") with { BidFairValue = "No, Premium" });
        store.AddBid(Bid(20002, "$25.00", "5 %") with { BidFairValue = "No, Discount" });
        var workflow = new FakeWorkflowService(store);

        var result = await CreateService(OpenedAt, store, workflow, new FakeQueueService())
            .RunAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.BidsMovedToIpgReview);
        Assert.Equal(1, result.BidsNotAllocated);
        Assert.Equal(
            [SqmsKeys.BidActions.MoveToIpgReview, SqmsKeys.BidActions.MoveToIpgReview, SqmsKeys.BidActions.MarkNotAllocated],
            workflow.BidActions.Select(request => request.ActionKey));
    }

    [Fact]
    public async Task InvalidBidCandidatesFailWithoutBlockingOtherSubmittedBids()
    {
        var store = new FakeStore();
        store.AddClosed(Opportunity(10000, "$100.00", new DateTime(2030, 3, 31)));
        store.AddBid(Bid(20000, "not-money", "Fair Value"));
        store.AddBid(Bid(20001, "$10.00", "unknown"));
        store.AddBid(Bid(20002, "$20.00", "Premium"));
        var workflow = new FakeWorkflowService(store);

        var result = await CreateService(OpenedAt, store, workflow, new FakeQueueService())
            .RunAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(2, result.Failures.Count);
        Assert.Equal(1, result.BidsMovedToIpgReview);
        Assert.Contains(workflow.BidActions, request => request.EntityId == 20002);
        Assert.DoesNotContain(workflow.BidActions, request => request.EntityId is 20000 or 20001);
    }

    [Fact]
    public async Task LaterBidActionFailureDoesNotUndoEarlierOrBlockLaterBidCommit()
    {
        var store = new FakeStore();
        store.AddClosed(Opportunity(10000, "$100.00", new DateTime(2030, 3, 31)));
        store.AddBid(Bid(20000, "$20.00", "Fair Value"));
        store.AddBid(Bid(20001, "$20.00", "Premium"));
        store.AddBid(Bid(20002, "$20.00", "Discount"));
        var workflow = new FakeWorkflowService(store) { FailingBidId = 20001 };

        var result = await CreateService(OpenedAt, store, workflow, new FakeQueueService())
            .RunAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(2, result.BidsMovedToIpgReview);
        Assert.Single(result.Failures);
        Assert.Equal([20000, 20001, 20002], workflow.BidActions.Select(request => request.EntityId));
        Assert.Equal(SqmsKeys.BidStates.IpgReview, store.Bids.Single(row => row.Id == 20000).WorkflowState);
        Assert.Equal(SqmsKeys.BidStates.Submitted, store.Bids.Single(row => row.Id == 20001).WorkflowState);
        Assert.Equal(SqmsKeys.BidStates.IpgReview, store.Bids.Single(row => row.Id == 20002).WorkflowState);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetryKeepsPreviouslyClassifiedBidsInThresholdWithoutExecutingThemAgain(bool advanceEarlierBids)
    {
        var store = new FakeStore();
        store.AddClosed(Opportunity(10000, "$100.00", new DateTime(2030, 3, 31)));
        store.AddBid(Bid(20000, "$60.00", "Fair Value"));
        store.AddBid(Bid(20001, "$50.00", "Premium"));
        store.AddBid(Bid(20002, "$25.00", "Discount"));
        var firstWorkflow = new FakeWorkflowService(store) { FailingBidId = 20002 };
        var first = await CreateService(OpenedAt, store, firstWorkflow, new FakeQueueService())
            .RunAsync(CancellationToken.None);
        Assert.Equal(2, first.BidsMovedToIpgReview);
        Assert.Single(first.Failures);
        if (advanceEarlierBids)
        {
            store.SetBidState(20000, SqmsKeys.BidStates.UnderReviewBySeller);
            store.SetBidState(20001, SqmsKeys.BidStates.UnderReviewBySeller);
        }

        var retryWorkflow = new FakeWorkflowService(store);
        var retry = await CreateService(OpenedAt.AddDays(1), store, retryWorkflow, new FakeQueueService())
            .RunAsync(CancellationToken.None);

        Assert.True(retry.Success);
        Assert.Equal(1, retry.BidsNotAllocated);
        Assert.Equal(0, retry.BidsMovedToIpgReview);
        Assert.Equal(20002, Assert.Single(retryWorkflow.BidActions).EntityId);
        Assert.Equal(SqmsKeys.BidStates.NotAllocated, store.Bids.Single(row => row.Id == 20002).WorkflowState);
    }

    private static SqmsScheduledService CreateService(
        DateTimeOffset now,
        FakeStore store,
        FakeWorkflowService workflow,
        FakeQueueService queue,
        bool enabled = true)
    {
        var options = new SqmsOptions
        {
            Enabled = enabled,
            Links = new SqmsLinkOptions
            {
                OpenBiddingDashboardUrl = "https://dashboard.example.test/open?static=true"
            },
            Scheduling = new SqmsSchedulingOptions
            {
                TimeZoneId = "UTC",
                BiddingDays = 15,
                SellerResponseDays = 5
            }
        };
        return new SqmsScheduledService(
            store,
            workflow,
            workflow,
            queue,
            new SqmsLinkBuilder(options.Links),
            Options.Create(options),
            new FixedTimeProvider(now));
    }

    private static SqmsOpportunityRow Opportunity(int id, string saleAmount, DateTime effectiveDate) =>
        new(id, "Seller Team", $"Account {id}", $"A-{id}", "RM", "Fund", "Vehicle", saleAmount, effectiveDate, "", SqmsKeys.OpportunityStates.OpenToBidding);

    private static SqmsBidRow Bid(int id, string amount, string category) =>
        new(id, 10000, "Buyer Team", $"Bid {id}", $"B-{id}", "RM", amount, category, "", SqmsKeys.BidStates.Submitted);

    private sealed class FakeStore : ISqmsDataStore
    {
        public List<SqmsOpenOpportunityRow> Open { get; } = [];
        public List<SqmsOpportunityRow> Closed { get; } = [];
        public List<SqmsBidRow> Bids { get; } = [];
        private readonly HashSet<int> classifiedBidIds = [];

        public void AddOpen(SqmsOpportunityRow row, DateTimeOffset openedAt) => Open.Add(new(row, openedAt));
        public void AddClosed(SqmsOpportunityRow row) => Closed.Add(row with { WorkflowState = SqmsKeys.OpportunityStates.ClosedToBidding });
        public void AddBid(SqmsBidRow row) => Bids.Add(row);

        public Task<SqmsOpportunityRow?> GetOpportunityAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(Open.Select(row => row.Opportunity).Concat(Closed).SingleOrDefault(row => row.Id == id));
        public Task<SqmsBidRow?> GetBidAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(Bids.SingleOrDefault(row => row.Id == id));
        public Task<IReadOnlyList<SqmsOpenOpportunityRow>> GetOpenOpportunitiesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SqmsOpenOpportunityRow>>(Open.Where(row => row.Opportunity.WorkflowState == SqmsKeys.OpportunityStates.OpenToBidding).ToArray());
        public Task<IReadOnlyList<SqmsOpportunityRow>> GetClosedOpportunitiesWithSubmittedBidsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SqmsOpportunityRow>>(Closed.Where(opportunity => Bids.Any(bid => bid.OpportunityId == opportunity.Id && bid.WorkflowState == SqmsKeys.BidStates.Submitted)).ToArray());
        public Task<IReadOnlyList<SqmsBidRow>> GetSubmittedBidsAsync(int opportunityId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SqmsBidRow>>(Bids.Where(row => row.OpportunityId == opportunityId && row.WorkflowState == SqmsKeys.BidStates.Submitted).ToArray());
        public Task<IReadOnlyList<SqmsBidRow>> GetBidClassificationBasisAsync(int opportunityId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SqmsBidRow>>(Bids.Where(row => row.OpportunityId == opportunityId &&
                (row.WorkflowState == SqmsKeys.BidStates.Submitted || classifiedBidIds.Contains(row.Id))).ToArray());
        public Task ProjectOpportunityStateAsync(int id, string state, string modifiedBy, DateTime modifiedAtUtc, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ProjectBidStateAsync(int id, string state, string modifiedBy, DateTime modifiedAtUtc, CancellationToken cancellationToken) => Task.CompletedTask;

        public void SetOpportunityState(int id, string state)
        {
            var index = Open.FindIndex(row => row.Opportunity.Id == id);
            if (index >= 0)
            {
                var updated = Open[index].Opportunity with { WorkflowState = state };
                Open[index] = Open[index] with { Opportunity = updated };
                if (state == SqmsKeys.OpportunityStates.ClosedToBidding)
                {
                    Closed.Add(updated);
                }
            }
        }

        public void SetBidState(int id, string state)
        {
            if (state is SqmsKeys.BidStates.IpgReview or SqmsKeys.BidStates.NotAllocated)
            {
                classifiedBidIds.Add(id);
            }
            var index = Bids.FindIndex(row => row.Id == id);
            Bids[index] = Bids[index] with { WorkflowState = state };
        }
    }

    private sealed class FakeWorkflowService(FakeStore store) : ISqmsWorkflowService, ISqmsScheduledTransitionService
    {
        public int? FailingBidId { get; init; }
        public List<SqmsExecuteRequest> OpportunityActions { get; } = [];
        public List<SqmsExecuteRequest> BidActions { get; } = [];

        public Task<SqmsOperationResult> GetOpportunityContextAsync(int opportunityId, SqmsActor actor, CancellationToken cancellationToken) =>
            Task.FromResult(Context(4, SqmsKeys.OpportunityStates.OpenToBidding));
        public Task<SqmsOperationResult> GetBidContextAsync(int bidId, SqmsActor actor, CancellationToken cancellationToken) =>
            Task.FromResult(Context(2, SqmsKeys.BidStates.Submitted));
        public Task<SqmsOperationResult> ExecuteOpportunityActionAsync(SqmsExecuteRequest request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The scheduler must not use the request-driven Opportunity action path.");
        }
        public Task<SqmsOperationResult> ExecuteBidActionAsync(SqmsExecuteRequest request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The scheduler must not use the request-driven Bid action path.");
        }
        public Task<SqmsOperationResult> ExecuteScheduledOpportunityActionAsync(SqmsExecuteRequest request, CancellationToken cancellationToken)
        {
            OpportunityActions.Add(request);
            store.SetOpportunityState(request.EntityId, SqmsKeys.OpportunityStates.ClosedToBidding);
            return Task.FromResult(Context(request.ExpectedWorkflowVersion + 1, SqmsKeys.OpportunityStates.ClosedToBidding));
        }
        public Task<SqmsOperationResult> ExecuteScheduledBidActionAsync(SqmsExecuteRequest request, CancellationToken cancellationToken)
        {
            BidActions.Add(request);
            if (request.EntityId == FailingBidId)
            {
                return Task.FromResult(new SqmsOperationResult(false, "ForcedFailure", "forced bid failure", null, null));
            }

            var state = request.ActionKey == SqmsKeys.BidActions.MarkNotAllocated
                ? SqmsKeys.BidStates.NotAllocated
                : SqmsKeys.BidStates.IpgReview;
            store.SetBidState(request.EntityId, state);
            return Task.FromResult(Context(request.ExpectedWorkflowVersion + 1, state));
        }
        public Task<SqmsOperationResult> StartOpportunityAsync(int opportunityId, SqmsActor actor, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SqmsOperationResult> StartBidAsync(int bidId, SqmsActor actor, CancellationToken cancellationToken) => throw new NotSupportedException();

        private static SqmsOperationResult Context(int version, string state) =>
            new(true, "Success", null, null, new WorkflowContextResponse
            {
                WorkflowContext = new WorkflowContext
                {
                    Workflow = new WorkflowContextWorkflow
                    {
                        WorkflowInstanceVersion = version,
                        CurrentState = state
                    }
                }
            });
    }

    private sealed class FakeQueueService : IQueueWorkflowNotificationService
    {
        private readonly Dictionary<string, long> ids = new(StringComparer.Ordinal);
        public List<QueueWorkflowNotificationRequest> Requests { get; } = [];

        public Task<QueueWorkflowNotificationResult> QueueAsync(QueueWorkflowNotificationRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (ids.TryGetValue(request.NotificationOccurrenceKey, out var existingId))
            {
                return Task.FromResult(new QueueWorkflowNotificationResult(true, "Success", existingId, true, []));
            }

            var id = ids.Count + 1L;
            ids.Add(request.NotificationOccurrenceKey, id);
            return Task.FromResult(new QueueWorkflowNotificationResult(true, "Success", id, false, []));
        }
    }
}
