using System;
using System.Collections.Generic;

namespace WorkflowEngine.DatabaseFirst.Generated.Entities;

public partial class NotificationOutbox
{
    public long Id { get; set; }

    public string WorkflowInstanceId { get; set; } = null!;

    public long? WorkflowTaskId { get; set; }

    public string BusinessObjectType { get; set; } = null!;

    public string BusinessObjectId { get; set; } = null!;

    public string NotificationType { get; set; } = null!;

    public string? RecipientRoleKey { get; set; }

    public string? RecipientUserId { get; set; }

    public string Subject { get; set; } = null!;

    public string PayloadJson { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string CorrelationId { get; set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? ProcessingToken { get; set; }

    public DateTimeOffset? ProcessingStartedAtUtc { get; set; }

    public int RetryCount { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset? SentAtUtc { get; set; }

    public virtual WorkflowInstance WorkflowInstance { get; set; } = null!;
}
