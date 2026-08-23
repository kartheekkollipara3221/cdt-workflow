-- TESTING ONLY: isolated disposable database. This deliberately changes trusted target data.
SET NOCOUNT ON;

DECLARE @EntityId int = 10000;
UPDATE [SSQMS].[Opportunity]
SET [EffectiveDateForSale] = NULL
WHERE [ID] = @EntityId;
