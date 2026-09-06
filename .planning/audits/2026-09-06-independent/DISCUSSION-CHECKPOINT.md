# Audit discussion checkpoint — 2026-09-06

User requested pause: "save the session need to step away will revisit".

## Current decision

Full source review of src/ and workflow-api/ is deferred. Existing audit is not exhaustive. User wants each defect explained individually, in simpler terms, focusing on impact.

## F01 disposition

Technical evidence: init-db -> WorkflowDatabaseInitializer.Initialize -> Database.Migrate applies pending destructive historical migrations, then SeedPortfolioTrade. User states production initialization ran on an empty database, is already complete, and will not run again. Therefore set F01 aside as not an immediate concern for this deployment. Do not erase the technical finding or claim production migration history was independently inspected.

## Exact resume point: F02

Explained that API trusts X-Workflow-* identity and role headers without independently authenticating their source. Potential impersonation exists only where an untrusted caller can control those headers and reach the API; trusted upstream enforcement could provide the boundary.

Outstanding question: In production, what authenticates the user and supplies those headers—the CDT/SQMS backend, a gateway, or the browser?

Continue F02 after that answer. Do not move to F03 until the user finishes discussing F02. No fixes, migrations, full-review restart, or new tests authorized merely by resuming the discussion.

## Established project context

Shared rules-driven approval engine replaces legacy SharePoint workflows across multiple applications. CDT and SQMS are integrated. Assess engine versus target-app responsibilities accordingly. Present findings through AUDIT.html; user does not prefer reading Markdown reviews.

## Saved evidence

AUDIT.html includes nine findings and live verification. 546 tests passed; SQL2022 isolated probes and SQL2025 private LocalDB suites; nine CDT/SQMS MIME matches. Application source unchanged. See LIVE-VERIFICATION.md and CODEX_HANDOFF.md for resource names, limits and failed harness attempts. Retained audit artifacts remain in the workspace; this pause does not delete or stop them.
