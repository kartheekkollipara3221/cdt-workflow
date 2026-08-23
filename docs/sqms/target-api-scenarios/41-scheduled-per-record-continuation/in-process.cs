using WorkflowEngine.Sqms.Contracts;

// Testing only: arrange submitted Bids 20000, 20001, and 20002 under one closed Opportunity.
// The injected ISqmsScheduledTransitionService returns failure only for Bid 20001.
SqmsScheduledRunResult result = await sqmsScheduledService.RunAsync(cancellationToken);
if (result.Success) throw new InvalidOperationException("The run must report the middle-candidate failure.");
if (result.Failures.Count != 1 || result.Failures[0].EntityId != "20001")
    throw new InvalidOperationException("Expected exactly the middle Bid failure.");
if (result.BidsMovedToIpgReview != 2)
    throw new InvalidOperationException("First and last candidates must succeed.");

// Operator host requirement: return a nonzero exit when result.Success is false.
