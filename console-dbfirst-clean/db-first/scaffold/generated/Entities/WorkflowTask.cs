using System;
using System.Collections.Generic;

namespace WorkflowEngine.DatabaseFirst.Generated.Entities;

public partial class WorkflowTask
{
    public long Id { get; set; }

    public string WorkflowInstanceId { get; set; } = null!;

    public string TaskKey { get; set; } = null!;

    public string AssignedRoleKey { get; set; } = null!;

    public string? AssignedUserId { get; set; }

    public string Status { get; set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? DueAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string? CompletedByUserId { get; set; }

    public string? ClosedByActionKey { get; set; }

    public virtual WorkflowInstance WorkflowInstance { get; set; } = null!;
}
