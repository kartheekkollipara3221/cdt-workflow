using System;
using System.Collections.Generic;

namespace WorkflowEngine.DatabaseFirst.Generated.Entities;

public partial class NotificationRoute
{
    public long Id { get; set; }

    public long WorkflowDefinitionVersionId { get; set; }

    public string RouteKey { get; set; } = null!;

    public string NotificationType { get; set; } = null!;

    public string? RecipientRoleKey { get; set; }

    public string? RecipientUserId { get; set; }

    public string SubjectTemplate { get; set; } = null!;

    public bool IsActive { get; set; }
}
