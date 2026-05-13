using System;
using System.Collections.Generic;

namespace WorkflowEngine.DatabaseFirst.Generated.Entities;

public partial class WorkflowStateDefinition
{
    public long Id { get; set; }

    public long WorkflowDefinitionVersionId { get; set; }

    public string StateKey { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public bool IsTerminal { get; set; }

    public bool IsActive { get; set; }
}
