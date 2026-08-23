-- Read-only verifier. Run only against an isolated scenario database.
DECLARE @occurrence nvarchar(300) = N'SQMS:OpportunityOpen:2030:Q1:2030-01-02';

SELECT [Id], [WorkflowInstanceId], [NotificationOccurrenceKey], [NotificationType],
       [NotificationRecipientsJson], [Subject], [Body], [HtmlBody], [PayloadJson],
       [Status], [RetryCount], [CreatedAtUtc]
FROM [dbo].[NotificationOutbox]
WHERE [NotificationOccurrenceKey] = @occurrence
  AND [WorkflowInstanceId] IS NULL
  AND [NotificationOccurrenceKey] IS NOT NULL
  AND [NotificationType] = N'SqmsOpportunityOpen';

SELECT COUNT(*) AS ExactOccurrenceRows
FROM [dbo].[NotificationOutbox]
WHERE [NotificationOccurrenceKey] = @occurrence;
-- Expected: 1. UX_NotificationOutbox_NotificationOccurrenceKey is filtered to non-null keys.

SELECT [name], [is_unique], [filter_definition]
FROM sys.indexes
WHERE [object_id] = OBJECT_ID(N'[dbo].[NotificationOutbox]')
  AND [name] = N'UX_NotificationOutbox_NotificationOccurrenceKey';

SELECT [ID], [Status], [WorkflowState], [ModifiedBy], [ModifiedDate]
FROM [SSQMS].[Opportunity]
WHERE [ID] = 10000;
-- Compare with the pre-run snapshot: every value must be unchanged.
