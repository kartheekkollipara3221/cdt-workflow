# SLA batch processing (C14)

SLA scanning now reads due schedule identifiers in pages and uses a fresh EF context
per page. The default page size is 100. A call continues through the eligible work;
500 due schedules normally require five processing pages, plus a final empty-page
check. Schedules that are not due are filtered in SQL, not loaded for processing.

Optional configuration through the existing AddWorkflowInfrastructure registration:

```json
{
  "Sla": {
    "BatchSize": 100
  }
}
```

BatchSize must be a positive integer. The existing two-argument RunSlaService
constructor uses 100; a new overload accepts SlaProcessingOptions. There are no
request/response, console command, database schema, package or seed changes.
This setting is separate from Notifications:Outbox:BatchSize, which controls delivery.

The scan fixes its time from RunSlaRequest.Now and captures an upper schedule ID
near the start of the run. Pages remain ordered by NextReminderAtUtc then Id.
The cursor advances past every selected row, including invalid/skipped schedules,
so an unchanged bad row cannot occupy the first page forever. New schedules beyond
the upper ID wait for the next invocation. This is not a database snapshot: concurrent
schedule changes can affect later page eligibility, and work moved behind the cursor
is evaluated on the next scheduled invocation.

Each schedule is rechecked inside its own short transaction. Existing recurrence,
occurrence uniqueness, recipient snapshots and per-schedule failure isolation remain.
Batch contexts are disposed after each page. They retain configured EF interceptors,
borrow the caller's live SQL connection without taking ownership and preserve its
runtime command timeout. They run sequentially; callers must not use that connection
or context concurrently during a scan. Pre-existing unchanged tracked schedules in
the caller's context are refreshed, including their row versions, so subsequent
inspection/completion remains valid. Caller edits are neither saved nor discarded.

Candidate-list and new tracked-entity growth are bounded by page size. The public
result still contains all queued reminder summaries and warnings, so those lists
and total run duration grow with the work performed. There is no fixed per-run time
budget or maximum number of pages. Cancellation remains supported; committed earlier
reminders remain committed if a later page fails. The caller should still use normal
scoped context lifetimes. A caller that already tracks many objects retains those
objects; the scanner does not clear them.

This change requires no active workflow closure or database migration. Deploy the
updated engine through normal application deployment. The existing external scheduler
must continue invoking SLA scans. Batch size 100 is a default, not a measured production
capacity recommendation. Measure logical reads, job time and allocations with target
data before tuning indexes or page size.

Archival is deferred to the end of docs/OPEN-ISSUES.md. This change does not archive,
purge or remove duplicate-prevention records. Remaining C14 topics include measured
query/index tuning and target application whole-table lookup costs.
