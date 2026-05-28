namespace WorkflowEngine.WorkflowApi.Models;

public sealed class WorkflowStartRequest
{
    public string? WorkflowKey { get; set; }

    public string? BusinessObjectType { get; set; }

    public string? BusinessObjectId { get; set; }
}
