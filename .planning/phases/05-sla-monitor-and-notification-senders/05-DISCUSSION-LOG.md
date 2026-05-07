# Phase 5: SLA Monitor and Notification Senders - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md; this log preserves the alternatives considered.

**Date:** 2026-05-07
**Phase:** 05-SLA Monitor and Notification Senders
**Areas discussed:** SLA Scan Behavior, Outbox Processing, Sender Configuration, SMTP Test Boundary

---

## SLA Scan Behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Pending tasks only | Scan active pending tasks with non-null `DueAtUtc`; skip completed tasks, completed workflows, and no-SLA tasks. | yes |
| All task rows | Scan every task and filter later; broader but easier to accidentally notify completed work. | |
| Active workflows only | Scan tasks through active workflow instances; similar result but more coupled to workflow status. | |

**User's choice:** Pending tasks only.
**Notes:** User locked SQL-level filtering: pending tasks, non-null due date, strict `DueAtUtc < now`, active workflow, and `NOT EXISTS` for any prior `SlaTaskOverdue` row by task id. Scan materializes outside a transaction, processes each row in a per-row transaction, uses deterministic `DueAtUtc, Id` ordering, and supports `--now` via fixed `TimeProvider`.

---

## Outbox Processing

| Option | Description | Selected |
|--------|-------------|----------|
| Per-row sender transaction | Load pending rows, call configured sender per row, mark `Sent` on success, leave `Pending` on sender failure. | yes |
| All-or-nothing batch | Send all rows in one transaction; one failure blocks unrelated notifications. | |
| Dry-run only for POC | Print pending rows without marking anything sent. | |

**User's choice:** Per-row sender transaction.
**Notes:** User locked fail-fast behavior. Sender exceptions roll back the current row, abort the command, leave current and later rows pending, and preserve already-sent earlier rows. Concurrent losers use `UPDATE ... WHERE Status = 'Pending'`; zero rows affected logs a warning and continues.

---

## Sender Configuration

| Option | Description | Selected |
|--------|-------------|----------|
| Configuration key only | `Notifications:Sender` selects `Console`, `NoOp`, or `Smtp`; default is `Console`. | yes |
| Command flag override | Allow `process-notification-outbox --sender Console|NoOp|Smtp`; useful for demos but adds another override path. | |
| Environment-specific hardcode | Use Console in dev/test and Smtp in production by environment name. | |

**User's choice:** Configuration key only.
**Notes:** User locked case-insensitive config selection at startup, default `Console`, fail-fast unknown values, and no command flag override. Tests override DI directly with recording senders instead of mutating configuration.

---

## SMTP Test Boundary

| Option | Description | Selected |
|--------|-------------|----------|
| Opt-in fake SMTP tests | Mark SMTP integration tests with `Category=Smtp`; default `dotnet test` excludes them and explicit filter runs against local fake SMTP only. | yes |
| Unit tests only | Test SMTP message construction through fakes, skip any network SMTP test. | |
| Default integration tests | Run SMTP tests in the normal suite; strongest coverage but risks slow or flaky local runs. | |

**User's choice:** Opt-in fake SMTP tests.
**Notes:** User locked three tiers: Tier 1 fake/unit tests run by default, Tier 2 fake SMTP integration tests run only with `--filter Category=Smtp`, and Tier 3 live relay smoke tests are manual only. Phase 5 owns Tier 1 and Tier 2.

---

## the agent's Discretion

- Exact service/class names, internal sender input DTOs, and SMTP test exclusion mechanism may be chosen during planning.
- The implementer may choose `smtp4dev` or Papercut for Tier 2 fake SMTP tests, then document the setup.

## Deferred Ideas

- Failed notification status, retry policies, dead-letter handling, and attempt tracking are v2 work.
- Database locking or skip-locked processing for concurrent SMTP runs is v2 work.
- Live SMTP relay smoke testing is manual and outside Phase 5 automation.
