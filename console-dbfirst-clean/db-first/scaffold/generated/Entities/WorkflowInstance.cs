using System;
using System.Collections.Generic;

namespace WorkflowEngine.DatabaseFirst.Generated.Entities;

public partial class WorkflowInstance
{
    public long Id { get; set; }

    public string WorkflowInstanceId { get; set; } = null!;

    public string WorkflowKey { get; set; } = null!;

    public long WorkflowDefinitionVersionId { get; set; }

    public string BusinessObjectType { get; set; } = null!;

    public string BusinessObjectId { get; set; } = null!;

    public string CurrentStateKey { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTimeOffset StartedAtUtc { get; set; }

    public string StartedByUserId { get; set; } = null!;

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public int Version { get; set; }

    public virtual ICollection<IdempotencyRecord> IdempotencyRecords { get; set; } = new List<IdempotencyRecord>();

    public virtual ICollection<NotificationOutbox> NotificationOutboxes { get; set; } = new List<NotificationOutbox>();

    public virtual ICollection<WorkflowAudit> WorkflowAudits { get; set; } = new List<WorkflowAudit>();

    public virtual WorkflowTask? WorkflowTask { get; set; }
}
