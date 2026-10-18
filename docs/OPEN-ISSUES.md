# Open issues and paused work

This is the current issue index after the 2026-10-14 folder cleanup.
The entries below preserve the 2026-10-11 review decisions. Cleanup does not
authorize fixes or establish that historical load measurements describe target capacity.

## Engine review

| Items | Status / remaining work |
|---|---|
| D02 / F06 | Open: SMTP failure can return an error after state is saved. Review dispatch and retry behavior. [Tracking](../.planning/defects/D02-email-in-request-load.md). |
| D03 | Understood: business failures return HTTP 500. Any changed status semantics require a new v2 endpoint. |
| D04 / C13 | Optional batch limits and durable exponential backoff implemented and verified on 2026-10-14. Enable explicitly after coordinated schema/consumer deployment. SMTP remains at least once; maximum attempts/dead-letter policy remains an owner decision. [Rollout](OUTBOX_RETRY_ROLLOUT.md). |
| D05 | Open: add API solution coverage to CI. |
| D06 | Open, Major: async operations, cancellation, and load limits. [Tracking](../.planning/defects/D06-sync-engine-no-cancellation.md). |
| D09 | Open, Major: transactional seed updates and workflow-version publication. [Tracking](../.planning/defects/D09-seed-updates-v1-in-place.md). |
| D10 | Open, Major: logging, health, correlation, and metrics. [Tracking](../.planning/defects/D10-no-logging-health-metrics.md). |
| L1 / F06 | Open: caller-controlled idempotency and safe retries after timeouts. |
| L2 | Open: HTTP pipeline integration tests. |
| L3 | Open: resolve Swashbuckle against the package policy. No package change authorized. |
| L4 | Open: SQL unique-violation detection depends on message text. |
| L5 | Open: console DI scope validation. |
| L6 / C14 | SLA batching and isolated context lifetimes implemented and verified on 2026-10-17. Default 100 with optional Sla:BatchSize. Measured index tuning and target whole-table lookup costs remain follow-up work. [Behavior](SLA_BATCH_PROCESSING.md). |
| L7 | Open: local SQL defaults and deployment configuration. |
| L8 | Open: acceptance tool has no explicit solution entry (it remains a test project dependency). |
| L9 | Known Pkcs vulnerability resolved on 2026-10-14: explicit 10.0.13 reference; core/API/tool vulnerability scans clear. Broader package-lifecycle review remains separate. [Verification](../.planning/quick/261014-p51-patch-vulnerable-pkcs-dependency-and-ver/261014-p51-SUMMARY.md). |
| F07 | Open: assignment role missing from request hash. |
| F08 | Open: action reason not accepted/enforced consistently. |
| F09 | Open: SLA edit versus task-completion race. |

Preserved owner decisions: authentication is target-owned (D01/F02); callers
keep business object IDs unique across workflow keys (D07); Draft/SaveDraft are
accepted for now (D08). Parallel approvals are out of scope. Timers do not move
workflow state. The caller selects actions from business decisions. F01 was set
aside for the initialized target deployment; F04 was accepted as target-owned
SLA edit protection. F03 and F05 were fixed in 589b89b and 3ca34fe.

The [paused engine work packages](../.planning/paused/engine-defect-fixes/.continue-here.md)
preserve the implementation order and unanswered decisions. No work package is
started by this cleanup.

## Other paused topics

Cleanup verification on 2026-10-14: 550 of 552 tests passed. Two fresh-clone
build tests fail with NU1902/NU1903 for the existing transitive
`System.Security.Cryptography.Pkcs` 10.0.0 package. Existing restore-state builds
pass; a fully clean build is not verified passing. Track this with L9. Logs are in
`C:/projects/cdt-workflow-archive/20261014-cleanup/verification/`. No package or
warning-policy changes were made during cleanup.

Subsequent package correction on 2026-10-14 resolves that cleanup limitation:
Pkcs now resolves to 10.0.13, both previously failing fresh-build tests pass,
and all 564 final core/API/local SMTP tests pass. See the L9 verification above.
Historical cleanup and C06 reports retain their original measured results.

- [SQMS Bid return to Seller Review](../.planning/paused/sqms-bid-return-to-seller-review/.continue-here.md): recipient/CC choice and existing-completed-Bid scope remain unanswered.
- [CDT Trading Desk recipients](../.planning/paused/cdt-trading-desk-recipients/.continue-here.md).
- [CDT recipient rules and SLA libraries](../.planning/paused/cdt-recipients-sla-libraries/.continue-here.md).

## Earlier API checklist

The archived TODO had five unchecked items. They are retained here for triage,
not treated as newly approved requirements:

- Review local SMTP launch-profile settings before publishing configuration.
- Decide whether audit/history, task, and notification read APIs are needed.
- Choose operational scheduling for SLA scans and notification processing; overlaps D02/D04.
- Target authentication integration: target-owned under O1; preserve trusted-header compatibility.
- Confirm target migration/deployment ownership. Use the current [DBA runbook](sqms/DBA-DEPLOYMENT.md); do not apply obsolete deployment advice automatically.

## Deferred archive decision (after current engine fixes)

User deferred archival on 2026-10-17. Same-database archive tables are the preferred
direction, but schema, retention periods and historical access remain undecided.
Before moving operational records, define eligibility for completed workflows,
preserve idempotent response replay and notification-occurrence duplicate protection,
verify copies and relationships, and agree on final purge policy. Active workflows
and unresolved notifications stay operational. No archive tables, purge job or data
movement are authorized by the current C14 SLA batching task.

## Historical evidence

The full pre-cleanup tree is at `C:/projects/cdt-workflow-archive/20261014-cleanup/snapshot`.
It includes the original reviews, TODO, legacy source evidence, and handoff history.
`manifest.csv` in the archive records SHA-256 hashes for moved files.
The archive is local and is not included in a future Git clone.

- [Final review](C:/projects/cdt-workflow-archive/20261014-cleanup/snapshot/WORKFLOW_ENGINE_FINAL_REVIEW.md).
- [Load assessment](C:/projects/cdt-workflow-archive/20261014-cleanup/snapshot/WORKFLOW_ENGINE_LOAD_DEFECT_ASSESSMENT.html).
- [Original API TODO](C:/projects/cdt-workflow-archive/20261014-cleanup/snapshot/TODO_WORKFLOW_API.md).
