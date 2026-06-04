using System.Text.Json;

namespace WorkflowEngine.Application.Contracts;

public sealed class WorkflowActionEnvelope
{
    public WorkflowRequest? Request { get; set; }

    public WorkflowBusinessObject? BusinessObject { get; set; }

    public WorkflowActor? Actor { get; set; }

    public WorkflowUiAction? UiAction { get; set; }

    public WorkflowDataSave? DataSave { get; set; }

    public WorkflowActionBlock? Workflow { get; set; }

    public WorkflowTestExpectations? TestExpectations { get; set; }
}

public sealed class WorkflowRequest
{
    public string? CommandType { get; set; }

    public int? CommandVersion { get; set; }

    public string? ClientActionId { get; set; }

    public string? CorrelationId { get; set; }

    public DateTimeOffset? RequestedAtUtc { get; set; }

    public string? SourceSystem { get; set; }
}

public sealed class WorkflowBusinessObject
{
    public string? ObjectType { get; set; }

    public string? ObjectId { get; set; }

    public int? ObjectVersion { get; set; }
}

public sealed class WorkflowActor
{
    public string? UserId { get; set; }

    public string? DisplayName { get; set; }

    public List<string>? Roles { get; set; }
}

public sealed class WorkflowUiAction
{
    public string? SectionKey { get; set; }

    public string? ButtonKey { get; set; }

    public string? ButtonLabelShown { get; set; }

    public bool? SaveAsDraft { get; set; }
}

public sealed class WorkflowDataSave
{
    public string? SectionKey { get; set; }

    public JsonElement Payload { get; set; }
}

public sealed class WorkflowActionBlock
{
    public string? WorkflowKey { get; set; }

    public string? WorkflowInstanceId { get; set; }

    public int? WorkflowDefinitionVersion { get; set; }

    public string? ExpectedCurrentState { get; set; }

    public string? ExpectedCurrentTaskId { get; set; }

    public string? ExpectedCurrentTaskKey { get; set; }

    public int? ExpectedInstanceVersion { get; set; }

    public string? ActionKey { get; set; }

    public string? ActionReason { get; set; }

    public string? AssignedRoleKey { get; set; }

    public Dictionary<string, string?>? NotificationContext { get; set; }

    public string? IdempotencyKey { get; set; }

    public WorkflowActionOptions? Options { get; set; }
}

public sealed class WorkflowActionOptions
{
    public bool? CloseCurrentTask { get; set; }

    public bool? CreateNextTask { get; set; }

    public bool? QueueNotifications { get; set; }

    public bool? EvaluateSla { get; set; }

    public bool? WriteAudit { get; set; }
}

public sealed class WorkflowTestExpectations
{
    public string? ExpectedResult { get; set; }

    public string? ExpectedOldState { get; set; }

    public string? ExpectedNewState { get; set; }

    public string? ExpectedClosedTaskKey { get; set; }

    public string? ExpectedCreatedTaskKey { get; set; }

    public string? ExpectedAssignedRole { get; set; }

    public string? ExpectedNotificationType { get; set; }

    public string? ExpectedAuditAction { get; set; }
}
