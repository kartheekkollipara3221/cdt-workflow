using System;
using System.Collections.Generic;

namespace WorkflowEngine.DatabaseFirst.Generated.Entities;

public partial class IdempotencyRecord
{
    public long Id { get; set; }

    public string IdempotencyKey { get; set; } = null!;

    public string WorkflowInstanceId { get; set; } = null!;

    public string ActionKey { get; set; } = null!;

    public string RequestHash { get; set; } = null!;

    public string ResultHash { get; set; } = null!;

    public string ResultJson { get; set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public virtual WorkflowInstance WorkflowInstance { get; set; } = null!;
}
