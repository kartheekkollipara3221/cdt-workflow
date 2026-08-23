-- Read-only verifier. Narrow by known IDs/time window in a shared database.
SELECT [Id], [WorkflowInstanceId], [NotificationOccurrenceKey], [NotificationType],
       [NotificationRecipientsJson], [Subject], [Body], [HtmlBody], [PayloadJson],
       [Status], [RetryCount], [SentAtUtc], [LastError], [ProcessingToken], [ProcessingStartedAtUtc]
FROM [dbo].[NotificationOutbox]
WHERE [NotificationType] IN (
 N'SqmsOpportunityOpen',
 N'SqmsOpportunityReminder',
 N'SqmsOpportunityPendingSellerApproval',
 N'SqmsBidAccepted',
 N'SqmsBidPartiallyAccepted',
 N'SqmsBidNotAccepted')
ORDER BY [Id];
-- Expected for each successful first-attempt scenario row:
-- Status=Sent, RetryCount=0, SentAtUtc non-null, LastError/lease fields null.

SELECT [NotificationType], [Status], [RetryCount], COUNT(*) AS MessageCount
FROM [dbo].[NotificationOutbox]
WHERE [NotificationType] IN (
 N'SqmsOpportunityOpen', N'SqmsOpportunityReminder', N'SqmsOpportunityPendingSellerApproval',
 N'SqmsBidAccepted', N'SqmsBidPartiallyAccepted', N'SqmsBidNotAccepted')
GROUP BY [NotificationType], [Status], [RetryCount]
ORDER BY [NotificationType];
