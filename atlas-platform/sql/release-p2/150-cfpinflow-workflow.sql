-- CP Atlas, release P2, script 150: the workflow engine metadata of the platform app CFPINFLOW (workflow ClientInflow).
-- Workflow ClientInflow, version 1. States: Draft (initial), Submitted, FundsReceived (end). Engine role: Write.
-- Actions: Submit (Draft -> Submitted, email E1), NotifyInformational (Submitted -> Submitted, email E2),
-- ConfirmFunds (Submitted -> FundsReceived, email E3). No task and no SLA policy.
-- Each recipient rule is a request input: the platform sends the addresses with the action. No email address here;
-- the addresses are in AppSettings:Modules:CFPINFLOW:Workflow:Lists of each environment.
-- DML only. Additive: all keys are new. Safe to run again: each row is added or set to the values of this script.
-- Run in the database of the workflow engine tables (the database of ConnectionStrings:DefaultConnection).
--
-- Rollback: set dbo.WorkflowDefinition.IsActive = 0 for WorkflowKey 'ClientInflow' (then a submit of CFPINFLOW gives an
-- engine error), and set AppSettings:Modules:CFPINFLOW:Enabled = false.
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.WorkflowDefinition', N'U') IS NULL OR OBJECT_ID(N'dbo.NotificationRecipientRule', N'U') IS NULL
        THROW 55150, 'The workflow engine tables do not exist in this database. Run the script in the engine database.', 1;

    DECLARE @Key nvarchar(100) = N'ClientInflow';
    IF NOT EXISTS (SELECT 1 FROM dbo.WorkflowDefinition WHERE WorkflowKey = @Key)
        INSERT dbo.WorkflowDefinition (WorkflowKey, DisplayName, BusinessObjectType, IsActive)
        VALUES (@Key, N'Client Flows Portal - Inflow', N'ClientInflow', 1);
    ELSE
        UPDATE dbo.WorkflowDefinition SET DisplayName = N'Client Flows Portal - Inflow', BusinessObjectType = N'ClientInflow', IsActive = 1
        WHERE WorkflowKey = @Key;
    DECLARE @DefinitionId bigint = (SELECT Id FROM dbo.WorkflowDefinition WHERE WorkflowKey = @Key);

    IF NOT EXISTS (SELECT 1 FROM dbo.WorkflowDefinitionVersion WHERE WorkflowDefinitionId = @DefinitionId AND VersionNumber = 1)
        INSERT dbo.WorkflowDefinitionVersion (WorkflowDefinitionId, VersionNumber, IsActive, CreatedAtUtc)
        VALUES (@DefinitionId, 1, 1, CONVERT(datetimeoffset, '2026-11-20T00:00:00+00:00'));
    UPDATE dbo.WorkflowDefinitionVersion SET IsActive = CASE WHEN VersionNumber = 1 THEN 1 ELSE 0 END
    WHERE WorkflowDefinitionId = @DefinitionId;
    DECLARE @VersionId bigint = (SELECT Id FROM dbo.WorkflowDefinitionVersion WHERE WorkflowDefinitionId = @DefinitionId AND VersionNumber = 1);

    -- States
    MERGE dbo.WorkflowStateDefinition AS target
    USING (VALUES (N'Draft', N'Draft', 0), (N'Submitted', N'Submitted', 0), (N'FundsReceived', N'Funds Received', 1))
        AS source (StateKey, DisplayName, IsTerminal)
    ON target.WorkflowDefinitionVersionId = @VersionId AND target.StateKey = source.StateKey
    WHEN MATCHED THEN UPDATE SET DisplayName = source.DisplayName, IsTerminal = source.IsTerminal, IsActive = 1
    WHEN NOT MATCHED BY TARGET THEN INSERT (WorkflowDefinitionVersionId, StateKey, DisplayName, IsTerminal, IsActive)
        VALUES (@VersionId, source.StateKey, source.DisplayName, source.IsTerminal, 1);

    -- Actions
    MERGE dbo.WorkflowActionDefinition AS target
    USING (VALUES (N'Submit', N'Submit', N'Submit', 10),
                  (N'NotifyInformational', N'Notify Informational', N'Notify', 20),
                  (N'ConfirmFunds', N'Confirm Funds Received', N'Complete', 30))
        AS source (ActionKey, DisplayName, Intent, SortOrder)
    ON target.WorkflowDefinitionVersionId = @VersionId AND target.ActionKey = source.ActionKey
    WHEN MATCHED THEN UPDATE SET DisplayName = source.DisplayName, SectionKey = N'Inflow', ButtonKey = source.ActionKey,
        ButtonLabel = source.DisplayName, Intent = source.Intent, RequiresReason = 0, SortOrder = source.SortOrder, IsActive = 1
    WHEN NOT MATCHED BY TARGET THEN INSERT
        (WorkflowDefinitionVersionId, ActionKey, DisplayName, SectionKey, ButtonKey, ButtonLabel, Intent, RequiresReason, SortOrder, IsActive)
        VALUES (@VersionId, source.ActionKey, source.DisplayName, N'Inflow', source.ActionKey, source.DisplayName, source.Intent, 0, source.SortOrder, 1);

    -- Role
    IF NOT EXISTS (SELECT 1 FROM dbo.WorkflowRoleDefinition WHERE WorkflowDefinitionVersionId = @VersionId AND RoleKey = N'Write')
        INSERT dbo.WorkflowRoleDefinition (WorkflowDefinitionVersionId, RoleKey, DisplayName, IsActive)
        VALUES (@VersionId, N'Write', N'Write', 1);
    ELSE
        UPDATE dbo.WorkflowRoleDefinition SET DisplayName = N'Write', IsActive = 1
        WHERE WorkflowDefinitionVersionId = @VersionId AND RoleKey = N'Write';

    -- Notification routes: the texts of the current SharePoint emails (E1 to E3).
    DECLARE @CrLf nchar(2) = NCHAR(13) + NCHAR(10);
    DECLARE @Fields nvarchar(max) = CONCAT(N'Client Name: {{accountName}}', @CrLf, N'Account Number: {{accountNumber}}', @CrLf,
        N'Amount: {{amount}}', @CrLf, N'Inflow Date: {{inflowDate}}');
    DECLARE @HtmlFields nvarchar(max) = N'<p><strong>Client Name:</strong> {{accountName}}<br><strong>Account Number:</strong> {{accountNumber}}<br>'
        + N'<strong>Amount:</strong> {{amount}}<br><strong>Inflow Date:</strong> {{inflowDate}}</p>';
    DECLARE @Routes TABLE (RouteKey nvarchar(100) PRIMARY KEY, SubjectTemplate nvarchar(300), BodyTemplate nvarchar(max), HtmlBodyTemplate nvarchar(max));
    INSERT @Routes VALUES
        (N'ClientInflow.ActionRequired',
         N'{{accountName}} Cash Inflow Instruction ' + NCHAR(8211) + N' Action Required',
         CONCAT(N'A cash inflow notification has been received. Please monitor the custodian cash balance accordingly and confirm using this form when the funds have been received: {{recordLink}}', @CrLf, @CrLf, @Fields),
         N'<p>A cash inflow notification has been received. Please monitor the custodian cash balance accordingly and confirm using <a href="{{recordLink}}">this form</a> when the funds have been received.</p>' + @HtmlFields),
        (N'ClientInflow.SubmittedInformational',
         N'{{accountName}} Inflow Instruction - Informational',
         CONCAT(N'A cash inflow notification has been received. You will be notified when the funds have been received. Additional information: {{recordLink}}', @CrLf, @CrLf, @Fields),
         N'<p>A cash inflow notification has been received. You will be notified when the funds have been received. Click <a href="{{recordLink}}">here</a> for additional information.</p>' + @HtmlFields),
        (N'ClientInflow.FundsReceived',
         N'{{accountName}} Cash Inflow - Informational',
         CONCAT(N'Please be advised that a cash inflow has been received. Additional information: {{recordLink}}', @CrLf, @CrLf, @Fields),
         N'<p>Please be advised that a cash inflow has been received. Click <a href="{{recordLink}}">here</a> for additional information.</p>' + @HtmlFields);

    MERGE dbo.NotificationRoute AS target
    USING @Routes AS source
    ON target.WorkflowDefinitionVersionId = @VersionId AND target.RouteKey = source.RouteKey
    WHEN MATCHED THEN UPDATE SET NotificationType = source.RouteKey, SubjectTemplate = source.SubjectTemplate,
        BodyTemplate = source.BodyTemplate, HtmlBodyTemplate = source.HtmlBodyTemplate, OverdueSubjectPrefix = N'', IsActive = 1
    WHEN NOT MATCHED BY TARGET THEN INSERT
        (WorkflowDefinitionVersionId, RouteKey, NotificationType, SubjectTemplate, BodyTemplate, HtmlBodyTemplate, OverdueSubjectPrefix, IsActive)
        VALUES (@VersionId, source.RouteKey, source.RouteKey, source.SubjectTemplate, source.BodyTemplate, source.HtmlBodyTemplate, N'', 1);

    -- Transitions
    MERGE dbo.WorkflowTransitionDefinition AS target
    USING (VALUES (N'Draft', N'Submit', N'Submitted', N'ClientInflow.ActionRequired'),
                  (N'Submitted', N'NotifyInformational', N'Submitted', N'ClientInflow.SubmittedInformational'),
                  (N'Submitted', N'ConfirmFunds', N'FundsReceived', N'ClientInflow.FundsReceived'))
        AS source (FromStateKey, ActionKey, ToStateKey, RouteKey)
    ON target.WorkflowDefinitionVersionId = @VersionId AND target.FromStateKey = source.FromStateKey
       AND target.ActionKey = source.ActionKey AND target.AllowedRoleKey = N'Write'
    WHEN MATCHED THEN UPDATE SET ToStateKey = source.ToStateKey, TaskToCloseKey = NULL, TaskToCreateKey = NULL,
        NotificationRouteKey = source.RouteKey, AuditLabel = source.ActionKey, IsActive = 1
    WHEN NOT MATCHED BY TARGET THEN INSERT
        (WorkflowDefinitionVersionId, FromStateKey, ActionKey, AllowedRoleKey, ToStateKey, TaskToCloseKey, TaskToCreateKey, NotificationRouteKey, AuditLabel, IsActive)
        VALUES (@VersionId, source.FromStateKey, source.ActionKey, N'Write', source.ToStateKey, NULL, NULL, source.RouteKey, source.ActionKey, 1);

    -- Recipient rules: delete and add again, so the rules are always the rules of this script.
    DELETE rule_ FROM dbo.NotificationRecipientRule AS rule_
    INNER JOIN dbo.NotificationRoute AS route ON route.Id = rule_.NotificationRouteId
    WHERE route.WorkflowDefinitionVersionId = @VersionId;

    INSERT dbo.NotificationRecipientRule
        (NotificationRouteId, RecipientKind, SourceType, SourceKey, StaticAddress, AssignedRoleKey, SortOrder, IsRequired, IsActive)
    SELECT route.Id, source.Kind, N'RequestInput', source.SourceKey, NULL, NULL, source.SortOrder, source.IsRequired, 1
    FROM (VALUES
            (N'ClientInflow.ActionRequired', N'To', N'icg', 1, 1),
            (N'ClientInflow.SubmittedInformational', N'To', N'investmentLists', 1, 1),
            (N'ClientInflow.SubmittedInformational', N'To', N'relationshipManager', 2, 0),
            (N'ClientInflow.FundsReceived', N'To', N'investmentLists', 1, 1),
            (N'ClientInflow.FundsReceived', N'To', N'relationshipManager', 2, 0),
            (N'ClientInflow.FundsReceived', N'To', N'csTeam', 3, 1),
            (N'ClientInflow.FundsReceived', N'Cc', N'rmcc', 4, 0),
            (N'ClientInflow.FundsReceived', N'Cc', N'imrg', 5, 1),
            (N'ClientInflow.FundsReceived', N'Cc', N'starFasIfiCash', 6, 1))
        AS source (RouteKey, Kind, SourceKey, SortOrder, IsRequired)
    INNER JOIN dbo.NotificationRoute AS route ON route.WorkflowDefinitionVersionId = @VersionId AND route.RouteKey = source.RouteKey;

    -- Log this script in the same transaction (when the engine tables are in the CP Atlas database).
    IF OBJECT_ID(N'dbo.CP_Atlas_ScriptLog', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_ScriptLog WHERE Release = N'P2' AND ScriptName = N'150-cfpinflow-workflow.sql')
        INSERT INTO dbo.CP_Atlas_ScriptLog (ScriptName, Release) VALUES (N'150-cfpinflow-workflow.sql', N'P2');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- Result: the transitions with their routes and the number of recipient rules of each route.
SELECT t.FromStateKey, t.ActionKey, t.ToStateKey, t.NotificationRouteKey,
       (SELECT COUNT(*) FROM dbo.NotificationRecipientRule AS r INNER JOIN dbo.NotificationRoute AS n ON n.Id = r.NotificationRouteId
        WHERE n.WorkflowDefinitionVersionId = t.WorkflowDefinitionVersionId AND n.RouteKey = t.NotificationRouteKey) AS RecipientRules
FROM dbo.WorkflowTransitionDefinition AS t
INNER JOIN dbo.WorkflowDefinitionVersion AS v ON v.Id = t.WorkflowDefinitionVersionId
INNER JOIN dbo.WorkflowDefinition AS d ON d.Id = v.WorkflowDefinitionId
WHERE d.WorkflowKey = N'ClientInflow' AND v.VersionNumber = 1
ORDER BY t.FromStateKey, t.ActionKey;
GO
