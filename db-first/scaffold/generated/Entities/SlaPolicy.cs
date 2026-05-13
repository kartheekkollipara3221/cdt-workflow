using System;
using System.Collections.Generic;

namespace WorkflowEngine.DatabaseFirst.Generated.Entities;

public partial class SlaPolicy
{
    public long Id { get; set; }

    public long WorkflowDefinitionVersionId { get; set; }

    public string SlaPolicyKey { get; set; } = null!;

    public int DurationMinutes { get; set; }

    public bool IsActive { get; set; }
}
