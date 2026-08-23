SET NOCOUNT ON;

-- Read-only evidence. The integration test must capture a full `SELECT *` snapshot before each call
-- and compare every value after the call; SQL cannot reconstruct a lost pre-call row by itself.
SELECT * FROM [SSQMS].[Opportunity] WHERE [ID] IN (10000, 10001) ORDER BY [ID];

-- Scheduled row 10001 must match authoritative runtime state and identify the system projection.
IF NOT EXISTS (
    SELECT 1
    FROM [SSQMS].[Opportunity] o
    JOIN [dbo].[WorkflowInstance] w ON w.[WorkflowInstanceId] = CONCAT(N'WF-SQMS-OPP-', o.[ID])
    WHERE o.[ID] = 10001
      AND o.[Status] = w.[CurrentStateKey]
      AND o.[WorkflowState] = w.[CurrentStateKey]
      AND o.[ModifiedBy] = N'system')
    THROW 51000, 'Scheduled four-column projection does not match workflow state.', 1;

-- Snapshot comparison must prove all non-owned columns, including the following representative
-- business and lifecycle values, are unchanged: AccountName, AccountNumber, SellerRMTeam,
-- RelationshipManager, CommitmentAmountSale, EffectiveDateForSale, CreatedBy, CreatedDate, IsDeleted.
