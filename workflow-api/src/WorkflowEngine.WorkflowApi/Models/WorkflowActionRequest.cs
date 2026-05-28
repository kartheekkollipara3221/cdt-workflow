namespace WorkflowEngine.WorkflowApi.Models;

public sealed class WorkflowActionRequest
{
    public string? WorkflowInstanceId { get; set; }

    public string? ActionKey { get; set; }
}
