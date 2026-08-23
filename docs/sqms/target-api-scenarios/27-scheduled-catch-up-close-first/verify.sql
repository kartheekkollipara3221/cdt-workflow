-- Read-only verifier. Use timestamps/IDs from the isolated scenario run.
DECLARE @opportunityId int = 10000;
DECLARE @workflowId nvarchar(150) = N'WF-SQMS-OPP-10000';

SELECT [ID], [Status], [WorkflowState], [ModifiedBy], [ModifiedDate]
FROM [SSQMS].[Opportunity]
WHERE [ID] = @opportunityId
  AND [Status] = N'CLOSED TO BIDDING'
  AND [WorkflowState] = N'CLOSED TO BIDDING'
  AND [ModifiedBy] = N'system';

SELECT [ActionKey], [FromStateKey], [ToStateKey], [CreatedAtUtc]
FROM [dbo].[WorkflowAudit]
WHERE [WorkflowInstanceId] = @workflowId
  AND [ActionKey] = N'CloseOpportunityBidding';

SELECT COUNT(*) AS ObsoleteGroupRows
FROM [dbo].[NotificationOutbox]
WHERE [NotificationOccurrenceKey] IN (
    N'SQMS:OpportunityOpen:2030:Q1:2030-01-02',
    N'SQMS:OpportunityReminder:2030:Q1:2030-01-15');
-- Expected: 0 only when the isolated fixture has no other still-open Opportunity in this group.
-- Outbox rows are group-level and intentionally do not store individual contributing Opportunity IDs;
-- the source-backed ordering test is the authoritative proof when a group contains multiple Opportunities.
