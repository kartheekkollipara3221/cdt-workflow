using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Sqms.Contracts;

namespace WorkflowEngine.Sqms.LiveAcceptance;

public sealed class AcceptanceScenarioRunner(
    ISqmsWorkflowService workflow,
    ISqmsScheduledService scheduled,
    IProcessNotificationOutboxService outbox,
    WorkflowDbContext dbContext,
    AcceptanceFixtureWriter fixtureWriter,
    AcceptanceReporter reporter,
    AcceptanceTimeProvider clock,
    AcceptanceEvidenceVerifier evidenceVerifier)
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<WorkflowRecipientInput>> NoRecipients =
        new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal);

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var manifest = await CreateFixturesAndDrivePreScheduleAsync(cancellationToken);
        await RunScheduledPhasesAsync(cancellationToken);
        await DrivePostScheduleAsync(manifest, cancellationToken);
        var processed = await outbox.ProcessAsync(cancellationToken);
        reporter.WriteOutbox(processed);
        if (processed.Failure is not null ||
            processed.Processed.Count != AcceptanceConstants.ExpectedEmailCount ||
            !string.Equals(processed.SenderName, "Smtp", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Outbox expected {AcceptanceConstants.ExpectedEmailCount} SMTP deliveries " +
                $"but processed {processed.Processed.Count}; " +
                $"failure={processed.Failure?.DisplayReference ?? "NONE"}.");
        }

        await evidenceVerifier.VerifyAsync(cancellationToken);
        reporter.WriteSummary(manifest);
        return 0;
    }

    public async Task<AcceptanceManifest> CreateFixturesAndDrivePreScheduleAsync(
        CancellationToken cancellationToken)
    {
        var matrix = AcceptanceScenarioMatrix.Create();
        var manifest = await fixtureWriter.CreateAsync(matrix, cancellationToken);
        reporter.WriteManifest(manifest);

        foreach (var opportunity in matrix.Opportunities)
        {
            var id = manifest.OpportunityIds[opportunity.Key];
            await ExecuteAsync(
                "Opportunity",
                id,
                "StartOpportunityAsync",
                null,
                null,
                2,
                SqmsKeys.OpportunityStates.Submitted,
                AcceptanceConstants.Mfo,
                NoRecipients,
                () => workflow.StartOpportunityAsync(id, AcceptanceConstants.Mfo, cancellationToken),
                cancellationToken);

            var version = 2;
            if (opportunity.Key == "A")
            {
                await ExecuteOpportunityActionAsync(
                    id,
                    SqmsKeys.OpportunityActions.ReturnToDraft,
                    version++,
                    version,
                    SqmsKeys.OpportunityStates.Draft,
                    AcceptanceConstants.Ipg,
                    NoRecipients,
                    cancellationToken);
                await ExecuteOpportunityActionAsync(
                    id,
                    SqmsKeys.OpportunityActions.Submit,
                    version++,
                    version,
                    SqmsKeys.OpportunityStates.Submitted,
                    AcceptanceConstants.Mfo,
                    NoRecipients,
                    cancellationToken);
            }

            await ExecuteOpportunityActionAsync(
                id,
                SqmsKeys.OpportunityActions.MarkPending,
                version++,
                version,
                SqmsKeys.OpportunityStates.Pending,
                AcceptanceConstants.Ipg,
                NoRecipients,
                cancellationToken);
            await ExecuteOpportunityActionAsync(
                id,
                SqmsKeys.OpportunityActions.OpenForBidding,
                version++,
                version,
                SqmsKeys.OpportunityStates.OpenToBidding,
                AcceptanceConstants.Ipg,
                NoRecipients,
                cancellationToken);
        }

        foreach (var bid in matrix.AllBids)
        {
            var id = manifest.BidIds[bid.Key];
            await ExecuteAsync(
                "Bid",
                id,
                "StartBidAsync",
                null,
                null,
                2,
                SqmsKeys.BidStates.Submitted,
                AcceptanceConstants.Mfo,
                NoRecipients,
                () => workflow.StartBidAsync(id, AcceptanceConstants.Mfo, cancellationToken),
                cancellationToken);
            if (bid.ReturnBeforeResubmit)
            {
                await ExecuteBidActionAsync(
                    id,
                    SqmsKeys.BidActions.ReturnToDraft,
                    2,
                    3,
                    SqmsKeys.BidStates.Draft,
                    AcceptanceConstants.Ipg,
                    NoRecipients,
                    cancellationToken);
                await ExecuteBidActionAsync(
                    id,
                    SqmsKeys.BidActions.Submit,
                    3,
                    4,
                    SqmsKeys.BidStates.Submitted,
                    AcceptanceConstants.Mfo,
                    NoRecipients,
                    cancellationToken);
            }
        }

        return manifest;
    }

    private async Task RunScheduledPhasesAsync(CancellationToken cancellationToken)
    {
        var definition = AcceptanceConstants.Definition;
        await RunScheduledAsync("OPEN", AcceptanceConstants.OpenRunUtc, 0, 0, 0, 1, 0, cancellationToken);
        await RunScheduledReplayAsync("OPEN-REPLAY", AcceptanceConstants.OpenRunUtc, 1, cancellationToken);
        await RunScheduledAsync("REMINDER", AcceptanceConstants.ReminderRunUtc, 0, 0, 0, 0, 1, cancellationToken);
        await RunScheduledReplayAsync("REMINDER-REPLAY", AcceptanceConstants.ReminderRunUtc, 2, cancellationToken);
        await RunScheduledAsync(
            "CLOSE",
            AcceptanceConstants.CloseRunUtc,
            definition.ExpectedOpportunityCount,
            definition.ExpectedBidsMovedToIpgReview,
            definition.ExpectedBidsNotAllocated,
            0,
            0,
            cancellationToken);
        await RunScheduledReplayAsync("CLOSE-REPLAY", AcceptanceConstants.CloseRunUtc, 0, cancellationToken);
    }

    private async Task RunScheduledAsync(
        string stage,
        DateTimeOffset now,
        int expectedClosed,
        int expectedIpg,
        int expectedNotAllocated,
        int expectedOpen,
        int expectedReminder,
        CancellationToken cancellationToken)
    {
        clock.SetUtcNow(now);
        var result = await scheduled.RunAsync(cancellationToken);
        reporter.WriteScheduled(stage, result);
        if (!result.Success ||
            result.OpportunitiesClosed != expectedClosed ||
            result.BidsMovedToIpgReview != expectedIpg ||
            result.BidsNotAllocated != expectedNotAllocated ||
            result.OpenNotificationsQueued != expectedOpen ||
            result.ReminderNotificationsQueued != expectedReminder)
        {
            throw new InvalidOperationException($"Scheduled stage {stage} did not match the acceptance counts.");
        }
    }

    private async Task RunScheduledReplayAsync(
        string stage,
        DateTimeOffset now,
        int expectedAlreadyQueued,
        CancellationToken cancellationToken)
    {
        clock.SetUtcNow(now);
        var result = await scheduled.RunAsync(cancellationToken);
        reporter.WriteScheduled(stage, result);
        if (!result.Success || result.AlreadyQueued != expectedAlreadyQueued ||
            result.OpportunitiesClosed != 0 || result.BidsMovedToIpgReview != 0 ||
            result.BidsNotAllocated != 0 || result.OpenNotificationsQueued != 0 ||
            result.ReminderNotificationsQueued != 0)
        {
            throw new InvalidOperationException($"Scheduled replay stage {stage} was not idempotent.");
        }
    }

    public async Task DrivePostScheduleAsync(
        AcceptanceManifest manifest,
        CancellationToken cancellationToken)
    {
        var matrix = AcceptanceScenarioMatrix.Create();
        foreach (var opportunity in matrix.Opportunities)
        {
            var parentId = manifest.OpportunityIds[opportunity.Key];
            foreach (var bid in opportunity.Bids)
            {
                var bidId = manifest.BidIds[bid.Key];
                if (bid.TerminalState == SqmsKeys.BidStates.NotAllocated)
                {
                    await AssertCurrentStateAsync(
                        "Bid",
                        bidId,
                        SqmsKeys.BidStates.NotAllocated,
                        bid.ReturnBeforeResubmit ? 5 : 3,
                        cancellationToken);
                    continue;
                }

                var parentBefore = await ReadTargetStateAsync("Opportunity", parentId, cancellationToken);
                var ipgVersion = bid.ReturnBeforeResubmit ? 5 : 3;
                await ExecuteBidActionAsync(
                    bidId,
                    SqmsKeys.BidActions.SendToSellerReview,
                    ipgVersion,
                    ipgVersion + 1,
                    SqmsKeys.BidStates.UnderReviewBySeller,
                    AcceptanceConstants.Ipg,
                    NoRecipients,
                    cancellationToken);
                await ExecuteBidActionAsync(
                    bidId,
                    TerminalBidAction(bid.TerminalState),
                    ipgVersion + 1,
                    ipgVersion + 2,
                    bid.TerminalState,
                    AcceptanceConstants.Seller,
                    BuyerRecipients(bid.Key),
                    cancellationToken);
                var parentAfter = await ReadTargetStateAsync("Opportunity", parentId, cancellationToken);
                if (parentBefore != parentAfter)
                {
                    throw new InvalidOperationException(
                        $"Bid {bid.Key} changed Opportunity {opportunity.Key} from '{parentBefore}' to '{parentAfter}'.");
                }
            }
        }

        foreach (var opportunity in matrix.Opportunities)
        {
            await CompleteOpportunityAsync(
                opportunity,
                manifest.OpportunityIds[opportunity.Key],
                cancellationToken);
        }
    }

    private Task CompleteOpportunityAsync(
        OpportunityScenario opportunity,
        int id,
        CancellationToken cancellationToken) => opportunity.AllocationPath switch
    {
        "AWAITING ALLOCATION -> COMPLETED" =>
            CompleteAwaitingAllocationAsync(opportunity.Key, id, cancellationToken),
        "PARTIALLY ALLOCATED -> COMPLETED" =>
            CompletePartiallyAllocatedAsync(opportunity.Key, id, cancellationToken),
        "NOT FULFILLED" => CompleteNotFulfilledAsync(opportunity.Key, id, cancellationToken),
        _ => throw new InvalidOperationException(
            $"Unknown Opportunity allocation path '{opportunity.AllocationPath}'.")
    };

    private async Task CompleteAwaitingAllocationAsync(
        string key,
        int id,
        CancellationToken cancellationToken)
    {
        await ExecuteOpportunityActionAsync(id, SqmsKeys.OpportunityActions.RequestSellerApproval, 7, 8,
            SqmsKeys.OpportunityStates.PendingSellerApproval, AcceptanceConstants.Ipg,
            SellerRecipients(key.ToLowerInvariant()), cancellationToken);
        await ExecuteOpportunityActionAsync(id, SqmsKeys.OpportunityActions.MarkAwaitingAllocation, 8, 9,
            SqmsKeys.OpportunityStates.AwaitingAllocation, AcceptanceConstants.Ipg, NoRecipients, cancellationToken);
        await ExecuteOpportunityActionAsync(id, SqmsKeys.OpportunityActions.Complete, 9, 10,
            SqmsKeys.OpportunityStates.Completed, AcceptanceConstants.Ipg, NoRecipients, cancellationToken);
    }

    private async Task CompletePartiallyAllocatedAsync(
        string key,
        int id,
        CancellationToken cancellationToken)
    {
        await ExecuteOpportunityActionAsync(id, SqmsKeys.OpportunityActions.RequestSellerApproval, 5, 6,
            SqmsKeys.OpportunityStates.PendingSellerApproval, AcceptanceConstants.Ipg,
            SellerRecipients(key.ToLowerInvariant()), cancellationToken);
        await ExecuteOpportunityActionAsync(id, SqmsKeys.OpportunityActions.MarkPartiallyAllocated, 6, 7,
            SqmsKeys.OpportunityStates.PartiallyAllocated, AcceptanceConstants.Ipg, NoRecipients, cancellationToken);
        await ExecuteOpportunityActionAsync(id, SqmsKeys.OpportunityActions.Complete, 7, 8,
            SqmsKeys.OpportunityStates.Completed, AcceptanceConstants.Ipg, NoRecipients, cancellationToken);
    }

    private async Task CompleteNotFulfilledAsync(
        string key,
        int id,
        CancellationToken cancellationToken)
    {
        await ExecuteOpportunityActionAsync(id, SqmsKeys.OpportunityActions.RequestSellerApproval, 5, 6,
            SqmsKeys.OpportunityStates.PendingSellerApproval, AcceptanceConstants.Ipg,
            SellerRecipients(key.ToLowerInvariant()), cancellationToken);
        await ExecuteOpportunityActionAsync(id, SqmsKeys.OpportunityActions.MarkNotFulfilled, 6, 7,
            SqmsKeys.OpportunityStates.NotFulfilled, AcceptanceConstants.Ipg, NoRecipients, cancellationToken);
    }

    private Task ExecuteOpportunityActionAsync(
        int id,
        string action,
        int inputVersion,
        int resultVersion,
        string state,
        SqmsActor actor,
        IReadOnlyDictionary<string, IReadOnlyList<WorkflowRecipientInput>> recipients,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            "Opportunity",
            id,
            "ExecuteOpportunityActionAsync",
            action,
            inputVersion,
            resultVersion,
            state,
            actor,
            recipients,
            () => workflow.ExecuteOpportunityActionAsync(
                new SqmsExecuteRequest(id, action, inputVersion, actor, recipients),
                cancellationToken),
            cancellationToken);

    private Task ExecuteBidActionAsync(
        int id,
        string action,
        int inputVersion,
        int resultVersion,
        string state,
        SqmsActor actor,
        IReadOnlyDictionary<string, IReadOnlyList<WorkflowRecipientInput>> recipients,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            "Bid",
            id,
            "ExecuteBidActionAsync",
            action,
            inputVersion,
            resultVersion,
            state,
            actor,
            recipients,
            () => workflow.ExecuteBidActionAsync(
                new SqmsExecuteRequest(id, action, inputVersion, actor, recipients),
                cancellationToken),
            cancellationToken);

    private async Task ExecuteAsync(
        string entityType,
        int id,
        string method,
        string? action,
        int? inputVersion,
        int resultVersion,
        string expectedState,
        SqmsActor actor,
        IReadOnlyDictionary<string, IReadOnlyList<WorkflowRecipientInput>> recipients,
        Func<Task<SqmsOperationResult>> operation,
        CancellationToken cancellationToken)
    {
        var sequence = reporter.WriteInput(
            entityType,
            id,
            method,
            action,
            inputVersion,
            actor,
            recipients,
            expectedState);
        var result = await operation();
        var (status, workflowState) = await ReadTargetStateAsync(entityType, id, cancellationToken);
        var actualWorkflow = result.Context?.WorkflowContext.Workflow;
        var parity = result.Success &&
            actualWorkflow?.CurrentState == expectedState &&
            actualWorkflow.WorkflowInstanceVersion == resultVersion &&
            status == expectedState &&
            workflowState == expectedState;
        reporter.WriteResult(sequence, result, status, workflowState, parity);
        if (!parity)
        {
            throw new InvalidOperationException(
                $"{entityType} {id} operation '{action ?? "START"}' failed acceptance. " +
                $"Expected {expectedState} v{resultVersion}; result was {actualWorkflow?.CurrentState} " +
                $"v{actualWorkflow?.WorkflowInstanceVersion}, code {result.ResultCode}.");
        }
    }

    private async Task AssertCurrentStateAsync(
        string entityType,
        int id,
        string expectedState,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        var result = entityType == "Opportunity"
            ? await workflow.GetOpportunityContextAsync(id, AcceptanceConstants.Ipg, cancellationToken)
            : await workflow.GetBidContextAsync(id, AcceptanceConstants.Ipg, cancellationToken);
        var actual = result.Context?.WorkflowContext.Workflow;
        var target = await ReadTargetStateAsync(entityType, id, cancellationToken);
        if (!result.Success || actual?.CurrentState != expectedState ||
            actual.WorkflowInstanceVersion != expectedVersion ||
            target.Status != expectedState || target.WorkflowState != expectedState)
        {
            throw new InvalidOperationException(
                $"{entityType} {id} expected current state {expectedState} v{expectedVersion}.");
        }
    }

    private async Task<(string? Status, string? WorkflowState)> ReadTargetStateAsync(
        string entityType,
        int id,
        CancellationToken cancellationToken)
    {
        var table = entityType switch
        {
            "Opportunity" => "Opportunity",
            "Bid" => "Bid",
            _ => throw new ArgumentOutOfRangeException(nameof(entityType))
        };
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT [Status], [WorkflowState] FROM [SSQMS].[{table}] WHERE [ID] = @id;";
            AddParameter(command, "@id", DbType.Int32, id);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException($"Target {entityType} {id} was not found.");
            }

            return (
                reader.IsDBNull(0) ? null : reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1));
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<WorkflowRecipientInput>> SellerRecipients(string key) =>
        Recipients(
            ("relationshipManager", $"rm-{key}.live@example.test"),
            ("sellerRmTeam", $"seller-{key}.live@example.test"));

    private static IReadOnlyDictionary<string, IReadOnlyList<WorkflowRecipientInput>> BuyerRecipients(string key) =>
        Recipients(
            ("relationshipManager", $"bid{key}-rm.live@example.test"),
            ("buyerRmTeam", $"bid{key}-team.live@example.test"));

    private static IReadOnlyDictionary<string, IReadOnlyList<WorkflowRecipientInput>> Recipients(
        params (string Key, string Address)[] values) =>
        values.ToDictionary(
            value => value.Key,
            value => (IReadOnlyList<WorkflowRecipientInput>)[new(value.Key, value.Address, value.Key)],
            StringComparer.Ordinal);

    private static string TerminalBidAction(string terminalState) => terminalState switch
    {
        SqmsKeys.BidStates.Accepted => SqmsKeys.BidActions.Accept,
        SqmsKeys.BidStates.PartiallyAccepted => SqmsKeys.BidActions.PartiallyAccept,
        SqmsKeys.BidStates.NotAccepted => SqmsKeys.BidActions.Reject,
        _ => throw new ArgumentOutOfRangeException(nameof(terminalState), terminalState, null)
    };

    private static void AddParameter(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
