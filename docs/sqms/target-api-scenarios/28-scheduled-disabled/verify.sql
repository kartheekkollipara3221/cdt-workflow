-- Read-only before/after snapshot. Capture these counts before and after the disabled call.
SELECT COUNT(*) AS OpportunityRows FROM [SSQMS].[Opportunity];
SELECT COUNT(*) AS BidRows FROM [SSQMS].[Bid];
SELECT COUNT(*) AS SqmsInstances FROM [dbo].[WorkflowInstance]
WHERE [WorkflowKey] IN (N'SqmsOpportunity', N'SqmsBid');
SELECT COUNT(*) AS SqmsOutboxRows FROM [dbo].[NotificationOutbox]
WHERE [NotificationType] IN (
 N'SqmsOpportunityOpen', N'SqmsOpportunityReminder', N'SqmsOpportunityPendingSellerApproval',
 N'SqmsBidAccepted', N'SqmsBidPartiallyAccepted', N'SqmsBidNotAccepted');
-- Expected: before and after counts match. The no-read guarantee is proven by the focused unit test.
