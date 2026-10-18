-- CDT PortfolioTrade: metadata rollback FOR NEW TRADES ONLY.
-- Sets version 2 inactive and version 1 active in one transaction.
-- Trades already on version 2 continue on version 2. Keep the CDT v2 engine build
-- installed while those trades are open. This script does not convert any trade.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @DefinitionId bigint = (SELECT Id FROM dbo.WorkflowDefinition WHERE WorkflowKey = N'PortfolioTrade');
    DECLARE @V1 bigint = (SELECT Id FROM dbo.WorkflowDefinitionVersion WHERE WorkflowDefinitionId = @DefinitionId AND VersionNumber = 1);
    DECLARE @V2 bigint = (SELECT Id FROM dbo.WorkflowDefinitionVersion WHERE WorkflowDefinitionId = @DefinitionId AND VersionNumber = 2);
    IF @V1 IS NULL
        THROW 52601, 'PortfolioTrade version 1 was not found.', 1;

    UPDATE dbo.WorkflowDefinitionVersion
    SET IsActive = CASE WHEN Id = @V1 THEN 1 ELSE 0 END
    WHERE WorkflowDefinitionId = @DefinitionId;

    COMMIT TRANSACTION;
    SELECT COUNT(*) AS OpenVersion2Trades
    FROM dbo.WorkflowInstance
    WHERE WorkflowDefinitionVersionId = @V2 AND Status = N'Active';
    PRINT N'PortfolioTrade version 1 is active for new trades. Open version 2 trades continue on version 2.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
