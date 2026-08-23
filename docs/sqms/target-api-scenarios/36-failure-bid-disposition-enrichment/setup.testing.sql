-- TESTING ONLY: isolated disposable database. This deliberately changes trusted target data.
SET NOCOUNT ON;

DECLARE @EntityId int = 20000;
UPDATE [SSQMS].[Bid]
SET [CommitmentAmountRequested] = NULL
WHERE [ID] = @EntityId;
