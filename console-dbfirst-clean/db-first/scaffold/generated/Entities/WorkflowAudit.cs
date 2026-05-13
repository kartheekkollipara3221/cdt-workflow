using System;
using System.Collections.Generic;

namespace WorkflowEngine.DatabaseFirst.Generated.Entities;

public partial class WorkflowAudit
{
    public long Id { get; set; }

    public string WorkflowInstanceId { get; set; } = null!;

    public string BusinessObjectType { get; set; } = null!;

    public string BusinessObjectId { get; set; } = null!;

    public string ActionKey { get; set; } = null!;

    public string FromStateKey { get; set; } = null!;

    public string ToStateKey { get; set; } = null!;

    public string PerformedByUserId { get; set; } = null!;

    public string PerformedByRoleKey { get; set; } = null!;

    public string? Reason { get; set; }

    public string CorrelationId { get; set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public virtual WorkflowInstance WorkflowInstance { get; set; } = null!;
}
