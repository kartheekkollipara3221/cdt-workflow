# SQMS Projection-Ownership Live Acceptance

This one-shot acceptance proves the corrected ownership boundary through one complete Opportunity and four Bid workflows against SQL Server and a fresh smtp4dev mailbox.

## Isolated evidence

```text
Server: DOLLY
Database: workflowengine_sqms_projection_ownership_live_acceptance
Run marker: SQMS-PROJECTION-OWNERSHIP-LIVE-20260822
smtp4dev SMTP: localhost:3027
smtp4dev UI: http://localhost:5002
smtp4dev data: C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-projection-ownership-live-acceptance-20260822
Scenario: projection-ownership
```

Run once from the approved worktree:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/run-sqms-projection-ownership-live-acceptance.ps1
```

The launcher refuses to overwrite an existing database or mailbox and refuses to take over ports 3027 or 5002. After a successful run, do not rerun it or remove the retained evidence.

## What is proven

- Every request-driven Start/Action stores a `RequestUnchanged` proof whose complete before/after target-row JSON is identical.
- A separately named target-owner step stores the returned authoritative state and a paired `TargetOwnerPersisted` proof.
- Scheduled Opportunity close and Bid classification directly change target rows and store five `ScheduledProjected` proofs.
- The final Opportunity is `COMPLETED` at version 10.
- The four Bids end as `ACCEPTED` version 7, `PARTIALLY ACCEPTED` version 5, `NOT ACCEPTED` version 5, and `NOT ALLOCATED` version 3.
- Exactly six messages are sent: open, reminder, seller approval, accepted, partially accepted, and not accepted.
- Tasks and SLA schedules remain empty, target/workflow states match, and calculated sentinels remain unchanged.

## Verification-only commands

Set the same environment values retained in the launcher, then run:

```powershell
dotnet run --project tools/WorkflowEngine.Sqms.LiveAcceptance/WorkflowEngine.Sqms.LiveAcceptance.csproj --no-build -- --verify-only
sqlcmd -S DOLLY -d workflowengine_sqms_projection_ownership_live_acceptance -E -C -b -i sql/sqms/testing/008_verify_sqms_projection_ownership_live_acceptance.sql
```

Inspect the ownership trail directly:

```sql
SELECT Id, EvidenceKind, EntityType, TargetId, Operation,
       ReturnedState, ReturnedVersion, Unchanged, RecordedAtUtc
FROM dbo.SqmsProjectionOwnershipEvidence
ORDER BY Id;
```

The smtp4dev mailbox at [http://localhost:5002](http://localhost:5002) must contain exactly six retained messages. Before stopping any process, verify its command line contains the exact retained data directory and ports above.
