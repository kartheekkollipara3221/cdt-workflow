using System;
using System.Collections.Generic;

namespace WorkflowEngine.DatabaseFirst.Generated.Entities;

public partial class WorkflowDefinition
{
    public long Id { get; set; }

    public string WorkflowKey { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual ICollection<WorkflowDefinitionVersion> WorkflowDefinitionVersions { get; set; } = new List<WorkflowDefinitionVersion>();
}
