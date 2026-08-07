using System.Text.Json.Serialization;

namespace WorkflowEngine.Application.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class WorkflowSlaInput
{
    public DateOnly? TargetDate { get; set; }
}
