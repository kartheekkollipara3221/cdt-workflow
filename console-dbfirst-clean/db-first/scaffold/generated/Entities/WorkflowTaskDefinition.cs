using System;
using System.Collections.Generic;

namespace WorkflowEngine.DatabaseFirst.Generated.Entities;

public partial class WorkflowTaskDefinition
{
    public long Id { get; set; }

    public long WorkflowDefinitionVersionId { get; set; }

    public string TaskKey { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string OwnerRoleKey { get; set; } = null!;

    public string? SlaPolicyKey { get; set; }

    public bool IsActive { get; set; }
}
