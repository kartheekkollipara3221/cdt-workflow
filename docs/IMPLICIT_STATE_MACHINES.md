# Implicit state machines in CP Atlas (workflow engine + Atlas platform)

Inventory for the next agent. Made on 2026-11-09 from the code at commit `c729452`, branch `console-code-first-0518`.

- This file is an analysis only. No code was changed. No build or test was run for it.
- All line numbers are from commit `c729452`. Read the code again before you change it.
- Each finding has a label:
  - **[Code fact]**: read directly in the code.
  - **[Known: ID]**: already in `docs/OPEN-ISSUES.md`, `CODEX_HANDOFF.md` or a review. Do not report it as new.
  - **[Inference - verify]**: a conclusion from reading. No test reproduced it. Write a test first.

## 0. How to use this file

1. Read section 1 (rules). Some work items need an approval before code.
2. Read section 2 (summary table) to find the machine.
3. Read the machine in section 3 (engine) or section 4 (platform): states, transitions, code lines, holes, tests.
4. Read section 6 (candidate work items) and section 8 (questions). The user selects the work. Nothing in this file is approved work.

## 1. Scope, method and rules

### 1.1 What "implicit state machine" means here

A thing has a life cycle (states and permitted moves between them), but the code has no one place that declares the states and the moves. The states are string literals, boolean flags, nullable dates or combinations of them. The moves are `if` statements and `UPDATE` statements in more than one file.

### 1.2 What is not in this list

- The metadata-driven workflow state machine (`WorkflowStateDefinition`, `WorkflowActionDefinition`, `WorkflowTransitionDefinition`). It is explicit and it is the design of the engine (`PROJECT.md` section 6). This file lists only the life cycles **around** it.
- Target code (`target-implementation-read-only/`). It belongs to the target team. It was not scanned.
- Stand-in code that is not copied to the target (`atlas-platform/**/StandIn/**`, `WebUi/src/app/stand-in/**`), test code, and `tools/WorkflowEngine.Sqms.LiveAcceptance`.
- Section 7 lists the items that were checked and are not state machines.

### 1.3 Method

Full read of the engine runtime (`src/WorkflowEngine.Infrastructure`, `.Application`, `.Sqms`, `workflow-api/src`), the platform server (`atlas-platform/src`, without stand-in logic), the platform SQL (`atlas-platform/sql/release-p1`), the platform client (`atlas-platform/WebUi/src/app/platform`), the SQMS metadata script and the CDT v2 activation script. Searches for status literals, enums and flags in all source folders.

### 1.4 Rules that bind any implementation

- `AGENTS.md` and `PROJECT.md` govern. Read `PROJECT.md` before you plan engine work.
- **Engine work is paused** until the user answers Q40 and Q41 (`CODEX_HANDOFF.md`, section B). Ask before you start an engine item.
- Persisted status strings, table names, column names, result codes, workflow/state/action/task keys and script-log names are **stable identifiers**. Do not rename them. Do not change their meaning.
- SQL `CHECK` constraints and filtered indexes use the status strings (`WorkflowDbContext.cs:272, 312, 318, 356, 365, 454`). A C# constant must have the same text.
- No new packages. No state-machine library, no BPMN, no event sourcing, no rule language (`PROJECT.md` 16.2 and section 22).
- Changes must be small and surgical (`PROJECT.md` 16.3). The target has copies of engine runtime files ("13 runtime files", `CODEX_HANDOFF.md` section B). Each engine file that you change must be merged at the target again. Keep the number of changed files small.
- Platform: fix here, run the platform gates, rebuild the package. Never edit target files. Any change to `050` or `090` is stop-and-ask. A change to a `v1/...` contract is stop-and-ask.
- Settled decisions. Do not raise them again as defects:
  - D08 / O3: the literals `Draft` (first state) and `SaveDraft` (action) are accepted ("manageable").
  - O4: no parallel approvals. O5: timers never move engine state. O6: no condition expressions in the engine; the caller sends the action key.
  - D01: authentication is target-owned. D07: the caller keeps business object IDs unique.

## 2. Summary

"Writers" = the number of code locations that change the state.

### Engine (`src/`, `workflow-api/`)

| ID | Machine | State is in | States | Writers | Central definition today | Note |
|---|---|---|---|---|---|---|
| E01 | Workflow instance life cycle | `WorkflowInstance.Status`, `CurrentStateKey`, `Version`, `CompletedAtUtc` | (none), Active, Completed | 2 services | SQL CHECK only | Known SaveDraft race (High). Terminal move does not check tasks. |
| E02 | Task life cycle | `WorkflowTask.Status` | Pending, Completed | 1 service | SQL CHECK + filtered index | Small and stable |
| E03 | SLA schedule life cycle | `WorkflowTaskSlaSchedule.Status`, `ScheduleVersion` | Active, Superseded, Completed | 1 service, 4 callers | SQL CHECK + filtered index | Known F09. One unhandled path. |
| E04 | SLA reminder cursor | `NextReminderAtUtc` + derived kind | Waiting, Due, Exhausted, Blocked (all derived) | 2 classes | None | The kind is not stored. "Blocked" has no state. |
| E05 | Notification outbox delivery | `NotificationOutbox.Status` + 6 columns | Pending, Processing, Sent, Cancelled | 6 files | SQL CHECK only | **Implemented two times.** One possible race. |
| E06 | Idempotency record | `IdempotencyRecord` row | Unknown, Stored | 1 service | Unique index | Known L1, L4, F07 |
| E07 | Request path (action kind) | Not persisted: literals, nulls, flags | SLA-only, SaveDraft, state change, admin override, start with action | 4 files | None | D08 settled |
| E08 | Definition version activation and metadata `IsActive` | `WorkflowDefinitionVersion.IsActive` and `IsActive` on each metadata row | Inactive, Active | Seed service + SQL scripts | None in the database | Known D09 |
| E09 | Result code to `Success` to HTTP status | Not persisted: string result codes | 16 codes | 4 files | None | Contract. Known D02, D03 |
| E10 | SQMS scheduled life cycle (timers in C#) | SSQMS tables + `WorkflowAudit` rows + outbox keys | Opportunity and Bid states | 1 service | `SqmsKeys` + SQL metadata (two copies) | Coupled to metadata by literals |
| E11 | SQMS state projection | `SSQMS.*.WorkflowState`, `SSQMS.*.Status`, engine state | 3 copies of one state | 2 owners | None | Split ownership |
| E12 | Participant capture | `WorkflowParticipant` rows | NotCaptured, Captured | 1 service | Metadata flags | Small and stable |
| E13 | Derived actor access (read model) | Not persisted | CanAct, read-only + reason | 1 class | None | Read only |

### Platform (`atlas-platform/`)

| ID | Machine | State is in | States | Writers | Central definition today | Note |
|---|---|---|---|---|---|---|
| P01 | Record life cycle | `ID` (0 or more), `IsDeleted`, `RowVersion` | New, Active, Deleted | 1 service | None | No restore. Delete has no version token. |
| P02 | Save pipeline and result protocol | `SaveState` object; `ID = 0` + `Errors` | 12 ordered steps; Saved or Failed | 1 service + client | Step list in `SavePage` (already clear) | Error codes are literals |
| P03 | Field state | `Visible`, `Disabled`, `Required`, `HiddenByRule`, `DisabledByRule` | Per field | Server 4 classes, client 3 files | Partly (flags on `AtlasFormField`) | The client does not know the rule locks |
| P04 | Record workflow state (not connected) | `CPRule.States`, `CPAtlasPage.Step`, action `Execute` | Always empty | None | None | Hook for the engine adapter (module 10) |
| P05 | App availability | Registration + manifest + `Enabled` + start-up errors + loaded tables | 6 derived states | 7 read locations | None | The rule is not the same at each location |
| P06 | Platform start-up | `AtlasTableRegistry.IsLoaded` + gate | NotStarted, Starting, Loaded, Failed | 1 class | A boolean | Small |
| P07 | Access decision and rollout modes | `AccessMode`, `Enabled`, operation | Allow, LogAndAllow, Deny | 3 locations | Enums for the modes | The decision table is in 3 places |
| P08 | TTDV2 cut-over and rollback | Script log rows + soft-delete flags on roles | NotInstalled, Installed, CutOver, RolledBack | 3 SQL scripts | None | **Stop-and-ask.** Known R01, R02 |
| P09 | User role life cycle (data) | `IsDeleted` on 4 tables | Active, Revoked | Target data + P08 | View `vw_CP_Atlas_ActiveUserRole` | The platform only reads it |
| P10 | Form component (client) | 8 signals + route + form status | Loading, LoadFailed, Ready, Saving, dialogs | 1 component | None | Delete has no busy state |
| P11 | Dashboard, history dialog, navigation cache (client) | Signals + a cached promise | Loading, Failed, Ready | 3 files | None | Small |

## 3. Engine machines

Short paths used below:

- `WES` = `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs`
- `SWS` = `src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs`
- `SLASYNC` = `src/WorkflowEngine.Infrastructure/Sla/WorkflowSlaSynchronizationService.cs`
- `RUNSLA` = `src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs`
- `OUTBOX` = `src/WorkflowEngine.Infrastructure/Notifications/ProcessNotificationOutboxService.cs`
- `DISPATCH` = `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionNotificationDispatcher.cs`
- `APIACT` = `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs`
- `CTX` = `src/WorkflowEngine.Infrastructure/WorkflowContext/WorkflowContextReader.cs`
- `DBC` = `src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs`
- Tests are in `tests/WorkflowEngine.Tests/` and `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/`.

### E01 Workflow instance life cycle

**State store.** Table `WorkflowInstance`: `Status` (`'Active'`, `'Completed'`, CHECK at `DBC:272`), `CurrentStateKey`, `Version`, `CompletedAtUtc`. One instance for each business object: unique index at `DBC:271`.

| From | To | Trigger | Guard | Code |
|---|---|---|---|---|
| (none) | Active, state `Draft`, `Version` 1 | Start | One active definition version. No instance for this business object. | `SWS:65-142`, insert `SWS:387-421` (literals `SWS:400-401, 416-417`) |
| (none) | Result `AlreadyExists` (no change) | Start for an existing instance | - | `SWS:86-96`; race on the unique index `SWS:124-141, 234-251` |
| Active | Active, new `CurrentStateKey`, `Version + 1` | Action with a transition row; destination is not terminal | Role match, concurrency guards, `WHERE Version = @v` | `WES:678-700` |
| Active | Completed, `CompletedAtUtc = now` | Action whose destination state has `IsTerminal = 1` | Same | `WES:678-685` |
| Active | Active, no change of state or `Version` | Action key `SaveDraft` | Literal compare | `WES:675-676, 839-848` |
| Completed | (no move) result `InvalidTransition` | Any action | - | `WES:97-103`; no available actions `CTX:108-111` |
| Any | Deleted, then Active again | Start with `Reset` (console only; the API and SQMS send `Reset: false`) | - | `SWS:73-84, 144-201`, delete `SWS:356-385` |

**Why implicit.** The values are literals in two services and one reader. `Status` is set from the `IsTerminal` flag of the destination state when the move runs. No table says "Active to Completed is the only move". The first state is a literal.

**Holes and risks.**

- **[Known: SaveDraft race, High, open]** `SaveDraft` does not run the version-guarded `UPDATE` (`WES:675-676`). A SaveDraft can commit section data, audit and an idempotency record after a concurrent transition moved the instance. Recommended first engine fix in `CODEX_HANDOFF.md` (Q41).
- **[Known: D08, settled]** `Draft` and `SaveDraft` are literals. Start does not read a "first state" from metadata.
- **[Code fact]** Start does not check that the version has a state with the key `Draft`. An instance of such a version has no transition.
- **[Code fact]** A terminal move does not check that no `Pending` task stays. `PROJECT.md` section 15 says "ensure no pending task remains". The code closes a task only when the transition row has `TaskToCloseKey` (`WES:702-724`). The current PortfolioTrade and SQMS metadata do not cause this. A new workflow with wrong metadata gives `Completed` + a `Pending` task + an `Active` SLA schedule that no scan reads (see E03, E04).
- **[Code fact]** Two sources for "is this instance finished": `Status` and `IsTerminal` of `CurrentStateKey`. All guards read `Status`. A metadata edit of `IsTerminal` in place (see D09) makes them different for old instances.
- **[Code fact]** `WorkflowDefinition.IsActive` is not read at Start. `SWS:441-452` reads only the version flag. Only `QueueWorkflowNotificationService.cs:36` reads the definition flag.

**Tests.** `WorkflowTransitionTests`, `WorkflowTerminalStateTests` (a workflow with no tasks), `StartWorkflowServiceTests`, `WorkflowStartInitialActionTests`, `WorkflowExecutionValidationTests`, `SchemaConstraintTests`.

### E02 Task life cycle

**State store.** Table `WorkflowTask`: `Status` (`'Pending'`, `'Completed'`, CHECK at `DBC:318`), `CompletedAtUtc`, `CompletedByUserId`, `ClosedByActionKey`, `DueAtUtc`. At most one `Pending` task for each instance: filtered unique index `DBC:310-312`.

| From | To | Trigger | Guard | Code |
|---|---|---|---|---|
| (none) | Pending | Transition with `TaskToCreateKey` | Role = task owner role, or the request `assignedRoleKey` for a task with dynamic assignment (`WES:909-922`) | `WES:726-766` (literal `WES:742`) |
| Pending | Completed | Transition with `TaskToCloseKey` equal to the pending task key | `CheckTransitionStillCurrent`, `WES:1061-1072` | `WES:702-724` (literals `WES:707-708`) |
| Any | Deleted | Start with `Reset` | - | `SWS:379-381` |

There is no `Cancelled` state (spec).

**Why implicit.** The relation "state X has pending task Y" exists only in the transition rows (`TaskToCloseKey`, `TaskToCreateKey`). No code checks it as a whole. A workflow with no tasks is valid (SQMS: `sql/sqms/002_sqms_workflow_metadata_dml.sql:412-413, 431`).

**Holes and risks.**

- **[Code fact]** A metadata error (the transition names task X, the pending task is Y) gives `ConcurrencyConflict` with guard `expectedCurrentTaskKey` (`WES:1061-1072`). The caller sees a concurrency problem, not a metadata problem.
- **[Code fact]** Three read styles for "the pending task": `OrderBy(Id).FirstOrDefault` (`WES:144-147, 647-650`), `SingleOrDefault` (`CTX:30-34`), `Single` (`SLASYNC:171-178`). All depend on the filtered index.

**Tests.** `WorkflowTransitionTests`, `WorkflowContextReaderTests`, `SchemaConstraintTests`.

### E03 SLA schedule life cycle

**State store.** Table `WorkflowTaskSlaSchedule`: `Status` (`'Active'`, `'Superseded'`, `'Completed'`, CHECK at `DBC:363-365`), `ScheduleVersion`, `SupersededAtUtc`, `CompletedAtUtc`, `RowVersion`. One `Active` schedule for each task: filtered unique index `DBC:354-356`. Unique `(WorkflowTaskId, ScheduleVersion)`: `DBC:353`.

| From | To | Trigger | Guard | Code |
|---|---|---|---|---|
| (none) | Active, version 1 (or highest + 1) | A task with an SLA policy is created; or a synchronization finds no active schedule | Policy is valid (`SLASYNC:245-292`). Target date is given for `ApplicationTargetDate` (`SLASYNC:48-53`). The reminder audience resolves (`SLASYNC:81-97`). | `SLASYNC:69-137` |
| Active | Active (no change) | Synchronization in duration mode, or with the same target date | - | `SLASYNC:61-67` |
| Active | Superseded, and a new Active with version + 1 | Synchronization with a different target date | - | `SLASYNC:99-137`. It cancels the undelivered outbox rows of the old schedule (`SLASYNC:104, 219-231`, reason `SlaScheduleSuperseded`). |
| Active | Completed, `NextReminderAtUtc = null` | A transition closes the task | - | `SLASYNC:142-169`, called from `WES:712-716`. It cancels the undelivered outbox rows (reason `WorkflowTaskCompleted`). |
| Any | Deleted | Start with `Reset` | - | `SWS:374-378` |

**Callers of `Synchronize`.** `WES:751-757` (task created), `WES:772-778` (transition with no task change), `WES:841-847` (SaveDraft), `APIACT:84-90` (API request with `slaTargetDate` and no action key).

**Rules that hold.** `WorkflowTask.DueAtUtc` = `OverdueAtUtc` of the active schedule (`SLASYNC:134`). The reminder audience is fixed when the first schedule is made (`SLASYNC:77-97`).

**Holes and risks.**

- **[Known: F09, open]** An SLA synchronization and a task completion can run at the same time. The synchronization reads the pending task with no lock (`SLASYNC:171-178`) and can make an `Active` schedule for a task that the other transaction completes.
- **[Inference - verify]** SLA-only request for an instance with no `Pending` task (state `Draft`, a completed instance, an unknown instance ID): `ResolvePendingTask` uses `.Single()` (`SLASYNC:176`). It throws `InvalidOperationException`. `APIACT:92-98` catches only `WorkflowSlaValidationException`. The caller gets HTTP 500 with a generic problem body, not a structured result.
- **[Code fact]** No code closes an `Active` schedule except `Complete`. If a task stays `Pending` on a completed instance (E01), its schedule stays `Active`.

**Tests.** `WorkflowSlaSynchronizationTests`, `CdtV2WorkflowTests`, `SchemaConstraintTests`; API: `Services/WorkflowActionServiceTests` (result `SlaSynchronized`).

### E04 SLA reminder cursor (inside an Active schedule)

**State store.** `WorkflowTaskSlaSchedule.NextReminderAtUtc` and the snapshot columns of the policy. The reminder kind (`DueToday` or `Overdue`) is **not** a column of the schedule. Each occurrence that was queued is an outbox row (`SlaOccurrenceAtUtc`, `SlaOccurrenceDate`, `SlaReminderKind`).

**Derived states.**

| State | Condition |
|---|---|
| Waiting | `NextReminderAtUtc > now` |
| Due | `NextReminderAtUtc <= now`, task `Pending`, instance `Active` |
| Exhausted | `NextReminderAtUtc IS NULL` and `Status` is still `Active` (a policy with no recurrence after its one reminder) |
| Blocked | Due, but the scan cannot queue the occurrence. The scan rolls back, adds a warning and does not move the cursor. The next scan selects the same schedule again. |

| Step | Code |
|---|---|
| Select candidates: schedule `Active`, cursor not null and `<= now`, task `Pending`, instance `Active`; pages by `(NextReminderAtUtc, Id)` | `RUNSLA:49-78` |
| Check each candidate again in its own transaction | `RUNSLA:129-147` |
| The occurrence is eligible later than `now`: cursor = that time | `RUNSLA:149-156` |
| The occurrence is in the outbox already: cursor = next, no insert | `RUNSLA:158-170` |
| Queue the occurrence (outbox `Pending`) and set cursor = next | `RUNSLA:242-265` |
| Kind rule. Duration mode: always `Overdue`. Application-date mode: `Overdue` when the local date is on or after target date + offset, else `DueToday`. | `RUNSLA:306-375` (rule at `RUNSLA:366`) |
| Next cursor: + minutes; or `null`; or the next local day at the reminder time; after `DueToday` it is `OverdueAtUtc` | `RUNSLA:377-422` |
| First cursor and kind when the schedule is made | `Sla/SlaScheduleCalculator.cs:5-113`, `SLASYNC:180-217` |

**Why implicit.** `CalculatedSlaSchedule.NextReminderKind` is calculated when the schedule is made, but no column keeps it. The scan derives the kind again with a different expression. The four states of the table above have no name in the code. `Blocked` is visible only as a warning text in the run result.

**Rules that hold.** One occurrence for each `(schedule, instant, kind)`: filtered unique index `DBC:426-433`, a check before the insert (`RUNSLA:158-162`) and a catch of the unique violation (`RUNSLA:288-294`). No backfill of missed slots (`RUNSLA:528-537`).

**Holes and risks.**

- **[Code fact]** Causes of `Blocked`: recipient snapshot missing or not valid (`RUNSLA:178-183`), route key missing (`RUNSLA:188-193`), task context JSON not valid (`RUNSLA:205-217`), primary e-mail missing or not unique (`RUNSLA:219-231`), subject longer than 300 (`RUNSLA:235-240`), calendar data error (`RUNSLA:280-287`). This is the specified behaviour ("warned and skipped"), but the state is not stored and it does not end.
- **[Code fact]** Rule between machines that no code checks before the scan: an SLA reminder copies the subject and the body from the "primary" outbox row of the task (`WorkflowTaskId = task AND SlaReminderKind IS NULL`, `RUNSLA:219-231`). So the transition that creates a task with an SLA policy must have a notification route. If it has none, the schedule is `Blocked` at each scan.
- The display status in the workflow context (`OnTrack`, `Overdue`) is a third derivation: `CTX:165-201` compares `WorkflowTask.DueAtUtc` with the clock. This is specified (`PROJECT.md` section 13).

**Tests.** `Phase5SlaTests`, `RecurringSlaMonitorTests`, `SlaScheduleCalculatorTests`, `CdtV2SlaCalculatorTests`, `SlaProcessingOptionsTests`, `WorkflowContextReaderTests`. Doc: `docs/SLA_BATCH_PROCESSING.md`.

### E05 Notification outbox delivery

**State store.** Table `NotificationOutbox`: `Status` (`'Pending'`, `'Processing'`, `'Sent'`, `'Cancelled'`, CHECK at `DBC:452-454`), `ProcessingToken`, `ProcessingStartedAtUtc`, `RetryCount`, `NextAttemptAtUtc`, `LastError`, `SentAtUtc`, `CancelledAtUtc`, `CancellationReason`.

**States.** Pending-ready (`NextAttemptAtUtc` null or in the past), Pending-deferred (`NextAttemptAtUtc` in the future), Processing with a fresh lease, Processing with a stale lease (15 minutes, constant at `OUTBOX:15`), Sent (terminal), Cancelled (terminal).

| From | To | Trigger | Code |
|---|---|---|---|
| (none) | Pending | A transition with a notification route | `WES:815-831` (literal `WES:827`) |
| (none) | Pending | The SLA scan queues an occurrence | `RUNSLA:242-262` (literal `RUNSLA:258`) |
| (none) | Pending | Notification with no workflow instance (SQMS open and reminder mails), key `NotificationOccurrenceKey` | `Notifications/QueueWorkflowNotificationService.cs:74-90` (literal `:86`) |
| Pending-ready, or Processing-stale | Processing (new token, `LastError` cleared) | Worker claim, one atomic `UPDATE` | `OUTBOX:81-92` |
| Pending-ready | Processing | API in-request dispatcher claim (rows of this instance and correlation ID only) | `DISPATCH:60-67` |
| Processing | Sent | Send succeeded and the token is still ours | `OUTBOX:141-149`; `DISPATCH:84-92` |
| Processing | Pending (`RetryCount + 1`, `LastError`, `NextAttemptAtUtc` from the backoff) | Send failed | `OUTBOX:251-278`; `DISPATCH:159-182`; backoff `Notifications/OutboxProcessingOptions.cs:26-33` |
| Processing | Cancelled (`SlaScheduleInactive`) | After the claim, the worker finds that the SLA schedule of the row is not `Active` | `OUTBOX:104-136` |
| Pending or Processing | Cancelled (`SlaScheduleSuperseded` or `WorkflowTaskCompleted`) | The SLA schedule is superseded or completed | `SLASYNC:219-231` |
| Any | Deleted | Start with `Reset` | `SWS:364-366` |

**Why implicit.** Six files write the status: three insert rows (`WES`, `RUNSLA`, `QueueWorkflowNotificationService.cs`) and three change them (`OUTBOX`, `DISPATCH`, `SLASYNC`). The delivery part (claim, send, mark, release) is written **two times**: once in the worker (`OUTBOX`) and once in the API dispatcher (`DISPATCH`). No type declares the states or the permitted moves.

**Differences between the two implementations. [Code fact]**

| Topic | Worker (`OUTBOX`) | API dispatcher (`DISPATCH`) |
|---|---|---|
| Claims a stale `Processing` row | Yes (`:85-87`) | No, only `Pending` (`:61-62`) |
| `NextAttemptAtUtc` after a failure with zero delay and a batch size | The failure time (`:265-268`) | `null` (`:172, 179`). Known low residual of H01. |
| Check that the SLA schedule is `Active` after the claim | Yes (`:104-136`) | No (its rows are transition rows) |
| Pending row deferred to a later retry | Not selected | Returns a failure text (`:30-35`) |
| Row to delivery object (`ToDelivery`) | `:202-249` | `:113-157` (a copy) |
| Call style | `async` | Blocks: `.GetAwaiter().GetResult()` (`:79-81`). Known D02, D06. |

**Holes and risks.**

- **[Known: D04, owner decision open]** No maximum number of attempts and no dead-letter state. A row that always fails stays `Pending` and `RetryCount` grows.
- **[Known: by design]** Delivery is "at least once". After a send, the "mark Sent" update can match zero rows (`OUTBOX:151-155`); a stale lease can be claimed again.
- **[Inference - verify]** Cancel during send. The worker has an SLA row in `Processing` and is in `SendAsync`. A transition closes the task. `SLASYNC:219-231` sets the row to `Cancelled` and clears the token. The send succeeds. The "mark Sent" update needs `Status = 'Processing'` and the token (`OUTBOX:142`), so it matches zero rows. Result: the e-mail was sent, the row says `Cancelled`, and the run has only a warning.
- **[Code fact]** `PROJECT.md` section 14 says the command stops at the first failure. The code goes on to the next rows (`OUTBOX:168-187`). `docs/OUTBOX_RETRY_ROLLOUT.md` describes the code. The requirement text is older than the code.

**Tests.** `Phase5OutboxTests` (order, claim, lease, retry, cancel for an inactive schedule), `OutboxProcessingOptionsTests`, `NotificationOnlyQueueTests`; API: `Services/WorkflowActionNotificationDispatcherTests`.

### E06 Idempotency record

**State store.** Table `IdempotencyRecord`: unique `IdempotencyKey` (`DBC:468`), `RequestHash`, `ResultJson`.

| State of a key | Request | Result | Code |
|---|---|---|---|
| Unknown | Any | Run the action. On success, insert the record in the same transaction. | `WES:885-891, 924-942` |
| Stored | Same request hash | Return the stored result with `Replay = true`. No other check runs first. | `WES:61-65, 952-979` |
| Stored | Different request hash | `IdempotencyConflict` | `WES:962-969` |
| Unknown (two requests at the same time) | Same key | The second insert breaks the unique index; the code reads the record and returns the replay | `WES:892-903`, also after a concurrency failure `WES:651-657, 698` |

**Why implicit.** The protocol is four checks at four places in one long method. The key source is different for each caller: the API makes a new GUID for each request (`APIACT:42-43`), SQMS builds a fixed key from the entity, the version and the action (`src/WorkflowEngine.Sqms/SqmsWorkflowService.cs:99, 133, 264-266`), Start uses the request key or a new GUID (`SWS:259-260`).

**Holes and risks.**

- **[Code fact]** Only a successful run is stored. A failed request with the same key runs again.
- **[Known: L1, open]** An HTTP caller cannot send its own key, so a retry after a timeout is a new action.
- **[Known: L4, open]** The unique-violation check reads the SQL message text (`WES:944-950`; the same pattern at `SWS:514-520`, `RUNSLA:539-543`, `QueueWorkflowNotificationService.cs:134-142`).
- **[Known: F07, open]** The assignment role is not in the request hash.
- **[Known: deferred]** No retention or archive rule for the records.

**Tests.** `ExecuteJsonCommandTests`, `WorkflowActionResultHashTests`, `WorkflowExecutionValidationTests`, `SqmsWorkflowServiceTests`.

### E07 Request path (action kind)

Not a stored state. Each request takes one path. Literals, null values and flags select the path.

| Path | Selected by | Code |
|---|---|---|
| SLA-only synchronization (no state change, no audit, no version change) | The action key is null and `slaTargetDate` is given (API only) | `APIACT:34-38, 79-119` |
| SaveDraft (no state change) | The action key equals the literal `SaveDraft` | `WES:675, 839-848` |
| State-changing action | Any other action key | `WES:676-838` |
| Admin override | Console flag; role `AdminDemoOverride`, reason prefix `[ADMIN OVERRIDE]` | `WES:171-200, 851-856` |
| Start with a first action, all in one transaction | `InitialActionKey` is not null | `SWS:98-104, 203-252` |

Caller profile flags on `WorkflowExecutionRequest` (`src/WorkflowEngine.Application/WorkflowExecution/IWorkflowExecutionService.cs:11-23`): the console uses the defaults (`ValidateActionEcho` true, `PersistSectionData` true, `DeriveIdentityFromPersistence` false). The API (`APIACT:121-130`), SQMS (`SqmsWorkflowService.cs:296-305`) and Start (`SWS:306-315`) set the opposite values.

**[Known: D08, settled]** PortfolioTrade names are in the shared validator of the console path: `src/WorkflowEngine.Application/Validation/WorkflowActionEnvelopeValidator.cs:120-147` (`MakerPrimary`, `SaveDraft`, `SubmitForChecker`, `ResubmitForChecker`). `PROJECT.md` section 7 specifies this rule.

**Tests.** `WorkflowActionEnvelopeValidationTests`, `ExecuteJsonCommandTests`, `WorkflowOptionsConflictTests`; API: `Services/WorkflowActionServiceTests`.

### E08 Definition version activation and metadata `IsActive`

**State store.** `WorkflowDefinitionVersion.IsActive`, `WorkflowDefinition.IsActive`, and `IsActive` on each metadata row (state, action, role, task, transition, SLA policy, route, recipient rule, participant definition, task assignment role).

**Rule.** One active version for each workflow key. A running instance keeps its own version (`WorkflowInstance.WorkflowDefinitionVersionId`). Activation changes only new starts.

| Move | Code |
|---|---|
| Version 1 becomes active if no other version is active | `Persistence/Seed/WorkflowSeedService.cs:13-17` |
| A version becomes the only active version (one transaction) | `WorkflowSeedService.cs:97-109`; `Persistence/Seed/WorkflowDatabaseInitializer.cs:18-33` |
| Target database: activate v2, or v1 again | `sql/upgrades/20261019_05_portfolio_trade_v2_activate_dml.sql:85-90` (error 52502 if the count is not 1), `20261019_06_portfolio_trade_v1_reactivate_dml.sql` |
| SQMS: version 1 is inserted as active | `sql/sqms/002_sqms_workflow_metadata_dml.sql:60-69` |
| A metadata row is set inactive, never deleted | `WorkflowSeedService.cs:316-330`; SQMS script `:445-469` |

**Readers.** Start: `SWS:441-452` (`SingleOrDefault` over the active versions; zero gives an exception, two give an exception). Notification with no instance: definition active and version active (`QueueWorkflowNotificationService.cs:29-42`). Action execution reads no version flag.

**Holes and risks.**

- **[Code fact]** The database does not enforce "one active version". There is no filtered unique index. The scripts and the seed service keep the rule; `Persistence/Verification/SeedVerificationService.cs:47` checks it for PortfolioTrade only.
- **[Code fact]** The `IsActive` filter is not the same in all readers. Example: the task definition for a new task is read with no `IsActive` filter (`WES:728-732`), but the SLA synchronization of that task needs an active task definition (`SLASYNC:32-35`, `.Single`).
- **[Code fact]** `src/WorkflowEngine.Sqms/Scheduling/SqmsScheduledService.cs:246` sends the version number `1` as a literal. If SQMS gets an active version 2, the open and reminder mails get `NotFound`.
- **[Known: D09, Major, open]** The seed changes version 1 in place and has no publication procedure.

**Tests.** `CdtV2SeedTests`, `CdtV2SqlScriptTests`, `SeedDataSmokeTests`, `MigrationSchemaTests`, `CdtSqmsCompatibilityTests`.

### E09 Result code to `Success` to HTTP status

**State store.** None. A result is a string code and a boolean.

- Codes of the engine (`src/WorkflowEngine.Application/Results/WorkflowActionResultBuilder.cs`): `AcceptedForPhase3Validation` (:13), `ValidationFailed` (:27, :236), `WorkflowInstanceNotFound` (:50), `WorkflowDefinitionVersionMismatch` (:65), `InvalidTransition` (:80), `Forbidden` (:95), `AmbiguousRoleMatch` (:110), `ConcurrencyConflict` (:127), `IdempotencyConflict` (:145), `AlreadyExists` (:173), `Success` (:207, :264, :300), `SlaSynchronized` (:278).
- Code of the API only: `NotificationDeliveryFailed` (`APIACT:166-173`; `workflow-api/.../Services/WorkflowStartService.cs:94-101`).
- Codes of the SQMS layer: `TargetNotFound`, `NotWorkflowManaged`, `ValidationFailed` (`SqmsWorkflowService.cs:483-484, 628-637`).
- Codes of the notification queue: `Success`, `NotFound`, `ValidationFailed` (`QueueWorkflowNotificationService.cs:45-47, 56, 71, 107`).

**Mapping.** `Success = false` gives HTTP 500, else 200 (`Controllers/WorkflowActionController.cs:24-30`, `Controllers/WorkflowStartController.cs:24-30`). Exceptions: `Infrastructure/GlobalExceptionHandler.cs:47-76`.

**Two-phase result. [Known: D02 / F06, open]** The action commits first. The e-mail dispatch runs after the commit. If the dispatch fails, the API changes the result of the committed action to `Success = false` and `NotificationDeliveryFailed` (`APIACT:139-173`). The caller gets HTTP 500, but the workflow state moved. The stored idempotency result still says `Success`.

**[Known: D03, understood]** Business failures return HTTP 500. A change of the status semantics needs a new versioned endpoint.

**Limit.** The result codes and the HTTP behaviour are the public contract. Constants are possible. A change of a value or of the mapping is stop-and-ask.

**Tests.** `WorkflowActionResultBuilderTests`; API: `Controllers/*Tests`, `Models/WorkflowRequestContractTests`, `Models/WorkflowScenarioSampleTests`.

### E10 SQMS scheduled life cycle (timer rules in C#)

The SQMS states and transitions are metadata (`sql/sqms/002_sqms_workflow_metadata_dml.sql`: states `:79-97`, transitions `:388-408`). `src/WorkflowEngine.Sqms/SqmsKeys.cs:10-60` has the same keys as C# constants. The **timer rules** are only in C#: `src/WorkflowEngine.Sqms/Scheduling/SqmsScheduledService.cs`. The service acts as a caller with the role `System` (this agrees with O5: the engine itself has no timers).

| Entity | From | To | Rule | Code |
|---|---|---|---|---|
| Opportunity | `OPEN TO BIDDING` | `CLOSED TO BIDDING` (action `CloseOpportunityBidding`) | Local date is on or after the open date + `BiddingDays` + 1 | `:41-76` |
| Bid | `SUBMITTED` | `NOT ALLOCATED` (action `MarkBidNotAllocated`) | The opportunity is `CLOSED TO BIDDING`; the sum of the FairValue and Premium bids is more than the sale amount; this bid is a Discount bid | `:78-154` (rule `:107-120`) |
| Bid | `SUBMITTED` | `IPG REVIEW` (action `MoveBidToIpgReview`) | The opportunity is `CLOSED TO BIDDING`; all other bids | `:78-154` |
| Mail occurrence | (none) | Queued `OpportunityOpen` | Local date is on or after the open date + 1; one for each year, quarter and date | `:156-195, 208-289` |
| Mail occurrence | (none) | Queued `OpportunityReminder` | Local date is on or after the open date + `BiddingDays` - 1 | `:220` |

**Where the timer state is.**

- "Open date" = `CreatedAtUtc` of the `WorkflowAudit` row with the action `OpenOpportunityForBidding` (`src/WorkflowEngine.Sqms/Persistence/SqlSqmsDataStore.cs:56-94`).
- "This bid is classified" = a `WorkflowAudit` row from `SUBMITTED` with one of the two classification actions (`SqlSqmsDataStore.cs:141-175`).
- "This mail was queued" = the unique `NotificationOccurrenceKey` `SQMS:<kind>:<year>:Q<n>:<date>` (`SqmsScheduledService.cs:239-241`; index `DBC:434-437`).
- Settings: `Sqms:Enabled`, `BiddingDays` (15), `SellerResponseDays` (5), `TimeZoneId` (`src/WorkflowEngine.Sqms/Configuration/SqmsOptions.cs`).

**Why implicit.** The audit table is used as a timer store. The rules join C# literals with metadata rows. No one place lists "what the scheduler does to which state".

**Holes and risks.**

- **[Code fact]** The candidates come from the projection (`SSQMS.Opportunity.WorkflowState`, `SSQMS.Bid.WorkflowState`, `SqlSqmsDataStore.cs:71, 102, 109, 130`), not from the engine tables. The engine then checks its own state. If the projection is behind, the run result has `InvalidTransition` failures (see E11).
- **[Inference - verify]** The open-date query joins all audit rows of the action `OpenOpportunityForBidding` (`SqlSqmsDataStore.cs:66-70`). The current transitions permit this action one time for each instance. If the metadata later permits a second time, the query returns the opportunity two times.
- **[Code fact]** Literal version `1` (see E08).

**Tests.** `SqmsScheduledServiceTests`, `SqmsEndToEndTests`, `SqmsDataStoreTests`, `SqmsWorkflowServiceTests`, `SqmsCommandTests`; `sql/sqms/testing/*`.

### E11 SQMS state projection

One state has three copies: `WorkflowInstance.CurrentStateKey` (the authority), `SSQMS.<table>.WorkflowState` and `SSQMS.<table>.Status`.

| Who moves the state | Who writes the two SSQMS columns | Code |
|---|---|---|
| A user action (`TargetProjectionMode.None`) | The target application, outside this repository | `SqmsWorkflowService.cs:172-204` |
| The scheduler (`TargetProjectionMode.Scheduled`) | This module, in the same transaction; both columns get the same value; exactly one row must change | `SqmsWorkflowService.cs:206-238, 514-535`; `SqlSqmsDataStore.cs:199-257` |

The mode is a private enum (`SqmsWorkflowService.cs:558-562`). The transaction commits only when the result is a success (`:564-599`).

**Why implicit.** The owner of the projection depends on a private flag for each call. No check compares the three copies.

**Tests.** `SqmsWorkflowServiceTests`, `SqmsEndToEndTests`, `sql/sqms/testing/008_verify_sqms_projection_ownership_live_acceptance.sql`.

### E12 Participant capture

`src/WorkflowEngine.Infrastructure/WorkflowStart/WorkflowParticipantCaptureService.cs`. A participant key is captured one time, at Start (`CaptureStage = 'Start'`, `:17-23`). A second capture is an error (`:34-43`). There is no update and no delete; `Reset` removes the rows with the instance (cascade, `DBC:286-290`). CHECK constraints for `Cardinality` and `CaptureStage`: `DBC:147-152`. Small and stable. Tests: `WorkflowParticipantCaptureTests`.

### E13 Derived actor access (read model)

`CTX:86-93, 102-163, 203-225`. `CanAct` = at least one available action. Available actions = active transitions from the current state for the roles of the actor, joined with active actions; none for a completed instance. `ReadOnlyReason` is a chain of four `if` statements. Read only; no writer. Tests: `WorkflowContextReaderTests`, `ShowContextCommandTests`.

## 4. Platform machines

Short paths used below:

- `CSS` = `atlas-platform/src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs`
- `PLAT` = `atlas-platform/src/BBH.CP.Atlas.Service/Platform`
- `APP` = `atlas-platform/src/BBH.CP.Atlas.App/Platform`
- `DATA` = `atlas-platform/src/BBH.CP.Atlas.Data/Platform`
- `SQLP` = `atlas-platform/sql/release-p1`
- `WEB` = `atlas-platform/WebUi/src/app/platform`
- Tests are in `atlas-platform/tests/BBH.CP.Atlas.Platform.Tests/` and `WEB/*.spec.ts`.

### P01 Record life cycle

**State store.** The main table of the app (pilot: `TTDV2.General`): key `ID`, `IsDeleted` (bit), `RowVersion`, system columns (`CSS:62-65`).

| From | To | Trigger | Guard | Code |
|---|---|---|---|---|
| New (`ID = 0`) | Active | Save with `ID = 0` | `Create` permission. Never "log only" (`CSS:344-346`). | `CSS:329-350, 469-474, 677-678` |
| Active | Active (new `RowVersion`) | Save with `ID > 0` | `Edit` permission; the `Version` of the load is sent and is equal (`CSS:416-428`); the `UPDATE` uses the client version (`CSS:498-506`) | `CSS:263-294, 670-712` |
| Active | Deleted (`IsDeleted = 1`) | `RemovePage` | `Delete` permission. Never "log only". | `CSS:725-750` |
| Deleted | (no move) | - | A deleted record is "not found" for load, save, delete and history | `DATA/AtlasDataExtensions.cs:82-97`; dashboard filter `PLAT/Dashboard/AtlasTabQueryBuilder.cs:42-47` |

Audit events: `Create`, `Update`, `Delete` (`PLAT/Audit/AtlasAuditWriter.cs:11-16`), written in the transaction of the save. An `Update` with no changed field writes no audit entry (`:34-35`).

**Why implicit.** "New" is `ID == 0`. "Deleted" is a bit column that each reader must filter. No type names the three states.

**Holes and risks.**

- **[Code fact]** No restore path. `IsDeleted` is set to true only at `CSS:739` and to false only at `CSS:473`.
- **[Code fact]** Delete sends no version token. `RemovePage(int id)` has no `Version` (`CSS:725`; client `WEB/atlas-api.service.ts:44-47`). A user can delete a record that another user changed after the first user opened it.
- **[Inference - verify]** A save with no changed field still writes `ModifiedBy` and `ModifiedDate` (`CSS:476-477`), so the row version changes and a second user gets `Conflict`.

**Tests.** `Sections/Ttdv2SectionTests` (`Two_users_save_the_same_record_and_the_second_gets_a_conflict`, `Update_and_delete_write_one_audit_entry_each_and_no_change_writes_none`, `An_update_with_no_Version_is_refused`), `Data/AtlasPlatformContextTests` (`A_soft_deleted_record_is_not_found`).

### P02 Save pipeline and result protocol

The steps are already an ordered list with one state object (`SaveState`, `CSS:297-326`): `LoadStored`, `ApplyRules`, `ReadAndFilter`, `ValidateFields`, `CheckVersion`, `MapRecord`, stop on errors, `ComputeAndSystemFields`, `RunBeforeSave`, stop on errors, `Persist`, `Complete` (`CSS:263-294`). This part is clear. Two parts are implicit:

- **Result protocol.** A failed save returns HTTP 200 with `ID = 0`, empty `Outputs` and `Errors` (`CSS:843-850`). A saved record returns `ID > 0`. The client reads `Errors.length` (`WEB/form/atlas-form.component.ts:317`). Access and "not found" failures use a second channel: an exception and an HTTP problem with 403, 404, 400 or 500 (`APP/AtlasProblemExceptionFilter.cs`).
- **Error codes are literals**: `NotAllowed`, `NotInOptions`, `InvalidGrid` (`CSS:382, 555, 573, 582, 591`; `PLAT/Sections/AtlasAllowList.cs:51`), `VersionRequired` (`CSS:834`), `Conflict` (`CSS:837`), `SaveFailed` (`CSS:702`), and the codes of the validator and of the value converter. The client adds `Http` and `Invalid` (`atlas-form.component.ts:348, 363`).

A failure in `OnAfterSave` is logged and the save still returns success (`CSS:514-526`). This is a decision (review SV-06).

**Tests.** `Sections/Ttdv2SectionTests`, `Sections/SectionStepTests`, `Api/EndpointTests`, `Api/ProblemFilterTests`.

### P03 Field state

**State store.** For each field: `Visible`, `Disabled`, `Required`, `HiddenByRule`, `DisabledByRule` (`PLAT/Sections/AtlasFormFields.cs:25-39`).

**Layers, in this order (server).**

1. Form JSON: `Visible`, `Disabled`, `Validator.Required` (`AtlasFormFields.cs:65-126`).
2. Rules (roles, owner): they can hide or disable; a child control can be shown or enabled by name (`PLAT/Rules/AtlasRuleEngine.cs:104-157`). Rule modes `FirstMatch` and `MostPermissive`, default `Keep`, `ReadOnly` or `Hidden` (`:48-97`).
3. Rule locks: `HiddenByRule`, `DisabledByRule` (`AtlasFormFields.cs:136-145`).
4. Effects (values): Show, Hide, Require, Unrequire, Enable, Disable. An effect cannot show a field with `HiddenByRule` and cannot enable a field with `DisabledByRule` (`PLAT/Sections/AtlasServerEffects.cs:16-44`).
5. Use: a hidden or disabled field can send only its stored value, else `NotAllowed` (`AtlasAllowList.cs:46-53`). Only visible and enabled fields are validated (`CSS:395`). A field with `HiddenByRule` gets no value in the load, the save result and the history (`CSS:170-186, 532, 766-770`).

**Client copy.** First flags from the JSON that the server sends after the rules (`WEB/form/atlas-form-model.ts:86-97`). Effects (`WEB/form/atlas-effects.ts:53-73`) call `setVisible`, `setRequired`, `setEnabled` (`atlas-form.component.ts:170-191`). The whole form is disabled when the user cannot save (`:119-121`). A control with no renderer is disabled (`:126-132`).

**Why implicit.** The effective state of a field is the result of five layers in four server classes, and the client has its own copy of layers 1 and 4.

**Holes and risks.**

- **[Inference - verify]** The client has no rule locks. The names `HiddenByRule` and `DisabledByRule` are not in `WebUi`. An `Enable` or `Show` effect on a field that a rule locked changes it on the client (`setEnabled` has no guard, `atlas-form.component.ts:184-191`). The server refuses a changed value, so the data is safe; the user sees an editable field and then an error. The same call can enable one control of a form that is read-only. The TTDV2 form has only `SetFromOption` and `Clear` effects, so the pilot does not reach this.
- The server and the client must give the same result. Shared test cases: `Sections/EffectParityTests.cs` with `WEB/form/atlas-effect-cases.ts`, and `Sections/ValidatorParityTests.cs` with `WEB/form/atlas-validator-cases.ts`.

**Tests.** `Rules/RuleEngineTests`, `Sections/SectionStepTests` (`An_effect_shows_and_sets_but_cannot_show_a_field_that_a_rule_hid`), `Sections/Ttdv2SectionTests`, `WEB/platform.spec.ts`, `WEB/platform.component.spec.ts`.

### P04 Record workflow state (declared, not connected)

The platform has the places for a record state, but no code gives them a value.

| Place | What the code does today | Code |
|---|---|---|
| Rule input `States` | Always an empty list | `CSS:800-801`; `PLAT/Dashboard/AtlasDashboardService.cs:39` |
| `CPRule.States` in the JSON | Read by the rule engine. A rule that has `States` never matches. If no other rule of the target matches, `RuleDefault` applies. The start-up check gives no message for such a rule. | `PLAT/Rules/AtlasRuleEngine.cs:185-191` |
| `CPAtlasPage.Step` | Copied from the request to the result, not used | `CSS:274` |
| Button action `Execute`, `Navigate` | Known to the lint, with the warning "the platform form does not do yet (it closes the form)" | `APP/AtlasConfigLint.cs:24-25, 166-177`; client `atlas-form.component.ts:270-281, 367-370` |
| `CPAuditLog.Source` | Always `App`; the comment says `App` or `Workflow` | `DATA/Dto/CPPlatformDto.cs:120`; `PLAT/Audit/AtlasAuditWriter.cs:121` |
| Target DTO members `Steps`, `CurrentState`, `Workflow` | In the stand-in DTO copy; no platform code reads them | `atlas-platform/src/BBH.CP.Atlas.Data/StandIn/DTO/CPAtlasApplicationConfig.cs:17, 22-41, 131-152` |

This is the connection point for the engine adapter (module 10, `docs/atlas-platform/10-engine-adapter.md`, design only, Q51). `docs/atlas-platform/04-rules-engine.md` (4.1, line 172) says that the states come from the workflow definition and that an unknown state is an error; the code does not have this check. The FABLE review listed the `States` and `Execute` paths as dead code (`docs/atlas-platform/FABLE_REVIEW.md:311`).

### P05 App availability

**Inputs.** (1) A registration line `AddAtlasModule` (`APP/AtlasPlatformServiceCollectionExtensions.cs:55-70`). (2) A manifest file that can be read (`PLAT/Registry/AtlasConfigStore.cs:82-127`). (3) `Enabled`, with an environment override that fails closed when the value is not valid (`AtlasConfigStore.cs:185-200`). (4) Not in `AtlasDisabledApps` (start-up errors, `PLAT/Registry/AtlasDisabledApps.cs`). (5) The tables of the app are in the registry (`PLAT/Registry/AtlasModuleResolver.cs:56-63`).

**Derived states.** NotRegistered, NoManifest, DisabledBySetting, DisabledByStartupCheck, TablesNotLoaded, Available. All of the first five give 404 with different texts. A restart is the only way back to Available.

| Read location | Inputs that it reads | Code |
|---|---|---|
| `IsPlatformApp` | 1, 2, 3, 4, 5 | `AtlasModuleResolver.cs:45-53` |
| `Get<TService>` | 1, 2, 3, then 4, then 5 (the same rule, written again, with messages) | `AtlasModuleResolver.cs:65-94` |
| `ConfigSectionService.Manifest` | 2, 3 | `CSS:85-88` |
| Dashboard of the app | 2, 3 | `PLAT/Dashboard/AtlasDashboardService.cs:30-32` |
| Dashboard tab with rows of another app | 2, 3 and the View check. It does not read 4. | `AtlasDashboardService.cs:87-111` |
| Menu rows | `IsPlatformApp`, else "current app" (enum) | `PLAT/Registry/AtlasApplicationMenu.cs:35-59` |
| Fall-through to the current `if` chains | A manifest exists and the code is not a current app: 404 | `PLAT/Registry/AtlasAppManifest.cs:121-129` |

**Why implicit.** No function returns "the availability state of app X and the reason". The rule is written two times in the resolver, and in part at four more locations.

**Holes and risks.**

- **[Inference - verify]** A dashboard tab of app A can show rows of app B when the start-up check disabled B (input 4), if B is enabled by setting and its table is loaded. Low risk; the start-up check of A catches the usual causes.

**Tests.** `Registry/RegistryTests` (`Resolver_returns_404_for_unknown_and_disabled_apps_and_400_for_a_missing_feature`, `An_app_whose_table_was_not_loaded_at_start_up_is_not_available`, `An_Enabled_setting_that_is_not_valid_disables_the_app_and_is_reported`), `Registry/ApplicationMenuTests`, `Dashboard/Ttdv2DashboardTests`, `Api/AtlasCheckTests`.

### P06 Platform start-up

`APP/AtlasPlatformStartup.cs:29-71`. State: `AtlasTableRegistry.IsLoaded` (`DATA/AtlasTableRegistry.cs:16-18`) and a semaphore gate.

| From | To | Trigger | Code |
|---|---|---|---|
| NotStarted | Starting | Hosted service, or the call in `program.cs` (the second call does nothing) | `:34-42` |
| Starting | Loaded (all apps) | The check has no errors | `:65` |
| Starting | Loaded (apps with errors are disabled) | Errors and mode `DisableModule` (default) | `:50-54, 63-65, 88-115` |
| Starting | Host does not start | Errors and mode `Stop` | `:60-62` |

`--atlas-check` runs the same checks, prints a report and exits; it does not mark the registry as loaded (`:121-147`). The config and the table metadata are read one time (`AtlasConfigStore.cs:68-80`); a change needs a restart (documented, not a defect). Tests: `Api/AtlasCheckTests`, `Api/StandInHostTests`.

### P07 Access decision and rollout modes

The modes are enums (`PLAT/Registry/AtlasAppManifest.cs:5-13`): `AccessMode` (LogOnly, Enforce), `AuditMode`, `RuleMode`, `RuleDefault`, `AuditTime`, and the boolean `Enabled`. The **decision** for a denied operation is in three places:

| Operation | `LogOnly` | `Enforce` | Code |
|---|---|---|---|
| View, Edit, History | Log and continue | 403 | `PLAT/Security/AtlasAccessService.cs:42-53` |
| Delete | 403 | 403 | `AtlasAccessService.cs:51` |
| Create at save | 403 | 403 | `CSS:344-346` (a second check after `Demand`) |
| Create at load (new form) | Log and continue | 403 | `CSS:137` |
| `CanEdit` flag for the client | True | The real decision | `AtlasAccessService.cs:87` |

Rollout path of an app (settings, not code): `Enabled = false`, then `Enabled = true` + `LogOnly`, then `Enforce`. The start-up check warns about `LogOnly` outside Development (`APP/AtlasStartupCheck.cs:152-153`). An `AccessMode` setting that is not valid gives `Enforce` (`AtlasConfigStore.cs:202-203`). Open decisions: Q19, Q20.

**Tests.** `Security/AccessServiceTests`, `Sections/Ttdv2SectionTests` (the four `LogOnly_...` tests), `Registry/RegistryTests`.

### P08 TTDV2 cut-over and rollback (SQL scripts)

**Stop-and-ask: do not change `050` or `090` without approval (`AGENTS.md`). Open decisions: Q07 to Q10.**

**State store.** No state column. The state is derived from: rows of `dbo.CP_Atlas_ScriptLog`, `IsDeleted` on `CP_Atlas_Application`, `CP_Atlas_Entitlement`, `CP_Atlas_UserEntitlement`, and `ModifiedBy` / `ModifiedDate` on the role rows.

| From | To | Script | What marks the state | Code |
|---|---|---|---|---|
| NotInstalled | Installed | `040` | Active app row `TTDV2`, 3 roles, log row `12-ttdv2-app.sql` | `SQLP/040-ttdv2-app.sql:127-159` |
| Installed | CutOver | `050` (change-over day only) | TTDV2 roles given, all TTD roles soft-deleted, rows and audit copied, log row `12-ttdv2-copy-from-ttd.sql` (written one time only, `:207`) | `SQLP/050-ttdv2-copy-from-ttd.sql:143-210` |
| Installed or CutOver | RolledBack | `090` | TTD roles given back, TTDV2 user roles, roles and app row soft-deleted, log row `12-ttdv2-rollback.sql` (one for each run) | `SQLP/090-ttdv2-rollback.sql:25-58` |
| RolledBack | Installed | By hand: set `IsDeleted = 0` on the app row and its roles, then `040` (it stops with 55010 before that) | - | `040:129-130`; `090:15` |

The run-time side of the same switch is `AppSettings:Modules:TTDV2:Enabled` (P05) and the roles view (P09).

**Why implicit.** "Which TTD roles did 050 revoke" is not stored. `090` finds them with a rule: `IsDeleted = 1`, `ModifiedBy = @CopyRunBy`, `ModifiedDate` in the hour before the time of the 050 log row (`090:31-39`).

**Holes and risks.**

- **[Known: R01, High, not fixed]** `090` also gives back roles that the same person revoked on purpose in that hour.
- **[Known: R02, High, not fixed]** A second cut-over after a rollback loses newer TTD data and history, because `050` copies only the IDs that are not in TTDV2 (`050:191, 203`). It also revokes the TTD roles again.
- **[Inference - verify]** `050` writes its log row one time only (`050:207`). After a second cut-over, `090` reads the time of the first run (`090:25-26`), so its one-hour rule does not find the roles that the second run revoked.
- The log names are not the file names (`11-script-log.sql`, `12-ttdv2-app.sql`, `12-ttdv2-copy-from-ttd.sql`, `12-ttdv2-rollback.sql`). They are stable identifiers.
- Review files (not committed): `atlas-platform/review/2026-11-01/`.

**Tests.** `Data/CopyScriptTests`, `Data/Ttdv2AppScriptTests`.

### P09 User role life cycle (data)

A role is active when four rows are not deleted and three values are not null: user, user entitlement, entitlement, application (`SQLP/030-active-user-roles-view.sql:15-41`). The platform reads the view one time for each request (`PLAT/Security/AtlasUserContext.cs:56-79`). The platform does not write these tables at run time; the target and the scripts of P08 do. Tests: `Security/UserContextSqlTests` (`A_revoked_role_is_not_active`).

### P10 Form component (client)

`WEB/form/atlas-form.component.ts`. The state is in independent signals: `loading`, `saving`, `loadError`, `pageErrors`, `section`, `historyOpen`, `confirmDelete`, `invalidOpen` (`:47-57`), the route values `code` and `id` (`:61-62`), and `canSave` from the permissions (`:261-264`).

| From | To | Trigger | Code |
|---|---|---|---|
| (route change) | Loading | A new `code` or `id`; the load before it is cancelled | `:82-106` |
| Loading | Ready (editable, or read-only when `canSave` is false) | The section arrives | `:108-123` |
| Loading | LoadFailed | HTTP error | `:96-100` |
| Ready | Ready + "mandatory fields" dialog | Save with a field that is shown and not valid | `:292-303` |
| Ready | Saving | Save with a valid form; guarded by `saving` | `:287-291, 305` |
| Saving | Left the form (dashboard) | Result with no errors | `:315-323` |
| Saving | Ready with field errors and page errors | Result with `Errors`, or HTTP error | `:317-320, 324-327, 352-364` |
| Ready | Ready + delete dialog | Delete button | `:275-277` |
| Delete dialog | (request runs; **no state**) | Yes | `:331-344` |
| (request runs) | Left the form, or Ready with page errors | Result | `:334-342` |

**Why implicit.** The page state is a combination of eight signals. The template reads three of them in an `if` chain (`WEB/form/atlas-form.component.html:2-12`).

**Holes and risks.**

- **[Code fact]** Delete has no busy state. `remove()` closes the dialog and sends the request (`:331-333`). The buttons are disabled only by `saving()` (`atlas-form.component.html:71-72`). During the delete request the user can press Delete or Save again.
- **[Code fact]** `saving` and the dialogs are independent. Nothing prevents `confirmDelete` during a save, except that the buttons are disabled.

**Tests.** `WEB/platform.component.spec.ts`, `WEB/platform.spec.ts`.

### P11 Dashboard, history dialog and navigation cache (client)

- Dashboard (`WEB/dashboard/atlas-dashboard.component.ts`): `loading`, `error`, `dashboard`, `tabIndex`, `search`, `historyRow`, `deleteRow` (`:51-58`). Loading to Ready or Failed (`:100-117`); a reload after a delete (`:96-98, 164-182`). The delete here also has no busy state.
- History dialog (`WEB/atlas-history-dialog.ts:78-110`): `loading`, `error`, `entries`; a new record or a close cancels the request.
- Navigation cache (`WEB/atlas-navigation.ts:42-68`): the menu rows (`FormType`) are read one time and kept as a promise for the life of the page; a failed read is not kept. `FormType` selects the route (`:18-29`).

Small. Listed so that the next agent does not search for them again.

## 5. Rules that connect the machines

| # | Rule | Enforced by |
|---|---|---|
| I1 | A completed instance has no `Pending` task | **Not enforced.** Metadata only (E01). |
| I2 | A completed task has no `Active` schedule | `WES:712-716` in the transaction of the move. Race F09 is open. |
| I3 | A schedule that is not `Active` has no `Pending` or `Processing` outbox row | `SLASYNC:219-231`, and the worker checks again (`OUTBOX:104-136`). See the cancel-during-send case in E05. |
| I4 | A `Pending` task with an SLA policy has one `Active` schedule | Made in the same transaction (`WES:749-758`). "At most one" by the filtered index. |
| I5 | `WorkflowTask.DueAtUtc` = `OverdueAtUtc` of the active schedule | `SLASYNC:134` |
| I6 | One `Pending` task for each instance | Filtered index `DBC:310-312` |
| I7 | One active version for each workflow key | **Not in the database.** Scripts and the seed service (E08). |
| I8 | A task with an SLA policy has a primary e-mail | **Not enforced.** Found by the SLA scan as a warning (E04). |
| I9 | The SQMS projection columns equal the engine state | **Not enforced.** Two owners (E11). |
| I10 | The client field state equals the server field state | Shared test cases for effects and validators. The rule locks are not shared (P03). |

Cascade of one transition: instance moves (E01), task closes (E02), schedule completes (E03), undelivered reminder rows are cancelled (E05); then a new task (E02), a new schedule (E03), one outbox row (E05); later the scan adds reminder rows (E04 to E05) and the worker sends them (E05).

## 6. Candidate work items

These are candidates. The user selects. Nothing here is approved. This list does not replace the milestones of `PROJECT.md`.

| # | Work item | Machines | Behaviour change | Approval needed first | Size |
|---|---|---|---|---|---|
| W1 | Tests that pin each transition table of section 3 (one test class for each machine: all permitted moves pass, each not-permitted move is refused or cannot be reached). Do this before any other engine item. | E01-E06 | None | Engine restart (Q40) | Small to medium |
| W2 | `const string` values for the persisted status strings, reminder kinds and cancellation reasons, in `src/WorkflowEngine.Domain` (all engine projects reference it). Use `const`, so the EF queries and the SQL text stay the same. 96 literals in 13 files (appendix A). | E01-E05 | None | Engine restart (Q40). The user must accept the merge of the changed runtime files at the target. | Medium (many files, small edits) |
| W3 | One outbox delivery helper for the worker and the API dispatcher (claim, mark sent, release, row to delivery). | E05 | Only if the user wants the three differences removed | Decision for each difference in the E05 table | Medium |
| W4 | SaveDraft race | E01 | Yes (the fix decides the result for a SaveDraft that comes after a concurrent transition) | Q41 | Small |
| W5 | Cancel during send (first a test that reproduces it) | E05, E03 | Yes | User | Small |
| W6 | SLA-only request with no pending task: a structured result in place of the exception | E03, E09 | Yes, a result for a case that is HTTP 500 today. Contract: ask. | User (stop-and-ask: result shape and status) | Small |
| W7 | Metadata validation for all workflows, not only PortfolioTrade: first state exists; a terminal transition closes the pending task and creates none; a task with an SLA policy has a creating transition with a route; one active version | E01, E02, E04, E08 | None at run time (a check command) | User. Relates to D09. | Medium |
| W8 | F09 (SLA edit and task completion at the same time) | E03 | Yes | User | Medium |
| W9 | One availability function that returns the state and the reason; the resolver, the menu, the dashboard and `ToLegacy` use it | P05 | None intended (check the tab case) | Platform gates; package rebuild | Small to medium |
| W10 | Form component: one page status in place of the independent flags, and a busy state for delete (form and dashboard) | P10, P11 | Buttons are disabled during a delete | Platform gates; package rebuild; the look must stay as the target | Small |
| W11 | Client rule locks: the client must not enable or show a field that a rule locked | P03 | Yes (client only) | An optional field in the load response is a `v1` contract change: stop-and-ask. First a spec that shows the problem. | Small to medium |
| W12 | Record state for rules, `Execute` action, workflow history source | P04 | New feature | This is the engine adapter (module 10). Q51. Do not start without the decision. | Large |
| W13 | Cut-over state: store what `050` revoked; second cut-over | P08 | Yes | Stop-and-ask. Q07 to Q10 (R01, R02). | Medium |

**Shape for "make it explicit" (within the project rules).** No library and no framework. For each machine: (1) constants for the state values, (2) one small static class with the permitted moves and one guard method, (3) the writers call the guard or use one shared method, (4) a test that walks the table. Keep the persisted text and the SQL constraints as they are.

**Quality gates.** Engine: `PROJECT.md` section 21 and the completion summary of section 23. Platform: the five gates in `AGENTS.md` ("Platform quality gates") and the package rebuild when a copied file changes.

## 7. Items checked and left out

| Item | Why it is not in the list |
|---|---|
| Metadata workflow definitions (PortfolioTrade v1 and v2, SQMS Opportunity and Bid) | Explicit state machines in data. This is the design. |
| `Notifications:Sender` (Console, NoOp, Smtp) and `Smtp:RecipientOverride` | Settings read one time at start. No moves at run time. (`InfrastructureServiceCollectionExtensions.cs:84-114`) |
| `OutboxProcessingOptions`, `SlaProcessingOptions`, `SqmsOptions` | Settings |
| `CommandRegistry`, `CommandArgumentParser`, demo step runner | Command dispatch and argument parsing. The demo runs a fixed list of steps. |
| `NotificationTemplateRenderer`, `CanonicalRequestHasher`, `NotificationRecipientResolver` | Pure functions |
| `SeedVerificationService` | A list of checks. It is the place where W7 could grow. |
| `AtlasValueConverter`, `AtlasTabQueryBuilder`, `AtlasComputedFields`, `AtlasDefaultTokens`, option providers, `AtlasAuditMask` | Pure functions |
| `AtlasUserDirectory` name cache (30 minutes, 2 minutes for a miss) | A cache with a time limit |
| Grid renderer (`WEB/form/renderers/atlas-grid-renderer.ts`) | Two-way copy between the grid row and the form group. No life cycle. |
| `FormType` (FORM, SECTION, STEPPER) | A route selector (`WEB/atlas-navigation.ts:18-29`) |
| `tools/WorkflowEngine.Sqms.LiveAcceptance`, stand-in host and stand-in client controls, `local/` scripts | Test and local tools; not copied to the target |
| `target-implementation-read-only/` | Target code. Not scanned. The business rules that select an action key (for example CDT "BDRM No" to `BdrmTradeFulfilled`, F01, by design) are there. |

## 8. Questions for the user before code

1. Goal: (a) make the machines explicit with no behaviour change (W1, W2, W3, W9, W10), (b) close the holes (W4 to W8, W11), or (c) both? Which machines first?
2. Does this work start the engine again? The engine is paused until Q40 and Q41 (`CODEX_HANDOFF.md`, section B).
3. W2 changes many engine runtime files with small edits. The target must merge them again. Is that acceptable, or must W2 touch only the files of one machine at a time (for example the outbox first)?
4. W3: for each of the three differences in the E05 table, keep the difference or make the API dispatcher equal to the worker?
5. W6 and W11 touch a public contract. Is an additive change permitted?
6. W12 is the engine adapter. When does it start (Q51)?
7. W13 needs the answers to Q07 to Q10.

## 9. Appendix A: status literals in the engine

Count of status-like string literals for each file (search for `"Pending"`, `"Processing"`, `"Sent"`, `"Cancelled"`, `"Active"`, `"Completed"`, `"Superseded"`, `"DueToday"`, `"Overdue"`, `"OnTrack"`, the two calculation modes and the three cancellation reasons). 96 in 13 files. No shared constant exists. The engine has no enum for a persisted status.

| File | Count | Note |
|---|---|---|
| `src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs` | 19 | Schedule, task, instance, outbox status; reminder kinds; calculation modes |
| `src/WorkflowEngine.Infrastructure/Sla/WorkflowSlaSynchronizationService.cs` | 16 | Schedule and outbox status; calculation modes; cancellation reasons |
| `src/WorkflowEngine.Infrastructure/Notifications/ProcessNotificationOutboxService.cs` | 15 | Outbox status; schedule status; cancellation reason |
| `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs` | 10 | Instance, task, outbox status; calculation mode |
| `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionNotificationDispatcher.cs` | 9 | Outbox status |
| `src/WorkflowEngine.Infrastructure/WorkflowContext/WorkflowContextReader.cs` | 7 | Instance, task, schedule status; `OnTrack`, `Overdue` |
| `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs` | 6 | Includes the PortfolioTrade state key `Completed` (seed data, not a status) |
| `src/WorkflowEngine.Infrastructure/Sla/SlaScheduleCalculator.cs` | 5 | Reminder kinds |
| `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedDataV2.cs` | 3 | Seed data |
| `src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs` | 2 | `Active` (and `Draft`, which the search does not count) |
| `src/WorkflowEngine.Infrastructure/Persistence/Verification/SeedVerificationService.cs` | 2 | Calculation modes |
| `src/WorkflowEngine.Infrastructure/Notifications/QueueWorkflowNotificationService.cs` | 1 | `Pending` |
| `src/WorkflowEngine.Application/Commands/ShowCommandHandler.cs` | 1 | `Pending`, compared with `OrdinalIgnoreCase` (all other compares are ordinal or SQL) |

The SQL side of the same values: CHECK constraints and filtered indexes in `WorkflowDbContext.cs:196, 272, 312, 318, 356, 365, 454`, the EF migrations, and `sql/upgrades/*`.

Other literals that select a path: `"Draft"` (`StartWorkflowService.cs:400, 416`), `"SaveDraft"` (`WorkflowExecutionService.cs:675`; `WorkflowActionEnvelopeValidator.cs:135`), `"AdminDemoOverride"` (`WorkflowExecutionService.cs:195`), `"ExecuteWorkflowAction"` (3 files), the result codes (E09).

## 10. Next-session prompt

"Read `docs/IMPLICIT_STATE_MACHINES.md` (sections 1, 2, 6 and 8) and the top of `CODEX_HANDOFF.md`. The user selected these work items: <W..>. Answers to the questions of section 8: <...>. For each item: read the machine in section 3 or 4 again against the current code, write the tests first, keep all stable identifiers, run the gates of the component, and give the completion summary of `AGENTS.md`."
