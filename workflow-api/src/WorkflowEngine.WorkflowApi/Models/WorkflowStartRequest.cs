using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.WorkflowApi.Models;

public sealed class WorkflowStartRequest
{
    public string? WorkflowKey { get; set; }

    public string? BusinessObjectType { get; set; }

    public string? BusinessObjectId { get; set; }

    public string? InitialActionKey { get; set; }

    public string? IdempotencyKey { get; set; }

    public WorkflowSlaInput? SlaInput { get; set; }

    public Dictionary<string, string?>? NotificationContext { get; set; }

    public NotificationRecipients? NotificationRecipients { get; set; }
}
