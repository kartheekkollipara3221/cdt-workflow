# Audit discussion checkpoint — 2026-09-06

User requested pause: "save the session need to step away will revisit".

## Current decision

Full source review of src/ and workflow-api/ is deferred. Existing audit is not exhaustive. User wants each defect explained individually, in simpler terms, focusing on impact.

## F01 disposition

Technical evidence: init-db -> WorkflowDatabaseInitializer.Initialize -> Database.Migrate applies pending destructive historical migrations, then SeedPortfolioTrade. User states production initialization ran on an empty database, is already complete, and will not run again. Therefore set F01 aside as not an immediate concern for this deployment. Do not erase the technical finding or claim production migration history was independently inspected.

## F02 disposition

Explained that API trusts X-Workflow-* identity and role headers without independently authenticating their source. Potential impersonation exists only where an untrusted caller can control those headers and reach the API; trusted upstream enforcement could provide the boundary.

User clarified that authentication and identity belong to the target API, not this project. Treat them as target-owned responsibilities; do not add an identity subsystem here.

## F03 correction authorized — 2026-09-07

User agreed to correct retry-dependent SQMS bid classification. The built-in SQL data store now reads submitted plus audit-confirmed previously classified bids for the total, including those advanced to seller review. Only submitted bids receive actions. No schema or HTTP contract changes. Custom stores must override the added compatible basis method to obtain the fix.

Two regression cases failed before the change and passed afterward. A real SQL trigger-induced projection failure passed after correction, including fresh-scope retry, rollback, draft exclusion and no duplicate actions. Broader verification is recorded in .planning/quick/20260907-f03-classification-retry/SUMMARY.md.

## F04 disposition — accepted

User confirmed that the target API protects deadline edits and accepted this boundary. No engine authorization change is needed under that integration contract. This is user-provided context, not independently inspected target API evidence.

## F05 correction authorized — 2026-09-07

User approved fixing one failed email blocking later healthy messages. The processor now records each failure and continues the finite pending-row list, leaving failed rows Pending with RetryCount/LastError and releasing claims. Cancellation still stops the run. Existing first-Failure result field remains, with an additive full failure collection and accurate console failure counts.

Regression evidence and limits are in .planning/quick/20260907-f05-outbox-isolation/SUMMARY.md. Retry delays, maximum retries/quarantine and relay outage circuit breaking are separate from this correction. Next discussion is F06 after user finishes F05. Full audit remains deferred. No other fixes or deployment are authorized.

## F06 disposition — set aside

User directed that F06 can be disregarded. No correction made; existing HTTP retry/delivery-result behavior remains unchanged.

## Deferred fix — F07

User confirms this needs fixing, but is lower priority and excluded from this round. Track separately from accepted/set-aside findings. AssignedRoleKey is omitted from the request comparison, so a reused operation key with a changed assignment role can replay the original result instead of conflict. Future correction must preserve historical replay compatibility. No code changes authorized for F07 now.

## F08 disposition — set aside

User stated that a reason is not needed and directed that F08 be disregarded. No code changes.

## Deferred agenda — low priority

- F07: include assignment role in retry comparison, preserving historical replay compatibility. Required fix, excluded from this round.
- F09: prevent a concurrent deadline edit from creating an active SLA schedule or rewriting the deadline after its task completes. User requested a later, low-priority fix. Validate both operation orderings with SQL-backed concurrency tests when implementing.

All nine current findings have now been discussed. F03/F05 are corrected locally; F07/F09 remain required deferred fixes. Other findings are accepted or set aside under the recorded user decisions. Full source audit remains deferred. Do not implement deferred items automatically.

## Established project context

Shared rules-driven approval engine replaces legacy SharePoint workflows across multiple applications. CDT and SQMS are integrated. Assess engine versus target-app responsibilities accordingly. Present findings through AUDIT.html; user does not prefer reading Markdown reviews.

## Saved evidence

AUDIT.html includes nine findings and live verification. 546 tests passed; SQL2022 isolated probes and SQL2025 private LocalDB suites; nine CDT/SQMS MIME matches. Application source unchanged. See LIVE-VERIFICATION.md and CODEX_HANDOFF.md for resource names, limits and failed harness attempts. Retained audit artifacts remain in the workspace; this pause does not delete or stop them.
