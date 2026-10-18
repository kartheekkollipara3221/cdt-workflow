# PortfolioTrade v2: CDT notification routes and SLA timing

Created: 2026-10-18. Final revision (2026-10-18): includes the plan review `docs/CDT_V2_PLAN_REVIEW_EXPLAINED.html` (R1-R5 and group upkeep).
Status: **final plan.** Approved by the user on 2026-10-18: D1 (v1 stays the local default) and the 6-column additive schema change. **Implementation starts only after the user says to start.** No code, seed, schema or database change is made before that.
Source requirement: `docs/CDT_requirements/one.jpeg`, `docs/CDT_requirements/2.jpeg`. The daily 2:30 PM summary email is out of scope.
Governing rules: `PROJECT.md` (sections 13, 14, 21, 22), `AGENTS.md` (backward compatibility).

## 1. Goal

Publish `PortfolioTrade` **version 2** with the agreed recipients and SLA timing. Keep version 1 unchanged, so the 4 running production workflows complete on version 1. Add only additive engine features. Deliver every database change as a DDL or DML SQL script.

## 2. Agreed behavior (user decisions, 2026-10-18)

### 2.1 Recipients

| Email (route) | To | Cc |
|---|---|---|
| Directed Trade Pending Checker Approval (`RouteCheckerAssignedOnSubmit`, `RouteCheckerAssignedOnResubmit`) | ICG RAs + Risk Team | `selectedCcUsers` |
| Overdue: ...Pending Checker Approval (`RouteSlaTaskOverdue`, snapshot route) | Task snapshot (same as above) | Task snapshot |
| Updates Required to Directed Trade Request (`RouteReturnedToMakerFromChecker`, `RouteReturnedToMakerFromTradingDesk`) | `originalMaker` | none |
| Directed Trade Request Pending Execution (`RouteTradingDeskAssigned`) | ICG RAs + Risk Team + selected desk users | `selectedCcUsers` (RM is inside it) |
| Due Today: ...Pending Execution (new `RouteTradingDeskDueToday`, own-rules route) | Selected desk users | none |
| Overdue: ...Pending Execution (new `RouteTradingDeskOverdue`, own-rules route) | Selected desk users | none |
| Trade Execution Pending Confirmation (`RouteBdrmAssigned`) | `originalMaker` + Risk Team | `selectedCcUsers` (RM is inside it) |
| Overdue: Trade Execution Pending Confirmation (new `RouteBdrmConfirmationOverdue`, own-rules route) | `originalMaker` | none |
| Confirmation that Trade settled in account (`RouteWorkflowCompleted`) | `originalMaker` + Risk Team | none |
| TradingDeskRework path (not used by CDT) | Unchanged copy of v1 | Unchanged copy of v1 |

The groups have no shared mailbox. Each person is one `StaticAddress` row. Desk users have `AssignedRoleKey` = `TradingDeskTaxable`, `TradingDeskTaxExempt` or `TradingDeskEquity`. ICG RA and Risk Team rows have `AssignedRoleKey = NULL`.

**SortOrder blocks** (the same on every route; they identify the group of a row):

| Block | Group |
|---|---|
| 1 | `originalMaker` (WorkflowParticipant) |
| 10-49 | ICG RAs |
| 50-89 | Risk Team |
| 100-199 | Taxable desk users |
| 200-299 | Tax Exempt desk users |
| 300-399 | Equity desk users |
| 1000 | `selectedCcUsers` (RequestInput) |

A person who is in both ICG RAs and Risk Team has one row in each block. Dedupe at resolution sends one copy.

### 2.2 SLA timing (America/New_York)

| Policy (v2) | Rule |
|---|---|
| `CheckerReviewSla` | Overdue 60 minutes after task creation, then every 60 minutes. |
| `TradingDeskReviewSla` | Due Today at 14:00 on the target date (same-day or future date). If the schedule is calculated after 14:00 on the target date, there is no Due Today reminder; the first reminder is Overdue the next day at 08:00. Overdue daily at 08:00, weekends included. |
| `BdrmClientServiceReviewSla` | First Overdue on the next local day at 09:00 after task creation, then daily at 09:00. |
| `TradingDeskReworkSla` | Unchanged copy of v1. |

**Late scans (R5).** These rules keep the existing catch-up behavior, with no backfill:

1. A late scan on the target date sends Due Today once (for example, at 14:20).
2. A scan on a later date follows the Overdue catch-up rule. A missed Due Today reminder is not sent.
3. No Overdue reminder is queued before 08:00 on its date. A scan at 07:00 on the next date defers to 08:00.
4. After several missed dates, only the current eligible Overdue occurrence is queued.

Each trade has its own task and schedule. Completion stops the schedule and cancels unsent reminders (current behavior).

### 2.3 Membership changes

Membership changes use option (a): the DML template changes the rows of a chosen version in place.

**The reminder audience is fixed at task creation (R4).** A pending task keeps the recipients of its task email and of its reminders. A membership change applies to tasks created after it.

**Never delete rule rows. Deactivate only (R3).**

## 3. Engine changes (additive)

### E1. Own-rules reminder routes, resolved and saved at task creation

- **Route kind (R3, implicit rule).** A reminder route that has **any** `NotificationRecipientRule` row, active or inactive, is an own-rules route. A route with no rule rows at all is a snapshot route (all v1 routes and the v2 Checker overdue route).
- **When the schedule is created** (`WorkflowSlaSynchronizationService.Synchronize`): for each own-rules due or overdue route of the policy, resolve the active rules with `NotificationRecipientResolver`. Use an empty request input and `task.AssignedRoleKey`. Save the result on the schedule:
  - `WorkflowTaskSlaSchedule.DueRecipientsJson`
  - `WorkflowTaskSlaSchedule.OverdueRecipientsJson`

  For a snapshot route the column stays NULL.
- **A resolution error rejects the action.** Examples are an own-rules route with no active rule for this desk (no To), or a `RequestInput` rule on the route. The engine throws `WorkflowSlaValidationException`, so the action fails and rolls back with a clear message. It does not send to a wider list.
- **Supersede on a target-date change** copies `DueRecipientsJson` and `OverdueRecipientsJson` from the previous schedule. It does not resolve again (R4).
- **At scan time** (`RunSlaService`), the engine uses the schedule column for the reminder kind if it is not NULL. Otherwise it uses `task.NotificationRecipientsJson` (current behavior). The scan does not read route rules.
- Recipient semantics do not change: route order, case-insensitive dedupe, first value kept, To over Cc (`PROJECT.md` section 22).
- `SeedVerificationService` and the verify script report:
  - a `RequestInput` rule on any SLA reminder route;
  - a v2 own-rules route that has no active row for a desk role that its task allows.
- `WorkflowSlaSynchronizationService` gets the resolver through a new constructor overload. The existing constructor keeps working.

### E2. Separate Due Today time and skipping a missed Due Today reminder

- `SlaPolicy.DueReminderLocalTime` (`time`, NULL). NULL means `ReminderLocalTime` (current behavior).
- `SlaPolicy.SkipMissedDueReminder` (`bit`, NOT NULL, default 0).
- `WorkflowTaskSlaSchedule.DueReminderLocalTime` (`time`, NULL). This is a snapshot, because the scan calculates the Due Today occurrence time.
- `SlaScheduleCalculator.CalculateApplicationDate`:
  - The due reminder uses the due time. The overdue start and the daily repeat use `ReminderLocalTime`.
  - If the skip flag is 1 and the calculation time is after the due reminder time, set `DueReminderAtUtc = NULL` and make the next reminder Overdue at `OverdueAtUtc`.
- `RunSlaService.ResolveOccurrence`: for kind `DueToday`, use `schedule.DueReminderLocalTime ?? schedule.ReminderLocalTime`. The late-scan rules in section 2.2 follow from the existing date-based kind selection.
- `ValidatePolicy`: the new values are allowed only in `ApplicationTargetDate` mode with `SendDueDateReminder = 1`.

### E3. First overdue on the next local day at the reminder time

- `SlaPolicy.OverdueStartsNextLocalDay` (`bit`, NOT NULL, default 0).
- Duration mode with flag 1: `OverdueAtUtc` = (task creation local date + 1 day) at `ReminderLocalTime`. `RecurrenceIntervalDays`, `ReminderLocalTime` and `TimeZoneId` are required. The existing daily recurrence then gives daily 09:00.
- **`DurationMinutes` stays positive (1440) on the v2 BDRM policy (R2).** The new engine ignores it when the flag is 1. An older engine accepts the policy and falls back to "+24 h, then daily at 09:00". It does not reject the action.

### Not in scope

- A check for delimiters in `StaticAddress`. `PROJECT.md` section 22 lists recipient syntax validation and delimited recipient parsing as stop-and-ask items.
- A "change desk while in TradingDeskReview" action.
- The 2:30 PM summary.

### Schema total: 6 additive columns

| Table | Column | Type |
|---|---|---|
| `SlaPolicy` | `DueReminderLocalTime` | `time` NULL |
| `SlaPolicy` | `SkipMissedDueReminder` | `bit` NOT NULL DEFAULT 0 |
| `SlaPolicy` | `OverdueStartsNextLocalDay` | `bit` NOT NULL DEFAULT 0 |
| `WorkflowTaskSlaSchedule` | `DueReminderLocalTime` | `time` NULL |
| `WorkflowTaskSlaSchedule` | `DueRecipientsJson` | `nvarchar(max)` NULL |
| `WorkflowTaskSlaSchedule` | `OverdueRecipientsJson` | `nvarchar(max)` NULL |

## 4. Seed changes (source of truth)

1. `PortfolioTradeSeedData` keeps the v1 arrays unchanged. Add `PortfolioTradeSeedDataV2` with the full v2 definition: the same state, action, role, task, participant and transition keys, plus the new routes, rules and policies above.
2. `WorkflowSeedService`:
   - `SeedPortfolioTrade()` keeps its current signature and behavior for v1. It sets v1 active **only if no other PortfolioTrade version is active**. This prevents two active versions.
   - Add `SeedPortfolioTradeVersion2(bool activate)`. It inserts or updates v2 rows. When `activate` is true, it sets v1 inactive and v2 active in one transaction.
   - Rule upsert **deactivates** active rule rows on a seeded route of the version being seeded when the seed for that route no longer lists them. It never deletes rows (R3).
3. Seed verification covers both versions and checks that exactly one PortfolioTrade version is active.
4. Compatibility check: for every action, v2 declares the same `RequestInput` keys as v1, except the agreed removals (`selectedCcUsers` on the two return routes and on completion). A test enforces this list.

### Decision D1 (approved 2026-10-18): default active version for local `init-db`

- **Approved: v1 stays active by default.** `init-db` behaves as today, so the 10 samples, the demo and the existing tests stay valid. In production, v2 is activated by the activation DML. Locally, it is activated by a new optional argument `init-db --portfolio-trade-version 2`.

## 5. SQL deliverables (`sql/upgrades/`)

| File | Type | Content |
|---|---|---|
| `20261019_01_cdt_v2_schema_ddl.sql` | DDL | Adds the 6 columns. Runs in one transaction, can run again, checks the column shape. Matches the EF migration `AddCdtV2SlaReminderControls`. |
| `20261019_02_portfolio_trade_v2_create_inactive_dml.sql` | DML | Inserts all v2 rows with **`IsActive = 0` on the version** (R1), in one transaction. Stops without changes if v2 already exists. v1 stays active. |
| `20261019_03_recipient_group_membership_dml_template.sql` | DML template | Parameters: `@VersionNumber`, group (`ICG_RA`, `RISK`, `DESK_TAXABLE`, `DESK_TAX_EXEMPT`, `DESK_EQUITY`), address, add or deactivate. Applies to every route of that group in that version, inside the group's SortOrder block. Add reactivates an inactive row with the same address in that block, or else uses the next free SortOrder. Repeated add and repeated deactivate change nothing. A full block stops the script with an error. One transaction. |
| `20261019_04_portfolio_trade_v2_verify.sql` | Read-only | Lists versions, v2 transitions, rules by route and block, and policies. **Fails** if any v2 own-rules route has no active row for a required desk, if a known placeholder address (`@example.test`) remains, or if any rule row is missing compared with the expected count. |
| `20261019_05_portfolio_trade_v2_activate_dml.sql` | DML | Runs the same checks as script 04 first. Then sets v1 inactive and v2 active in one transaction. Stops if a check fails. |
| `20261019_06_portfolio_trade_v1_reactivate_dml.sql` | DML | Metadata rollback **for new instances only** (R2): v2 inactive, v1 active. Existing v2 instances continue on v2 and need the v2-capable engine. |

A SQL-backed test applies script 02 to a database seeded with v1 and compares the result row by row with `SeedPortfolioTradeVersion2`.

## 6. Tests

New tests (SQL-backed, `FixedTimeProvider`):

1. **E1:**
   - The reminder audience is saved at task creation and is filtered by desk.
   - A snapshot route leaves the columns NULL and the scan uses the task snapshot.
   - An own-rules route with no active rule for the desk rejects the action at creation, and nothing is saved.
   - A membership change after creation does not change the reminders of the pending task, and an outbox retry keeps its own snapshot (R4).
   - Supersede keeps the saved audience.
   - Deactivating the last rule never widens the list (R3). The legacy snapshot route is the control case.
2. **E2:**
   - Same-day at 10:00: Due Today at 14:00, then Overdue at 08:00.
   - Same-day at 15:00: no Due Today; Overdue the next day at 08:00.
   - A future date: Due Today at 14:00 on that date.
   - The four late-scan cases in section 2.2, with each scan repeated to prove there are no duplicates. Covers a DST transition and a weekend.
   - A target date change supersedes the schedule correctly.
3. **E3:**
   - Created Monday 10:30: Overdue Tuesday 09:00, then daily.
   - An older-engine compatibility case: the v2 BDRM policy passes the v1 validation rules because `DurationMinutes` is kept (R2).
4. **v2 end to end, for each desk:** the recipients of each email and each reminder match section 2.1.
5. **Reroute regression (the reported case):** return to the Maker, resubmit with a new desk and a new target date. The new desk task, its recipients, its saved reminder audience and its schedule use the new values. Run it on v1 and on v2.
6. **Seed:**
   - v1 is unchanged.
   - v2 activation leaves exactly one active version.
   - A rule removed from the seed is deactivated, not deleted.
   - The request-key compatibility list from section 4.
7. **Scripts:**
   - The DDL runs twice.
   - Create-inactive equals the seed and does not change the active version.
   - Membership: duplicate add, repeated deactivate, a person in both groups, a full block, add after deactivate.
   - Verify fails with placeholders, then passes after membership updates.
   - Activate refuses while checks fail.
   - Reactivating v1 changes new starts only; an existing v2 trade still reaches BDRM with its next-day 09:00 schedule (R2).
8. **v1 regression:** running v1 instances keep their behavior after the DDL, the inactive v2 creation and the activation.

Quality gates (`PROJECT.md` section 21): both solutions build with warnings as errors; full core and API test suites; `init-db` and `verify-seed`; all 10 samples; both return branches; `demo`; the typed-recipient sample gates; local SMTP tests.

## 7. Integration requirements for the target team

1. Stop sending `selectedCcUsers` on `CheckerReturnToMaker`, `TradingReturnToMaker` and `BdrmTradeFulfilled`. This includes requests that the target maps to `BdrmTradeFulfilled`. **Do this before v2 activation.** It is safe on v1, because those v1 rules are optional.
2. No other request changes. RM stays inside `selectedCcUsers`. `assignedRoleKey` and `slaTargetDate` are unchanged.
3. **Do not install an engine build older than this release** while any v2 workflow instance is not completed (R2).

## 8. Release sequence (R1, R2)

1. Target team: integration requirement 1.
2. Run the DDL script and check the column shape.
3. Deploy the v2-capable engine build to every host that runs the engine: the target API, the SLA runner and the outbox processor. v1 stays active, so behavior is unchanged.
4. Run the create-inactive DML. v2 exists but no new trade can use it.
5. Run the membership template with `@VersionNumber = 2` to install the real addresses for all groups and all three desks. Then deactivate the placeholder rows.
6. Run the verify script. It must pass: no placeholders, every desk covered on every own-rules route.
7. Run the activate DML (one transaction).
8. Run one test trade per desk. Check the task and outbox recipient JSON and the first reminder.

Do not run `init-db` or the seed against production.

**Rollback:** the v1 reactivate DML changes new starts only. Keep the v2-capable engine installed while v2 instances exist. A software downgrade needs a separate, tested migration plan. No active workflow closure is required.

## 9. Risks

- The `originalMaker` participant address becomes the only To on the BDRM overdue and the return emails. An empty or invalid address fails the whole email.
- **Implicit route kind (R3, option b):** if a person deletes all rule rows of an own-rules route in SQL, the route turns back into a snapshot route without any error. Mitigation: deactivate only, and the verify script reports v2 own-rules routes with no active row per desk.
- A desk user who is missing from one of the three desk routes gets partial emails. The membership template prevents this if it is always used.
- The 4 running v1 workflows keep the old recipients until they complete.
- D09 is only partly addressed. Seed updates of an existing version are still in place.

## 10. Approvals

| Item | Status |
|---|---|
| D1: v1 stays the default active version for local `init-db` | Approved 2026-10-18 |
| Additive SQL schema change, 6 columns (`PROJECT.md` section 22) | Approved 2026-10-18 |
| Start implementation | **Pending: the user says when to start** |

## 11. Implementation tasks (in order)

Each task ends with a build that has zero warnings and with the focused tests for that task. Task T9 runs the full quality gates.

| Task | Work | Main files |
|---|---|---|
| T1 Schema | Add the 6 columns to the entities and the model. Add the EF migration `AddCdtV2SlaReminderControls`. Write the matching manual DDL script `20261019_01`. Test that an upgrade keeps existing data. | `Persistence/Entities/SlaPolicy.cs`, `Persistence/Entities/WorkflowTaskSlaSchedule.cs`, `Persistence/WorkflowDbContext.cs`, `Migrations/`, `sql/upgrades/` |
| T2 E2 and E3 | Separate due time, skip of a missed due reminder, next-local-day first overdue. Policy validation. Copy the new values to the schedule. Due Today occurrence time at scan. | `Sla/SlaScheduleCalculator.cs`, `Sla/WorkflowSlaSynchronizationService.cs`, `Sla/RunSlaService.cs`, `Persistence/Verification/SeedVerificationService.cs` |
| T3 E1 | Own-rules route detection (any rule row). Resolve and save `DueRecipientsJson` and `OverdueRecipientsJson` at schedule creation. Reject the action on a resolution error. Copy on supersede. Use the saved audience at scan. New constructor overload that takes the resolver; the old one stays. Update DI and the convenience constructor in the execution service. | `Sla/WorkflowSlaSynchronizationService.cs`, `Sla/RunSlaService.cs`, `DependencyInjection/InfrastructureServiceCollectionExtensions.cs`, `WorkflowExecution/WorkflowExecutionService.cs` |
| T4 Seed | Add `PortfolioTradeSeedDataV2`. Make the upserts take a version and a seed set. Activate v1 only when no other version is active. Add `SeedPortfolioTradeVersion2(bool activate)`. Deactivate (never delete) removed rule rows. Verification covers both versions and exactly one active version. Request-key compatibility list. | `Persistence/Seed/PortfolioTradeSeedData.cs`, new `Persistence/Seed/PortfolioTradeSeedDataV2.cs`, `Persistence/Seed/WorkflowSeedService.cs`, `Persistence/Seed/WorkflowDatabaseInitializer.cs`, `Persistence/Verification/SeedVerificationService.cs` |
| T5 Console | Optional `init-db --portfolio-trade-version 2`. Plain `init-db` stays unchanged. | `Application/Commands/CommandArgumentParser.cs`, `Application/Commands/CommandRegistry.cs`, `Persistence/Seed/WorkflowDatabaseInitializer.cs` |
| T6 SQL scripts | Scripts 02 to 06 (create inactive, membership template, verify, activate, v1 reactivate). A test that compares script 02 with the seed. | `sql/upgrades/` |
| T7 Tests | All tests in section 6, including the reroute regression on v1 and v2. | `tests/WorkflowEngine.Tests/` (new test classes for E1, E2/E3, v2 end to end, seed v2, SQL scripts) |
| T8 Documentation | Release guide with the section 8 sequence, rollback and the target-team requirements. Update `PROJECT.md` sections 13 and 14 for the saved reminder audience and the new policy fields. Update `docs/OPEN-ISSUES.md` and `CODEX_HANDOFF.md`. | new `docs/CDT_V2_RELEASE.md`, `PROJECT.md`, `docs/OPEN-ISSUES.md`, `CODEX_HANDOFF.md` |
| T9 Quality gates | Section 6 quality gates. Seed verification, all 10 samples, both return branches, demo, API tests, local SMTP tests. Completion summary in the `AGENTS.md` format. No commit or push without the user's request. | - |
