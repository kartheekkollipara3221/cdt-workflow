# Overdue Email Link and Date Format Design

**Status:** Approved for implementation planning

**Date:** 2026-08-09

**Scope:** Surgical correction to the existing SLA overdue notification content

## 1. Goal

Every `SlaTaskOverdue` email must include the saved request link and must display its due and overdue values as date-only strings derived using a fixed UTC-5 offset.

The displayed date format is `MM/dd/yyyy`. The email must not display a clock time or a timezone suffix. For example, `2030-01-15T02:00:00Z` becomes `01/14/2030` after applying the fixed UTC-5 offset.

## 2. Confirmed Root Cause

- The task's persisted `NotificationContextJson` already carries `tradeLink`, and the SLA monitor copies that snapshot into its render context.
- The seeded `RouteSlaTaskOverdue` plain-text and HTML templates do not reference `{{tradeLink}}`, so the link is omitted even though the value is available.
- `RunSlaService` currently adds `dueAtUtc` and `overdueAtUtc` to the render context using round-trip ISO formatting, and the overdue templates render those raw UTC tokens directly.
- The live smtp4dev message reproduced both defects: no link and raw `+00:00` timestamps.

## 3. Locked Requirements

- Scope is limited to `SlaTaskOverdue` notification content.
- Convert due and overdue instants using a fixed UTC-5 offset all year. Do not apply daylight-saving adjustment.
- Render only `MM/dd/yyyy`.
- Do not append `EST`, `EDT`, `UTC-5`, or another timezone label.
- Add the request link to both plain-text and HTML bodies.
- The HTML body uses a clickable `Open request` anchor whose `href` comes from `tradeLink`.
- The plain-text body prints the saved `tradeLink` value.
- Preserve the raw UTC context values and persisted schedule instants for diagnostics and scheduling correctness.
- Do not change public Workflow Start, Action, or Context contracts.
- Do not change SQL schema, SLA recurrence, recipient resolution, outbox lifecycle, or SMTP behavior.
- Previously sent outbox rows and emails remain immutable history.

## 4. Rendering Design

`RunSlaService` remains responsible for adding generic workflow-owned values to the snapshotted notification context.

It will retain the existing raw tokens:

```text
dueAtUtc
overdueAtUtc
```

It will also add date-only display tokens:

```text
dueDateDisplay
overdueDateDisplay
```

Each display token is derived by:

1. Taking the corresponding `DateTimeOffset` instant;
2. Calling `ToOffset(TimeSpan.FromHours(-5))`;
3. Formatting with `MM/dd/yyyy` and invariant culture.

This conversion happens only when building notification display context. It does not alter `WorkflowTask.DueAtUtc`, `WorkflowTaskSlaSchedule.OverdueAtUtc`, occurrence selection, recurrence calculation, or persisted UTC values.

The seeded overdue templates become conceptually:

```text
Workflow instance {{workflowInstanceId}} has an overdue task {{taskKey}} assigned to {{assignedRoleKey}}.
Task due date: {{dueDateDisplay}}
Overdue date: {{overdueDateDisplay}}
Request link: {{tradeLink}}
```

```html
<p>Workflow instance <strong>{{workflowInstanceId}}</strong> has an overdue task <strong>{{taskKey}}</strong> assigned to <strong>{{assignedRoleKey}}</strong>.</p>
<p>Task due date: <strong>{{dueDateDisplay}}</strong></p>
<p>Overdue date: <strong>{{overdueDateDisplay}}</strong></p>
<p><a href="{{tradeLink}}">Open request</a></p>
```

The existing HTML renderer continues to encode `tradeLink` before inserting it into the template.

## 5. Metadata and Existing Databases

`PortfolioTradeSeedData` owns the Portfolio Trade email wording. `WorkflowSeedService` already upserts `BodyTemplate` and `HtmlBodyTemplate`, so no migration or direct SQL edit is required.

After the implementation passes tests, running the normal `init-db` command against `workflowengine` updates the active route metadata. It must not reset runtime workflow data or rewrite existing `NotificationOutbox` rows.

Only newly queued overdue rows use the corrected templates. The already-sent message for outbox row 2 remains unchanged.

## 6. Failure Behavior

`tradeLink` becomes a required token for newly rendered overdue notifications. If a task snapshot does not contain a nonblank `tradeLink`, the existing template-validation behavior reports a missing-token warning and skips queuing the malformed reminder.

No fallback URL is invented, and the engine does not query the form/business API for a link.

## 7. Test-Driven Verification

Implementation starts with focused failing tests that prove:

- The plain-text overdue body contains the persisted request URL.
- The HTML overdue body contains a clickable `Open request` link.
- A UTC instant near midnight is converted before formatting; for example, `2030-01-15T02:00:00Z` renders as `01/14/2030`.
- Neither overdue body contains the raw ISO UTC timestamp.
- Neither overdue body contains an `EST`, `EDT`, or UTC offset suffix.
- The outbox payload still retains `dueAtUtc` and `overdueAtUtc` as raw UTC diagnostic values.
- Seed verification confirms that the overdue route references `tradeLink`, `dueDateDisplay`, and `overdueDateDisplay` in both body formats.

After the focused tests pass, run the relevant warning-as-error build, default test suite, `init-db`, and `verify-seed` gates sequentially with `-m:1` where applicable.

## 8. Live Acceptance

Do not mutate or replay the existing sent overdue row. After updating seed metadata, prove the fix with a fresh user-approved workflow/task occurrence so a new outbox row and smtp4dev message are created legitimately.

The live email must show:

- The expected To/Cc snapshot;
- A request link in plain text;
- A clickable `Open request` link in HTML;
- Date-only due and overdue values in `MM/dd/yyyy`; and
- No UTC timestamp or timezone suffix.

## 9. Files Expected to Change

- `src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs`
- `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs`
- Focused SLA and seed verification tests under `tests/WorkflowEngine.Tests/`
- `CODEX_HANDOFF.md` after implementation and verification

No API DTO, entity, migration, recipient, SMTP sender, or public sample change is expected.
