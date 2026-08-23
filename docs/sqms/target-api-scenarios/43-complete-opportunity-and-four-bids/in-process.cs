using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Contracts;

// Implemented service contracts. Everything named actorFactory, recipientResolver,
// targetRepository, targetErrorMapper, or targetTransactionCoordinator below is a
// target-owned integration placeholder, not a class supplied by this repository.
var mfo = actorFactory.CreateFromAuthenticatedUser([SqmsKeys.Roles.Mfo]);
var ipg = actorFactory.CreateFromAuthenticatedUser([SqmsKeys.Roles.Ipg]);
var seller = actorFactory.CreateFromAuthenticatedUser([SqmsKeys.Roles.Seller]);
var noRecipients = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal);

async Task AcceptRequestAsync(
    string entityType,
    int entityId,
    Func<CancellationToken, Task<SqmsOperationResult>> invoke)
{
    // Target implementation required: begin one outer SQL transaction, save prerequisite
    // business data, and enlist WorkflowDbContext on the same connection and transaction.
    await using var transaction = await targetTransactionCoordinator.BeginAsync(cancellationToken);
    var before = await targetRepository.LoadAsync(entityType, entityId, cancellationToken);
    var result = await invoke(cancellationToken);
    if (!result.Success)
    {
        await transaction.RollbackAsync(cancellationToken);
        throw targetErrorMapper.Map(result);
    }

    var replay = result.WorkflowResult?.Result.Diagnostics.Replay ?? false;
    var state = result.WorkflowResult?.Result.NewState
        ?? result.WorkflowResult?.Result.CurrentState
        ?? result.Context?.WorkflowContext.Workflow.CurrentState
        ?? throw new InvalidOperationException("Successful SQMS operation returned no authoritative state.");

    // Request-driven ISqmsWorkflowService calls must leave the complete target row unchanged.
    await targetRepository.AssertUnchangedAsync(before, cancellationToken);
    if (replay)
    {
        await transaction.RollbackAsync(cancellationToken);
        await targetRepository.ReloadAndVerifyProjectionAsync(entityType, entityId, state, cancellationToken);
        return;
    }

    await targetRepository.SaveReturnedStateAsync(
        entityType,
        entityId,
        status: state,
        workflowState: state,
        modifiedBy: actorFactory.Current.UserId,
        modifiedAtUtc: timeProvider.GetUtcNow(),
        cancellationToken);
    await transaction.CommitAsync(cancellationToken);
}

// Opportunity request calls: five before scheduling and three afterward.
await AcceptRequestAsync("Opportunity", 10000,
    ct => sqmsWorkflowService.StartOpportunityAsync(10000, mfo, ct));
await AcceptRequestAsync("Opportunity", 10000,
    ct => sqmsWorkflowService.ExecuteOpportunityActionAsync(
        new(10000, SqmsKeys.OpportunityActions.ReturnToDraft, 2, ipg, noRecipients), ct));
await AcceptRequestAsync("Opportunity", 10000,
    ct => sqmsWorkflowService.ExecuteOpportunityActionAsync(
        new(10000, SqmsKeys.OpportunityActions.Submit, 3, mfo, noRecipients), ct));
await AcceptRequestAsync("Opportunity", 10000,
    ct => sqmsWorkflowService.ExecuteOpportunityActionAsync(
        new(10000, SqmsKeys.OpportunityActions.MarkPending, 4, ipg, noRecipients), ct));
await AcceptRequestAsync("Opportunity", 10000,
    ct => sqmsWorkflowService.ExecuteOpportunityActionAsync(
        new(10000, SqmsKeys.OpportunityActions.OpenForBidding, 5, ipg, noRecipients), ct));

// Four Bid starts plus the existing-Draft round trip for Bid 10000.
foreach (var bidId in new[] { 10000, 10001, 10002, 10003 })
{
    await AcceptRequestAsync("Bid", bidId,
        ct => sqmsWorkflowService.StartBidAsync(bidId, mfo, ct));
}
await AcceptRequestAsync("Bid", 10000,
    ct => sqmsWorkflowService.ExecuteBidActionAsync(
        new(10000, SqmsKeys.BidActions.ReturnToDraft, 2, ipg, noRecipients), ct));
await AcceptRequestAsync("Bid", 10000,
    ct => sqmsWorkflowService.ExecuteBidActionAsync(
        new(10000, SqmsKeys.BidActions.Submit, 3, mfo, noRecipients), ct));

// Operator-owned scheduling: set the controlled TimeProvider to the configured occurrences.
// There is no public scheduled HTTP route. The repeated calls prove occurrence replay.
foreach (var runAtUtc in new[] { openRunUtc, openRunUtc, reminderRunUtc, reminderRunUtc, closeRunUtc })
{
    controlledTimeProvider.SetUtcNow(runAtUtc);
    SqmsScheduledRunResult scheduled = await sqmsScheduledService.RunAsync(cancellationToken);
    if (!scheduled.Success) throw new InvalidOperationException("SQMS scheduled processing failed.");
}
// Scheduled close/classification directly projects only Status, WorkflowState, ModifiedBy,
// and ModifiedDate. Do not call the target-owned request persistence helper for these rows.

async Task BidActionAsync(int bidId, string actionKey, int version, SqmsActor actor)
{
    var recipients = actionKey is SqmsKeys.BidActions.Accept
        or SqmsKeys.BidActions.PartiallyAccept
        or SqmsKeys.BidActions.Reject
        ? await recipientResolver.ResolveBidOutcomeAsync(bidId, cancellationToken)
        : noRecipients;
    await AcceptRequestAsync("Bid", bidId,
        ct => sqmsWorkflowService.ExecuteBidActionAsync(
            new SqmsExecuteRequest(bidId, actionKey, version, actor, recipients), ct));
}

await BidActionAsync(10000, SqmsKeys.BidActions.SendToSellerReview, 5, ipg);
await BidActionAsync(10000, SqmsKeys.BidActions.Accept, 6, seller);
await BidActionAsync(10001, SqmsKeys.BidActions.SendToSellerReview, 3, ipg);
await BidActionAsync(10001, SqmsKeys.BidActions.PartiallyAccept, 4, seller);
await BidActionAsync(10002, SqmsKeys.BidActions.SendToSellerReview, 3, ipg);
await BidActionAsync(10002, SqmsKeys.BidActions.Reject, 4, seller);

var sellerApprovalRecipients = await recipientResolver.ResolveOpportunitySellerApprovalAsync(
    10000,
    cancellationToken);
await AcceptRequestAsync("Opportunity", 10000,
    ct => sqmsWorkflowService.ExecuteOpportunityActionAsync(
        new(10000, SqmsKeys.OpportunityActions.RequestSellerApproval, 7, ipg, sellerApprovalRecipients), ct));
await AcceptRequestAsync("Opportunity", 10000,
    ct => sqmsWorkflowService.ExecuteOpportunityActionAsync(
        new(10000, SqmsKeys.OpportunityActions.MarkAwaitingAllocation, 8, ipg, noRecipients), ct));
await AcceptRequestAsync("Opportunity", 10000,
    ct => sqmsWorkflowService.ExecuteOpportunityActionAsync(
        new(10000, SqmsKeys.OpportunityActions.Complete, 9, ipg, noRecipients), ct));

// Target/operator implementation required: invoke the existing outbox processor with a
// fake/local SMTP sender, then run expected-results.sql and inspect expected-emails.md.
