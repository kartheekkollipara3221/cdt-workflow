# C14 SLA batching; defer archival

User authorized SLA batch processing and moving archive decisions to the end of the issue list. Preserve PROJECT.md milestones and all reminder/public contract semantics.

1. Red SQL-backed test: more than 100 due schedules must use multiple isolated contexts with bounded tracking; caller context must not accumulate entities.
2. Add validated Sla:BatchSize (default 100) while preserving the existing constructor. Page due identifiers using a stable cursor and a run-start upper schedule ID; use a fresh context per batch, preserving provider configuration/interceptors. Keep one transaction per schedule. Continue all pages and aggregate existing result shape. Known bad schedules must not starve later pages.
3. Test ties, multiple pages, future schedules, poison rows, repeated runs, cancellation/fatal propagation and unchanged caller tracking. Run core/API regressions and warning-as-error builds.
4. Document that candidate/tracking memory is bounded but legacy result lists and total run work still scale with processed rows. No performance claim without measurement. No schema/archive implementation in this task.
5. Move archival decision near end of OPEN-ISSUES; update STATE/SUMMARY/handoff, review and commit only intended files. No remote push/deployment unless requested.
