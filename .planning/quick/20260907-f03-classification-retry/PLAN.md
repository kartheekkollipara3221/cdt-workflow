# F03: Preserve SQMS classification totals across retries

Authorization: user agreed this defect should be corrected. Full audit remains deferred; no other finding is included.

1. Reproduce a discount classification failure after fair-value/premium bids commit, followed by a fresh scheduler retry. Cover previously processed bids advancing to seller review.
2. Add a compatible data-store read for submitted and audit-confirmed classified bids. Keep the existing submitted-only method unchanged. Only execute actions on submitted rows.
3. Validate with SQL Server: inject a projection failure, verify rollback and earlier commits, retry in a fresh DI scope, exclude drafts, and check no duplicate actions. Run core/API regressions, SQL seed and sample gates, and warning-as-error builds.
4. Record the result and resume discussion at F04, without restarting the full audit.

Boundaries: no schema changes, target HTTP changes, identity implementation, generic-engine changes, or batch transaction. Existing custom ISqmsDataStore implementations retain their old behavior through the default interface method; custom stores must implement the added read to obtain this fix. Audit history and unchanged target financial inputs are assumptions; this is not an immutable financial snapshot.
