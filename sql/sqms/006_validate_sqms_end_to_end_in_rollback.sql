:ON ERROR EXIT

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

:r .\sql\sqms\001_seed_sqms_transition_metadata.sql
GO
:r .\sql\sqms\004_seed_sqms_end_to_end_metadata.sql
GO
:r .\sql\sqms\004_seed_sqms_end_to_end_metadata.sql
GO
:r .\sql\sqms\002_verify_sqms_transition_metadata.sql
GO
:r .\sql\sqms\005_verify_sqms_end_to_end_metadata.sql
GO

ROLLBACK TRANSACTION;

IF @@TRANCOUNT <> 0
    THROW 51240, 'SQMS end-to-end rollback validation left an open SQL transaction.', 1;

PRINT 'SQMS end-to-end seed-twice verification passed and was rolled back.';
