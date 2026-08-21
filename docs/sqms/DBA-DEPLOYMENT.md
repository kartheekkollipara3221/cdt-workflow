# SQMS Production Deployment Runbook

This runbook is the production handoff for the additive SQMS thin-layer integration. The DBA owns execution of the SQL package. Application operations owns deployment, configuration, smoke testing, and Windows Task Scheduler. Keep the SQMS scheduled task disabled until both the CDT regression gate and SQMS gate have passed.

## Release inputs

- Application build from the approved release commit.
- Existing target database containing the workflow tables and the DBA-owned `SSQMS.Opportunity` and `SSQMS.Bid` tables.
- [`001_sqms_notification_outbox_ddl.sql`](../../sql/sqms/001_sqms_notification_outbox_ddl.sql)
- [`002_sqms_workflow_metadata_dml.sql`](../../sql/sqms/002_sqms_workflow_metadata_dml.sql)
- [`003_verify_sqms_deployment.sql`](../../sql/sqms/003_verify_sqms_deployment.sql)
- Four approved recipient JSON arrays: open To, open Cc, Seller-approval Cc, and Bid-disposition Cc. Open To must contain at least one address; the others may be empty.
- Approved environment URLs for the open-bidding dashboard, Seller bids dashboard, and Bid form.

Only `001` through `003` are production files. Files under `sql/sqms/testing` are isolated-test helpers and must never run in production. Production also never runs `init-db`, `Database.Migrate()`, `EnsureCreated()`, or EF-generated DDL/seed operations.

## 1. Change approval and preflight

Take the normal database backup/recovery checkpoint and record the application release and SQL file hashes. Confirm the selected database explicitly; do not rely on a user's default database.

Run this read-only preflight in the intended database:

```sql
SELECT DB_NAME() AS SelectedDatabase;

SELECT
    OBJECT_ID(N'dbo.NotificationOutbox', N'U') AS NotificationOutboxObjectId,
    OBJECT_ID(N'SSQMS.Opportunity', N'U') AS OpportunityObjectId,
    OBJECT_ID(N'SSQMS.Bid', N'U') AS BidObjectId;

SELECT
    SUM(p.rows) AS OutboxRows,
    SUM(a.total_pages) * 8.0 / 1024 AS ReservedMB
FROM sys.tables AS t
JOIN sys.indexes AS i ON i.object_id = t.object_id AND i.index_id IN (0, 1)
JOIN sys.partitions AS p ON p.object_id = i.object_id AND p.index_id = i.index_id
JOIN sys.allocation_units AS a ON a.container_id = p.partition_id
WHERE t.object_id = OBJECT_ID(N'dbo.NotificationOutbox');

SELECT name, is_nullable, max_length
FROM sys.columns
WHERE object_id = OBJECT_ID(N'dbo.NotificationOutbox')
  AND name IN (N'WorkflowInstanceId', N'NotificationOccurrenceKey');
```

All three object IDs must be non-null. The DBA must inspect `NotificationOutbox` size, current workload, long-running transactions, replication/availability topology, and the deployment window. `001` alters a column and adds an index and check constraint; these operations can acquire schema-modification locks and block concurrent application work. Schedule or pause traffic according to the DBA's assessment.

The verifier enforces the exact user-approved target schemas. Stop if either target table is missing or differs; production must not use the test helper to create or repair those tables.

## 2. Apply the database package

From the approved repository/package directory, use an authenticated DBA connection and an explicit server/database. The following examples are PowerShell commands; replace every bracketed value.

```powershell
sqlcmd -S '<server>' -d '<database>' -E -b -i 'sql/sqms/001_sqms_notification_outbox_ddl.sql'

sqlcmd -S '<server>' -d '<database>' -E -b `
  -v 'SqmsOpenToRecipientsJson=["open-team@example.com"]' `
     'SqmsOpenCcRecipientsJson=[]' `
     'SqmsSellerApprovalCcRecipientsJson=["seller-oversight@example.com"]' `
     'SqmsBidDispositionCcRecipientsJson=["bid-oversight@example.com"]' `
  -i 'sql/sqms/002_sqms_workflow_metadata_dml.sql'

sqlcmd -S '<server>' -d '<database>' -E -b -i 'sql/sqms/003_verify_sqms_deployment.sql'
```

Use the site's required encryption/certificate options and authentication mechanism. Do not copy the example addresses into production. `-b` is required so a SQL error produces a failing process exit code. Both `001` and `002` are transactional and rerunnable, but a failed or incompatible precondition is a release stop, not permission to edit around the guard. The final command must print `SQMS deployment verification passed.`

## 3. Deploy and configure the application

Deploy the approved target API and console artifacts with external scheduling still disabled. Preserve all existing CDT endpoints, command arguments, settings, and notification processing.

Supply secrets and environment-specific values through the approved configuration provider. Relevant environment keys are:

```text
ConnectionStrings__DefaultConnection
Sqms__Enabled
Sqms__Links__OpenBiddingDashboardUrl
Sqms__Links__SellerApprovalBidsDashboardUrl
Sqms__Links__BidFormUrl
Sqms__Scheduling__TimeZoneId
Sqms__Scheduling__BiddingDays
Sqms__Scheduling__SellerResponseDays
Notifications__Sender
Smtp__Host
Smtp__Port
Smtp__From
Smtp__RecipientOverride
```

Production defaults are `Eastern Standard Time`, 15 bidding days, and 5 Seller-response days unless the approved release configuration says otherwise. Replace all `example.test` URLs. The open-bidding URL remains static; the adapter adds `opportunityId` to the Seller dashboard and `bidId` to the Bid form while preserving existing query strings and fragments.

First start with `Sqms__Enabled=false` and prove the existing CDT health/readiness and one supported CDT workflow path. Then set `Sqms__Enabled=true`, restart through the normal controlled process, and run one authorized SQMS start/context/action smoke path using test business data. Confirm target `Status`/`WorkflowState`, workflow instance/audit, expected outbox row, rendered links, and recipient override delivery. Remove `Smtp__RecipientOverride` only after delivery validation and approval. Do not manually select a next state or send `toState`.

The existing `process-notification-outbox` operational job remains responsible for SMTP delivery. `run-sqms-scheduled` queues scheduled notifications and transitions; it does not replace the outbox processor.

## 4. Configure Windows Task Scheduler

Create one task with three operations-approved daily triggers. Every trigger runs the identical command and every invocation checks all due categories; the trigger times do not map to individual categories.

- Program: the deployed `dotnet.exe` host.
- Arguments: `"<publish-directory>\WorkflowEngine.Console.dll" run-sqms-scheduled`.
- Start in: the exact publish directory.
- If the previous instance is running: **Do not start a new instance**.
- Identity: a non-interactive service identity with read/execute access to the publish directory, access to the approved configuration/secret provider, and least-privilege database access.
- Environment: provide the same production connection, SQMS, link, timezone, and notification configuration used by the deployed console. Do not put secrets in task arguments.
- Logging: capture stdout/stderr and Task Scheduler history in the approved centralized or retained log location. Include timestamps, task result, and application release identifier.
- Monitoring: alert on a nonzero process exit, missed trigger, launch failure, or stale last-success time. A nonzero SQMS result means at least one candidate failed even when other records succeeded.

Keep all three triggers disabled until database verification, application deployment, CDT smoke, SQMS smoke, logging, and alerting have passed. Enable them only under the approved release change. Record the task definition, identity, trigger times, first run result, and next run times. Database occurrence keys provide replay protection, but they do not replace the non-overlap setting.

## 5. Post-deployment verification

Run `003_verify_sqms_deployment.sql` again and retain its output. Confirm:

- existing CDT workflows, SLA processing, public samples, and notification outbox processing still operate unchanged;
- the SQMS adapter returns the authoritative workflow state and only the approved target state columns change;
- `TotalBid`, `LargestBid`, and `LowestBid` remain application/UI owned;
- scheduled catch-up closes due Opportunities before classifying Bids and before queueing still-valid reminders;
- duplicate scheduled invocations do not create duplicate occurrence rows;
- outbox failures follow the existing retry path and scheduled candidate failures produce a nonzero task result.

## 6. Rollback order

Prefer a forward fix after data has been created. Any database reversal requires a separate DBA-reviewed script and change approval; the package intentionally contains no destructive automatic rollback.

1. Disable all three SQMS triggers and prevent new target-API SQMS calls (`Sqms__Enabled=false`). Leave the existing CDT outbox processor available unless incident response specifically requires stopping delivery.
2. Capture logs, failing record identifiers, SQMS workflow/runtime rows, notification occurrence rows, and the deployed configuration. Preserve evidence before changing data.
3. Roll back the application binaries/configuration through the standard application release procedure and re-run the CDT smoke test.
4. If SQMS metadata must be disabled, deactivate only the two SQMS workflow definitions/routes in a DBA-reviewed transaction after confirming no in-flight SQMS work depends on them. Do not rename/delete stable keys or remove generic CDT metadata.
5. Reverse `001` only if the database must return to the prior physical schema and every occurrence-owned outbox row has been safely handled. Before making `WorkflowInstanceId` non-null, prove no row has a null value. Then, in one reviewed transaction, remove the owner check constraint, filtered occurrence index, occurrence column, recreate the original workflow-instance dependency shape, and alter `WorkflowInstanceId` to `NOT NULL`. Validate existing CDT row counts/hashes and constraints afterward.
6. Re-run CDT regression/smoke checks and retain the rollback evidence. Do not re-enable SQMS scheduling until a corrected release passes every gate again.

Deleting occurrence-owned rows or SQMS runtime history is destructive and is not implied by application rollback. Archive or remove such data only under explicit data-owner and DBA approval with a recovery plan.
