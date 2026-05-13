using System;
using System.Collections.Generic;

namespace WorkflowEngine.DatabaseFirst.Generated.Entities;

public partial class WorkflowTransitionDefinition
{
    public long Id { get; set; }

    public long WorkflowDefinitionVersionId { get; set; }

    public string FromStateKey { get; set; } = null!;

    public string ActionKey { get; set; } = null!;

    public string AllowedRoleKey { get; set; } = null!;

    public string ToStateKey { get; set; } = null!;

    public string? TaskToCloseKey { get; set; }

    public string? TaskToCreateKey { get; set; }

    public string? NotificationRouteKey { get; set; }

    public string AuditLabel { get; set; } = null!;

    public bool IsActive { get; set; }
}
