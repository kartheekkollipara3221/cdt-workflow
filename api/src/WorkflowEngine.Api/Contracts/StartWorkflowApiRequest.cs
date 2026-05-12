using System.ComponentModel.DataAnnotations;

namespace WorkflowEngine.Api.Contracts;

public sealed class StartWorkflowApiRequest
{
    [Required]
    public string BusinessObjectType { get; set; } = string.Empty;

    [Required]
    public string BusinessObjectId { get; set; } = string.Empty;
}
