# F05: Isolate failed outbox messages

User authorization: fix F05 after explanation; a failed email should retain its retry/error state while the processor continues with independent messages. F04 is accepted because the target API protects deadline edits; this is user-confirmed integration context, not independently reviewed target code.

1. Reproduce a sender failure and a malformed recipient snapshot blocking later healthy messages in SQL-backed tests.
2. Accumulate row failures, continue the finite pending-row list, preserve first Failure and existing constructor compatibility, and report all failures/counts in the command. Preserve cancellation stop behavior and claim release.
3. Verify repeated runs, multiple failures, command diagnostics, cancellation, concurrent claims, stale SLA cancellation, and real smtp4dev capture. Run both warning-as-error builds and full core/API suites using private audit LocalDB only.
4. Update audit presentation and handoff. No other findings, production deployment, database repair, or schema changes.

Scope limits: one attempt per selected row per processor invocation. Retry scheduling/backoff, quarantine, batch-size limits and relay-outage circuit breaking are separate work. SMTP at-least-once semantics remain. If persisting failure state fails, the run still stops rather than concealing a database failure.
