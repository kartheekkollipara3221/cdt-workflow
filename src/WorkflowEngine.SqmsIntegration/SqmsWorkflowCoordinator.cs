using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.WorkflowExecution;
using WorkflowEngine.Application.WorkflowStart;
using WorkflowEngine.Infrastructure.Persistence;

namespace WorkflowEngine.SqmsIntegration;

public sealed class SqmsWorkflowCoordinator(
    WorkflowDbContext dbContext,
    SqmsApplicationRepository repository,
    IStartWorkflowService startWorkflowService,
    IWorkflowContextReader contextReader,
    IWorkflowExecutionService workflowExecutionService,
    WorkflowActionEnvelopeNormalizer normalizer,
    CanonicalRequestHasher hasher,
    TimeProvider timeProvider)
{
    public SqmsWorkflowResult StartOpportunity(int opportunityId, SqmsActor actor)
    {
        return Start(
            SqmsEntityKind.Opportunity,
            opportunityId,
            "SqmsOpportunity",
            SqmsWorkflowIdentity.OpportunityBusinessObjectId(opportunityId),
            SqmsWorkflowIdentity.OpportunityWorkflowInstanceId(opportunityId),
            actor);
    }

    public SqmsWorkflowResult StartBid(int bidId, SqmsActor actor)
    {
        return Start(
            SqmsEntityKind.Bid,
            bidId,
            "SqmsBid",
            SqmsWorkflowIdentity.BidBusinessObjectId(bidId),
            SqmsWorkflowIdentity.BidWorkflowInstanceId(bidId),
            actor);
    }

    public WorkflowContext GetOpportunityContext(int opportunityId, SqmsActor actor)
    {
        ValidateActor(actor);
        return ReadContext(SqmsWorkflowIdentity.OpportunityWorkflowInstanceId(opportunityId), actor);
    }

    public WorkflowContext GetBidContext(int bidId, SqmsActor actor)
    {
        ValidateActor(actor);
        return ReadContext(SqmsWorkflowIdentity.BidWorkflowInstanceId(bidId), actor);
    }

    public SqmsWorkflowResult ExecuteOpportunityAction(
        int opportunityId,
        SqmsActionRequest request,
        SqmsActor actor,
        WorkflowNotificationInput? notificationInput = null)
    {
        return ExecuteAction(
            SqmsEntityKind.Opportunity,
            opportunityId,
            SqmsWorkflowIdentity.OpportunityWorkflowInstanceId(opportunityId),
            request,
            actor,
            notificationInput);
    }

    public SqmsWorkflowResult ExecuteBidAction(
        int bidId,
        SqmsActionRequest request,
        SqmsActor actor,
        WorkflowNotificationInput? notificationInput = null)
    {
        return ExecuteAction(
            SqmsEntityKind.Bid,
            bidId,
            SqmsWorkflowIdentity.BidWorkflowInstanceId(bidId),
            request,
            actor,
            notificationInput);
    }

    private SqmsWorkflowResult Start(
        SqmsEntityKind entityKind,
        int entityId,
        string workflowKey,
        string businessObjectId,
        string workflowInstanceId,
        SqmsActor actor)
    {
        ValidateActor(actor);
        return InTransaction($"start SQMS {entityKind}", () =>
        {
            var applicationState = repository.ReadForUpdate(entityKind, entityId);
            var startResult = startWorkflowService.Start(new StartWorkflowRequest(
                workflowKey,
                workflowKey,
                businessObjectId,
                actor.UserId,
                Reset: false,
                StartedByDisplayName: actor.DisplayName,
                StartedByRoles: actor.Roles));
            if (!startResult.Envelope.Result.Success
                && !string.Equals(startResult.Envelope.Result.ResultCode, "AlreadyExists", StringComparison.Ordinal))
            {
                throw EngineFailure("start", startResult.Envelope);
            }

            var context = ReadContext(workflowInstanceId, actor);
            return Project(entityKind, entityId, applicationState.WorkflowState, actor.UserId, context);
        });
    }

    private SqmsWorkflowResult ExecuteAction(
        SqmsEntityKind entityKind,
        int entityId,
        string workflowInstanceId,
        SqmsActionRequest request,
        SqmsActor actor,
        WorkflowNotificationInput? notificationInput)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateActor(actor);
        if (string.IsNullOrWhiteSpace(request.ActionKey))
        {
            throw new ArgumentException("SQMS actionKey is required.", nameof(request));
        }

        return InTransaction($"execute SQMS {entityKind} action", () =>
        {
            var applicationState = repository.ReadForUpdate(entityKind, entityId);
            var before = ReadContext(workflowInstanceId, actor);
            if (!string.Equals(applicationState.WorkflowState, before.Workflow.CurrentState, StringComparison.Ordinal))
            {
                throw new SqmsIntegrationException(
                    $"SQMS {entityKind} row '{entityId}' is in '{applicationState.WorkflowState ?? "<null>"}' " +
                    $"but workflow '{workflowInstanceId}' is in '{before.Workflow.CurrentState}'.");
            }

            var action = before.AvailableActions.SingleOrDefault(candidate =>
                string.Equals(candidate.ActionKey, request.ActionKey.Trim(), StringComparison.Ordinal));
            if (action is null)
            {
                throw new SqmsIntegrationException(
                    $"Action '{request.ActionKey}' is not available for SQMS {entityKind} row '{entityId}'.");
            }

            if (entityKind == SqmsEntityKind.Bid
                && string.Equals(action.ActionKey, "SubmitBid", StringComparison.Ordinal))
            {
                ValidateBidSubmission(entityId);
            }

            var operationId = Guid.NewGuid().ToString("N");
            var envelope = new WorkflowActionEnvelope
            {
                Request = new WorkflowRequest
                {
                    CommandType = "ExecuteWorkflowAction",
                    CommandVersion = 1,
                    ClientActionId = operationId,
                    CorrelationId = operationId,
                    RequestedAtUtc = timeProvider.GetUtcNow(),
                    SourceSystem = "SQMS"
                },
                Actor = new WorkflowActor
                {
                    UserId = actor.UserId,
                    DisplayName = actor.DisplayName,
                    Roles = actor.Roles.ToList()
                },
                UiAction = new WorkflowUiAction
                {
                    SectionKey = action.SectionKey,
                    ButtonKey = action.ButtonKey,
                    ButtonLabelShown = action.ButtonLabel,
                    SaveAsDraft = false
                },
                Workflow = new WorkflowActionBlock
                {
                    WorkflowKey = action.RequestDefaults.WorkflowKey,
                    WorkflowInstanceId = action.RequestDefaults.WorkflowInstanceId,
                    WorkflowDefinitionVersion = action.RequestDefaults.WorkflowDefinitionVersion,
                    ExpectedCurrentState = action.RequestDefaults.ExpectedCurrentState,
                    ExpectedCurrentTaskId = action.RequestDefaults.ExpectedCurrentTaskId,
                    ExpectedCurrentTaskKey = action.RequestDefaults.ExpectedCurrentTaskKey,
                    ExpectedInstanceVersion = action.RequestDefaults.ExpectedInstanceVersion,
                    ActionKey = action.ActionKey,
                    ActionReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                    NotificationContext = notificationInput is null
                        ? null
                        : new Dictionary<string, string?>(notificationInput.TemplateData, StringComparer.Ordinal),
                    IdempotencyKey = operationId
                }
            };
            var normalized = normalizer.Normalize(envelope, null, null);
            var result = workflowExecutionService.Execute(new WorkflowExecutionRequest(
                    normalized,
                    hasher.ComputeHash(envelope, notificationInput),
                    AdminOverride: false)
                {
                    DeriveIdentityFromPersistence = true,
                    ValidateActionEcho = false,
                    PersistSectionData = false,
                    NotificationInput = notificationInput
                });
            if (!result.Result.Success)
            {
                throw EngineFailure("execute", result);
            }

            var context = result.WorkflowContext ?? ReadContext(workflowInstanceId, actor);
            return Project(entityKind, entityId, applicationState.WorkflowState, actor.UserId, context);
        });
    }

    private SqmsWorkflowResult Project(
        SqmsEntityKind entityKind,
        int entityId,
        string? expectedWorkflowState,
        string actorUserId,
        WorkflowContext context)
    {
        var state = context.Workflow.CurrentState;
        var status = context.Workflow.CurrentStateLabel.ToUpperInvariant();
        repository.ProjectWorkflowState(
            entityKind,
            entityId,
            expectedWorkflowState,
            state,
            status,
            actorUserId,
            timeProvider.GetUtcNow().UtcDateTime);
        return new SqmsWorkflowResult(entityId, state, status, context);
    }

    private WorkflowContext ReadContext(string workflowInstanceId, SqmsActor actor)
    {
        return contextReader.Read(new WorkflowContextReadRequest(
            workflowInstanceId,
            actor.UserId,
            actor.Roles)).WorkflowContext;
    }

    private T InTransaction<T>(string operation, Func<T> action)
    {
        using var transaction = dbContext.Database.CurrentTransaction is null
            ? dbContext.Database.BeginTransaction()
            : null;
        try
        {
            var result = action();
            transaction?.Commit();
            return result;
        }
        catch (Exception exception)
        {
            transaction?.Rollback();
            dbContext.ChangeTracker.Clear();
            if (exception is SqmsIntegrationException)
            {
                throw;
            }

            throw new SqmsIntegrationException(
                $"Unable to {operation}: {exception.Message}",
                exception);
        }
    }

    private static SqmsIntegrationException EngineFailure(
        string operation,
        WorkflowActionResultEnvelope result)
    {
        return new SqmsIntegrationException(
            $"Workflow {operation} failed with '{result.Result.ResultCode}': " +
            (result.Result.Message ?? "No diagnostic message was returned."));
    }

    private static void ValidateActor(SqmsActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (string.IsNullOrWhiteSpace(actor.UserId))
        {
            throw new ArgumentException("SQMS actor userId is required.", nameof(actor));
        }

        if (actor.UserId.Length > 50)
        {
            throw new ArgumentException("SQMS actor userId cannot exceed 50 characters.", nameof(actor));
        }

        if (actor.Roles is null || actor.Roles.Count == 0 || actor.Roles.All(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("SQMS actor must have at least one role.", nameof(actor));
        }
    }

    private void ValidateBidSubmission(int bidId)
    {
        var gate = repository.ReadBidSubmissionGate(bidId);
        if (!string.Equals(gate.ParentWorkflowState, "OpenToBidding", StringComparison.Ordinal))
        {
            throw new SqmsIntegrationException(
                $"SQMS Bid '{bidId}' cannot be submitted because parent Opportunity " +
                $"'{gate.OpportunityId}' is not in OpenToBidding.");
        }

        if (gate.BiddingCloseAt is null || timeProvider.GetUtcNow() >= gate.BiddingCloseAt.Value)
        {
            throw new SqmsIntegrationException(
                $"SQMS Bid '{bidId}' cannot be submitted because bidding is closed.");
        }

        if (gate.EffectiveDateForSale is null
            || gate.NoticeAt is null
            || gate.ReminderAt is null
            || gate.ResponseDueDate is null)
        {
            throw new SqmsIntegrationException(
                $"SQMS Bid '{bidId}' cannot be submitted because parent scheduling fields are incomplete.");
        }

        var cycleWorkflowInstanceId = SqmsWorkflowIdentity.SaleCycleWorkflowInstanceId(
            gate.EffectiveDateForSale.Value,
            gate.NoticeAt.Value,
            gate.ReminderAt.Value,
            gate.ResponseDueDate.Value);
        var noticeSent = dbContext.NotificationOutbox.AsNoTracking().Any(row =>
            row.WorkflowInstanceId == cycleWorkflowInstanceId
            && row.NotificationType == "SqmsOpenBiddingNotice"
            && row.Status == "Sent");
        if (!noticeSent)
        {
            throw new SqmsIntegrationException(
                $"SQMS Bid '{bidId}' cannot be submitted until the open-bidding notice is Sent.");
        }

        if (string.IsNullOrWhiteSpace(gate.CommitmentAmountRequested)
            || !decimal.TryParse(
                gate.CommitmentAmountRequested,
                NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                CultureInfo.GetCultureInfo("en-US"),
                out var commitmentAmount)
            || commitmentAmount <= 0)
        {
            throw new SqmsIntegrationException(
                $"SQMS Bid '{bidId}' CommitmentAmountRequested must contain a positive en-US monetary value.");
        }

        if (gate.BidPricingCategory is not ("FairValue" or "Premium" or "Discount"))
        {
            throw new SqmsIntegrationException(
                $"SQMS Bid '{bidId}' has invalid BidPricingCategory '{gate.BidPricingCategory}'.");
        }
    }
}
