using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.WorkflowExecution;
using WorkflowEngine.Application.WorkflowStart;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Sqms.Contracts;
using WorkflowEngine.Sqms.Configuration;
using WorkflowEngine.Sqms.Persistence;

namespace WorkflowEngine.Sqms;

public sealed class SqmsWorkflowService : ISqmsWorkflowService
{
    private readonly WorkflowDbContext dbContext;
    private readonly ISqmsDataStore dataStore;
    private readonly IStartWorkflowService startWorkflowService;
    private readonly IWorkflowExecutionService workflowExecutionService;
    private readonly IWorkflowContextReader contextReader;
    private readonly WorkflowActionEnvelopeNormalizer normalizer;
    private readonly CanonicalRequestHasher hasher;
    private readonly TimeProvider timeProvider;
    private readonly SqmsLinkBuilder linkBuilder;
    private readonly SqmsOptions options;

    public SqmsWorkflowService(
        WorkflowDbContext dbContext,
        ISqmsDataStore dataStore,
        IStartWorkflowService startWorkflowService,
        IWorkflowExecutionService workflowExecutionService,
        IWorkflowContextReader contextReader,
        WorkflowActionEnvelopeNormalizer normalizer,
        CanonicalRequestHasher hasher,
        TimeProvider timeProvider)
        : this(
            dbContext,
            dataStore,
            startWorkflowService,
            workflowExecutionService,
            contextReader,
            normalizer,
            hasher,
            timeProvider,
            new SqmsLinkBuilder(new SqmsLinkOptions()),
            Options.Create(new SqmsOptions()))
    {
    }

    public SqmsWorkflowService(
        WorkflowDbContext dbContext,
        ISqmsDataStore dataStore,
        IStartWorkflowService startWorkflowService,
        IWorkflowExecutionService workflowExecutionService,
        IWorkflowContextReader contextReader,
        WorkflowActionEnvelopeNormalizer normalizer,
        CanonicalRequestHasher hasher,
        TimeProvider timeProvider,
        SqmsLinkBuilder linkBuilder,
        IOptions<SqmsOptions> options)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        this.startWorkflowService = startWorkflowService ?? throw new ArgumentNullException(nameof(startWorkflowService));
        this.workflowExecutionService = workflowExecutionService ?? throw new ArgumentNullException(nameof(workflowExecutionService));
        this.contextReader = contextReader ?? throw new ArgumentNullException(nameof(contextReader));
        this.normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
        this.hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.linkBuilder = linkBuilder ?? throw new ArgumentNullException(nameof(linkBuilder));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<SqmsOperationResult> StartOpportunityAsync(
        int opportunityId,
        SqmsActor actor,
        CancellationToken cancellationToken)
    {
        ValidateActor(actor);
        var row = await dataStore.GetOpportunityAsync(opportunityId, cancellationToken);
        if (row is null)
        {
            return TargetNotFound("Opportunity", opportunityId);
        }

        return await ExecuteTransactionAsync(async () =>
        {
            var result = startWorkflowService.Start(new StartWorkflowRequest(
                SqmsKeys.OpportunityWorkflow,
                SqmsKeys.OpportunityWorkflow,
                SqmsKeys.OpportunityBusinessObjectId(opportunityId),
                actor.UserId,
                Reset: false,
                InitialActionKey: SqmsKeys.OpportunityActions.Submit,
                StartedByDisplayName: actor.DisplayName,
                StartedByRoles: actor.Roles,
                IdempotencyKey: $"SQMS:OPP:{opportunityId}:START:SUBMIT"));
            return await CompleteWorkflowOperationAsync(
                result.Envelope,
                opportunityId,
                actor,
                isOpportunity: true,
                cancellationToken);
        }, cancellationToken);
    }

    public async Task<SqmsOperationResult> StartBidAsync(
        int bidId,
        SqmsActor actor,
        CancellationToken cancellationToken)
    {
        ValidateActor(actor);
        var row = await dataStore.GetBidAsync(bidId, cancellationToken);
        if (row is null)
        {
            return TargetNotFound("Bid", bidId);
        }

        return await ExecuteTransactionAsync(async () =>
        {
            var result = startWorkflowService.Start(new StartWorkflowRequest(
                SqmsKeys.BidWorkflow,
                SqmsKeys.BidWorkflow,
                SqmsKeys.BidBusinessObjectId(bidId),
                actor.UserId,
                Reset: false,
                InitialActionKey: SqmsKeys.BidActions.Submit,
                StartedByDisplayName: actor.DisplayName,
                StartedByRoles: actor.Roles,
                IdempotencyKey: $"SQMS:BID:{bidId}:START:SUBMIT"));
            return await CompleteWorkflowOperationAsync(
                result.Envelope,
                bidId,
                actor,
                isOpportunity: false,
                cancellationToken);
        }, cancellationToken);
    }

    public async Task<SqmsOperationResult> GetOpportunityContextAsync(
        int opportunityId,
        SqmsActor actor,
        CancellationToken cancellationToken)
    {
        ValidateActor(actor);
        if (await dataStore.GetOpportunityAsync(opportunityId, cancellationToken) is null)
        {
            return TargetNotFound("Opportunity", opportunityId);
        }

        return ReadContext(SqmsKeys.OpportunityWorkflowInstanceId(opportunityId), actor);
    }

    public async Task<SqmsOperationResult> GetBidContextAsync(
        int bidId,
        SqmsActor actor,
        CancellationToken cancellationToken)
    {
        ValidateActor(actor);
        if (await dataStore.GetBidAsync(bidId, cancellationToken) is null)
        {
            return TargetNotFound("Bid", bidId);
        }

        return ReadContext(SqmsKeys.BidWorkflowInstanceId(bidId), actor);
    }

    public async Task<SqmsOperationResult> ExecuteOpportunityActionAsync(
        SqmsExecuteRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        if (await dataStore.GetOpportunityAsync(request.EntityId, cancellationToken) is null)
        {
            return TargetNotFound("Opportunity", request.EntityId);
        }

        return await ExecuteActionAsync(request, isOpportunity: true, cancellationToken);
    }

    public async Task<SqmsOperationResult> ExecuteBidActionAsync(
        SqmsExecuteRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        if (await dataStore.GetBidAsync(request.EntityId, cancellationToken) is null)
        {
            return TargetNotFound("Bid", request.EntityId);
        }

        return await ExecuteActionAsync(request, isOpportunity: false, cancellationToken);
    }

    private async Task<SqmsOperationResult> ExecuteActionAsync(
        SqmsExecuteRequest request,
        bool isOpportunity,
        CancellationToken cancellationToken)
    {
        var workflowInstanceId = isOpportunity
            ? SqmsKeys.OpportunityWorkflowInstanceId(request.EntityId)
            : SqmsKeys.BidWorkflowInstanceId(request.EntityId);
        if (!await dbContext.WorkflowInstances.AsNoTracking().AnyAsync(
                row => row.WorkflowInstanceId == workflowInstanceId,
                cancellationToken))
        {
            return NotWorkflowManaged(workflowInstanceId);
        }

        return await ExecuteTransactionAsync(async () =>
        {
            var enrichment = await BuildNotificationInputAsync(request, isOpportunity, cancellationToken);
            if (enrichment.Error is not null)
            {
                return enrichment.Error;
            }

            var idempotencyKey = isOpportunity
                ? $"SQMS:OPP:{request.EntityId}:{request.ExpectedWorkflowVersion}:{request.ActionKey}"
                : $"SQMS:BID:{request.EntityId}:{request.ExpectedWorkflowVersion}:{request.ActionKey}";
            var notificationInput = enrichment.Input!;
            var envelope = new WorkflowActionEnvelope
            {
                Request = new WorkflowRequest
                {
                    CommandType = "ExecuteWorkflowAction",
                    CommandVersion = 1,
                    ClientActionId = idempotencyKey,
                    CorrelationId = idempotencyKey,
                    SourceSystem = "SQMS"
                },
                Actor = new WorkflowActor
                {
                    UserId = request.Actor.UserId,
                    DisplayName = request.Actor.DisplayName,
                    Roles = request.Actor.Roles.ToList()
                },
                Workflow = new WorkflowActionBlock
                {
                    WorkflowInstanceId = workflowInstanceId,
                    ExpectedInstanceVersion = request.ExpectedWorkflowVersion,
                    ActionKey = request.ActionKey,
                    IdempotencyKey = idempotencyKey,
                    NotificationContext = new Dictionary<string, string?>(
                        notificationInput.TemplateData,
                        StringComparer.Ordinal)
                }
            };
            var normalized = normalizer.Normalize(envelope, null, null);
            var workflowResult = workflowExecutionService.Execute(new WorkflowExecutionRequest(
                    normalized,
                    hasher.ComputeHash(envelope, notificationInput),
                    AdminOverride: false)
                {
                    DeriveIdentityFromPersistence = true,
                    PersistSectionData = false,
                    ValidateActionEcho = false,
                    NotificationInput = notificationInput
                });
            return await CompleteWorkflowOperationAsync(
                workflowResult,
                request.EntityId,
                request.Actor,
                isOpportunity,
                cancellationToken);
        }, cancellationToken);
    }

    private async Task<NotificationEnrichment> BuildNotificationInputAsync(
        SqmsExecuteRequest request,
        bool isOpportunity,
        CancellationToken cancellationToken)
    {
        if (isOpportunity && request.ActionKey == SqmsKeys.OpportunityActions.RequestSellerApproval)
        {
            var opportunity = await dataStore.GetOpportunityAsync(request.EntityId, cancellationToken);
            if (opportunity is null)
            {
                return NotificationEnrichment.Failed(TargetNotFound("Opportunity", request.EntityId));
            }

            var errors = RequiredValues(
                ("Opportunity.RelationshipManager", opportunity.RelationshipManager),
                ("Opportunity.SellerRMTeam", opportunity.SellerRmTeam),
                ("Opportunity.FundConsideredSecondarySale", opportunity.FundConsideredSecondarySale),
                ("Opportunity.Vehicle", opportunity.Vehicle),
                ("Opportunity.CommitmentAmountSale", opportunity.CommitmentAmountSale));
            if (opportunity.EffectiveDateForSale is null)
            {
                errors.Add("Opportunity.EffectiveDateForSale is required.");
            }

            var link = linkBuilder.BuildSellerApprovalBidsDashboardLink(opportunity.Id);
            if (!link.Success)
            {
                errors.Add(link.Error!);
            }

            if (errors.Count > 0)
            {
                return NotificationEnrichment.Failed(ValidationFailure(errors));
            }

            var effectiveDate = opportunity.EffectiveDateForSale!.Value;
            var localNow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
                timeProvider.GetUtcNow(),
                options.Scheduling.TimeZoneId);
            var sellerResponseDate = DateOnly.FromDateTime(localNow.DateTime)
                .AddDays(options.Scheduling.SellerResponseDays);
            return NotificationEnrichment.Succeeded(new WorkflowNotificationInput
            {
                RecipientInputs = SelectNormalizedRecipients(
                    request.RecipientInputs,
                    "relationshipManager",
                    "sellerRmTeam"),
                TemplateData = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["quarter"] = Quarter(effectiveDate),
                    ["year"] = effectiveDate.Year.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["vehicle"] = opportunity.Vehicle!.Trim(),
                    ["accountNumber"] = opportunity.AccountNumber.Trim(),
                    ["accountName"] = opportunity.AccountName.Trim(),
                    ["fund"] = opportunity.FundConsideredSecondarySale!.Trim(),
                    ["saleAmount"] = opportunity.CommitmentAmountSale!.Trim(),
                    ["sellerResponseDate"] = FormatDate(sellerResponseDate),
                    ["opportunityUrl"] = link.Url
                }
            });
        }

        if (!isOpportunity && IsBidDispositionAction(request.ActionKey))
        {
            var bid = await dataStore.GetBidAsync(request.EntityId, cancellationToken);
            if (bid is null)
            {
                return NotificationEnrichment.Failed(TargetNotFound("Bid", request.EntityId));
            }

            var opportunity = await dataStore.GetOpportunityAsync(bid.OpportunityId, cancellationToken);
            if (opportunity is null)
            {
                return NotificationEnrichment.Failed(
                    ValidationFailure(["The parent Opportunity is required for a Bid disposition notification."]));
            }

            var errors = RequiredValues(
                ("Bid.RelationshipManager", bid.RelationshipManager),
                ("Bid.BuyerRmteam", bid.BuyerRmTeam),
                ("Bid.CommitmentAmountRequested", bid.CommitmentAmountRequested),
                ("Opportunity.FundConsideredSecondarySale", opportunity.FundConsideredSecondarySale),
                ("Opportunity.Vehicle", opportunity.Vehicle));
            if (opportunity.EffectiveDateForSale is null)
            {
                errors.Add("Opportunity.EffectiveDateForSale is required.");
            }

            var link = linkBuilder.BuildBidFormLink(bid.Id);
            if (!link.Success)
            {
                errors.Add(link.Error!);
            }

            if (errors.Count > 0)
            {
                return NotificationEnrichment.Failed(ValidationFailure(errors));
            }

            var effectiveDate = opportunity.EffectiveDateForSale!.Value;
            return NotificationEnrichment.Succeeded(new WorkflowNotificationInput
            {
                RecipientInputs = SelectNormalizedRecipients(
                    request.RecipientInputs,
                    "relationshipManager",
                    "buyerRmTeam"),
                TemplateData = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["quarter"] = Quarter(effectiveDate),
                    ["year"] = effectiveDate.Year.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["buyerAccountNumber"] = bid.AccountNumber.Trim(),
                    ["buyerAccountName"] = bid.AccountName.Trim(),
                    ["fund"] = opportunity.FundConsideredSecondarySale!.Trim(),
                    ["vehicle"] = opportunity.Vehicle!.Trim(),
                    ["requestedAmount"] = bid.CommitmentAmountRequested!.Trim(),
                    ["effectiveDate"] = FormatDate(DateOnly.FromDateTime(effectiveDate)),
                    ["bidUrl"] = link.Url
                }
            });
        }

        return NotificationEnrichment.Succeeded(new WorkflowNotificationInput
        {
            RecipientInputs = request.RecipientInputs
        });
    }

    private static bool IsBidDispositionAction(string actionKey) =>
        actionKey == SqmsKeys.BidActions.Accept
        || actionKey == SqmsKeys.BidActions.PartiallyAccept
        || actionKey == SqmsKeys.BidActions.Reject;

    private static List<string> RequiredValues(params (string Name, string? Value)[] values) =>
        values
            .Where(value => string.IsNullOrWhiteSpace(value.Value))
            .Select(value => $"{value.Name} is required.")
            .ToList();

    private static IReadOnlyDictionary<string, IReadOnlyList<WorkflowRecipientInput>> SelectNormalizedRecipients(
        IReadOnlyDictionary<string, IReadOnlyList<WorkflowRecipientInput>> source,
        params string[] keys)
    {
        var result = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            var normalized = source.TryGetValue(key, out var values)
                ? values
                    .Where(value => !string.IsNullOrWhiteSpace(value.EmailAddress))
                    .Select(value => new WorkflowRecipientInput(
                        value.UserId?.Trim(),
                        value.EmailAddress.Trim(),
                        value.DisplayName?.Trim()))
                    .DistinctBy(value => value.EmailAddress, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : [];
            result[key] = normalized;
        }

        return result;
    }

    private static string Quarter(DateTime date) =>
        $"Q{((date.Month - 1) / 3) + 1}";

    private static string FormatDate(DateOnly date) =>
        date.ToString("M/d/yyyy", System.Globalization.CultureInfo.InvariantCulture);

    private static SqmsOperationResult ValidationFailure(IReadOnlyList<string> errors) =>
        new(false, "ValidationFailed", string.Join(" ", errors), null, null);

    private sealed record NotificationEnrichment(
        WorkflowNotificationInput? Input,
        SqmsOperationResult? Error)
    {
        public static NotificationEnrichment Succeeded(WorkflowNotificationInput input) => new(input, null);

        public static NotificationEnrichment Failed(SqmsOperationResult error) => new(null, error);
    }

    private async Task<SqmsOperationResult> CompleteWorkflowOperationAsync(
        WorkflowActionResultEnvelope workflowResult,
        int entityId,
        SqmsActor actor,
        bool isOpportunity,
        CancellationToken cancellationToken)
    {
        if (!workflowResult.Result.Success)
        {
            return FromWorkflowResult(workflowResult, context: WrapContext(workflowResult.WorkflowContext));
        }

        var state = workflowResult.Result.NewState ?? workflowResult.Result.CurrentState;
        if (string.IsNullOrWhiteSpace(state))
        {
            throw new InvalidOperationException("Successful workflow result did not return an authoritative state.");
        }

        var modifiedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (isOpportunity)
        {
            await dataStore.ProjectOpportunityStateAsync(
                entityId,
                state,
                actor.UserId,
                modifiedAtUtc,
                cancellationToken);
        }
        else
        {
            await dataStore.ProjectBidStateAsync(
                entityId,
                state,
                actor.UserId,
                modifiedAtUtc,
                cancellationToken);
        }

        var context = contextReader.Read(new WorkflowContextReadRequest(
            workflowResult.Result.WorkflowInstanceId,
            actor.UserId,
            actor.Roles));
        return FromWorkflowResult(workflowResult, context);
    }

    private SqmsOperationResult ReadContext(string workflowInstanceId, SqmsActor actor)
    {
        if (!dbContext.WorkflowInstances.AsNoTracking().Any(row => row.WorkflowInstanceId == workflowInstanceId))
        {
            return NotWorkflowManaged(workflowInstanceId);
        }

        var context = contextReader.Read(new WorkflowContextReadRequest(
            workflowInstanceId,
            actor.UserId,
            actor.Roles));
        return new SqmsOperationResult(true, "Success", null, null, context);
    }

    private async Task<SqmsOperationResult> ExecuteTransactionAsync(
        Func<Task<SqmsOperationResult>> operation,
        CancellationToken cancellationToken)
    {
        await using var transaction = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            var result = await operation();
            if (transaction is not null)
            {
                if (result.Success)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
                else
                {
                    await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                }
            }

            return result;
        }
        catch
        {
            if (transaction is not null)
            {
                await RollbackSafelyAsync(transaction, cancellationToken);
                dbContext.ChangeTracker.Clear();
            }

            throw;
        }
    }

    private static async Task RollbackSafelyAsync(
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // The inner workflow service may already have rolled back the owned transaction.
        }
    }

    private static SqmsOperationResult FromWorkflowResult(
        WorkflowActionResultEnvelope result,
        WorkflowContextResponse? context) =>
        new(
            result.Result.Success,
            result.Result.ResultCode,
            result.Result.Message,
            result,
            context);

    private static WorkflowContextResponse? WrapContext(WorkflowContext? context) =>
        context is null ? null : new WorkflowContextResponse { WorkflowContext = context };

    private static SqmsOperationResult TargetNotFound(string entityType, int id) =>
        new(false, "TargetNotFound", $"SSQMS {entityType} ID {id} was not found.", null, null);

    private static SqmsOperationResult NotWorkflowManaged(string workflowInstanceId) =>
        new(
            false,
            "NotWorkflowManaged",
            $"Workflow instance '{workflowInstanceId}' has not been started.",
            null,
            null);

    private static void ValidateRequest(SqmsExecuteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.EntityId, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ActionKey);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.ExpectedWorkflowVersion, 1);
        ArgumentNullException.ThrowIfNull(request.RecipientInputs);
        ValidateActor(request.Actor);
    }

    private static void ValidateActor(SqmsActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor.UserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor.DisplayName);
        ArgumentNullException.ThrowIfNull(actor.Roles);
        if (actor.Roles.Count == 0 || actor.Roles.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("SQMS actor must contain normalized non-empty roles.", nameof(actor));
        }
    }
}
