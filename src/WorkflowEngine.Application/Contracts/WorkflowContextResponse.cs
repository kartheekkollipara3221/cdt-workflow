namespace WorkflowEngine.Application.Contracts;

public sealed class WorkflowContextResponse
{
    public WorkflowContext WorkflowContext { get; set; } = new();
}

public sealed class WorkflowContext
{
    public WorkflowContextWorkflow Workflow { get; set; } = new();

    public WorkflowContextBusinessObject BusinessObject { get; set; } = new();

    public WorkflowContextActorAccess ActorAccess { get; set; } = new();

    public WorkflowContextTask? CurrentTask { get; set; }

    public WorkflowContextSla Sla { get; set; } = new();

    public List<AvailableWorkflowAction> AvailableActions { get; set; } = [];

    public DateTimeOffset ServerTimeUtc { get; set; }
}

public sealed class WorkflowContextWorkflow
{
    public string WorkflowKey { get; set; } = string.Empty;

    public string WorkflowInstanceId { get; set; } = string.Empty;

    public int WorkflowDefinitionVersion { get; set; }

    public int WorkflowInstanceVersion { get; set; }

    public string CurrentState { get; set; } = string.Empty;

    public string CurrentStateLabel { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }
}

public sealed class WorkflowContextBusinessObject
{
    public string ObjectType { get; set; } = string.Empty;

    public string ObjectId { get; set; } = string.Empty;

    public int? ObjectVersion { get; set; }
}

public sealed class WorkflowContextActorAccess
{
    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public List<string> EffectiveRoles { get; set; } = [];

    public bool CanAct { get; set; }

    public string? ReadOnlyReason { get; set; }
}

public sealed class WorkflowContextTask
{
    public string TaskId { get; set; } = string.Empty;

    public string TaskKey { get; set; } = string.Empty;

    public string TaskLabel { get; set; } = string.Empty;

    public string AssignedRole { get; set; } = string.Empty;

    public string? AssignedUserId { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? DueAtUtc { get; set; }
}

public sealed class WorkflowContextSla
{
    public string Status { get; set; } = "NoSla";

    public DateTimeOffset? DueAtUtc { get; set; }

    public bool Overdue { get; set; }

    public int OverdueByMinutes { get; set; }

    public string? CalculationMode { get; set; }

    public DateOnly? TargetDate { get; set; }

    public int? ScheduleVersion { get; set; }

    public DateTimeOffset? NextReminderAtUtc { get; set; }
}
