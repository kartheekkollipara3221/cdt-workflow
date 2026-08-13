:ON ERROR EXIT

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

:r .\sql\sqms\001_seed_sqms_transition_metadata.sql
GO
:r .\sql\sqms\001_seed_sqms_transition_metadata.sql
GO
:r .\sql\sqms\002_verify_sqms_transition_metadata.sql
GO

ROLLBACK TRANSACTION;

IF @@TRANCOUNT <> 0
    THROW 51200, 'Rollback validation left an open SQL transaction.', 1;

PRINT 'SQMS seed and verification passed; outer validation transaction rolled back with no open transaction.';
