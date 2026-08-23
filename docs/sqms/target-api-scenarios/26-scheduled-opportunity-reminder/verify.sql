-- Read-only verifier.
DECLARE @occurrence nvarchar(300) = N'SQMS:OpportunityReminder:2030:Q1:2030-01-15';
SELECT [Id], [WorkflowInstanceId], [NotificationOccurrenceKey], [NotificationType],
       [NotificationRecipientsJson], [Subject], [Body], [HtmlBody], [PayloadJson], [Status]
FROM [dbo].[NotificationOutbox]
WHERE [NotificationOccurrenceKey] = @occurrence
  AND [WorkflowInstanceId] IS NULL
  AND [NotificationOccurrenceKey] IS NOT NULL
  AND [NotificationType] = N'SqmsOpportunityReminder';

SELECT COUNT(*) AS ExactOccurrenceRows
FROM [dbo].[NotificationOutbox]
WHERE [NotificationOccurrenceKey] = @occurrence;
-- Expected: 1, enforced by the filtered unique non-null occurrence-key index.

SELECT [ID], [Status], [WorkflowState], [ModifiedBy], [ModifiedDate]
FROM [SSQMS].[Opportunity]
WHERE [ID] = 10000;
-- Compare with the pre-run snapshot: notification queueing changes no target value.
