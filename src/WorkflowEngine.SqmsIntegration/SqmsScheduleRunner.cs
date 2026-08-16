using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.WorkflowExecution;
using WorkflowEngine.Application.WorkflowStart;
using WorkflowEngine.Infrastructure.Persistence;

namespace WorkflowEngine.SqmsIntegration;

public sealed class SqmsScheduleRunner(
    WorkflowDbContext dbContext,
    SqmsApplicationRepository repository,
    IStartWorkflowService startWorkflowService,
    IWorkflowContextReader contextReader,
    IWorkflowExecutionService workflowExecutionService,
    WorkflowActionEnvelopeNormalizer normalizer,
    CanonicalRequestHasher hasher,
    SqmsNotificationInputFactory notificationInputFactory,
    SqmsIntegrationOptions options,
    TimeProvider timeProvider,
    ILogger<SqmsScheduleRunner>? logger = null)
{
    private static readonly SqmsActor SystemActor = new(
        "sqms.scheduler",
        "SQMS Scheduler",
        ["System"]);

    public SqmsScheduleRunResult RunNoticesAndReminders(CancellationToken cancellationToken = default)
    {
        ValidateOptions();
        var now = timeProvider.GetUtcNow();
        var candidates = repository.ReadDueSaleCycles(now, options.MaxCyclesPerRun);
        var noticesQueued = 0;
        var remindersQueued = 0;
        var remindersBlocked = 0;
        var cyclesCompleted = 0;
        var errors = new List<string>();

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var businessObjectId = BusinessObjectId(candidate);
            var workflowInstanceId = $"WF-{businessObjectId}";
            try
            {
                var notificationInput = notificationInputFactory.CreateOpenBiddingInput(
                    new SqmsSaleCycleNotificationData(
                        candidate.EffectiveDateForSale,
                        candidate.ResponseDueDate,
                        options.OpenBiddingViewUrl));
                var start = startWorkflowService.Start(new StartWorkflowRequest(
                    "SqmsSaleCycle",
                    "SqmsSaleCycle",
                    businessObjectId,
                    SystemActor.UserId,
                    Reset: false,
                    InitialActionKey: "SendOpenBiddingNotice",
                    StartedByDisplayName: SystemActor.DisplayName,
                    StartedByRoles: SystemActor.Roles,
                    IdempotencyKey: $"{businessObjectId}:SendOpenBiddingNotice",
                    NotificationInput: notificationInput));
                if (start.Envelope.Result.Success)
                {
                    noticesQueued++;
                }
                else if (!string.Equals(start.Envelope.Result.ResultCode, "AlreadyExists", StringComparison.Ordinal))
                {
                    throw EngineFailure("start sale cycle", start.Envelope);
                }

                if (candidate.ReminderAt > now)
                {
                    continue;
                }

                if (!InitialNoticeWasSent(workflowInstanceId))
                {
                    remindersBlocked++;
                    logger?.LogInformation(
                        "SQMS reminder for cycle {BusinessObjectId} is blocked until the initial notice is Sent.",
                        businessObjectId);
                    continue;
                }

                if (ExecuteIfAvailable(
                        workflowInstanceId,
                        "SendOpenBiddingReminder",
                        $"{businessObjectId}:SendOpenBiddingReminder",
                        notificationInput))
                {
                    remindersQueued++;
                }

                if (ExecuteIfAvailable(
                        workflowInstanceId,
                        "CompleteSaleCycle",
                        $"{businessObjectId}:CompleteSaleCycle",
                        notificationInput: null))
                {
                    cyclesCompleted++;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                dbContext.ChangeTracker.Clear();
                var message = $"SQMS sale cycle '{businessObjectId}' failed: {exception.Message}";
                errors.Add(message);
                logger?.LogError(exception, "SQMS sale cycle {BusinessObjectId} failed.", businessObjectId);
            }
        }

        return new SqmsScheduleRunResult(
            candidates.Count,
            noticesQueued,
            remindersQueued,
            remindersBlocked,
            cyclesCompleted,
            errors);
    }

    private bool ExecuteIfAvailable(
        string workflowInstanceId,
        string actionKey,
        string idempotencyKey,
        WorkflowNotificationInput? notificationInput)
    {
        var context = ReadContext(workflowInstanceId);
        var action = context.AvailableActions.SingleOrDefault(candidate =>
            string.Equals(candidate.ActionKey, actionKey, StringComparison.Ordinal));
        if (action is null)
        {
            return false;
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
                SourceSystem = "SQMS.Scheduler"
            },
            Actor = new WorkflowActor
            {
                UserId = SystemActor.UserId,
                DisplayName = SystemActor.DisplayName,
                Roles = SystemActor.Roles.ToList()
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
                WorkflowInstanceId = workflowInstanceId,
                WorkflowDefinitionVersion = action.RequestDefaults.WorkflowDefinitionVersion,
                ExpectedCurrentState = action.RequestDefaults.ExpectedCurrentState,
                ExpectedCurrentTaskId = action.RequestDefaults.ExpectedCurrentTaskId,
                ExpectedCurrentTaskKey = action.RequestDefaults.ExpectedCurrentTaskKey,
                ExpectedInstanceVersion = action.RequestDefaults.ExpectedInstanceVersion,
                ActionKey = actionKey,
                IdempotencyKey = idempotencyKey,
                NotificationContext = notificationInput is null
                    ? null
                    : new Dictionary<string, string?>(notificationInput.TemplateData, StringComparer.Ordinal)
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
            throw EngineFailure($"execute {actionKey}", result);
        }

        return !result.Result.Diagnostics.Replay;
    }

    private bool InitialNoticeWasSent(string workflowInstanceId)
    {
        return dbContext.NotificationOutbox.AsNoTracking().Any(row =>
            row.WorkflowInstanceId == workflowInstanceId
            && row.NotificationType == "SqmsOpenBiddingNotice"
            && row.Status == "Sent");
    }

    private WorkflowContext ReadContext(string workflowInstanceId)
    {
        return contextReader.Read(new WorkflowContextReadRequest(
            workflowInstanceId,
            SystemActor.UserId,
            SystemActor.Roles)).WorkflowContext;
    }

    private static string BusinessObjectId(SqmsSaleCycleCandidate candidate)
    {
        return SqmsWorkflowIdentity.SaleCycleBusinessObjectId(
            candidate.EffectiveDateForSale,
            candidate.NoticeAt,
            candidate.ReminderAt,
            candidate.ResponseDueDate);
    }

    private void ValidateOptions()
    {
        if (options.MaxCyclesPerRun <= 0)
        {
            throw new InvalidOperationException("SQMS MaxCyclesPerRun must be greater than zero.");
        }

        if (!Uri.TryCreate(options.OpenBiddingViewUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("SQMS OpenBiddingViewUrl must be an absolute HTTP or HTTPS URL.");
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
}
