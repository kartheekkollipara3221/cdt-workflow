using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.WorkflowExecution;
using WorkflowEngine.Application.WorkflowNotification;
using WorkflowEngine.Application.WorkflowStart;

namespace BBH.CP.Atlas.Service.Platform.Workflow;

/// <summary>One engine action for a record: the instance, the action, the actor, the key and the email inputs.</summary>
/// <param name="BusinessObjectId">The business object ID: the prefix of the binding and the record ID.</param>
/// <param name="IsStart">True: start the instance with this initial action. False: run the action on the instance.</param>
/// <param name="IdempotencyKey">The same key for a repeated request, so the engine runs the action one time.</param>
public sealed record AtlasWorkflowCall(
    string WorkflowKey,
    string BusinessObjectId,
    string ActionKey,
    bool IsStart,
    string UserId,
    string DisplayName,
    IReadOnlyList<string> Roles,
    string IdempotencyKey,
    WorkflowNotificationInput NotificationInput)
{
    /// <summary>The engine instance ID: "WF-" and the business object ID.</summary>
    public string InstanceId => "WF-" + BusinessObjectId;
}

/// <summary>The result of one engine action.</summary>
/// <param name="NotificationQueued">True when the action put an email in the engine outbox.</param>
public sealed record AtlasWorkflowResult(
    bool Success, string ResultCode, string? Message, string? NewState, string InstanceId, string? CorrelationId, bool NotificationQueued);

/// <summary>
/// Runs the engine actions of the platform apps: start, execute, the state read and the email send.
/// </summary>
/// <remarks>
/// The adapter uses the engine services of the host, as the current CDT and SQMS services do. It never commits the
/// platform data; the caller decides the order of the writes.
/// </remarks>
public interface IAtlasWorkflowAdapter
{
    /// <summary>Starts the instance with its initial action, or runs an action on the instance.</summary>
    AtlasWorkflowResult Run(AtlasWorkflowCall call);

    /// <summary>Returns the current engine state of the instance, or null when the engine has no such instance.</summary>
    string? ReadState(string instanceId, string userId, IReadOnlyList<string> roles);

    /// <summary>Sends the emails that a successful action put in the outbox.</summary>
    /// <returns>Null when the send worked or there was nothing to send, else the reason of the failure.</returns>
    /// <remarks>A failed send stays in the outbox, and the engine job sends it again.</remarks>
    string? DispatchNotifications(AtlasWorkflowResult result);
}

/// <summary>
/// The adapter on the engine services of the host (dependency injection).
/// </summary>
/// <remarks>
/// The engine services are read on the first engine call. Thus a host with no engine runs the apps with no workflow.
/// </remarks>
public sealed class AtlasWorkflowAdapter(IServiceProvider services) : IAtlasWorkflowAdapter
{
    /// <summary>The source system that the engine records for a platform action.</summary>
    public const string SourceSystem = "BBH.CP.Atlas.Platform";

    /// <inheritdoc/>
    public AtlasWorkflowResult Run(AtlasWorkflowCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        WorkflowActionResult result = call.IsStart ? Start(call) : Execute(call);
        return new AtlasWorkflowResult(
            result.Success,
            result.ResultCode,
            result.Message,
            result.NewState,
            string.IsNullOrEmpty(result.WorkflowInstanceId) ? call.InstanceId : result.WorkflowInstanceId,
            result.CorrelationId,
            result.Notification?.Queued == true);
    }

    /// <inheritdoc/>
    public string? ReadState(string instanceId, string userId, IReadOnlyList<string> roles)
    {
        try
        {
            return services.GetRequiredService<IWorkflowContextReader>()
                .Read(new WorkflowContextReadRequest(instanceId, userId, roles)).WorkflowContext.Workflow.CurrentState;
        }
        catch (KeyNotFoundException)
        {
            return null; // the engine has no instance for this record
        }
    }

    /// <inheritdoc/>
    public string? DispatchNotifications(AtlasWorkflowResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Success || !result.NotificationQueued || string.IsNullOrWhiteSpace(result.CorrelationId))
            return null;

        var dispatch = services.GetRequiredService<IWorkflowActionNotificationDispatcher>()
            .DispatchCreatedNotifications(result.InstanceId, result.CorrelationId);
        return dispatch.Success ? null : dispatch.FailureMessage ?? "The email was not sent.";
    }

    private WorkflowActionResult Start(AtlasWorkflowCall call) =>
        services.GetRequiredService<IStartWorkflowService>().Start(new StartWorkflowRequest(
            WorkflowKey: call.WorkflowKey,
            BusinessObjectType: string.Empty,
            BusinessObjectId: call.BusinessObjectId,
            StartedByUserId: call.UserId,
            Reset: false,
            InitialActionKey: call.ActionKey,
            StartedByDisplayName: call.DisplayName,
            StartedByRoles: call.Roles,
            IdempotencyKey: call.IdempotencyKey,
            NotificationInput: call.NotificationInput)).Envelope.Result;

    private WorkflowActionResult Execute(AtlasWorkflowCall call)
    {
        WorkflowActionEnvelope envelope = new()
        {
            Request = new WorkflowRequest
            {
                CommandType = "ExecuteWorkflowAction",
                CommandVersion = 1,
                ClientActionId = call.IdempotencyKey,
                CorrelationId = call.IdempotencyKey,
                SourceSystem = SourceSystem
            },
            Actor = new WorkflowActor { UserId = call.UserId, DisplayName = call.DisplayName, Roles = [.. call.Roles] },
            Workflow = new WorkflowActionBlock
            {
                WorkflowInstanceId = call.InstanceId,
                ActionKey = call.ActionKey,
                // The engine renders the templates of an executed action from the notification context.
                NotificationContext = new Dictionary<string, string?>(call.NotificationInput.TemplateData, StringComparer.Ordinal),
                IdempotencyKey = call.IdempotencyKey
            }
        };

        NormalizedWorkflowAction normalized = services.GetRequiredService<WorkflowActionEnvelopeNormalizer>()
            .Normalize(envelope, businessObjectOverride: null, workflowInstanceOverride: null);
        string hash = services.GetRequiredService<CanonicalRequestHasher>().ComputeHash(normalized.Envelope, call.NotificationInput);
        return services.GetRequiredService<IWorkflowExecutionService>().Execute(new WorkflowExecutionRequest(normalized, hash, AdminOverride: false)
        {
            DeriveIdentityFromPersistence = true,
            PersistSectionData = false,
            ValidateActionEcho = false,
            NotificationInput = call.NotificationInput
        }).Result;
    }
}
