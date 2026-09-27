-- Read-only Portfolio Trade configuration: one row per active transition.
SELECT
    w.WorkflowKey,
    v.VersionNumber,
    t.FromStateKey,
    t.ActionKey,
    t.AllowedRoleKey,
    t.ToStateKey,
    t.TaskToCloseKey,
    t.TaskToCreateKey,
    td.OwnerRoleKey AS TaskDefaultOwnerRole,
    t.NotificationRouteKey,
    nr.IsActive AS NotificationRouteActive,
    nr.NotificationType,
    nr.SubjectTemplate,
    recipients.RecipientRules,
    td.SlaPolicyKey,
    sp.IsActive AS SlaPolicyActive,
    sp.CalculationMode,
    sp.DurationMinutes,
    sp.SendDueDateReminder,
    sp.ReminderLocalTime,
    sp.TimeZoneId,
    sp.OverdueStartOffsetDays,
    sp.RecurrenceIntervalMinutes,
    sp.RecurrenceIntervalDays,
    sp.DueNotificationRouteKey,
    dueRoute.IsActive AS DueRouteActive,
    dueRoute.OverdueSubjectPrefix AS DueSubjectPrefix,
    sp.OverdueNotificationRouteKey,
    overdueRoute.IsActive AS OverdueRouteActive,
    overdueRoute.OverdueSubjectPrefix AS OverdueSubjectPrefix
FROM dbo.WorkflowDefinition AS w
JOIN dbo.WorkflowDefinitionVersion AS v
    ON v.WorkflowDefinitionId = w.Id
JOIN dbo.WorkflowTransitionDefinition AS t
    ON t.WorkflowDefinitionVersionId = v.Id
LEFT JOIN dbo.WorkflowTaskDefinition AS td
    ON td.WorkflowDefinitionVersionId = v.Id
   AND td.TaskKey = t.TaskToCreateKey
LEFT JOIN dbo.SlaPolicy AS sp
    ON sp.WorkflowDefinitionVersionId = v.Id
   AND sp.SlaPolicyKey = td.SlaPolicyKey
LEFT JOIN dbo.NotificationRoute AS nr
    ON nr.WorkflowDefinitionVersionId = v.Id
   AND nr.RouteKey = t.NotificationRouteKey
LEFT JOIN dbo.NotificationRoute AS dueRoute
    ON dueRoute.WorkflowDefinitionVersionId = v.Id
   AND dueRoute.RouteKey = sp.DueNotificationRouteKey
LEFT JOIN dbo.NotificationRoute AS overdueRoute
    ON overdueRoute.WorkflowDefinitionVersionId = v.Id
   AND overdueRoute.RouteKey = sp.OverdueNotificationRouteKey
OUTER APPLY
(
    SELECT STRING_AGG(
        CAST(CONCAT(
            rr.RecipientKind, ': ',
            CASE WHEN rr.SourceType = N'StaticAddress'
                 THEN rr.StaticAddress
                 ELSE CONCAT(rr.SourceType, ':', rr.SourceKey)
            END,
            CASE WHEN rr.AssignedRoleKey IS NOT NULL
                 THEN CONCAT(' [assignment=', rr.AssignedRoleKey, ']')
                 ELSE '' END,
            CASE WHEN rr.IsRequired = 1
                 THEN ' [required]' ELSE ' [optional]' END
        ) AS nvarchar(max)), N'; '
    ) WITHIN GROUP (ORDER BY rr.SortOrder, rr.Id) AS RecipientRules
    FROM dbo.NotificationRecipientRule AS rr
    WHERE rr.NotificationRouteId = nr.Id
      AND rr.IsActive = 1
) AS recipients
WHERE w.WorkflowKey = N'PortfolioTrade'
  AND w.IsActive = 1
  AND v.IsActive = 1
  AND t.IsActive = 1
ORDER BY v.VersionNumber, t.FromStateKey, t.ActionKey, t.AllowedRoleKey;