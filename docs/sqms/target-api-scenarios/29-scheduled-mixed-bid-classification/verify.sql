-- Read-only verifier for the documented isolated fixture.
SELECT [ID], [BidExpressedAsFairValue], [CommitmentAmountRequested],
       [Status], [WorkflowState], [ModifiedBy], [ModifiedDate]
FROM [SSQMS].[Bid]
WHERE [ID] IN (20000, 20001, 20002)
ORDER BY [ID];
-- Expected: 20000/20001 IPG REVIEW; 20002 NOT ALLOCATED; ModifiedBy=system.

SELECT [BusinessObjectId], [CurrentStateKey], [Status], [Version]
FROM [dbo].[WorkflowInstance]
WHERE [WorkflowKey] = N'SqmsBid'
  AND [BusinessObjectId] IN (N'20000', N'20001', N'20002')
ORDER BY [BusinessObjectId];

SELECT wi.[BusinessObjectId], a.[ActionKey], a.[FromStateKey], a.[ToStateKey], a.[PerformedByRoleKey]
FROM [dbo].[WorkflowAudit] AS a
INNER JOIN [dbo].[WorkflowInstance] AS wi ON wi.[WorkflowInstanceId] = a.[WorkflowInstanceId]
WHERE wi.[WorkflowKey] = N'SqmsBid'
  AND wi.[BusinessObjectId] IN (N'20000', N'20001', N'20002')
  AND a.[ActionKey] IN (N'MoveBidToIpgReview', N'MarkBidNotAllocated')
ORDER BY wi.[BusinessObjectId];
