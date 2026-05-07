namespace WorkflowEngine.Application.Contracts;

public sealed class AvailableWorkflowAction
{
    public string ActionKey { get; set; } = string.Empty;

    public string? SectionKey { get; set; }

    public string? ButtonKey { get; set; }

    public string? ButtonLabel { get; set; }

    public string? Intent { get; set; }

    public bool RequiresReason { get; set; }

    public WorkflowActionRequestDefaults RequestDefaults { get; set; } = new();
}

public sealed class WorkflowActionRequestDefaults
{
    public string WorkflowKey { get; set; } = string.Empty;

    public string WorkflowInstanceId { get; set; } = string.Empty;

    public int WorkflowDefinitionVersion { get; set; }

    public string ExpectedCurrentState { get; set; } = string.Empty;

    public string? ExpectedCurrentTaskId { get; set; }

    public string? ExpectedCurrentTaskKey { get; set; }

    public int ExpectedInstanceVersion { get; set; }
}
