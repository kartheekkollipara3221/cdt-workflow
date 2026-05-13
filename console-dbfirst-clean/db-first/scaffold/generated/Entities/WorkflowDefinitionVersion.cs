using System;
using System.Collections.Generic;

namespace WorkflowEngine.DatabaseFirst.Generated.Entities;

public partial class WorkflowDefinitionVersion
{
    public long Id { get; set; }

    public long WorkflowDefinitionId { get; set; }

    public int VersionNumber { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public virtual WorkflowDefinition WorkflowDefinition { get; set; } = null!;
}
