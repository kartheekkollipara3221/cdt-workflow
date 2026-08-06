using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.WorkflowApi.Models;

public sealed class WorkflowActionRequest
{
    public string? WorkflowInstanceId { get; set; }

    public string? ActionKey { get; set; }

    public string? AssignedRoleKey { get; set; }

    public Dictionary<string, string?>? NotificationContext { get; set; }

    public NotificationRecipients? NotificationRecipients { get; set; }
}
