SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM [SSQMS].[Bid] WHERE [ID] = 20000 AND [Status] = N'IPG REVIEW' AND [WorkflowState] = N'IPG REVIEW')
    THROW 51000, 'First candidate did not commit.', 1;
IF NOT EXISTS (SELECT 1 FROM [SSQMS].[Bid] WHERE [ID] = 20001 AND [Status] = N'SUBMITTED' AND [WorkflowState] = N'SUBMITTED')
    THROW 51000, 'Middle failed candidate changed.', 1;
IF NOT EXISTS (SELECT 1 FROM [SSQMS].[Bid] WHERE [ID] = 20002 AND [Status] = N'IPG REVIEW' AND [WorkflowState] = N'IPG REVIEW')
    THROW 51000, 'Last candidate did not commit.', 1;
IF EXISTS (
    SELECT 1 FROM [dbo].[WorkflowAudit]
    WHERE [WorkflowInstanceId] = N'WF-SQMS-BID-20001' AND [ActionKey] = N'MoveBidToIpgReview')
    THROW 51000, 'Failed middle candidate retained an audit.', 1;

SELECT [ID], [Status], [WorkflowState], [ModifiedBy], [ModifiedDate], [AccountName], [AccountNumber]
FROM [SSQMS].[Bid]
WHERE [ID] IN (20000, 20001, 20002)
ORDER BY [ID];
