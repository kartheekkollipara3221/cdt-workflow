:setvar SqmsOpenToRecipientsJson "[""sqms-open@example.test""]"
:setvar SqmsOpenCcRecipientsJson "[""sqms-open-cc@example.test""]"
:setvar SqmsSellerApprovalCcRecipientsJson "[""sqms-seller-cc@example.test""]"
:setvar SqmsBidDispositionCcRecipientsJson "[""sqms-bid-cc@example.test""]"

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;
GO

:r sql/sqms/001_sqms_notification_outbox_ddl.sql
GO
:r sql/sqms/002_sqms_workflow_metadata_dml.sql
GO
:r sql/sqms/002_sqms_workflow_metadata_dml.sql
GO
:r sql/sqms/003_verify_sqms_deployment.sql
GO

IF XACT_STATE() <> 1
    THROW 51301, 'SQMS package validation transaction is not committable.', 1;

ROLLBACK TRANSACTION;
PRINT 'SQMS package rollback validation passed.';
