# Codex Handoff

## 2026-08-10 100-Workflow Live API and SLA Performance Acceptance

### Current goal

- Completed an isolated Release-build performance run for 100 concurrent Portfolio Trade workflows through API writes, SLA scanning, outbox claiming, SMTP delivery, completion, and context reads.

### Environment

- Source commit: `66e1bfb71394a5f365ef5afba5763312f189dc92` (`console-code-first-0518`).
- API: PID `21232`, `http://127.0.0.1:5088`, Release build, NoOp sender for API measurements.
- Database: `workflowengine_perf_20260810_1518` on local SQL Server.
- Host: 8 physical cores, 16 logical processors, 15.33 GB RAM, .NET SDK 10.0.300.
- Raw metrics/logs: `%LOCALAPPDATA%\Temp\cdt-perf-20260810-1518-api`.

### API metrics

- 100 concurrent Start+Submit: 100 success, 2.706 s wall, 36.96/s, p50 2.449 s, p95 2.629 s, p99 2.688 s.
- 110 concurrent Checker requests with ten same-workflow races: 100 success and ten controlled rejections, 2.764 s wall, 39.79/s, p50 1.712 s, p95 2.707 s, p99 2.725 s.
- 100 concurrent TradingTradeExecuted: 100 success, 2.171 s wall, 46.07/s, p50 1.524 s, p95 2.118 s, p99 2.135 s.
- 100 concurrent BdrmTradeFulfilled: 100 success, 1.007 s wall, 99.35/s, p50 309 ms, p95 953 ms, p99 971 ms.
- 100 concurrent final WorkflowContext reads: 100 valid, 109 ms wall, 914.21/s, p50 62 ms, p95 80 ms, p99 81 ms.
- API peak working set: 253.34 MB; final working set 203.04 MB; total process CPU 31.766 s; API error log remained empty.

### SLA, outbox, and SMTP metrics

- Two concurrent Checker SLA scans: both exit 0, 4.133 s wall, exactly 100 measured occurrences. Two concurrent outbox processors: both exit 0, 1.956 s, exactly 100 sent rows with no retries/errors.
- Trading due-today scan: 3.162 s; NoOp dispatch 2.149 s. Trading overdue scan: 3.069 s; NoOp dispatch 1.933 s.
- BDRM overdue scan: 4.737 s. SMTP dispatch to smtp4dev: 100 messages in 4.225 s (23.67/s), zero warnings.
- smtp4dev retained all 100 BDRM overdue subjects; its 100-message retention cap evicted the eight older mailbox messages.

### Final SQL integrity

- 100/100 instances completed; 300/300 tasks completed; 300/300 SLA schedules completed.
- 800/800 outbox rows Sent and clean: 300 assignment, 100 due-today, 300 overdue, 100 completion.
- 400 audit rows, 400 successful idempotency rows, zero task/outbox snapshot mismatches, zero active measured schedules.
- Database used 17.06 MB (72 MB allocated).
- Role distribution: 34 Taxable, 33 Tax Exempt, 33 Equity. Ten duplicate Checker races created zero duplicate pending tasks.

### Finding

- The ten expected duplicate-race rejections preserved state correctly but returned HTTP 500: eight `ConcurrencyConflict`, two `InvalidTransition`. `WorkflowActionController` currently maps every unsuccessful workflow result to 500. Production integration should map expected conflicts/invalid actions to explicit non-500 responses before using HTTP 5xx rate as a server-health signal.

### Changed files and preservation

- No source, seed, migration, contract, sample, or test file was changed.
- This handoff entry remains uncommitted with the existing user-owned handoff changes.
- Preserve the API, isolated database, and raw metric directory until the user asks for cleanup.

### Next-session prompt

```text
Review the 100-workflow performance evidence in workflowengine_perf_20260810_1518 and %LOCALAPPDATA%\Temp\cdt-perf-20260810-1518-api. API PID 21232 remains on port 5088. The only product finding is that expected ConcurrencyConflict/InvalidTransition race rejections are mapped to HTTP 500 by WorkflowActionController. Do not implement a mapping change unless the user asks.
```

## 2026-08-10 Trading Desk Routing Full Live API and SLA Acceptance

### Current goal

- Completed a fresh live happy-path scenario through Maker, Checker, the selected Taxable Trading Desk, and BDRM/Client Service while exercising every active SLA schedule.

### Completed evidence

- Business object: `CDT-LIVE-SLA-20260810-1420`.
- Workflow instance: `WF-CDT-LIVE-SLA-20260810-1420`.
- Isolated database: `workflowengine_live_sla_20260810_1420`.
- Path: Maker SaveDraft -> SubmitForChecker -> CheckerSendToTradingDesk (`TradingDeskTaxable`) -> TradingTradeExecuted -> BdrmTradeFulfilled.
- Final public context: `Completed`, version `5`, `NoSla`, no current task, and no available actions.
- Three tasks were created and completed; each SLA schedule is `Completed` by the actor who closed its task.
- Checker overdue, Trading Desk due-today and overdue, and BDRM overdue occurrences were queued and sent.
- Eight outbox rows are `Sent`, all with retry count `0`, null `LastError`, and matching smtp4dev messages.
- Every task/outbox recipient snapshot matched. All four SLA plain-text and HTML bodies matched the original assignment notification body.
- The Taxable desk assignment and both SLA messages went to both seeded Taxable desk addresses, with cumulative Maker/manager/Checker Cc recipients.
- smtp4dev contains exactly eight scenario messages with zero warnings.
- A post-completion scan found zero active SLA schedules and queued zero notifications.

### Running services

- Current-build isolated Workflow API PID `1672` is listening on `http://127.0.0.1:5068` against `workflowengine_live_sla_20260810_1420` with SMTP enabled.
- The pre-existing Workflow API on port `5058` was not touched.
- smtp4dev PID `16072` remains on UI `http://localhost:5000` and SMTP `localhost:3025`.
- Build artifacts are under `%TEMP%\cdt-live-sla-20260810-1420-api`.

### Changed files

- No source, migration, seed, contract, sample, or test file was changed by this acceptance run.
- This handoff entry is intentionally left uncommitted because `CODEX_HANDOFF.md` already contains unrelated workspace changes.

### Next-session prompt

```text
Inspect the completed live scenario WF-CDT-LIVE-SLA-20260810-1420 in database workflowengine_live_sla_20260810_1420. Current-build API PID 1672 listens on port 5068 and smtp4dev PID 16072 listens on ports 5000/3025. Preserve the evidence until the user asks to stop the isolated API or remove the database.
```

## 2026-08-10 Workflow API Integration Envelope Guide

### Current goal

- Provide the integration team one simple, self-explanatory document covering the fixed public Start and Action JSON envelopes and all seeded Portfolio Trade scenario values.

### Completed changes

- Added `workflow-api/docs/WORKFLOW_API_ENVELOPES.md` with endpoint-selection guidance, actor headers, canonical Start/Action structures, all 18 scenario mappings, notification recipient rules, template-data profiles, copy-ready examples, prohibited fields, and valid sequences.
- Kept the document limited to the public Workflow API; it does not describe or expose the internal Console `WorkflowActionEnvelope`.

### Changed files

- `workflow-api/docs/WORKFLOW_API_ENVELOPES.md`
- `CODEX_HANDOFF.md`

### Build/test status

- Focused public request-contract and scenario-sample tests: 20 passed, 0 failed.
- Documentation audit: 19 scenario/catalog links resolved, all required public contract terms present, no potential secrets, and no TODO/TBD markers.
- The first test attempt with a build was blocked because live Workflow API PID `6272` owns the API output DLLs. The same already-built focused tests passed with `--no-build`; the live API was not interrupted.

### Remaining tasks

- User/integration-team review of the new handoff document.
- No further publishing work remains for the integration guide.

### Known risks

- The guide is tied to the current public DTOs and seeded Portfolio Trade metadata. Reverify it if those contracts or scenario packs change.
- Existing unrelated worktree changes remain preserved.

### Next-session prompt

```text
Review workflow-api/docs/WORKFLOW_API_ENVELOPES.md with the integration team. It documents the current fixed public WorkflowStart and WorkflowAction bodies, actor headers, all 18 Portfolio Trade scenarios, notification profiles, CC behavior, and prohibited fields. Focused API contract/sample tests passed 20/20 and all document links/contract terms were verified. The guide was committed as 553370a and pushed to origin/console-code-first-0518 with matching local and remote SHA.
```

## 2026-08-10 Trading Desk Recipient Routing Implementation

### Current goal

- Implement the approved narrow design: Checker approval supplies the existing `assignedRoleKey`; notification metadata resolves every active email address for that selected Trading Desk.

### Completed changes

- Added optional `AssignedRoleKey` filtering to notification-recipient metadata and its EF Core migration.
- Kept the reusable engine generic; Portfolio Trade desk keys and addresses exist only in seed/sample metadata.
- Seeded two To addresses for each of `TradingDeskTaxable`, `TradingDeskTaxExempt`, and `TradingDeskEquity`; the common rework address is unchanged.
- Passed the normalized task assignment role into recipient resolution and preserved original Maker plus cumulative selected Cc recipients.
- Added seed verification for missing dynamic-role To mappings and updated public integration documentation without changing the public JSON shape.

### Changed files

- `PROJECT.md`, `README.md`, Workflow API boundary/envelope documentation.
- Notification recipient entity, resolver, seed persistence/verification, workflow execution, EF model/migration.
- Resolver, workflow, seed, schema, migration, and coverage-audit tests.

### Build and test status

- Core build with warnings as errors: passed, 0 warnings and 0 errors.
- Engine tests: 336 passed, 4 SMTP-category tests skipped, 0 failed.
- Workflow API tests: 63 passed, 2 skipped, 0 failed; focused contract tests: 20 passed.
- README validation: 11 passed; schema-focused tests: 39 passed.
- `init-db` and `verify-seed` passed against local SQL Server.

### Live verification

- Workflow `WF-PT-DESK-ROUTE-20260810` advanced from CheckerReview to TradingDeskReview using public `assignedRoleKey: TradingDeskTaxable`.
- Task 8 was assigned to `TradingDeskTaxable`.
- The outbox/task snapshots matched and contained both Taxable desk To addresses plus Maker, manager, and Checker Cc addresses.
- The isolated API proof used the NoOp sender and was stopped afterward; the pre-existing API process was not touched.

### Known risks and remaining tasks

- Feature commits are local until the user explicitly requests a push.
- The live proof data remains in the local `workflowengine` database for inspection.
- Repository-wide `dotnet format --verify-no-changes` still reports pre-existing whitespace in untouched code.
- Preserve the unrelated dirty workspace files listed by `git status`; do not stage or revert them.

### Next-session prompt

```text
Inspect the committed Trading Desk recipient-routing implementation at `e2ccd86` on branch console-code-first-0518. Only if the user asks, push it to the branch. Preserve unrelated dirty workspace changes and the live database evidence for WF-PT-DESK-ROUTE-20260810.
```

## 2026-08-10 SLA Email Inheritance Implementation and Live UAT

### Current goal

- Implemented the approved behavior so SLA due-today and overdue emails inherit the original task email bodies exactly and prefix the original subject.
- Completed the full live path: Maker SaveDraft -> SubmitForChecker -> Checker ReturnToMaker -> Maker SaveDraft -> ResubmitForChecker -> Checker SendToTradingDesk -> Trading TradeExecuted -> BDRM TradeFulfilled.
- Left the isolated Workflow API and smtp4dev running for user UAT.

### Implementation

- Commit `d8eae70` changes `RunSlaService` to resolve exactly one non-SLA outbox row for the task, copy its plain/HTML bodies, and prefix its subject using SLA route metadata.
- Due-today seed prefix is `Due today: `; overdue remains `Overdue: `.
- Missing or ambiguous primary emails and subjects over 300 characters fail closed without advancing the SLA schedule.
- No public DTO, SQL schema, migration, SLA timing, recipient-resolution, or recipient-snapshot change.

### Automated verification

- Core warnings-as-errors build: 0 warnings, 0 errors.
- Workflow API warnings-as-errors build: 0 warnings, 0 errors.
- Shared suite: 328 passed, 0 failed.
- Workflow API suite: 63 passed, 2 intentionally skipped dispatcher tests, 0 failed.
- Edge-guard mutation check: all three focused tests failed when guard warning behavior was mutated, then 3 passed after restoration.
- Isolated `init-db` and `verify-seed`: passed.

### Live scenario

- Business object: `CDT-SLA-INHERIT-20260810-0921`.
- Workflow instance: `WF-CDT-SLA-INHERIT-20260810-0921`.
- Isolated database: `workflowengine_sla_inherit_20260810_092135`.
- Final state/status/version: `Completed` / `Completed` / `7`.
- Tasks: 5 total, 5 completed.
- SLA schedules: 4 total, 4 completed.
- Audit rows: 8, matching the complete return/resubmit path.
- Outbox: 11 total, 11 Sent, all retry count 0 with null `LastError`.
- SLA rows: 5. All five matched their primary task email for expected prefixed subject, SHA-256 plain-body equality, SHA-256 HTML-body equality, and exact task recipient snapshot.
- Post-completion SLA scan: 0 active schedules and 0 queued occurrences.
- One initial Checker-return request was rejected at HTTP 400 because the ad hoc request included unsupported top-level `reason`; SQL proved the workflow remained `CheckerReview` version 2. The checked-in public DTO/sample omit that property, and the corrected request succeeded. No code change resulted from this diagnostic.

### smtp4dev UAT mailbox

- UI: `http://localhost:5000`.
- SMTP PID: `16072`; SMTP port `3025`.
- Data directory: `.tools/smtp4dev/data/liveinherit20260810_0921`.
- Message count: 11; warning count: 0; MIME parse errors: 0; relay errors: 0.
- `de5e93b8-e2ad-420a-b871-00ccf3af65d7` - Checker assignment.
- `25526f06-d272-49e8-9005-b263932d5f66` - `Overdue: Directed Trade Pending Checker Approval`.
- `1938ca6a-74ce-46eb-ad0a-6e17661b21fe` - Return to Maker.
- `f5088386-b581-47f1-9a60-2061091b8b6f` - Checker resubmission assignment.
- `90040ad4-4f0a-4a93-b6a2-7fb07c0e74fb` - `Overdue: Directed Trade Pending Checker Approval`.
- `741dc83b-8a89-4e88-b460-0c0171927117` - Trading Desk assignment.
- `c6a68aba-3a92-47b2-a0c9-0ad233a0de67` - `Due today: Directed Trade Request Pending Execution`.
- `6aa0453d-41e9-4f0d-b02e-e739e0a1f38d` - `Overdue: Directed Trade Request Pending Execution`.
- `33bd4bf3-4b9e-412b-a344-1c27b0ceeb8d` - BDRM assignment.
- `311d087e-9c7a-4349-bc91-7a45982a557e` - `Overdue: Trade Execution Pending Confirmation`.
- `c4d95139-8eff-4b1d-b070-ee90384f1f9b` - Completion.

### Running services and next-session prompt

- Workflow API PID `6272` is listening at `http://localhost:5058` against the isolated database.
- smtp4dev PID `16072` is listening at `http://localhost:5000` and `localhost:3025`.
- Do not replay any workflow action or SLA scan for this completed instance.
- Preserve both processes and the isolated database until user UAT sign-off.

```text
Continue from completed SLA-inheritance UAT scenario WF-CDT-SLA-INHERIT-20260810-0921 in database workflowengine_sla_inherit_20260810_092135. API PID 6272 and smtp4dev PID 16072 remain live. The workflow is Completed version 7; all 5 tasks and 4 schedules are completed; 11/11 outbox messages are Sent; smtp4dev contains 11 messages with zero warnings. All five SLA rows have expected Due today/Overdue prefixes, byte-equal original task plain/HTML bodies, and exact recipient snapshots. Await UAT sign-off and do not replay the scenario.
```

## 2026-08-09 Isolated Full Happy-Path Live API Acceptance

### Completed scenario

- Fresh business object: `CDT-HAPPY-20260809-223836`.
- Workflow instance: `WF-CDT-HAPPY-20260809-223836`.
- Live API: PID `17968`, `http://localhost:5058`.
- Isolated database: `workflowengine_happy_20260809_223836`; the existing `workflowengine` database was not reset or modified by this scenario after the isolated database was selected.
- smtp4dev: PID `3060`, UI `http://localhost:5000`, SMTP `localhost:3025`.
- Final workflow state/status/version: `Completed` / `Completed` / `5`.
- All three tasks are completed: Checker task 1, Trading Desk Taxable task 2, and BDRM Client Service task 3.
- Audit path: `SubmitForChecker` -> `CheckerSendToTradingDesk` -> `TradingTradeExecuted` -> `BdrmTradeFulfilled`.

### SLA proof

- Checker overdue occurrence queued at `2026-08-10T02:42:23.7123144Z` and sent as outbox row 2.
- Trading Desk used application target date `2026-08-08`; overdue occurrence `2026-08-09T13:00:00Z` was sent as outbox row 4.
- BDRM overdue occurrence queued at `2026-08-10T02:45:30.5705916Z` and sent as outbox row 6.
- A post-completion scan at `2026-08-11T14:00:00Z` found zero active schedules and queued zero reminders.
- All three overdue plain/HTML bodies contain `https://example.test/cdt/CDT-HAPPY-20260809-223836`, a clickable HTML link, date-only `08/09/2026`, no EST/EDT/UTC/GMT suffix, and zero smtp4dev warnings.

### smtp4dev messages

1. `5c5587c1-1763-4f58-abb7-7b45c11fe7b3` - Checker assignment - To Checker; Cc Manager.
2. `30a7c056-3410-4ef5-9513-e93e2523e59b` - Checker overdue - To Checker; Cc Manager.
3. `582cade9-a5af-4d49-a9b1-f7817dbb8480` - Trading Desk assignment - To Trading Desk.
4. `71aaddb4-363a-4134-93e9-a42d89daf9b2` - Trading Desk overdue - To Trading Desk.
5. `da70fb28-af69-4236-9b56-d2cf421cbbbb` - BDRM assignment - To BDRM; Cc Maker, Checker, Manager.
6. `57cbfbd2-53b5-4609-b12d-192958c2d396` - BDRM overdue - To BDRM; Cc Maker, Checker, Manager.
7. `871d5623-60e5-4ec1-b2b7-8c9abc4f900a` - Completion - To original Maker.

### Next action

- User is validating the seven messages in smtp4dev.
- Keep API PID `17968` and the isolated database active until validation is complete.
- After validation, restore the API to the original `workflowengine` database only when requested or when continuing the prior live scenarios.

## 2026-08-09 Fresh Maker Submit Live Scenario

### Executed request

- Submitted `POST /v1/WorkflowStart` as `cdt.live.maker1` / `Maker` with initial action `SubmitForChecker`.
- Fresh business object: `CDT-MAKER-LIVE-20260809-190524`; derived workflow instance: `WF-CDT-MAKER-LIVE-20260809-190524`.
- Captured immutable original Maker `cdt.live.maker1@example.test`, selected Cc manager `cdt.live.manager1@example.test`, and request link `https://example.test/cdt/CDT-MAKER-LIVE-20260809-190524`.
- Windows PowerShell `Invoke-WebRequest` threw a local `NullReferenceException` while handling the response. The request had already committed, so it was not retried.

### Verified result

- Persistence confirms Active workflow version 2 in `CheckerReview`, started by `cdt.live.maker1` at `2026-08-09T23:05:53.4331668Z`.
- Pending task 4 is `CheckerReviewTask`, assigned to `Checker`, created at `2026-08-09T23:05:54.114549Z`, with first SLA occurrence at `2026-08-09T23:07:54.114549Z`.
- Checker context is actionable and exposes `CheckerReturnToMaker` and `CheckerSendToTradingDesk`.
- Assignment outbox row 8 is Sent: To `checker@example.test`, Cc `cdt.live.manager1@example.test`, subject `Directed Trade Pending Checker Approval`.
- smtp4dev message `3591d5ec-c09e-4cd9-a073-6b650897eb6c` arrived with no warnings.

### Overdue reminder executed

- Ran the real-time global `run-sla` monitor at `2026-08-09T23:11:48Z` with no `--now` override.
- The requested task 4 queued outbox row 10 for anchored occurrence `2026-08-09T23:09:54.114549Z`; task deadline remained `2026-08-09T23:07:54.114549Z`.
- Because `run-sla` is global, the older still-pending recurring Checker task 3 also queued catch-up row 9. Both rows were sent by one `process-notification-outbox` run using SMTP.
- Outbox row 10 is Sent with retry count 0 and correlation `sla-4-639219137941145490-Overdue`.
- smtp4dev message `dfe12b15-fa5d-4d2d-9992-4284f6575592` has To `checker@example.test`, Cc `cdt.live.manager1@example.test`, no warnings, the plain request URL, clickable HTML `Open request` link, date-only `08/09/2026`, and no timezone suffix in either body.
- Older catch-up row 9 was delivered as smtp4dev message `eb5d4087-dd49-4733-b1c1-61d159da10d8`.

### Sent to Trading Desk and Trading Desk SLA executed

- Executed `CheckerSendToTradingDesk` as `checker.live1` with `assignedRoleKey = TradingDeskTaxable` and application target date `2026-08-08` (yesterday).
- A first native `curl.exe` attempt was rejected with HTTP 400 because Windows PowerShell split the multiline JSON; persistence remained unchanged. The identical object-serialized `Invoke-RestMethod` request then returned Success.
- Workflow moved `CheckerReview -> TradingDeskReview`, version 3. Checker task 4 and its recurring schedule completed; Trading Desk task 5 is pending and assigned to `TradingDeskTaxable`.
- Application-date SLA target is `2026-08-08`; overdue boundary is `2026-08-09T13:00:00Z` (9:00 AM `America/New_York` during daylight time).
- Trading Desk assignment outbox row 11 is Sent to `trading-desk@example.test`; smtp4dev message `22b07f8a-2108-4159-834a-afff2fb29b81` has no warnings.
- Real-time global `run-sla` queued Trading Desk overdue row 13 for exact occurrence `2026-08-09T13:00:00Z`; global processing also queued older Checker catch-up row 12.
- Rows 12/13 were sent with retry count 0. Trading Desk smtp4dev message `efd4f2c5-4e76-48f5-9ee1-40321daf6e21` has To `trading-desk@example.test`, no Cc, no warnings, correct plain/clickable request links, date-only `08/09/2026`, and no timezone suffix.
- Trading Desk schedule remains active with next daily occurrence `2026-08-10T13:00:00Z` until task completion. Older catch-up row 12 was sent as message `f3696635-35b4-4f01-ad32-30d8821242e9`.

### Moved to BDRM/Client Service

- Executed `TradingTradeExecuted` as `trader.live1` with roles `TradingDesk,TradingDeskTaxable`; correlation ID `71095ac051924dd1ad1e1daec5374f9d` returned Success without replay.
- Workflow moved `TradingDeskReview -> BdrmClientServiceReview`, version 4. Trading Desk task 5 and its SLA schedule completed at `2026-08-09T23:28:11.9427667Z`.
- Pending task 6 is `BdrmClientServiceReviewTask`, assigned to `BDRMClientService`, created at `2026-08-09T23:28:11.9427667Z`, with first SLA occurrence at `2026-08-09T23:30:11.9427667Z`.
- Assignment outbox row 14 is Sent to `bdrm-client-service@example.test`, subject `Trade Execution Pending Confirmation`.
- smtp4dev message `e121721b-40e1-4d34-af35-e20a5c206cdd` arrived with no warnings.
- BDRM context is actionable and exposes `BdrmTradeFulfilled` and `BdrmTradeNotExecutedProperly` (reason required for return to Trading Desk).
- The BDRM overdue monitor was not run. If task 6 remains pending, its first overdue occurrence is the two-minute deadline above; later occurrences use the metadata-owned daily 9:00 AM `America/New_York` schedule.

### Running services and next action

- Workflow API PID `21736` remains on `http://localhost:5058`; smtp4dev PID `3060` remains on ports `5000`/`3025`.
- Do not replay the Maker submission, Checker overdue step, Checker-to-Trading action, Trading Desk overdue step, or `TradingTradeExecuted`. BDRM task 6 remains pending; wait for the user to choose `BdrmTradeFulfilled`, `BdrmTradeNotExecutedProperly`, or to run its overdue reminder.

### Next-session prompt

```text
Continue the fresh live scenario for WF-CDT-MAKER-LIVE-20260809-190524. It is Active in BdrmClientServiceReview version 4 with pending BdrmClientServiceReviewTask 6 assigned to BDRMClientService. TradingTradeExecuted succeeded under correlation 71095ac051924dd1ad1e1daec5374f9d, closed Trading Desk task 5 and its SLA schedule, and created BDRM task 6 at 2026-08-09T23:28:11.9427667Z with first deadline 2026-08-09T23:30:11.9427667Z. Assignment outbox row 14 was sent to bdrm-client-service@example.test as smtp4dev message e121721b-40e1-4d34-af35-e20a5c206cdd without warnings. The BDRM overdue monitor has not been run. API PID 21736 and smtp4dev PID 3060 remain live. Do not replay completed steps; await BdrmTradeFulfilled, BdrmTradeNotExecutedProperly, or an explicit request to run the BDRM overdue reminder.
```

## 2026-08-09 Checker Duration SLA Minute Recurrence Implemented and Live-Tested

### Current goal

- Complete the approved generic duration-SLA recurrence behavior and prove two real Checker reminders through the live Workflow API, console processors, SQL Server, and smtp4dev.
- Preserve existing Checker schedules/outbox history and keep production cadence metadata-owned (`240` minute first deadline, `60` minute recurrence).

### Completed changes

- Added nullable `RecurrenceIntervalMinutes` to versioned SLA policies and new task schedule snapshots, with positive-value and minute/day mutual-exclusion constraints.
- Added nullable `SlaOccurrenceAtUtc` to the notification outbox and replaced date-only SLA uniqueness with exact scheduled UTC occurrence uniqueness.
- Added anchored latest-slot catch-up: a delayed scan queues one latest eligible minute slot, skips older missed slots, and advances from the scheduled occurrence rather than processing time.
- Added exact occurrence UTC ticks to SLA correlation IDs and `slaOccurrenceAtUtc` to the persisted render payload and console diagnostics.
- Configured the Portfolio Trade Checker demo policy as `DurationMinutes = 2`, `RecurrenceIntervalMinutes = 2`; production remains a metadata deployment choice using `240/60`.
- Preserved one-time duration, BDRM calendar-day duration recurrence, application-target-date recurrence, recipient snapshots, outbox retries, and task-completion cancellation.
- Closed independent review gaps by aligning `PROJECT.md` and `tests/COVERAGE_AUDIT.md` with exact occurrence identity and Checker recurrence, adding a four-context concurrent scan test, exercising the 240/60 cadence at runtime, and proving a normal Checker action completes and cancels its minute-recurring schedule.
- Applied migration `20260809220405_AddDurationSlaMinuteRecurrence` to `workflowengine`; `verify-seed` passed.
- Verified schedules 1/2 and outbox rows 1-4 were not rewritten: existing schedules retained null minute recurrence and historical rows retained null exact occurrence times and original correlations/status.

### Changed files and commits

- Design: `docs/superpowers/specs/2026-08-09-duration-sla-minute-recurrence-design.md` (`a983d54`).
- Plan: `docs/superpowers/plans/2026-08-09-duration-sla-minute-recurrence.md` (`5294d98`).
- Schema/entities/migration/tests: `6458a0d`.
- Seed, validation, and schedule snapshot tests: `f0811c3`.
- Runtime catch-up, exact occurrence payload/diagnostics, and regression tests: `40f755c`.
- Review-closure documentation and acceptance tests: `e56bf97`.

### Build and test status

- Shared solution warning-as-error build: passed with 0 warnings and 0 errors.
- Shared SQL-backed suite: 319 passed, 4 opt-in smtp4dev integration tests skipped, 0 failed.
- Workflow API warning-as-error build: passed with 0 warnings and 0 errors.
- Workflow API suite: 67 passed, 0 failed.
- Focused schema/migration tests: 38 passed.
- Focused seed/synchronization/Start tests: 24 passed.
- Focused SLA/outbox/workflow-action regression tests: 57 passed.

### Live request and JSON evidence

- Submitted `POST /v1/WorkflowStart` for business object `CDT-CHECKER-RECUR-20260809-01` as `cdt.recur.maker1` / `Maker` with initial action `SubmitForChecker`.
- API returned Success for `WF-CDT-CHECKER-RECUR-20260809-01`, moved `Draft` to `CheckerReview`, and created pending Checker task 3 with active schedule 3.
- Task/schedule first deadline: `2026-08-09T22:24:18.3776954Z`; schedule snapshot recurrence: 2 minutes.
- First wall-clock scan ran at `22:25:57Z` and queued outbox 6 for the anchored occurrence `22:24:18.3776954Z`; its JSON payload contains matching `slaOccurrenceAtUtc`, raw UTC deadline fields, date-only display `08/09/2026`, and the saved request link.
- Second scan ran at `22:26:58Z` and queued outbox 7 for `22:26:18.3776954Z`; it advanced schedule 3 to `22:28:18.3776954Z`.
- Rows 6/7 are both Sent with retry count 0 and distinct exact-time correlation IDs.
- smtp4dev messages: `ea2486d2-f758-4907-a990-fec74744d3a2` and `6481ed51-d5e7-4448-a174-997080d43e3c`; both have To `checker@example.test`, Cc `cdt.recur.manager1@example.test`, no warnings, the plain request URL, clickable HTML request link, date-only `08/09/2026`, and no timezone suffix in either body.

### Remaining tasks

- Choose whether to push the branch/create a PR or keep it local; the implementation and review closure are committed locally.
- In production deployment metadata, set Checker `DurationMinutes = 240` and `RecurrenceIntervalMinutes = 60`; schedule the console job hourly as `run-sla` followed by `process-notification-outbox`.

### Known risks

- Production hourly polling can deliver an occurrence up to one polling interval late by design; missed slots are intentionally not backfilled.
- The recurrence migration `Down` path cannot preserve multiple same-day exact occurrences under the prior date-only unique key; archive or resolve those rows before an intentional rollback.
- Live task 3 remains pending with an active recurrence schedule. No scheduler is currently running, so no additional reminder will queue unless `run-sla` is invoked; a later invocation will queue only the latest eligible slot.
- `CODEX_HANDOFF.md` contained pre-existing local edits before this task and remains unstaged to avoid mixing ownership.

### Running services

- Workflow API: PID `21736`, `http://localhost:5058`, SMTP explicitly configured for `localhost:3025`.
- smtp4dev: PID `3060`, UI `http://localhost:5000`, SMTP `localhost:3025`.

### Next-session prompt

```text
Continue in C:\projects\cdt-workflow on console-code-first-0518. Generic Checker minute recurrence is implemented in commits a983d54, 5294d98, 6458a0d, f0811c3, 40f755c, and review closure e56bf97. Final gates passed: 319 shared tests plus 67 API tests, zero failures, warning-as-error builds clean, and seed verification passed. Live instance WF-CDT-CHECKER-RECUR-20260809-01 has pending Checker task 3 and active schedule 3 using 2/2 demo metadata. Sent SLA rows 6/7 represent exact occurrences 22:24:18.3776954Z and 22:26:18.3776954Z; smtp4dev messages ea2486d2-f758-4907-a990-fec74744d3a2 and 6481ed51-d5e7-4448-a174-997080d43e3c passed link/date/recipient checks. API PID 21736 and smtp4dev PID 3060 remain live. Production metadata should use 240/60 and the hourly job runs run-sla then process-notification-outbox. Do not reset the live database; choose branch integration after reviewing final status.
```

## 2026-08-09 Fresh CDT Overdue Email Live Acceptance Passed

### Executed request

- Submitted a new Portfolio Trade request with business object `CDT-SLA-LINK-20260809-01` through `POST /v1/WorkflowStart` as `cdt.sla.maker1` / `Maker` using initial action `SubmitForChecker`.
- Supplied persisted `tradeLink` `https://example.test/cdt/CDT-SLA-LINK-20260809-01`, original Maker `cdt.sla.maker1@example.test`, and selected Cc manager `cdt.sla.manager1@example.test`.
- API returned `Success`; instance `WF-CDT-SLA-LINK-20260809-01` moved from `Draft` to `CheckerReview`, version 2, with pending `CheckerReviewTask` ID 2.

### Real-time SLA execution

- The task was created at `2026-08-09T21:19:15.345155Z` with the two-minute deadline `2026-08-09T21:21:15.345155Z`.
- Waited for the actual deadline; no `--now` override was used.
- Ran console `run-sla` at `2026-08-09T21:21:32Z`. It found one eligible schedule and queued exactly one overdue occurrence as outbox row 4.
- Read the pending row before delivery. `PayloadJson` preserved raw `dueAtUtc`/`overdueAtUtc`, added `dueDateDisplay`/`overdueDateDisplay` as `08/09/2026`, and retained the saved CDT `tradeLink`.
- Ran console `process-notification-outbox` with explicit smtp4dev configuration. It processed exactly one notification through SMTP.

### Verified delivery

- Outbox row 4 is `Sent`, retry count 0, `SentAtUtc = 2026-08-09T21:21:56.1334581Z`, with no error.
- Exact recipient snapshot and SMTP headers: To `checker@example.test`; Cc `cdt.sla.manager1@example.test`.
- smtp4dev message ID: `730de490-f9da-4a6e-ad00-628260326edf`; subject `Overdue: Workflow task overdue`; no warnings.
- Plain MIME part contains `Request link: https://example.test/cdt/CDT-SLA-LINK-20260809-01`.
- HTML MIME part contains clickable `<a href="https://example.test/cdt/CDT-SLA-LINK-20260809-01">Open request</a>`.
- Both MIME parts display task due and overdue dates as `08/09/2026`; neither contains EST, EDT, UTC, or a numeric timezone suffix after the date.
- SLA schedule 2 remains active with `NextReminderAtUtc = null`, so this one-time Checker occurrence will not queue again.

### Running services and remaining decision

- Workflow API remains PID `27904` on port `5058`; smtp4dev remains PID `3060` on ports `5000`/`3025`.
- The fresh workflow remains active in `CheckerReview`. Do not send a Checker action until the user chooses the next scenario.

### Next-session prompt

```text
Continue in C:\projects\cdt-workflow. Fresh live acceptance of the overdue email correction passed for WF-CDT-SLA-LINK-20260809-01: task 2, schedule 2, sent outbox row 4, smtp4dev message 730de490-f9da-4a6e-ad00-628260326edf. The delivered plain and HTML bodies contain the saved CDT request link, clickable Open request HTML, date-only 08/09/2026 values, and no timezone suffix. API PID 27904 and smtp4dev PID 3060 remain live. The workflow is still in CheckerReview; wait for the user's next action.
```

## 2026-08-09 Overdue Email Link and Date Correction Implemented

### Current goal

- Run a fresh, user-approved overdue workflow/task occurrence through the live API and smtp4dev stack to visually accept the corrected email.
- Preserve the existing `WF-CDT-SAVE-DRAFT-20260809-01` CheckerReview instance and sent overdue outbox row 2 until the user chooses the next scenario.

### Completed changes

- Added `dueDateDisplay` and `overdueDateDisplay` to SLA notification render context using a fixed UTC-5 offset and invariant `MM/dd/yyyy` formatting.
- Preserved `dueAtUtc` and `overdueAtUtc` as raw UTC values in the outbox payload; persisted tasks, schedules, recurrence, and occurrence selection are unchanged.
- Updated only `RouteSlaTaskOverdue` plain-text and HTML templates to use the display tokens and persisted `tradeLink`; HTML renders a clickable `Open request` anchor.
- Added regression coverage for the UTC boundary (`2030-01-15T02:00:00Z` renders as `01/14/2030`), request-link rendering, absence of timezone suffixes, raw UTC payload preservation, seed template tokens, and warning/skip behavior when `tradeLink` is missing.
- Applied the normal `init-db` metadata upsert to `workflowengine`; `verify-seed` passed.
- Read-only SQL verification found all six expected plain/HTML token checks on the active overdue route. Existing sent outbox row 2 remains `Sent`, retains its original timestamps/content, and was not replayed or rewritten.

### Changed files and commits

- `src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs`
- `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs`
- `tests/WorkflowEngine.Tests/Phase5SlaTests.cs`
- `tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs`
- `docs/superpowers/specs/2026-08-09-overdue-email-link-date-format-design.md` (`71512a4`)
- `docs/superpowers/plans/2026-08-09-overdue-email-link-date-format.md` (`797ce7c`)
- Implementation commit: `bcc0194 fix: correct overdue email link and dates`

### Build/test and seed status

- Focused pre-change baseline: 2 passed, 0 failed.
- TDD RED: 3 intended failures for missing display tokens, missing link requirement, and old raw timestamp content.
- TDD GREEN: 3 passed, 0 failed.
- Shared engine warning-as-error build: 0 warnings, 0 errors.
- Shared engine default tests: 313 passed, 4 SMTP-only tests skipped, 0 failed, 317 total.
- Workflow API warning-as-error build: 0 warnings, 0 errors.
- Workflow API tests: 67 passed, 0 skipped, 0 failed.
- `init-db`: passed; `verify-seed`: passed.
- No sample JSON or new workflow action was executed because fresh live acceptance requires explicit user approval.

### Running services

- Workflow API is live in Development mode on `http://localhost:5058`, PID `27904`, configured for `workflowengine` and smtp4dev; Swagger returned HTTP 200 and contains Start, Action, and Context routes.
- smtp4dev remains PID `3060`, listening on UI port `5000` and SMTP port `3025` with the existing mailbox.

### Remaining tasks

- Obtain explicit approval for a fresh workflow/task occurrence that legitimately creates a new overdue outbox row.
- Inspect the new smtp4dev message for the expected To/Cc snapshot, plain request link, clickable `Open request` link, date-only `MM/dd/yyyy` values, and no UTC/EST/EDT suffix.
- After live acceptance, resume the original Checker decision only when the user chooses `CheckerReturnToMaker` or `CheckerSendToTradingDesk`.

### Known risks

- Fixed UTC-5 is intentional all year and does not follow daylight-saving time.
- Existing sent outbox rows and smtp4dev messages are immutable history, so row 2 still shows the original content.

### Next-session prompt

```text
Continue in C:\projects\cdt-workflow. The overdue email fix is implemented in bcc0194: newly queued overdue mail uses fixed UTC-5 date-only MM/dd/yyyy display values and includes the persisted request link in plain text plus a clickable Open request HTML anchor. Builds, 313 engine tests, 67 API tests, init-db, verify-seed, and direct active-route SQL checks passed. API PID 27904 is on port 5058 with SMTP settings; smtp4dev PID 3060 is on ports 5000/3025. Existing outbox row 2 and workflow WF-CDT-SAVE-DRAFT-20260809-01 were not changed. Obtain user approval before creating a fresh overdue occurrence for live email acceptance.
```

## 2026-08-09 Resumed CDT Checker Decision Live Test

### Current goal

- Continue the directed CDT live API test one user-selected action at a time without changing source, resetting runtime data, replaying prior actions, or rewriting terminal outbox history.

### Resume verification

- The saved API and smtp4dev PIDs were no longer running when the session resumed; SQL data and the `workflowengine-clean.db` mailbox remained persisted.
- Restored smtp4dev against the existing clean mailbox as PID `3060`, listening on UI port `5000` and SMTP port `3025`.
- An API launch inside the sandbox reproduced `Cannot generate SSPI context` for Windows integrated SQL authentication; a read-only `verify-seed` succeeded outside the sandbox, confirming the database itself is healthy.
- Restored the Workflow API outside the sandbox as PID `29552` on `http://localhost:5058`, with explicit `workflowengine`, SMTP sender, host, port, and from-address environment settings.
- GET `WorkflowContext` as `cdt.checker1` / `Checker` succeeded for `WF-CDT-SAVE-DRAFT-20260809-01`.
- Live context remains `CheckerReview`, Active, version `2`, with pending `CheckerReviewTask` ID `1`; SLA is overdue and has no next reminder.
- Available actions are `CheckerReturnToMaker` and `CheckerSendToTradingDesk`.
- No workflow action was sent during resumption.

### Remaining tasks

- Wait for the user to choose the next Checker action.
- Print the exact endpoint, trusted headers, and optimized public JSON body before sending it.
- If sending to Trading Desk, obtain the dynamic Trading Desk role and application-owned `slaTargetDate` before execution.

### Next-session prompt

```text
Continue the directed CDT live API test in C:\projects\cdt-workflow. Instance WF-CDT-SAVE-DRAFT-20260809-01 is live in CheckerReview version 2 with pending CheckerReviewTask 1. The available actions are CheckerReturnToMaker and CheckerSendToTradingDesk. API PID 29552 is on port 5058 with explicit workflowengine/SMTP configuration; smtp4dev PID 3060 is on ports 5000/3025 using workflowengine-clean.db. Wait for the user's Checker choice and print the exact endpoint, headers, and optimized public body before sending. If the user chooses Trading Desk, first obtain the dynamic Trading Desk role and slaTargetDate.
```

## 2026-08-09 CDT Checker SLA Reminder Live Test

### Commands executed

- Ran `run-sla` at the real system time `2026-08-09T20:02:14Z` with no `--now` override against `workflowengine`.
- Ran `process-notification-outbox` with explicit SMTP sender configuration for `localhost:3025` and from address `workflow@example.test`.

### Verified result

- The monitor found one pending task with an active schedule and queued one overdue occurrence: notification outbox row 2.
- Reminder type/kind/date: `SlaTaskOverdue`, `Overdue`, `2026-08-09`.
- The reminder copied the task snapshot unchanged: To `checker@example.test`; Cc `cdt.maker1@example.test`, then `cdt.manager1@example.test`.
- The processor found one pending notification and sent it through `Smtp`.
- Outbox row 2 is `Sent`, retry count 0, `SentAtUtc = 2026-08-09T20:02:43.1606236Z`, no error.
- smtp4dev contains exactly one message: ID `d9a07ff6-6733-4171-bffb-2e0cf7a7f986`, subject `Overdue: Workflow task overdue`, from `workflow@example.test`, exact To/Cc placement as above, with no reported warning.
- The one-time Checker SLA schedule remains active with `NextReminderAtUtc = null`; it will not create another occurrence for the same task.

### Running services

- Workflow API remains live on `http://localhost:5058`, PID `7256`, with explicit SMTP configuration.
- smtp4dev remains live on UI port 5000 and SMTP port 3025, PID `25560`.

### Remaining tasks

- Wait for the user's next Checker action. Do not submit another action until instructed.

### Next-session prompt

```text
Continue the directed CDT live API test in C:\projects\cdt-workflow. Instance WF-CDT-SAVE-DRAFT-20260809-01 is in CheckerReview with pending CheckerReviewTask 1. Its overdue reminder was queued and sent to smtp4dev as message d9a07ff6-6733-4171-bffb-2e0cf7a7f986: checker@example.test To; cdt.maker1@example.test and cdt.manager1@example.test Cc. The API is PID 7256 and smtp4dev PID 25560. Wait for the user's next action and print its exact endpoint, headers, and public body before sending.
```

## 2026-08-09 CDT Submit to Checker Live API Test

### Executed input

- Endpoint: `POST http://localhost:5058/v1/WorkflowAction`.
- Actor: `cdt.maker1`, role `Maker`, display header `CDT Maker One`.
- Public body: instance `WF-CDT-SAVE-DRAFT-20260809-01`, action `SubmitForChecker`, UI-selected Cc users `cdt.maker1@example.test` and `cdt.manager1@example.test`, plus checker-assignment template data.
- The body contained no final To/Cc arrays, `toState`, SLA configuration, or caller idempotency/concurrency fields.

### Verified workflow result

- HTTP 200 and action `Success`.
- State moved `Draft` to `CheckerReview`; workflow version is 2.
- Created pending task 1, `CheckerReviewTask`, assigned to role `Checker`.
- Created active `CheckerReviewSla` schedule version 1 using `DurationFromTaskCreation`; first threshold is task creation plus two minutes.
- Task and outbox recipient snapshots match exactly: To `checker@example.test`; Cc `cdt.maker1@example.test`, then `cdt.manager1@example.test`.
- Immediate assignment outbox row 1 is `Sent` with zero retries; audits contain SaveDraft and SubmitForChecker.

### SMTP configuration finding

- smtp4dev remained at zero messages because the first foreground API process inherited checked-in `Notifications:Sender = NoOp`; the launch-profile SMTP overrides were not applied when host policy prevented `dotnet run` background launch.
- The valid workflow transition and persisted outbox row were not rewritten or replayed.
- Stopped only the NoOp API PID `28096`, then restarted the API with explicit SMTP environment variables.

### Running services

- Workflow API: `http://localhost:5058`, PID `7256`, explicit `Notifications__Sender=Smtp`, host `localhost`, port `3025`, from `workflow@example.test`, database `workflowengine`.
- smtp4dev: UI port 5000 and SMTP port 3025, PID `25560`, message count 0.

### Remaining tasks

- Wait for the user's next action. Do not reset or replay the submit action unless the user specifically wants a new SMTP assignment-message scenario.
- The Checker can now use `CheckerReturnToMaker` or `CheckerSendToTradingDesk`.

### Next-session prompt

```text
Continue the directed CDT live API test in C:\projects\cdt-workflow on console-code-first-0518. Read AGENTS.md, PROJECT.md, and the top of CODEX_HANDOFF.md. Instance WF-CDT-SAVE-DRAFT-20260809-01 is in CheckerReview, version 2, with pending CheckerReviewTask 1 and active two-minute Checker SLA. Its recipient snapshot is checker@example.test To and cdt.maker1@example.test plus cdt.manager1@example.test Cc. The immediate assignment outbox row is Sent by the earlier NoOp API, so smtp4dev has zero messages. The API is now correctly restarted with SMTP on PID 7256. Wait for the next user instruction and print the exact endpoint, headers, and public body before sending it.
```

## 2026-08-09 CDT Save Draft Live API Test

### Current goal

- Continue user-directed CDT scenarios one request at a time against the live Workflow API and `workflowengine`.

### Executed input

- Endpoint: `POST http://localhost:5058/v1/WorkflowStart`
- Actor headers: user `cdt.maker1`, role `Maker`, display name `CDT Maker One`.
- Body: workflow `PortfolioTrade`, business object `CDT-SAVE-DRAFT-20260809-01`, action `SaveDraft`, and required immutable `originalMaker` recipient input.
- No caller To/Cc arrays or server-derived workflow/SLA fields were supplied.

### Verified result

- HTTP 200 with `Success`.
- Created instance `WF-CDT-SAVE-DRAFT-20260809-01` in active `Draft`, version 1.
- Persisted `originalMaker`: `cdt.maker1`, `cdt.maker1@example.test`, `CDT Maker One`.
- Persisted counts for this instance: 0 tasks, 0 SLA schedules, 0 outbox rows, 1 audit, and 1 server-generated idempotency record.
- Audit: `SaveDraft`, `Draft` to `Draft`, actor `cdt.maker1` / `Maker`.
- smtp4dev message count remains 0.

### Running services

- Workflow API is running in the active foreground execution cell on `http://localhost:5058`, listener PID `28096`, database `workflowengine`.
- smtp4dev remains on UI port 5000 and SMTP port 3025, PID `25560`.

### Remaining tasks

- Wait for the user's next scenario instruction. Do not submit another workflow action until instructed.

### Next-session prompt

```text
Continue the directed CDT live API test in C:\projects\cdt-workflow on console-code-first-0518. Read AGENTS.md, PROJECT.md, and the top of CODEX_HANDOFF.md. Instance WF-CDT-SAVE-DRAFT-20260809-01 is active in Draft after a successful SaveDraft. It has no task, SLA, or outbox row; originalMaker is cdt.maker1 and smtp4dev has zero messages. The API is listening on port 5058 and smtp4dev on ports 5000/3025. Wait for the user's next scenario and print the exact endpoint, headers, and public body before sending it.
```

## 2026-08-09 Public Workflow API Scenario Envelope Library

### Current goal

- Provide the UI/API integration team with one executable public request pack for every distinct valid Portfolio Trade Start, transition, dynamic Trading Desk assignment, and SLA-only synchronization behavior.
- Keep `workflowengine` runtime rows and the smtp4dev mailbox unchanged until the user supplies the live scenario instructions.

### Completed changes

- Added an approved design and an implementation/GSD quick plan without changing the API DTOs, runtime, SQL schema, migrations, or seed metadata.
- Added `workflow-api/samples/scenarios/README.md` plus 18 numbered scenario subfolders.
- Every pack contains `request.json`, `request.http`, and `README.md` with trusted actor headers, prerequisites, expected state/task/SLA/notification effects, and valid next packs.
- Covered Start-only, Start SaveDraft, Start Submit, all 11 seeded transitions, all three valid Trading Desk assignment roles, and the actionless SLA no-change/reschedule cases.
- Added `WorkflowScenarioSampleTests`, which strictly deserializes every body into the live public Start/Action DTO, rejects internal/removed fields, verifies the exact 18-row matrix, and checks executable endpoints/headers.
- Preserved the three existing flat public samples.

### Changed files

- `docs/plans/2026-08-09-workflow-api-scenario-envelope-library-design.md`
- `docs/superpowers/plans/2026-08-09-workflow-api-scenario-envelope-library.md`
- `.planning/quick/260809-lb6-create-executable-public-workflow-api-en/**`
- `workflow-api/samples/scenarios/**`
- `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Models/WorkflowScenarioSampleTests.cs`
- `CODEX_HANDOFF.md`

### Build/test status

- TDD RED: the focused test failed because the scenario root README did not exist.
- TDD GREEN: focused scenario test passed, 1 passed and 0 failed.
- Workflow API warning-as-error build passed with 0 warnings and 0 errors.
- Full Workflow API suite passed: 62 passed, 2 opt-in notification-dispatch tests skipped, 0 failed, 64 total.
- Scenario tree verification found 18 directories and 55 files (root index plus three files per scenario).
- No sample JSON was sent to the API, no seed verification was required, and no database/mailbox state was changed.

### Running services

- The prior Workflow API PID `6428` was stopped because it locked Debug assemblies during the RED test run; port 5058 is currently free.
- smtp4dev PID `25560` remains listening on UI port 5000 and SMTP port 3025.

### Remaining tasks

- Finish repository diff/whitespace review, GSD quick summary/state update, and the task commits.
- Restart the Workflow API against `workflowengine` before user-directed scenarios. Two compliant hidden `Start-Process` attempts were rejected by the command policy before launch, so port 5058 remains free.
- Do not execute any scenario request until the user provides the first live-test instruction.

### Known risks

- Action packs require the server-generated ID of an instance already in the README's prerequisite state. Replace `REPLACE_WITH_PERSISTED_INSTANCE_ID` in the `.http` file before sending.
- The catalog covers distinct valid behaviors, not every combinatorial variation of optional notification template values.

### Next-session prompt

```text
Continue in C:\projects\cdt-workflow on console-code-first-0518. Read AGENTS.md, PROJECT.md, and the latest CODEX_HANDOFF.md section. The public scenario catalog is under workflow-api/samples/scenarios with 18 valid packs, each containing request.json, request.http, and README.md. Keep workflowengine and smtp4dev clean. Wait for the user's exact scenario, then execute the chosen packs one step at a time and verify API response, SQL task/SLA/outbox state, Console processing, and smtp4dev delivery before continuing.
```

Last updated: 2026-08-09T13:03:14-04:00

## 2026-08-09 Clean `workflowengine` Live-Test Environment

### Current goal

- Wait for the user to provide the exact business scenario, then execute it one step at a time through the live optimized Workflow API.
- Keep the new SQL database and empty smtp4dev mailbox clean until those instructions arrive.

### Completed changes

- Confirmed the prior application database had been removed by the user.
- Stopped only the prior repo-local Workflow API PID `17492` and smtp4dev PID `6336`.
- Created SQL database `workflowengine` through the normal EF Core `init-db` command and applied the Portfolio Trade seed metadata.
- Ran `verify-seed`; it passed.
- Preserved the old smtp4dev mailbox in `.tools/smtp4dev/data/database.db` and started smtp4dev against a separate empty mailbox file `.tools/smtp4dev/data/workflowengine-clean.db`.
- Started the Workflow API against `Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False` with the real SMTP sender targeting `localhost:3025`.

### Clean-state verification

- Applied migrations: 10.
- Seeded workflow definitions: 1.
- Seeded Portfolio Trade transitions: 11.
- Seeded notification recipient rules: 11.
- Runtime workflow rows across instances, tasks, outbox, and audit: 0.
- Individual runtime counts also verified as zero for workflow instances, tasks, notification outbox, workflow audits, and idempotency rows.
- smtp4dev message count: 0.
- OpenAPI document `http://localhost:5058/swagger/v1/swagger.json`: HTTP 200.

### Running services

- Workflow API: PID `260`, `http://localhost:5058`, database `workflowengine`.
- smtp4dev: PID `25560`, UI `http://localhost:5000`, SMTP `localhost:3025`, database file `workflowengine-clean.db`.

### Changed files

- `CODEX_HANDOFF.md` only among tracked files.
- Untracked smtp4dev runtime file `.tools/smtp4dev/data/workflowengine-clean.db` was created; the prior mailbox was not deleted or modified after the switch.

### Remaining tasks

- Receive the user's first scenario instruction.
- Before each action, state the endpoint, headers, and optimized body being sent; afterward verify API response, SQL state, and smtp4dev effect before advancing.

### Next-session prompt

```text
Continue the clean live API test in C:\projects\cdt-workflow on console-code-first-0518. Read AGENTS.md, PROJECT.md, and the top of CODEX_HANDOFF.md. The fresh SQL database is workflowengine with zero runtime rows. Workflow API PID 260 is on http://localhost:5058. smtp4dev PID 25560 is on http://localhost:5000 / localhost:3025 with zero messages in workflowengine-clean.db. Wait for and follow the user's scenario one step at a time; do not reuse the prior scenario or create an instance before instructed.
```

## 2026-08-09 Optimized API and smtp4dev Live Acceptance

### Current goal

- Let the user inspect a real optimized Workflow API run and all resulting messages through smtp4dev.
- Keep the Workflow API at `http://localhost:5058` and smtp4dev at `http://localhost:5000` / SMTP `localhost:3025` running until the user finishes reviewing them.

### Completed changes

- No application source, SQL schema, seed metadata, or public contract was changed.
- Ran the live optimized Start/Action DTOs against business object `PT-API-SMTP-20260809-A1` and instance `WF-PT-API-SMTP-20260809-A1`.
- Exercised Start `SaveDraft`, Maker submit, Checker return, Maker correction save/resubmit, Checker-to-Trading Desk with flat `slaTargetDate`, due-today and overdue SLA delivery, Trading return, a second resubmit/approval, trade execution, BDRM rejection, Trading re-execution, and BDRM fulfillment.
- Proved final recipients come from route metadata, immutable `originalMaker`, and declared `selectedCcUsers`; the UI never sent final To/Cc arrays.
- Proved To-over-Cc deduplication by selecting the original Maker again as a Checker-return Cc; the resulting message kept Maker once in To and omitted the duplicate from Cc.
- Proved minimal SaveDraft Action payload needs only `workflowInstanceId` and `actionKey`; it preserved state/version/task and sent no email.
- Proved repeated same-date SLA scanning queued no duplicate and a far-future post-completion scan queued nothing.

### Runtime acceptance evidence

- Final API context: `Completed`, version `12`, no current task, `NoSla`, no available actions.
- SQL: 10 completed tasks, 8 completed SLA schedules, 13 audits, 13 `Sent` outbox rows, zero retries, and one immutable `originalMaker` participant.
- smtp4dev baseline for the business run was 39 messages; it now contains 64 total. Exactly 13 messages match this workflow, all have plain-text and HTML bodies and no smtp4dev warnings.
- Current-workflow smtp4dev message IDs in flow order: `2582fddf-67d7-4fa0-9b64-7a93890b9c79`, `e178b319-8d41-4689-b345-088338d2a583`, `73c1a092-fdc4-4e87-8413-7224984e37f9`, `cf77f522-5a4f-41b3-a4bc-280d26e7e80f`, `8817e6f7-4c17-421f-80e2-5db138f5df93`, `c5633ec2-1209-46cd-9374-2718533d0f9f`, `40fbf3f0-a6fb-4afa-8b23-e6d049bb52d6`, `381daf7c-0c83-4d7c-9386-5a90e36f38b5`, `98ca4a79-0cdd-49b9-9454-07b96a903b3b`, `a32da224-14ff-48b4-9a77-c87c205c2806`, `7860fe0a-b1d2-47c7-90e3-2a7b3d92a9ae`, `8b46ebd9-cdf5-4f67-8d80-c2f6e4790622`, `f43127f5-5000-4201-b77b-52234845bf88`.
- The due-today row was delivered through `Smtp:RecipientOverride=operator.smtp.live@example.test` because the global outbox first encountered an older unrelated workflow containing the intentionally opaque address `manager opaque value`. MailKit rejected that old address, returned its row to `Pending` with retry diagnostics, and stopped before the current due-today row. The override safely sent both rows without changing their stored intended snapshots; the current due-today body records intended To `trading-desk@example.test`. The next-day overdue row delivered normally to `trading-desk@example.test`.

### Build/test status

- Workflow API warning-as-error build: passed with 0 warnings and 0 errors.
- `verify-seed`: passed against `cdt_workflow_api_20260527`.
- Live Tier 2 SMTP tests: initial run passed 3 and failed the SQL-backed inactive-schedule test because no test connection environment variable was set; rerun with `WORKFLOW_ENGINE_TEST_CONNECTION_STRING` passed all 4, with 0 failed/skipped.
- Full Workflow API suite after live acceptance: 66 passed, 0 failed, 0 skipped.

### Changed files

- `CODEX_HANDOFF.md` only. Existing user-owned `.tools/`, `SQMS/`, `tareget-api-integration/`, and earlier handoff changes remain preserved.

### Known risks

- Opaque addresses are intentionally accepted and stored without validation. A syntactically unusable value can fail later in MailKit, increment the complete message row's retry diagnostics, and stop the current outbox batch. `Smtp:RecipientOverride` remains the safe integration-test escape hatch.
- smtp4dev contains unrelated historical/test messages as well as the 13 current-workflow messages; filter using the instance ID or the message IDs above.

### Remaining tasks

- User reviews the optimized payload flow and 13 messages in smtp4dev.
- After review, stop API PID `17492` and smtp4dev PID `6336` if the user requests shutdown.

### Next-session prompt

```text
Continue live review in C:\projects\cdt-workflow on console-code-first-0518. Read AGENTS.md, PROJECT.md, and the top of CODEX_HANDOFF.md. Inspect workflow WF-PT-API-SMTP-20260809-A1 through GET http://localhost:5058/v1/WorkflowContext and inspect its 13 listed messages at http://localhost:5000. API PID 17492 and smtp4dev PID 6336 are intentionally running. Do not change source. Stop them only after the user finishes review.
```

## 2026-08-07 Application-Anchored Recurring SLA Implementation

### Current goal

- Application-anchored recurring SLA implementation and live acceptance are complete on `feature/application-anchored-recurring-sla`.
- Leave smtp4dev and the Workflow API running for user review on ports 5000/3025 and 5058.

### Completed changes

- Added typed `slaInput.targetDate` to core/API start and action contracts; SLA configuration fields are rejected as unmapped input.
- Added `DurationFromTaskCreation` and `ApplicationTargetDate` calculations using `TimeProvider` and independent `TimeZoneInfo` conversion for each calendar occurrence.
- Added versioned `WorkflowTaskSlaSchedule` persistence and occurrence-level outbox identity/cancellation fields with clean-schema EF migration `20260807032848_AddApplicationAnchoredRecurringSla`.
- Seeded Trading Desk review for configurable 09:00 `America/New_York`, due-today on target date, overdue target + 1 calendar day, and daily calendar recurrence. Other SLA tasks retain configured two-minute duration mode.
- Integrated schedule create/no-op/reschedule/complete behavior into the existing workflow transaction. Changed dates supersede schedules and cancel undelivered old occurrences while preserving sent history.
- Added actionless SLA synchronization through the existing `POST /v1/WorkflowAction`; no second API, state movement, workflow version increment, or workflow audit.
- Replaced task-level one-time scanning with schedule/date/kind occurrences, local-time eligibility, weekend recurrence, current-only catch-up, and completion stop behavior.
- Added post-claim inactive-schedule suppression so stale outbox rows become `Cancelled` before sender invocation.
- Fixed the pre-existing fresh-clone test harness pipe hang by disabling child MSBuild reuse and draining stdout/stderr concurrently.
- Fixed the actionless reschedule response so `actionResult.BusinessObject` is populated from persisted workflow context instead of returning empty strings.
- Updated the sample contract, `PROJECT.md`, README, API boundary, HTML handoffs, and coverage audit for the implemented behavior.

### Changed files

- Core contracts/application: `src/WorkflowEngine.Application/**`
- Persistence/SLA/notification runtime: `src/WorkflowEngine.Infrastructure/**`
- API models/services/tests: `workflow-api/**`
- Migration and seed: `src/WorkflowEngine.Infrastructure/Persistence/Migrations/**`, `Persistence/Seed/**`
- Samples/tests/docs: `samples/**`, `tests/**`, `PROJECT.md`, `README.md`, `docs/**`, this handoff.

### Build/test status

- Both solution restores passed and both warning-as-error builds passed with 0 warnings/errors.
- Full core suite: 287 passed, 4 optional SMTP skipped, 0 failed, 291 total, in 1m16s.
- Full Workflow API suite after the final response-only regression fix: 51 passed, 0 skipped/failed. Its focused red/green test first failed with the empty business object, then passed after the fix.
- Focused recurring SLA/outbox suite: 14 passed.
- Documentation/coverage/sample contract suite: 24 passed.
- EF `has-pending-model-changes`: no model changes since the latest migration.
- Opt-in SMTP suite: 4 passed, including proof that an inactive-schedule row is not delivered.
- Clean database `cdt_workflow_sla_live_20260807`: `init-db` and `verify-seed` passed without changing the existing application database.

### Live acceptance evidence

- Actual HTTP instance: `WF-PT-SLA-LIVE-20260807`, business object `PT-SLA-LIVE-20260807`.
- `CheckerSendToTradingDesk` created task 2 and schedule 2/version 1 for target `2030-01-20`; assignment email was sent immediately.
- Fast-forwarded `run-sla` sent due-today on `2030-01-20` and overdue on `2030-01-21`; a repeated Jan 21 scan queued zero duplicates.
- Jan 22 overdue occurrence 5 was deliberately left pending. Actionless `POST /v1/WorkflowAction` changed the target to `2030-01-25`, kept workflow state/version at `TradingDeskReview`/3 and audit count at 2, superseded schedule 2, cancelled occurrence 5 with `SlaScheduleSuperseded`, and created schedule 3/version 2.
- Revised due-today was sent on Jan 25. `TradingReturnToMaker` completed schedule 3; a Jan 26 scan found zero active SLA schedules and queued zero reminders.
- smtp4dev contains six acceptance messages: two assignment messages, original due-today, first overdue, revised due-today, and return-to-maker. The error log is empty.
- Review services are running against the preserved acceptance database: Workflow API `http://localhost:5058` on PID 7640; smtp4dev UI `http://localhost:5000` and SMTP 3025 on PID 10496.

### Full-path live acceptance rerun

- Actual HTTP instance `WF-PT-FULL-LIVE-20260807-01` completed this route: Maker submit, Checker return/reject, Maker resubmit, Checker approve/send to Trading Desk, Trader execute, and BDRM fulfill/approve.
- The application supplied original target `2030-03-10` on Maker submit and revised target `2030-03-15` on Maker resubmit. Duration-mode Checker schedules correctly ignored target-date authority; Checker approval forwarded the current revised date and created the Trading Desk `ApplicationTargetDate` schedule for `2030-03-15`.
- The final instance is `Completed` at version 7. All five tasks and all four SLA schedules are completed. The Trading Desk schedule persisted due-today `2030-03-15T13:00:00Z` and overdue start `2030-03-16T13:00:00Z`, reflecting 09:00 Eastern daylight time.
- Six audit rows record the exact route and six outbox rows (IDs 8-13) are `Sent` with no errors. smtp4dev received exactly six new messages, increasing from 27 to 33; API and smtp4dev error logs remain empty.
- A post-completion `run-sla --now 2030-03-17T13:00:00Z` found zero pending tasks with active SLA and queued zero reminder occurrences.

### Remaining tasks

- None for the approved SLA scope. The feature branch is ready for review/integration; runtime log files remain untracked under `.runtime/` while the API is running.

### Known risks

- The migration intentionally has no legacy SLA backfill and requires a clean latest-schema application database.
- SQL Integrated Security commands require the normal Windows token; sandbox identity produces SSPI failures.
- The original checkout has user-owned `.tools/`, `SQMS/`, `tareget-api-integration/`, and handoff changes. Feature work remains isolated in `.worktrees/application-anchored-recurring-sla`.

### Next-session prompt

```text
Review or integrate the completed application-anchored recurring SLA in C:\projects\cdt-workflow\.worktrees\application-anchored-recurring-sla on feature/application-anchored-recurring-sla. Read AGENTS.MD, PROJECT.md, and the top handoff section. Use the dedicated database cdt_workflow_sla_live_20260807 for preserved acceptance evidence; do not destructively recreate the existing application database.
```

Historical note timestamp: 2026-08-06T22:57:46.8753814-04:00

## 2026-08-06 Generic Application-Anchored SLA Design Discovery

### Current goal

- Design, but do not yet implement, a generic task SLA mode whose schedule can be anchored to a business date supplied by a calling application.
- Preserve the existing metadata-driven duration mode (`task created time + SlaPolicy.DurationMinutes`) for workflows that do not supply an application anchor.
- CDT example under discussion: assignment notification immediately when Trading Desk receives the task, an SLA reminder on the Maker-selected Target Completion Date, then one reminder every day from the following date until the task is completed.

### Completed work

- Stopped the live Workflow API process `1184` and smtp4dev process `7000` at the user's request.
- Verified ports `5058`, `5000`, and `3025` have no listening process.
- Read the current `PROJECT.md` SLA, contract, schema, notification, quality-gate, and stop-and-ask rules and inspected the implemented task creation and SLA scan paths.
- Confirmed current task deadlines are calculated only as `TimeProvider.GetUtcNow() + SlaPolicy.DurationMinutes`.
- Confirmed current `run-sla` emits only `SlaTaskOverdue`, at most once per task ever, enforced by a filtered unique index on `(WorkflowTaskId, NotificationType)`.
- Confirmed application form data remains opaque to the engine, so a generic SLA anchor must be supplied through an explicit workflow integration contract and enabled/interpreted by metadata; the engine must not parse CDT's `targetCompletionDate` field from `dataSave.payload`.
- No application source, schema, seed, sample, or test implementation was changed.

### Remaining design decisions

- Confirmed: Target Completion Date gets a due-today reminder; the next calendar date is the SLA breach/overdue start and begins daily reminders.
- Confirmed: the reminder time is 9:00 AM Eastern and must be configurable as SLA policy metadata rather than hardcoded CDT behavior.
- Confirmed: changing the application target date reschedules the currently pending task to the new date; the design must protect this update with concurrency, idempotency, and audit evidence.
- Confirmed: recurrence uses calendar days, including weekends and holidays; no business-calendar capability is part of this change.
- Confirmed: already-sent reminder rows/messages remain immutable history; a revised schedule starts a new version and can send a new due-today reminder on its revised target date.
- Confirmed: when creation or rescheduling occurs after a scheduled occurrence, the next SLA scan queues one current-state catch-up reminder immediately and never backfills every missed calendar day; normal 9:00 AM Eastern recurrence resumes afterward.
- Clarification is complete. The user approved the recommended approach: SLA policy metadata plus typed caller-supplied schedule input, versioned task schedule snapshots, occurrence-level deduplication, and a generic reschedule operation.
- Present the detailed design section by section for user approval before writing the design specification.
- Architecture and ownership section is approved and locked: the application maps its field to a generic date, policy metadata owns timing/cadence, a schedule calculator produces UTC instants, task creation snapshots the schedule, rescheduling does not move workflow state, and `run-sla` queues occurrences into the existing outbox/sender path.
- Confirmed: rescheduling cancels/suppresses old-schedule reminders that are queued but not delivered; sent rows/messages remain immutable history.
- Present and lock the policy, versioned schedule, task projection, and occurrence-level outbox data model.
- User clarified that `DurationMinutes` is application/workflow configuration and must never be supplied or changed per task. The intended location is the versioned `SlaPolicy` metadata row referenced by `WorkflowTaskDefinition`; only `targetDate` varies per task for application-date policies.
- Persistence model is approved with that correction: policy calculation/cadence metadata, versioned task schedule rows, `WorkflowTask.DueAtUtc` compatibility projection, occurrence-level outbox uniqueness, and cancellation of unsent superseded-schedule reminders.
- User rejected a separate SLA reschedule endpoint because it would make the application detect and orchestrate date changes. Revise the contract so the existing workflow call always carries the application's current target date; the engine compares it to the active schedule and decides create/no-op/reschedule in the same transaction.
- Preserve the technical boundary: the application maps and forwards its current value, but performs no comparison or SLA decision. The engine cannot observe an external application database change until an existing save/action call reaches it and must not poll application-specific storage.
- Revised same-API contract is approved and locked: every normal save/action call forwards the current target date; the engine performs create/no-op/reschedule synchronization before/with any transition. A save with no state transition may synchronize SLA only through the same endpoint.
- Present and lock the monitor occurrence algorithm, completion behavior, notification routing, and error/concurrency handling.
- Monitor/error section is approved and locked: occurrence-based scans, due-today versus overdue kinds, one catch-up only, calendar-day advancement, task-completion cancellation, stale-schedule delivery recheck, message-level retry, and externally scheduled invocation of the existing monitor.
- Present and lock backward-compatible migration and verification coverage; then the complete design can be written and committed for review.
- User rejected backward compatibility/backfill requirements; implementation may target a clean latest-schema database with fresh seed data and does not need to support legacy active SLA schedules or legacy SLA outbox deduplication.
- Local testing must finish within minutes. Use injected-time fast-forward (`run-sla --now` and fake `TimeProvider`) to exercise the real 9:00 AM Eastern and daily-calendar policy immediately; SIT/UAT use the actual configured schedule.
- All design sections are approved. The complete self-reviewed specification is committed at `36cd8f7` in `docs/superpowers/specs/2026-08-06-application-anchored-recurring-sla-design.md`.
- Await explicit user review/approval of the written specification. Only after that approval should `writing-plans` be invoked; implementation remains unstarted.
- Compare metadata/contract/storage approaches, recommend one, present the design for approval, then write an implementation plan only after design approval.
- The eventual change requires explicit approval because it changes the workflow integration contract and makes non-trivial SQL schema/index changes under `PROJECT.md` section 22.

### Known implementation pressure points

- `SlaPolicy` currently contains only `DurationMinutes` and `IsActive`.
- `WorkflowTask` stores one `DueAtUtc` but no anchor source, schedule snapshot, next reminder time, or recurrence state.
- `NotificationOutbox` currently cannot store multiple `SlaTaskOverdue` occurrences for one task because of its filtered unique index.
- `WorkflowContextResponse` currently derives `OnTrack`/`Overdue` solely from `DueAtUtc`; any distinction between `DueToday` and `Overdue` may require an approved response-contract decision.

### Next-session prompt

```text
Continue the generic application-anchored SLA design in C:\projects\cdt-workflow. Read AGENTS.MD, PROJECT.md, and this top handoff section. No source implementation has started. The Workflow API and smtp4dev were intentionally stopped. First confirm the Target Completion Date day sequence, then ask one question at a time about timezone/cutoff and rescheduling. After requirements are clear, compare 2-3 generic metadata-driven approaches and recommend one. Do not write an implementation plan or code until the user approves the design.
```

## 2026-08-06 Live API, SLA, and smtp4dev Verification

### Current goal

- Leave the latest Workflow API and local smtp4dev running so the user can review real workflow, SLA, and notification behavior interactively.
- Workflow API / Swagger: `http://localhost:5058` (process `1184`).
- smtp4dev web UI: `http://localhost:5000`; SMTP listener: `localhost:3025` (process `7000`).
- Dedicated live scenario: business object `PT-LIVE-20260806-A1`, workflow instance `WF-PT-LIVE-20260806-A1`.

### Completed changes and runtime scenarios

- No application source was changed.
- Built both .NET 10 solutions with warnings treated as errors; both passed with 0 warnings and 0 errors.
- Applied the configured SQL migrations/seed to `cdt_workflow_api_20260527`; `verify-seed` passed.
- Started the workflow through `POST /v1/WorkflowStart`, then drove actions through `POST /v1/WorkflowAction` using trusted actor headers and typed two-To/two-Cc recipient arrays.
- Exercised `SubmitForChecker`, Checker Return to Maker, Maker resubmission, Checker-to-Trading Desk, Trading Return to Maker, a second resubmission, trade execution, BDRM rejection to Trading Desk Rework, re-execution, and BDRM fulfillment.
- Ran `run-sla --now 2026-08-07T01:07:00Z` against the live Checker task; it queued one `SlaTaskOverdue` notification. The same SLA scan rerun queued 0 duplicates.
- Ran `process-notification-outbox` with the real `SmtpNotificationSender`; it delivered the SLA row to smtp4dev. A rerun found 0 pending rows.
- The workflow finished in `Completed`, version `12`, with 0 pending tasks, 10 completed tasks, 11 audit rows, 12 sent product notifications, 0 non-sent rows, and 0 retries.
- All 12 product messages have two To recipients, two Cc recipients, plain-text and HTML parts, and zero smtp4dev MIME warnings.
- Three additional `Phase 5 SMTP ...` messages were generated by the opt-in SMTP integration tests; the 12 real scenario messages immediately below them in smtp4dev cover assigned, overdue, returned, execution, rework, confirmation, and completion templates.
- The first smtp4dev process was launched under the restricted sandbox token. SMTP receipt and `api/messages` worked, but the UI's `api/Server` initialization failed with `UnauthorizedAccessException` while probing `%APPDATA%\smtp4dev` and ASP.NET data-protection paths. smtp4dev was restarted under the normal Windows token with the same SQLite database. All 18 messages were preserved, `api/Server` now succeeds, and the restarted Server Log contains 0 errors.

### Build and test status

- `dotnet restore src\WorkflowEngine.sln`: passed.
- `dotnet build src\WorkflowEngine.sln --no-restore -warnaserror`: passed, 0 warnings/errors.
- `dotnet restore workflow-api\WorkflowEngine.WorkflowApi.slnx`: passed.
- `dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx --no-restore -warnaserror`: passed, 0 warnings/errors.
- Opt-in `Category=Smtp` tests against the running smtp4dev: 3 passed, 0 failed, 0 skipped.
- Targeted Workflow API start/action/notification-dispatcher tests: 33 passed, 0 failed, 0 skipped.
- Fresh endpoint verification: Swagger HTTP 200, smtp4dev UI HTTP 200, and Workflow Context reports `Completed`, `NoSla`, no pending task, and no available actions.

### Changed files and known risks

- Updated `CODEX_HANDOFF.md` for this live session.
- Runtime-only untracked logs: `.tools/workflow-api.out.log` and `.tools/workflow-api.err.log` while the API remains running.
- SQL Integrated Security fails under the sandbox token with `Cannot generate SSPI context`; SQL-backed commands must run with the normal Windows token. This is an execution-token boundary, not a SQL service or workflow-code failure.
- Do not stop processes `1184` or `7000` until the user finishes reviewing Swagger and smtp4dev. When approved, stop them with `Stop-Process -Id 1184,7000`.

### Remaining tasks

- User reviews the 12 real scenario messages in smtp4dev, including To/Cc headers, plain/HTML alternatives, rendered links/content, and subject lines.
- User may inspect `WF-PT-LIVE-20260806-A1` through `GET /v1/WorkflowContext` in Swagger using Maker headers.
- After review, stop the API and smtp4dev processes and decide whether to retain or remove the two runtime log files.

### Next-session prompt

```text
Continue the live verification in C:\projects\cdt-workflow. Read AGENTS.MD, PROJECT.md, and the top of CODEX_HANDOFF.md. Workflow API/Swagger is expected at http://localhost:5058 (PID 1184) and smtp4dev at http://localhost:5000 / SMTP localhost:3025 (PID 7000). Review the 12 product messages for WF-PT-LIVE-20260806-A1; the newest three `Phase 5 SMTP ...` rows are integration-test messages. The live workflow is Completed at version 12 with 12 sent notifications and zero retries. Do not modify source. When the user confirms review is finished, stop PIDs 1184 and 7000 and report the runtime-log cleanup choice.
```

## 2026-08-06 Session Pause Status

### Active branch and remote state

- Active checkout: `C:\projects\cdt-workflow`
- Active branch: `console-code-first-0518`
- Latest pushed product/bundle commit: `92c42de chore: update portable repository bundle`.
- This pause handoff is committed locally after `92c42de`; unless separately pushed, the active branch will be one handoff-only commit ahead of `origin/console-code-first-0518`.
- The completed feature worktree still exists at `.worktrees/multiple-notification-recipients` on `feature/multiple-notification-recipients` at `f60f40e`; its commits are already merged into the active branch, so it is retained only as an optional cleanup item.

### Work completed today

- Completed and verified typed multiple-`To`/multiple-`Cc` recipient support across workflow start/action contracts, task and outbox snapshots, SLA reminder processing, console inspection, SMTP delivery, and Workflow API dispatch.
- Preserved one outbox row and one message-level retry lifecycle for all recipients; per-recipient delivery tracking was intentionally not added.
- Kept recipient selection and email-address validation in CDT/SQMS/calling applications. The generic engine preserves the supplied ordered arrays and does not normalize, deduplicate, resolve, or validate them.
- Merged `feature/multiple-notification-recipients` into `console-code-first-0518` as `f55470d merge: multiple notification recipients`.
- Validated `restore-repo.ps1`: PowerShell syntax passed, 312 files restored, and SHA-256 `f0582eb80a27b08ab462d443ba9613959789ff60eee644839ea1a66d00827968` matched.
- Committed and pushed the bundle as `92c42de chore: update portable repository bundle`.
- Post-merge engine and Workflow API builds passed with 0 warnings and 0 errors.

### SQMS integration status

- SQMS integration is **not implemented yet**. There are no SQMS-specific engine code, seed, migration, contract, sample, or test changes.
- All five source files are present under the untracked `SQMS/` folder and were previously reviewed as business-rule evidence.
- The current source-backed direction is two metadata-driven workflow definitions, `SqmsOpportunity` and `SqmsBid`, plus a thin coordinator for timed close, submitted-bid classification, aggregation, and normal `System` workflow actions.
- The existing SQMS application remains responsible for opportunity/bid business records, parent-child queries, field validation, contextual authorization, Seller delegation, allocation-state selection, and recipient validation.
- The generic engine remains responsible for metadata transitions, context, tasks/SLA, outbox, audit, idempotency, and concurrency.
- The detailed Opportunity/Bid transition flow still needs explicit user review/confirmation before an SQMS implementation plan or code work begins.
- The multiple-recipient work completed today is a generic prerequisite SQMS can use later; it is not the SQMS integration itself.

### Verification and workspace state at pause

- Latest verified feature suites before merge: engine 252 passed, 3 smtp4dev-gated skipped; SQL-enabled Workflow API 50 passed.
- Latest post-merge builds: both solutions passed with 0 warnings/errors.
- No `dotnet` or `testhost` process is running at pause.
- Local uncommitted/untracked material to preserve: `SQMS/` and `tareget-api-integration/`.
- Known separate dependency issue: normal audited Workflow API restore reports transitive `Microsoft.OpenApi 2.4.1` advisory `NU1903` through `Swashbuckle.AspNetCore 10.1.7`.

### Next-session prompt

```text
Continue in C:\projects\cdt-workflow on branch console-code-first-0518. Read AGENTS.MD and PROJECT.md first, then CODEX_HANDOFF.md and .planning/.continue-here.md. The multiple-To/multiple-Cc feature and base64 bundle are merged and pushed through 92c42de; a later local commit may contain only this pause handoff. Do not redo the recipient feature. SQMS-specific integration has not started. Review the source-derived SqmsOpportunity/SqmsBid transition flow and thin-coordinator boundary recorded in CODEX_HANDOFF.md with the user. Do not create SQMS code, seeds, migrations, contracts, samples, tests, or an implementation plan until the user explicitly confirms the detailed flow and authorizes implementation. Preserve the untracked SQMS/ and tareget-api-integration/ folders.
```

## 2026-08-05 Multiple-Recipient Implementation Status

### Current goal

The approved typed multiple-To/multiple-Cc notification design has now been implemented and verified. The work is isolated from the original checkout on:

- Branch: `feature/multiple-notification-recipients`
- Worktree: `C:\projects\cdt-workflow\.worktrees\multiple-notification-recipients`
- GSD summary: `.planning/quick/260803-vp1-implement-typed-multiple-to-and-cc-notif/260803-vp1-SUMMARY.md`

The original `console-code-first-0518` checkout still contains the user's unrelated `CODEX_HANDOFF.md` edits and untracked `SQMS/` and `tareget-api-integration/` material. Do not discard or overwrite those changes when integrating the feature branch.

### Completed changes

- Added typed ordered `notificationRecipients.toEmailAddresses` and `ccEmailAddresses` arrays to workflow start/action contracts and the canonical idempotency hash.
- Added required `NotificationRecipientsJson` snapshots to `WorkflowTask` and `NotificationOutbox` with migration `20260803120000_AddNotificationRecipientSnapshots`.
- Stored one exact snapshot on the created task and initial outbox row; `run-sla` copies the task snapshot unchanged into its reminder row.
- Removed runtime singular/delimited/payload/SMTP-role-map recipient fallback. CDT/SQMS own address selection and validation.
- Updated MailKit delivery to add every To/Cc address to one message with one outbox status/retry lifecycle. `Smtp:RecipientOverride` suppresses original recipients and records intended lists diagnostically.
- Updated Workflow API models/services/dispatcher, console inspection, samples 02-10, PROJECT.md, README/context, target/API handoffs, migration deployment guidance, and regression tests.
- Fixed explicit-null recipient arrays so malformed caller shapes reach message-level retry rather than crashing workflow start.
- Fixed the FreshClone test harness to build into isolated temporary artifacts instead of overwriting DLLs loaded by Windows testhost.

### Changed files and commits

The implementation changes 67 files. Commits after the plan commit `dc0c8fb`:

```text
8089ffd feat: support multiple notification recipients
ce0cca1 test: strengthen recipient snapshot diagnostics
db46e0c feat: expose multiple recipients through API
4bc34e4 test: cover API notification dispatch
790569c fix: preserve malformed recipient snapshots on start
bd106a0 test: correct recipient integration assertions
172b8f3 test: isolate fresh clone build artifacts
f60f40e docs: complete multiple recipient quick task
```

### Build and test status

- `dotnet restore src\WorkflowEngine.sln -m:1`: passed.
- `dotnet build src\WorkflowEngine.sln -m:1 --no-restore`: passed, 0 warnings/errors.
- `dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 --no-restore`: passed, 0 warnings/errors.
- Full SQL-backed engine suite: 252 passed, 0 failed, 3 skipped smtp4dev-gated tests.
- SQL-enabled Workflow API suite: 50 passed, 0 failed, 0 skipped.
- Both formerly failing SQL recipient tests and both return-branch recipient assertions pass.
- Normal Workflow API restore reports existing transitive `Microsoft.OpenApi 2.4.1` advisory `NU1903` through `Swashbuckle.AspNetCore 10.1.7`. This feature added no packages; dependency remediation remains separate work.

### Seed and sample verification

Used dedicated database `cdt_workflow_multiple_recipient_verify`, leaving the existing application database unchanged:

- `init-db`: passed.
- `verify-seed`: passed.
- Started `WF-PT-1001` and executed `samples\workflow-actions\02-submit-for-checker.json`: passed.
- Initial `WorkflowTaskAssigned` row persisted two ordered To and two ordered Cc addresses.
- `run-sla --now "2030-01-15T16:00:00Z"` queued one `SlaTaskOverdue` row with the identical task snapshot.
- `process-notification-outbox` with `Notifications__Sender=NoOp` processed both messages.
- Final inspection showed both rows `Sent` with the same ordered recipient arrays.

### Remaining tasks and known risks

- Choose how to integrate `feature/multiple-notification-recipients` into `console-code-first-0518` without disturbing the original checkout's user changes.
- Remediate the transitive `Microsoft.OpenApi 2.4.1` advisory separately if a clean audited Workflow API restore is required.
- Run the three opt-in `Category=Smtp` tests only when the local smtp4dev gate is enabled; automated MailKit construction, override, and malformed-address retry tests already pass.
- Before deploying the target API, apply the new EF migration; it intentionally deletes disposable `NotificationOutbox` and `WorkflowTask` runtime rows as previously approved.

### Next-session prompt

```text
Continue in C:\projects\cdt-workflow. Read AGENTS.MD and PROJECT.md first, then this handoff. The typed multiple-To/multiple-Cc notification implementation is complete on feature/multiple-notification-recipients in .worktrees/multiple-notification-recipients, with summary f60f40e. Engine tests are 252 passed/3 smtp4dev-gated skipped; SQL-enabled API tests are 50 passed. Decide how to integrate the feature branch while preserving the original checkout's modified CODEX_HANDOFF.md and untracked SQMS/ and tareget-api-integration/ content. Do not discard those user changes. The only verification issue is the pre-existing transitive Microsoft.OpenApi 2.4.1 NU1903 on normal Workflow API restore.
```

## Current Goal

Target API integration is complete. The overall objective is to integrate `SQMS` behavior into the existing generic workflow engine, adding only the thin layers genuinely needed around it. SQMS is not a new engine or standalone platform. No SQMS implementation is approved yet.

Approved generic notification recipient design:

- The approved specification is `docs/superpowers/specs/2026-08-03-multiple-notification-recipients-design.md`.
- CDT, SQMS, and future calling applications supply and validate concrete recipient lists.
- One notification supports multiple `To` and multiple `Cc` addresses in a single outbox message.
- The engine stores the exact recipient snapshot on the created task and reuses it for the initial notification and later SLA reminder.
- The SLA monitor does not refresh recipients from CDT, SQMS, a directory, or another external API.
- The engine does not validate, normalize, deduplicate, resolve, or look up recipient addresses.
- Per-recipient delivery tracking is not required; delivery and retry status remain message-level.
- No backward compatibility or legacy runtime-data migration is required for singular recipients, delimited CC strings, or SMTP role-map resolution.
- Existing notification and in-flight task data may be discarded when the future implementation changes the contract/schema.
- The design was subsequently authorized, implemented, and verified on `feature/multiple-notification-recipients`; the status section above supersedes the earlier design-only gate.

Current SQMS state:

- All five current files under `SQMS/` were reread on 2026-07-29.
- `SQMS/Opportunity status.txt`, `SQMS/BidWorkflow.cs.txt`, and `SQMS/Bidstatus change.txt` are business-rule evidence for the discovery.
- `SQMS/Advantage data info.txt` and `SQMS/Opportunity setup.txt` remain explicitly excluded from workflow scope.
- `docs/sqms-workflow-questionnaire.html` is a discovery reference, not a requirement that the user answer all 33 questions. On 2026-07-29 the user clarified that the supplied SharePoint source and status flow should answer the design wherever they provide evidence.
- Derive the workflow from those sources first. Ask only about a contradiction or a rule that materially changes the transition matrix and is genuinely absent from the supplied evidence.
- Do not make code, seed, migration, contract, or implementation-plan changes until the source-derived transition matrices are reviewed and approved.
- Prefer an integration-only slice: SQMS metadata definitions, existing generic API usage, scheduled coordination where the legacy behavior is time-driven, samples, and tests.
- Add or generalize engine capabilities only when a source-backed SQMS requirement cannot be expressed through the existing generic metadata and execution contracts. Any such change must remain workflow-generic.
- Keep SQMS business records, field validation, record ownership, and opportunity-to-bid relationships in the existing application unless the supplied behavior proves the workflow engine must own something.
- Do not create SQMS-specific branches inside the reusable transition engine.

Current source classification:

- The files are legacy SharePoint/batch snippets with syntax and OCR defects. Treat them as business-rule evidence, not copyable implementation.
- `Opportunity status.txt` is the strongest status source. It covers the Pending Seller Approval notification path, open-to-bidding notifications/reminders, automatic close to bidding, and automatic bid classification to `NOT ALLOCATED` or `IPG REVIEW`.
- `BidWorkflow.cs.txt` performs bid/person synchronization and aggregate calculations; it is background processing rather than a user workflow transition.
- `Bidstatus change.txt` sends bid-disposition email and refreshes opportunity bid statistics. It does not establish the full authorization, reversibility, or opportunity-allocation rules.
- Scheduled reminders, automatic close, classification, and aggregation must remain distinct from human actions.
- The current reusable engine can support another workflow definition through metadata, but the current seed and verification services are still PortfolioTrade-specific.

Current discussion state:

- Confirmed integration architecture: add two metadata-driven workflow definitions, `SqmsOpportunity` and `SqmsBid`, to the existing generic engine, plus a thin SQMS coordinator for time-driven parent/child processing.
- The generic engine continues to own transition resolution, workflow context, tasks/SLA, notification outbox, audit, idempotency, and concurrency. The coordinator uses normal workflow actions; it does not update workflow state directly.
- The existing application remains authoritative for SQMS opportunity/bid records, parent-child lookup, contextual MFO/IPG/Seller authorization, field validation, and form behavior.
- Allocation-state selection is application/IPG-owned. The application validates its allocation data and submits the appropriate metadata action (`MarkAwaitingAllocation`, `MarkPartiallyAllocated`, or `MarkNotFulfilled`); neither the generic engine nor the SQMS coordinator calculates this outcome.
- The committed questionnaire over-expanded the discovery into 33 questions. Do not walk the user through it as an interview.
- Build a compact, source-derived Opportunity and Bid flow from the supplied status information and legacy code.
- Resolve project-level defaults from `PROJECT.md` and treat unsupported optional capabilities as out of scope rather than asking about them.
- Map each SQMS behavior onto an existing generic engine capability: definition metadata, start, context, execute action, task/SLA, notification outbox, audit, or scheduled coordinator.
- The architecture and ownership split looked good to the user. The detailed flow below was then presented for confirmation. No code changes are authorized yet.

## SQMS Flow Presented for Confirmation

### Opportunity workflow

```text
Draft
  -- MFO / SubmitOpportunity --> Submitted

Submitted
  -- IPG / ReturnToDraft --> Draft
  -- IPG / MarkPending --> Pending

Pending
  -- IPG / OpenBidding --> OpenToBidding

OpenToBidding
  -- System / CloseBidding after 15 calendar days --> ClosedToBidding

ClosedToBidding
  -- IPG / BeginSellerApproval --> PendingSellerApprovalIpg

PendingSellerApprovalIpg
  -- IPG via existing app / MarkAwaitingAllocation --> AwaitingAllocation
  -- IPG via existing app / MarkPartiallyAllocated --> PartiallyAllocated
  -- IPG via existing app / MarkNotFulfilled --> NotFulfilled

AwaitingAllocation, PartiallyAllocated, or NotFulfilled
  -- IPG / CompleteOpportunity --> Completed
```

- The existing application validates SQMS fields and sends only `actionKey`; metadata resolves the destination state.
- `ReturnToDraft` completes the IPG task, creates an MFO correction task, and queues a notification. Resubmission returns the opportunity to `Submitted`.
- The existing application/IPG owns the allocation outcome. Neither the workflow engine nor the SQMS coordinator calculates whether the opportunity is awaiting allocation, partially allocated, or not fulfilled.

### Bid workflow

```text
Draft
  -- MFO / SaveBid --> Draft
  -- MFO / SubmitBid --> Submitted

Submitted
  -- System during parent close / MarkNotAllocated --> NotAllocated
  -- System during parent close / MarkIpgReview --> IpgReview

IpgReview
  -- IPG / SendToSeller --> UnderReviewBySeller

UnderReviewBySeller
  -- Seller / AcceptBid --> Accepted
  -- Seller / PartiallyAcceptBid --> PartiallyAccepted
  -- Seller / RejectBid --> NotAccepted
```

- Each bid is an independent workflow instance linked to its opportunity through the existing application.
- Bid edits do not change workflow state. The submitting MFO/team can edit while the parent opportunity is open.
- When the parent closes, unsubmitted `Draft` bids stay `Draft`, become read-only in the existing application, and are excluded from automatic classification.
- Seller disposition is independent per bid. The existing application validates disposition data such as a partial-acceptance amount before invoking the action.

### Automatic close coordinator

1. Find `OpenToBidding` opportunities whose 15-calendar-day deadline has passed.
2. Read the application-owned opportunity and child-bid data.
3. Validate the committed amount, bid amounts, and classifications before moving workflow state.
4. Execute `CloseBidding` as `System` through the normal workflow action path.
5. Classify only `Submitted` bids.
6. Sum fair-value and premium bid amounts.
7. If that total is strictly greater than the committed sale amount, execute `MarkNotAllocated` for discount bids.
8. Execute `MarkIpgReview` for every submitted bid not marked `NotAllocated`.
9. Leave `Draft` bids unchanged.
10. Queue the close summary through the notification outbox.

The coordinator must use deterministic idempotency keys and normal workflow actions. It must never update workflow state tables directly. If the parent close succeeds and some child actions fail, the opportunity remains closed and a retry processes only unfinished child actions.

### Notifications and ownership

- Action-created notifications are persisted to the existing outbox; the coordinator does not send email directly.
- The original open-to-bidding notice and reminder are scheduled notifications, not workflow transitions.
- `BeginSellerApproval` queues the Seller notification. Seller disposition queues the bid-owner/team notification.
- The existing engine owns transition resolution, context, tasks/SLA, outbox, audit, idempotency, and concurrency.
- The existing application owns SQMS records, parent-child lookup, form behavior, field validation, contextual authorization, Seller delegation, and allocation-state selection.
- The thin SQMS coordinator owns only timed close/classification orchestration and invokes metadata-defined `System` actions.
- `SQMS/Advantage data info.txt` and `SQMS/Opportunity setup.txt` remain excluded.

### Current gate

- No material business-rule question remains identified from the supplied sources and decisions.
- The detailed flow above is awaiting explicit confirmation after being explained to the user.
- Do not write a design spec, implementation plan, seed data, migrations, contracts, tests, or code until the user confirms the flow and authorizes the next artifact.

Boundary for the next session:

- Keep SQMS metadata-driven.
- Do not hardcode SQMS business behavior inside the reusable workflow engine.
- Separate true user workflow steps/actions from scheduled jobs such as daily notifications or automatic close-to-bidding processing.

## Required Reading

1. `AGENTS.MD`
2. `PROJECT.md`
3. `context.md`
4. `CODEX_HANDOFF.md`
5. `NEXT_CODEX_PROMPT.md`
6. `SQMS/`
7. `docs/sqms-workflow-questionnaire.html`
8. `workflow-api/WORKFLOW_API_BOUNDARIES.md` if API contract questions come up
9. `TODO_WORKFLOW_API.md` when continuing API migration or operational follow-up

## Active Repo Shape

- `src/WorkflowEngine.sln`: reusable workflow engine, infrastructure, console/demo host, EF migrations, seed data, SLA, notification outbox, and SMTP sender.
- `workflow-api/WorkflowEngine.WorkflowApi.slnx`: active ASP.NET Core workflow API host.
- `tests/WorkflowEngine.Tests`: shared engine/infrastructure tests.
- `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests`: active API host tests.
- `samples/workflow-actions`: Portfolio Trade sample action JSON.
- `docs/superpowers`: active design and implementation plans/specs.
- `.tools/smtp4dev`: retained local SMTP test tool folder.
- `SQMS/`: currently untracked legacy SQMS reference/source material; all five current files were reviewed on 2026-07-29.
- `docs/sqms-workflow-questionnaire.html`: committed requirements-discovery questionnaire and current decision surface.

The old tracked `api/` host has been removed from the working tree by user approval. Do not bring it back unless the user explicitly asks.

## Completed Cleanup

- Removed local/generated folders unrelated to the current source shape: `.dotnet/`, `.dotnet_home/`, `.nuget/`, `.templateengine/`, `.tmp/`, `.vs/`, `.\~`, and `console-dbfirst-clean/`.
- Preserved `.tools/smtp4dev`.
- Removed the old tracked `api/` host.
- Removed stale generated/historical docs that described the deleted host:
  - `docs/api-conversion/`
  - `docs/workflow-map/`
  - `docs/workflow-visualization.html`
  - `docs/console-to-api-integration-analysis.html`
- Removed stale root/extracted-copy artifacts:
  - `FULL_PROJECT_REVIEW.html`
  - `docs/BEGINNERS_GUIDE.md`
  - `cdt-src-bundle.ps1`
  - `cdt-src-bundle.sh`
  - `restore-repo.ps1`
- Updated `README.md` to describe the current `src/` plus `workflow-api/` shape and current local verification DB.
- Updated `PROJECT.md` to remove stale notification-delivery wording, align the local SQL database name, and match the current SMTP/outbox implementation shape.
- Rewrote `CODEX_HANDOFF.md`, `NEXT_CODEX_PROMPT.md`, and `context.md` around the current shape.
- Added `docs/README.md` to describe the remaining documentation surface.

## Beginner Handoff Guide

Added `docs/beginners-api-console-handoff.html` as the single-file beginner handoff requested for:

- current API and console project layout
- local console/API smoke-test commands
- workflow-only API request/response shapes
- target API integration steps
- action-created notification dispatch
- Task Scheduler setup for `run-sla` and `process-notification-outbox`
- database/migration and SMTP configuration notes

The guide intentionally distinguishes the local `workflow-api/` reference host from the external target API so future work does not drift back to the removed `api/` host.

## Latest Completed Changes

### SQMS discovery resumed

No workflow implementation code was changed for SQMS.

The current untracked SQMS source folder contains:

```text
SQMS/Advantage data info.txt
SQMS/Bidstatus change.txt
SQMS/BidWorkflow.cs.txt
SQMS/Opportunity setup.txt
SQMS/Opportunity status.txt
```

The full source pass is complete. The questionnaire is now a reference checklist only. Derive the flow from the supplied statuses and code, apply repository defaults, and surface only material rules that cannot be established from either source.

### Frontend workflow API handoff

Added `docs/frontend-workflow-api-handoff.html` as a frontend-focused handoff for the current reference API in `workflow-api/WorkflowEngine.WorkflowApi.slnx`.

The guide documents:

- `POST /v1/WorkflowStart`, `GET /v1/WorkflowContext`, and `POST /v1/WorkflowAction`.
- Required trusted actor headers.
- Current API JSON casing: lower-case wrapper properties with PascalCase model fields.
- Input and output parameter tables.
- The approved full happy path: Maker submit, Checker reject, Maker resubmit, Checker approve, Trading Desk reject, Maker save draft, Maker submit again, Checker approve, Trading Desk approve, BDRM approve/complete.
- The reference API boundary that `WorkflowAction` is workflow-only and does not save form payload data.

Updated `docs/README.md` so the new frontend handoff is discoverable.

### Per-record SMTP recipient resolution

The Maker recipient changes per business record. The code supports that without adding a SQL user table or changing public request DTOs:

- `SmtpNotificationSender` resolves recipients in this order: `Smtp:RecipientOverride`, then payload `recipientEmail`, then `Smtp:RoleEmailMap:{RecipientRoleKey}`.
- `SmtpNotificationSender` adds SMTP CC recipients from payload `ccEmailAddresses`, split by comma or semicolon.
- Override-mode bodies preserve `OriginalRecipientRoleKey`, `OriginalRecipientEmail`, and `OriginalCcEmailAddresses` when available.
- `Smtp:RecipientOverride` suppresses real CC delivery during smoke tests.
- Return-to-Maker persistence is covered by `WorkflowExecutionValidationTests.ReturnToMakerPersistsRecipientEmailFromNotificationContext`.
- SMTP recipient and CC behavior is covered by `SmtpNotificationSenderTests`.
- SLA CC persistence is covered by `Phase5SlaTests.RunSlaQueuesOnlyOverduePendingTasksWithoutExistingOverdueNotification`.
- `ConsoleNotificationSenderTests` and `tests/COVERAGE_AUDIT.md` were updated because PROJECT.md section 20 coverage audit is enforced by tests.

The target API should populate `workflow.notificationContext.recipientEmail` for Return-to-Maker and any other business-record-specific recipient. If the UI supplies copied recipients, the target API should pass them through as `workflow.notificationContext.ccEmailAddresses` so action-created and SLA overdue emails share the same CC list.

### HTML notification email rendering

The email format change is implemented in schema-backed metadata, not hardcoded Portfolio Trade logic:

- `NotificationRoute` now has `HtmlBodyTemplate`.
- `NotificationOutbox` now has rendered `HtmlBody`.
- Migration `20260620130000_AddHtmlNotificationBodies` adds both columns under schema `dbo`.
- The renderer has `RenderHtml(...)`, which HTML-escapes token values while preserving template-owned markup.
- Seeded notification templates include plain text and HTML versions for action-created and SLA overdue routes.
- Important UI-provided values are wrapped with `<strong>...</strong>` in seeded HTML templates, and `tradeLink` renders as `<a href="...">Open trade</a>`.
- `SmtpNotificationSender` sends `multipart/alternative` email when `HtmlBody` is present, with text/plain and text/html parts.
- Override-mode SMTP still suppresses real CC delivery and records original recipient/CC metadata in the test email body.

Pushed commits:

- `95c0d4a feat: support per-notification SMTP recipients`
- `cb0d65b test: verify per-record SMTP recipient with smtp4dev`

### Documentation updates

`PROJECT.md`, `README.md`, `docs/beginners-api-console-handoff.html`, `context.md`, and this file now document the recipient resolution order and target API handoff guidance.

Added `docs/target-api-notification-changes-handoff.html` as the precise target API integration checklist for the last two notification changes: per-record SMTP recipients/copied recipients and persisted HTML notification bodies. It lists target API files to update, corresponding source files in this repo, SQL column changes, request DTO shape, dispatch pattern, SMTP configuration, and verification steps.

### API JSON body and console real-usage handoff

Added `docs/api-json-bodies-and-console-real-usage.html` as the current one-file reference for:

- all public workflow API JSON body shapes for `POST /v1/WorkflowStart` and `POST /v1/WorkflowAction`;
- the no-body shape for `GET /v1/WorkflowContext`;
- trusted actor headers;
- the Portfolio Trade sample action flow and dynamic trading desk role keys;
- action-specific `notificationContext` examples;
- fields the target API must not send, including `toState`, form payload, UI echo fields, expected state/task/version guards, and generated IDs;
- console commands classified as real operational use, support diagnostics, or demo-only.

Updated `docs/README.md` so the new API JSON body handoff is discoverable.

### Restore bundle

`restore-repo.ps1` is the portable base64 PowerShell bundle for target-api handoff. It restores the repository source/handoff content without `.git`, `bin`, `obj`, `.tools`, local caches, logs, or the bundle script itself.

## Notification Template Feature State

Latest feature commit before cleanup: `a0e5640 docs: record notification template verification`.

The generic notification-template feature is implemented and verified:

- Notification subject/body templates live in SQL metadata through `NotificationRoute`.
- Portfolio Trade/Directed Trade wording is seed/sample metadata only.
- The reusable engine stays generic and renders `{{tokenName}}` placeholders from caller-provided notification context.
- Workflow action and workflow start API requests accept optional `notificationContext`.
- SLA overdue notifications reuse the route/context snapshotted on the created task and prefix the subject with metadata, currently `Overdue: `.
- `returnReason` was removed from all seeded templates.
- SMTP sends persisted outbox body content.

## Current Operational Notes

- Local API and console testing use:

```text
Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False
```

- Trusted local actor headers for the workflow API remain:

```text
X-Workflow-UserId
X-Workflow-Roles
X-Workflow-DisplayName
```

- `POST /v1/WorkflowAction` commits workflow/task/audit/outbox data before SMTP delivery. If delivery fails, the retry path is `process-notification-outbox`.
- Normal action notifications are API-owned: after the workflow action execution returns success and queues notification rows, the target API should dispatch the rows created by that action.
- The action dispatcher should select by `WorkflowInstanceId` plus `CorrelationId`, matching the existing `WorkflowActionNotificationDispatcher` pattern in `workflow-api/src/WorkflowEngine.WorkflowApi/Services/`.
- The hosted/scheduled SLA service should call `IRunSlaService.RunAsync(...)` against the workflow DB. It should not call an external API to rebuild overdue email context; the task snapshot carries the notification route and context.
- Task Scheduler is available on the target machine. Preferred processor shape:

```powershell
dotnet .\WorkflowEngine.Console.dll run-sla
dotnet .\WorkflowEngine.Console.dll process-notification-outbox
```

- Keep `RecipientOverride` configured during live SMTP smoke testing so real users do not receive test email.

## Current Worktree Expectations

Expected local state after this pause:

- `SQMS/` may be untracked while the user is adding remaining source files.
- No SQMS implementation edits should exist yet.
- Handoff/context files may contain this SQMS pause state.

Run before further edits:

```powershell
git status --short --untracked-files=normal
git diff --stat
git diff --name-only
```

## Verification Status

Latest bundle and push-prep verification on 2026-06-22:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\restore-repo.ps1 -DestinationPath .\.bundle-test\restore-20260622-225258 -Force
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -warnaserror -m:1 /p:UseAppHost=false
git diff --check
dotnet build src\WorkflowEngine.sln -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false --no-restore
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter "Category!=FreshClone" -m:1 /p:UseAppHost=false --no-restore
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- verify-seed
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- start-workflow PortfolioTrade PT-1001 --user maker1 --reset
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- execute-json .\samples\workflow-actions\02-submit-for-checker.json
```

Results:

- `restore-repo.ps1` regenerated as a base64 ZIP PowerShell restore bundle.
- Bundle selected 294 files and excluded `.git`, `.tools`, `.bundle-test`, `.bundle-work`, build outputs, caches, and the previous restore script.
- Bundle SHA-256 verified: `d6ca44471252e6af0a7a6753dc7f8a6521531ce61795fe0fe11c118b0005d85b`.
- Restored file count matched: 294 files.
- Required target API handoff/source files were present in the restored tree; forbidden paths were absent.
- Restored `workflow-api\WorkflowEngine.WorkflowApi.slnx` warnings-as-errors build passed with 0 warnings/errors.
- Main tree `git diff --check` was clean except Git line-ending warnings.
- Main tree shared solution warnings-as-errors build passed with 0 warnings/errors.
- Main tree workflow API warnings-as-errors build passed with 0 warnings/errors.
- Workflow API tests passed: 41 passed.
- Core tests excluding known long `FreshClone` walkthrough category passed: 229 passed, 3 skipped SMTP integration tests.
- Full unfiltered core test command timed out under the tool after 5 minutes and left stale test processes; those PIDs were stopped before rerunning the filtered core suite.
- `verify-seed` passed.
- Sample JSON `samples/workflow-actions/02-submit-for-checker.json` executed successfully and queued a notification.

Latest retest of the last two notification changes on 2026-06-22:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter "NotificationTemplateRendererTests|SmtpNotificationSenderTests|CoverageAuditTests" -m:1 /p:UseAppHost=false --no-restore
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter "SeedDataSmokeTests|WorkflowTransitionTests|Phase5SlaTests" -m:1 /p:UseAppHost=false --no-restore
dotnet build src\WorkflowEngine.sln -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false --no-restore
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- init-db
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- verify-seed
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- start-workflow PortfolioTrade PT-1001 --user maker1 --reset
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- execute-json .\samples\workflow-actions\02-submit-for-checker.json
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 /p:UseAppHost=false --no-restore
git diff --check
```

Results:

- Focused renderer/SMTP/audit tests passed: 19 passed.
- SQL-backed seed/transition/SLA tests passed: 19 passed when run outside the sandbox with normal Windows credentials.
- Shared solution warnings-as-errors build passed with 0 warnings/errors.
- Active workflow API warnings-as-errors build passed with 0 warnings/errors.
- Active workflow API tests passed: 41 passed.
- `init-db` applied the schema and Portfolio Trade seed data.
- `verify-seed` passed.
- Sample JSON `samples/workflow-actions/02-submit-for-checker.json` executed successfully and queued a notification.
- Full core test project passed: 235 passed, 3 skipped SMTP integration tests.
- `git diff --check` was clean except Git line-ending warnings.
- Note: SQL-backed commands fail under the sandbox identity `dolly\codexsandboxoffline` with `Cannot generate SSPI context`; run SQL Integrated Security gates outside the sandbox.

Latest verification after the HTML notification email change:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter "NotificationTemplateRendererTests|SmtpNotificationSenderTests|CoverageAuditTests" -m:1 /p:UseAppHost=false --no-restore
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter "SeedDataSmokeTests|WorkflowTransitionTests|Phase5SlaTests" -m:1 /p:UseAppHost=false --no-restore
dotnet build src\WorkflowEngine.sln -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false --no-restore
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- init-db
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- verify-seed
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- start-workflow PortfolioTrade PT-1001 --user maker1 --reset
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- execute-json .\samples\workflow-actions\02-submit-for-checker.json
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 /p:UseAppHost=false --no-restore
```

Results:

- Focused renderer/SMTP/audit tests passed: 19 passed.
- SQL-backed seed/transition/SLA tests passed: 19 passed.
- Shared solution warnings-as-errors build passed with 0 warnings/errors.
- Active workflow API warnings-as-errors build passed with 0 warnings/errors.
- Active workflow API tests passed: 41 passed.
- `init-db` applied the schema and seed.
- `verify-seed` passed.
- Sample JSON `samples/workflow-actions/02-submit-for-checker.json` executed successfully and queued a notification.
- Full core test project passed: 235 passed, 3 skipped SMTP integration tests.

Latest verification for the frontend handoff documentation slice:

- `dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false --no-restore` passed: 41 passed, 0 failed, 0 skipped.
- `git diff --check` reported only Git line-ending warnings for edited markdown files, with no whitespace errors.

Latest verification for the API JSON body and console real-usage documentation slice:

- Targeted text check confirmed the new HTML contains the current endpoints, key action names, dynamic Trading Desk role key, and console outbox command.
- `git diff --check` passed with no whitespace errors; it reported only Git line-ending warnings for edited files.

Latest verification after the per-record recipient change:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter SmtpNotificationSenderTests -m:1 /p:UseAppHost=false --no-restore
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter WorkflowExecutionValidationTests -m:1 /p:UseAppHost=false --no-restore
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 /p:UseAppHost=false --no-restore
dotnet build src\WorkflowEngine.sln -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false --no-restore
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- verify-seed
git diff --check
```

Results:

- `SmtpNotificationSenderTests` passed: 7 passed.
- `WorkflowExecutionValidationTests` passed: 19 passed.
- Full core test project passed: 231 passed, 2 skipped SMTP integration tests.
- Shared solution warnings-as-errors build passed with 0 warnings/errors.
- Active workflow API warnings-as-errors build passed with 0 warnings/errors.
- Active workflow API tests passed: 41 passed.
- `verify-seed` passed.
- `git diff --check` was clean except Git line-ending warnings.

Latest smtp4dev verification:

```powershell
RUN_SMTP_TESTS=1 dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter Category=Smtp -m:1 /p:UseAppHost=false --no-restore
```

Result: 3 passed, 0 failed, including per-record `recipientEmail` delivery to `maker.one@example.test`.

Latest restore-bundle verification:

- `restore-repo.ps1` was generated from commit `cb0d65b`.
- Restored into a temp folder using `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\restore-repo.ps1 -DestinationPath <temp> -Force`.
- Restored file count matched source-selected count.
- Source/restored SHA-256 comparison found no mismatches.
- Required target-api handoff files were present.
- Forbidden paths were absent: `.git`, `bin`, `obj`, `.tools`, local caches, logs, temp files, and `restore-repo.ps1`.

## Remaining Work

- Obtain explicit user confirmation or corrections for the SQMS flow recorded above.
- After confirmation, update the written SQMS design/decision artifact only if the user authorizes that documentation step.
- Do not create an implementation plan or touch code, seed data, migrations, contracts, samples, or tests until separately authorized.
- For the target API integration, ensure Return-to-Maker request construction includes `notificationContext.recipientEmail`.
- Keep `Smtp:RecipientOverride` configured during live SMTP smoke testing, then remove it only when ready to send to real per-record or role-map recipients.
- For target-machine operations, publish/copy the workflow console processor, configure DB/SMTP settings, and schedule `run-sla` followed by `process-notification-outbox` in Windows Task Scheduler.
- Replace trusted-header actor resolution with the target auth/claims resolver when integrating into the real host.
- Integrate EF migrations into the target deployment process.

## Next Session Prompt

```text
Continue the SQMS design discussion in C:\projects\cdt-workflow on branch console-code-first-0518. Read AGENTS.MD and PROJECT.md first, then CODEX_HANDOFF.md. The supplied SQMS sources have already been reviewed; do not restart the 33-question questionnaire or re-derive settled decisions unless a source conflict appears. The approved direction is integration-only: two metadata-driven definitions (`SqmsOpportunity` and `SqmsBid`) plus a thin timed-close coordinator around the existing generic engine. The existing application/IPG owns allocation-state selection. Review the exact opportunity, bid, automatic-close, notification, and ownership flow in `CODEX_HANDOFF.md` with me. Make no code, seed, migration, contract, test, sample, or implementation-plan changes unless I explicitly authorize them.
```

## 2026-08-09 Optimized UI Workflow API Envelope

### Current goal

- Completed the approved UI-facing `WorkflowStart` and `WorkflowAction` payload optimization on `console-code-first-0518`.
- Kept the reusable engine generic and metadata-driven while moving definition-owned and server-derived values out of the browser body.

### Completed changes

- Public Start now exposes exactly `workflowKey`, `businessObjectId`, optional `actionKey`, optional flat `slaTargetDate`, and optional `notification`.
- Public Action now exposes exactly `workflowInstanceId`, optional `actionKey`, optional `assignedRoleKey`, optional flat `slaTargetDate`, and optional `notification`.
- Unknown root/nested fields are rejected; obsolete body fields such as business-object type, body idempotency, nested SLA input, legacy notification context, and final recipient arrays are no longer accepted publicly.
- Added definition metadata for business-object type, workflow participant definitions/snapshots, and ordered notification recipient rules.
- Start derives business-object type and captures the required immutable `originalMaker` participant once.
- Metadata resolves StaticAddress, WorkflowParticipant, and RequestInput sources into final To/Cc snapshots. Resolution uses rule/source order, case-insensitive duplicate comparison, first opaque value retention, and To-over-Cc precedence.
- Opaque address strings are stored without syntax, directory, mailbox, SMTP, trimming, or rewriting validation.
- Immediate outbox rows and tasks store the same final snapshot; SLA reminders reuse the task snapshot byte-for-byte.
- The API generates client-action, correlation, and idempotency identity inside the trusted boundary.
- Preserved the console-only `WorkflowActionEnvelope` and existing console sample fixtures.
- Added current public examples under `workflow-api/samples` and replaced stale HTML integration handoffs.

### Changed files

- Public API DTOs/services/tests under `workflow-api/src` and `workflow-api/tests`.
- Application notification contracts, participant capture, canonical hashing, and recipient resolver.
- Workflow execution and Start integration.
- EF entities/configuration/migration/seed verification for definition type, participants, and recipient rules.
- SQL-backed Start/Action/SLA regression tests.
- `PROJECT.md`, `README.md`, `workflow-api/WORKFLOW_API_BOUNDARIES.md`, current design/plan artifacts, and four integration handoff HTML files.

### Build and test status

- Core warnings-as-errors build: passed with 0 warnings/errors.
- Core full SQL-backed suite: 308 passed, 0 failed, 4 smtp4dev-only tests skipped.
- Workflow API warnings-as-errors build: passed with 0 warnings/errors.
- Workflow API full suite: 66 passed, 0 failed, 0 skipped.
- `init-db` and `verify-seed`: passed against `cdt_workflow_api_20260527`.
- Required Checker Return, Trading Desk Return, SLA, notification inspection, and 14-step demo commands: passed.
- Live NoOp HTTP verification: Start moved to CheckerReview; Checker Return moved to ReturnedToMaker. SQL showed the captured Maker address and selected addresses retained surrounding spaces, with each task snapshot matching its immediate Sent outbox row.
- The live API process was stopped and port 5058 was verified available.

### Known risks and open items

- The migration intentionally clears workflow runtime rows because backward compatibility was explicitly not required; metadata and mock business data remain.
- Four smtp4dev integration tests remain opt-in/skipped because no live fake SMTP service was requested for this verification. Unit and SQL tests cover message-level retry behavior.
- The local verification database now contains the final console/demo/live-HTTP evidence rows.
- Existing unrelated untracked `.tools`, `SQMS`, and `tareget-api-integration` content was preserved.

### Next-session prompt

```text
Continue UI integration from the optimized Workflow API contract on console-code-first-0518. Read AGENTS.MD, PROJECT.md, workflow-api/WORKFLOW_API_BOUNDARIES.md, and the 2026-08-09 handoff section. Use workflow-api/samples for public bodies; do not use samples/workflow-actions as HTTP payloads. Start must send the resolved immutable originalMaker. Actions may send only metadata-declared recipient input keys such as selectedCcUsers. The engine owns final To/Cc placement, state transition, SLA configuration, correlation, and idempotency identity.
```

## 2026-08-09 BDRM Recurring Overdue Reminders

### Current goal

- Implement the approved BDRM/Client Service reminder lifecycle directly on `console-code-first-0518`.
- Send the first overdue reminder two minutes after assignment, then at 9:00 AM `America/New_York` on every later calendar day, including weekends and holidays, while the same task remains pending.

### Completed changes

- Configured `BdrmClientServiceReviewSla` as a two-minute duration policy with daily calendar recurrence at 9:00 AM Eastern.
- Extended the generic duration-policy scheduler so recurrence remains metadata-driven: the first occurrence uses the duration deadline and later occurrences use timezone-aware local calendar scheduling.
- Kept one-time duration policies unchanged.
- Preserved no-backfill behavior: a delayed monitor run queues at most the currently eligible local-date occurrence.
- Proved both BDRM exits close the schedule and cancel an undelivered reminder.
- Proved Trading Desk re-execution creates a new BDRM task with a fresh schedule.
- Reused the task's exact ordered To/Cc snapshot for every reminder.
- Made no public API DTO, database schema, migration, or model-snapshot changes.

### Changed files

- `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs`
- `src/WorkflowEngine.Infrastructure/Persistence/Verification/SeedVerificationService.cs`
- `src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs`
- `src/WorkflowEngine.Infrastructure/Sla/WorkflowSlaSynchronizationService.cs`
- `tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs`
- `tests/WorkflowEngine.Tests/Phase5SlaTests.cs`
- `tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs`
- `tests/COVERAGE_AUDIT.md`
- `PROJECT.md`
- `docs/superpowers/specs/2026-08-09-bdrm-recurring-overdue-reminders-design.md`
- `docs/superpowers/plans/2026-08-09-bdrm-recurring-overdue-reminders.md`

### Build and test status

- Baseline relevant SQL-backed tests: 40 passed after providing `WORKFLOW_ENGINE_TEST_CONNECTION_STRING`.
- Seed red/green cycle: the new BDRM expectation failed against the old one-time metadata, then passed after the seed and validation changes; focused seed tests: 2 passed.
- Generic recurring-duration SLA tests: 17 passed.
- BDRM exit and Trading Desk re-execution lifecycle tests: 3 passed.
- Core warnings-as-errors build: passed with 0 warnings/errors.
- Core full SQL-backed suite: 312 passed, 0 failed, 4 smtp4dev-only tests skipped.
- Workflow API warnings-as-errors build: passed with 0 warnings/errors.
- Workflow API full suite: 66 passed, 0 failed, 0 skipped.
- `init-db` and `verify-seed`: passed against `workflowengine`.
- SQL inspection confirmed active BDRM metadata: duration 2 minutes, reminder time 09:00, timezone `America/New_York`, recurrence interval 1 day, and `RouteSlaTaskOverdue`.
- SQL inspection confirmed zero rows in `WorkflowInstance`, `WorkflowParticipant`, `WorkflowTask`, `WorkflowTaskSlaSchedule`, `NotificationOutbox`, `WorkflowAudit`, and `IdempotencyRecord`.
- smtp4dev contains zero messages. The live API was restarted against `workflowengine` on port 5058 as PID 24388; root Swagger and `/swagger/v1/swagger.json` both returned HTTP 200.

### Remaining tasks

- Do not submit live API sample JSON or add smtp4dev messages before the user's scenario instructions.
- Push the completed commits only after confirming the branch handoff with the user.

### Known risks

- Calendar recurrence depends on the host resolving `America/New_York`; seed and runtime verification fail if the configured timezone is invalid.
- The external scheduler must continue invoking `run-sla`; `process-notification-outbox` remains responsible for delivery.

### Next-session prompt

```text
Continue the live BDRM scenario in C:\projects\cdt-workflow on console-code-first-0518. Read AGENTS.MD, PROJECT.md, and the latest CODEX_HANDOFF.md section. The implementation and full quality gates are complete. The live API is on port 5058 against `workflowengine`; database runtime tables and smtp4dev are empty. Wait for the user's exact scenario, then execute each API action and explain the task, SLA schedule, outbox, and smtp4dev result before proceeding to the next action.
```

## 2026-08-09 Shared Local Workflow Database Configuration

### Current goal

- Completed alignment of every normal local Console and Workflow API launch path to `Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False`.
- Preserved test, CI, EF design-time, and historical database isolation.

### Completed changes

- Added the shared connection to both hosts' base `appsettings.json` files.
- Updated both hosts' `appsettings.Development.json` files.
- Pinned both Workflow API launch profiles to the same database.
- Updated current `PROJECT.md` and `README.md` guidance.
- Added and committed the approved design and implementation plan.
- Left workflow behavior, public contracts, schema, seed metadata, SMTP configuration, and historical artifacts unchanged.

### Verification

- All five changed runtime JSON files parsed successfully.
- The obsolete local database name is absent from `PROJECT.md`, `README.md`, `src`, and `workflow-api`.
- Core and Workflow API warnings-as-errors builds passed with 0 warnings/errors.
- Console `verify-seed` passed after removing the connection-string environment override.
- API started through the checked-in `http` launch profile after removing the shell connection override.
- Final live API PID 6428 returned HTTP 200 for Swagger/OpenAPI, and SQL Server reported that process connected to `workflowengine`.
- Targeted configuration tests passed: 3 core and 3 Workflow API structure tests. The API test build emitted one file-lock warning because the verified live API remained running.
- `workflowengine` contains zero rows across the checked runtime tables, and smtp4dev contains zero messages.

### Open issue

- A read-only request for a nonexistent workflow context reached `workflowengine` but returned HTTP 500 rather than the controller's documented 404 response. This is an existing error-mapping issue outside the configuration-only task and was not changed.

### Next-session prompt

```text
Continue the clean live Workflow API and Console scenario on console-code-first-0518. Both hosts now resolve workflowengine from checked-in default configuration without a connection-string environment override. The live API is PID 6428 on port 5058, runtime tables are empty, and smtp4dev has zero messages. Wait for the user's first scenario and execute it one step at a time, showing API response, SQL task/SLA/outbox state, Console processor output, and smtp4dev delivery before continuing.
```

## 2026-08-09 Cumulative Notification Audience Implementation

### Current goal

- Implement the approved caller-owned cumulative notification audience for every Portfolio Trade task assignment, SLA reminder, return, rework, and completion notification.
- Keep current task owners in To, immutable original Maker and the complete caller-supplied `selectedCcUsers` snapshot in Cc, and Maker in To for return/completion routes.

### Completed changes

- Added exact ordered recipient metadata for all eight notification-producing transition routes; Portfolio Trade now seeds 22 recipient rules.
- Kept the generic resolver, public DTOs, schema, migrations, outbox lifecycle, and SLA engine unchanged.
- Added SQL-backed coverage for Checker, Trading Desk, BDRM, return-to-Maker, rework, completion, ordered deduplication, To precedence, and exact SLA task-snapshot reuse.
- Updated all notification-producing public API scenario packs with stage-appropriate complete cumulative `selectedCcUsers` arrays and documented To/Cc outcomes.
- Updated `PROJECT.md` and `workflow-api/WORKFLOW_API_BOUNDARIES.md` to define caller ownership, no directory resolution, full-snapshot behavior, and immutable historical snapshots.
- Work is merged and published on `console-code-first-0518` through `8985e17`; local and `origin/console-code-first-0518` match.

### Changed files

- `PROJECT.md`
- `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs`
- `tests/WorkflowEngine.Tests/NotificationRecipientResolverTests.cs`
- `tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs`
- `tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs`
- `tests/WorkflowEngine.Tests/WorkflowStartInitialActionTests.cs`
- `workflow-api/WORKFLOW_API_BOUNDARIES.md`
- `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Models/WorkflowScenarioSampleTests.cs`
- Notification-producing scenario `request.json`/`README.md` files for packs 03, 05, 07-11, and 14-18.
- `docs/superpowers/specs/2026-08-09-cumulative-notification-audience-design.md`
- `docs/superpowers/plans/2026-08-09-cumulative-notification-audience-implementation.md`

### Build and test status

- TDD seed contract failed at 13 versus expected 22 rules, then passed after the metadata update.
- Focused runtime recipient tests: 30 passed.
- Focused public scenario tests failed on missing cumulative arrays, then passed after sample updates.
- Core warnings-as-errors build: passed with 0 warnings/errors.
- Core full SQL-backed suite: 325 passed, 0 failed, 4 opt-in smtp4dev tests skipped.
- Workflow API warnings-as-errors build: passed with 0 warnings/errors.
- Workflow API full suite: 68 passed, 0 failed, 0 skipped.
- Isolated `init-db` and `verify-seed`: passed against `workflowengine_cumulative_audience_verify_20260809`.
- PROJECT section 21 Checker-return, Trading-return, and 14-step demo JSON flows passed on the isolated database.
- Demo overdue output showed assignment and SLA notification using identical ordered To/Cc snapshots.
- Recipient-boundary negative searches found no production singular-recipient declaration, SMTP role-address map, active removed route columns, public `toState`, or notification-address delimiter parsing.

### Remaining tasks

- No implementation, integration, or push work remains for the cumulative-audience change.
- The owned cumulative-audience worktree and feature branch were removed after the verified fast-forward and push.

### Known risks

- Existing persisted tasks and outbox rows are intentionally immutable; the new audience applies only when a new notification-producing action resolves a new snapshot.
- The calling application must send the complete current cumulative `selectedCcUsers` list on every notification-producing action; the engine does not accumulate earlier request inputs.
- Internal Console `execute-json` fixtures still exercise their separate legacy/internal envelope recipient snapshots; public HTTP callers must use the workflow-only DTO scenario packs.

### Next-session prompt

```text
Resume from console-code-first-0518 at 8985e17 in C:\projects\cdt-workflow. Local and origin match. Read AGENTS.MD, PROJECT.md, the approved cumulative-audience design/plan, and this handoff section. The cumulative audience implementation, builds, full tests, seed verification, representative JSON flows, boundary searches, merge, and push are complete. Preserve unrelated dirty planning/handoff files and wait for the user's next workflow scenario or change request.
```

## 2026-08-10 Full Return-and-Resubmit Live Acceptance

### Current goal

- Completed a fresh isolated live Workflow API plus Console scenario within the requested ten-minute execution window.
- Scenario: Maker SaveDraft -> SubmitForChecker -> CheckerReturnToMaker -> Maker SaveDraft -> ResubmitForChecker -> CheckerSendToTradingDesk -> TradingTradeExecuted -> BdrmTradeFulfilled.
- Exercised every active SLA stage and passed complete cumulative `selectedCcUsers` snapshots on every notification-producing action.

### Completed evidence

- Business object: `CDT-FULL-20260810-0826`.
- Workflow instance: `WF-CDT-FULL-20260810-0826`.
- Isolated database: `workflowengine_live_full_20260810_0826`.
- Final context: `Completed`, version `7`, `NoSla`, no current task, no available actions.
- Five tasks were created and completed: two Checker tasks, one Maker rework task with null due date, one `TradingDeskTaxable` task, and one BDRM Client Service task.
- Four SLA schedules completed. Console scans produced two Checker overdue reminders, Trading Desk due-today plus overdue reminders for target date `2026-08-10`, and one BDRM overdue reminder.
- A post-completion scan found zero active schedules and queued zero notifications.
- Eleven outbox rows are `Sent`, all with retry count `0`, null `LastError`, and matching smtp4dev messages.
- All five SLA notification recipient JSON snapshots exactly matched their source task snapshots.
- smtp4dev contains exactly eleven scenario messages with zero warnings.
- Audit history contains all eight expected actions, including both SaveDraft no-move entries and the full return/resubmit path.

### Build and seed status

- Core warnings-as-errors build: 0 warnings, 0 errors.
- Workflow API warnings-as-errors build: 0 warnings, 0 errors.
- `init-db` and `verify-seed` passed for the isolated database.
- No source code, seed, migration, contract, test, or sample file was changed.

### Diagnostic note

- The first Step 2 attempt was rejected with `ValidationFailed` because the ad hoc template omitted `submitterDisplayName`, `deskName`, and `securityType`. SQL confirmed a full rollback to Draft version 1 with no task or outbox row. The request was then executed using the exact route-required template keys from the public scenario pack.

### Running services

- Workflow API PID `28436` is listening on `http://localhost:5058` against the isolated database.
- smtp4dev PID `13892` is listening on UI `http://localhost:5000` and SMTP `localhost:3025` using isolated data directory `.tools/smtp4dev/data/livefull20260810_0826`.
- Before restarting either service, resolve the current listener PID and stop only that process.

### Next-session prompt

```text
Inspect or continue from the completed live scenario WF-CDT-FULL-20260810-0826 in database workflowengine_live_full_20260810_0826. The API is PID 28436 on port 5058 and smtp4dev is PID 13892 on ports 5000/3025. The workflow is Completed version 7 with five completed tasks, four completed SLA schedules, eleven Sent outbox rows, eleven smtp4dev messages with zero warnings, and eight audit rows. Do not replay any action. Preserve the isolated evidence until the user asks to stop the services or remove the database.
```
# 2026-08-12 SQMS Transition Metadata SQL

## Current goal

- Deliver Iteration 1 as executable SQL Server seed metadata for complete SQMS Opportunity and Bid transitions only.
- Keep tasks, SLA, notifications, scheduling, aggregation, and application integration out of this slice.

## Completed changes

- Added transactional, rerunnable SQL seed for `SqmsOpportunity` and `SqmsBid` version 1.
- Seeded 11 Opportunity states, 11 actions, 3 roles, and 22 transitions.
- Seeded 8 Bid states, 7 actions, 3 roles, and 9 transitions.
- Human roles are `MFO` and `IPG`; trusted coordinator transitions use the technical `System` role.
- All transition task and notification keys are null for this iteration.
- Added independent exact-matrix verification and a rollback wrapper that runs the seed twice before verification.
- No .NET source, EF migration, public API contract, application table, or existing code-owned seed was changed.
- The production-style `workflowengine` database was not modified; all SQMS SQL validation was rolled back.

## Changed files

- `sql/sqms/001_seed_sqms_transition_metadata.sql`
- `sql/sqms/002_verify_sqms_transition_metadata.sql`
- `sql/sqms/003_validate_sqms_seed_in_rollback.sql`
- `docs/superpowers/specs/2026-08-12-sqms-transition-metadata-design.md`
- `docs/superpowers/plans/2026-08-12-sqms-transition-metadata.md`
- `CODEX_HANDOFF.md`

## Build and test status

- SQL verifier red test: failed as expected with `Missing active SQMS workflow definition` before seed execution.
- Seed-twice transactional verification: passed with exact counts; outer transaction rolled back.
- Core warnings-as-errors build: passed with 0 warnings and 0 errors.
- Core SQL-backed tests: 336 passed, 0 failed, 4 opt-in SMTP tests skipped.
- Workflow API warnings-as-errors build: passed with 0 warnings and 0 errors.
- Workflow API tests: 63 passed, 0 failed, 2 SMTP dispatcher tests skipped.
- Final seed-twice rollback verification after regressions: passed.

## Remaining tasks

- User may apply `001_seed_sqms_transition_metadata.sql` to the chosen workflow database and then run `002_verify_sqms_transition_metadata.sql`.
- A later integration iteration must start and action workflows using namespaced business IDs such as `SQMS-OPP-1001` and `SQMS-BID-501`.
- Later iterations may attach task, notification, SLA, scheduler, and calculation behavior to these stable keys.

## Known risks

- The current generic execution service marks runtime `WorkflowInstance.Status` completed only when `ToStateKey` equals `Completed`. Other terminal SQMS states correctly have no outgoing transitions, but their instance status remains `Active`. A later minimal generic fix should derive completion from `WorkflowStateDefinition.IsTerminal`.
- Bare numeric SQMS Opportunity and Bid IDs can collide in the engine's generated workflow-instance ID. The caller must pass type-prefixed business object IDs.

## Next-session prompt

```text
Continue SQMS from the verified SQL-only Iteration 1. Read AGENTS.MD, PROJECT.md, the 2026-08-12 SQMS design/plan, and this handoff. The scripts seed the complete Opportunity and Bid state machines and have been validated twice per transaction without persisting rows. Before adding tasks, SLA, notifications, scheduling, or application coordination, apply and verify the seed in the user-selected workflow database. Preserve the stable workflow/state/action/role keys and use namespaced business object IDs.
```
# 2026-08-12 Approved SQMS Matrices Applied

## Current goal

- Apply and verify the user-approved SQMS Opportunity and Bid transition matrices in `workflowengine`.

## Completed changes

- Added IPG `Pending -> Draft` through `ReturnOpportunityToDraft`.
- Replaced MFO cancellation ownership with separate IPG and System transitions for each of eight cancellable Opportunity states.
- Preserved old MFO cancellation rows as inactive during an in-place upgrade; the script does not delete them.
- Left the approved Bid matrix unchanged.
- Updated independent SQL verification to require exactly 31 active Opportunity transitions and 9 active Bid transitions.
- Applied the revised seed to `Server=DOLLY;Database=workflowengine` and reran it successfully in place.
- Made the rollback validation wrapper safe to run when SQMS definitions already exist.

## Changed files

- `sql/sqms/001_seed_sqms_transition_metadata.sql`
- `sql/sqms/002_verify_sqms_transition_metadata.sql`
- `sql/sqms/003_validate_sqms_seed_in_rollback.sql`
- `docs/superpowers/specs/2026-08-12-sqms-transition-metadata-design.md`
- `docs/superpowers/plans/2026-08-12-sqms-transition-metadata.md`
- `CODEX_HANDOFF.md`

## Build and test status

- TDD red: revised verifier rejected the previous 22-row Opportunity seed with SQL error 51109, `Missing or incorrect active SQMS transition`.
- Seed-twice rollback validation: passed with 31 Opportunity and 9 Bid transitions.
- Core warnings-as-errors build: passed with 0 warnings and 0 errors.
- Workflow API warnings-as-errors build: passed with 0 warnings and 0 errors.
- Core SQL-backed tests: 336 passed, 0 failed, 4 opt-in SMTP tests skipped.
- Workflow API tests: 63 passed, 0 failed, 2 SMTP dispatcher tests skipped.
- Existing Portfolio Trade `verify-seed`: passed.
- Final applied SQMS exact-matrix verification: passed.
- Direct SQL proof: 8 active IPG cancellations, 8 active System cancellations, 0 active MFO cancellations, and exactly 1 active IPG `Pending -> Draft` transition.

## Remaining tasks

- No work remains for the approved Iteration 1 transition metadata.
- The next iteration can integrate SQMS Opportunity/Bid records with Workflow Start/Action, using type-prefixed business object identifiers.

## Known risks

- Runtime terminal status remains the pre-existing limitation: the generic engine sets `WorkflowInstance.Status = Completed` only when `ToStateKey` is literally `Completed`. Other terminal SQMS states have no outgoing transitions but may retain runtime status `Active`.
- No SQMS action-envelope sample was executed in this SQL-only iteration; transition metadata was verified exactly in SQL and generic execution remained covered by the full regression suite.

## Next-session prompt

```text
Continue from the applied and verified SQMS Iteration 1 metadata in workflowengine. Read AGENTS.MD, PROJECT.md, the SQMS design/plan, and this handoff. Opportunity has 31 active transitions, including IPG return from Pending to Draft and IPG/System-only cancellation. Bid remains at 9 approved transitions. Do not change these stable matrices without user approval. The next candidate slice is the thin SQMS Start/Action integration using namespaced business object IDs.
```

# 2026-08-12 SQMS Iteration 2 No-Code Runtime API Validation

## Current goal

- Prove the existing public Workflow Start, Context, and Action APIs execute the Iteration 1 SQMS Opportunity and Bid metadata without workflow-engine or SQMS-specific integration code changes.

## Completed changes

- Made no source, seed, migration, contract, sample, or test changes.
- Started the existing Workflow API at `http://127.0.0.1:5058` against `DOLLY/workflowengine` with the NoOp notification sender.
- Started Opportunity `SQMS-OPP-20260801-ITER2-01` as `WF-SQMS-OPP-20260801-ITER2-01`.
- Executed `SubmitOpportunity`, `MarkOpportunityPending`, and `OpenOpportunityForBidding`; the instance reached `OpenToBidding`, version 4.
- Verified role-specific Opportunity context: IPG received the approved Submitted/Pending actions and System received `CancelOpportunity` plus `CloseOpportunityBidding` at `OpenToBidding`.
- Started Bid `SQMS-BID-20260801-ITER2-01` as `WF-SQMS-BID-20260801-ITER2-01`.
- Executed `SubmitBid`, `MoveBidToIpgReview`, and `SendBidToSellerReview`; the instance reached `UnderReviewBySeller`, version 4.
- Verified seller-side MFO context returned `AcceptBid`, `PartiallyAcceptBid`, and `RejectBid` without duplicate application transition logic.
- Stopped before a terminal Bid action because non-`Completed` terminal runtime status handling remains explicitly deferred.

## Changed files

- `CODEX_HANDOFF.md` only for this runtime-validation record.

## Build and test status

- Public API calls: all intended Start, Context, and Action calls returned HTTP 200.
- Opportunity SQL audit: 3 ordered rows matching `Draft -> Submitted -> Pending -> OpenToBidding` and MFO/IPG ownership.
- Bid SQL audit: 3 ordered rows matching `Draft -> Submitted -> IpgReview -> UnderReviewBySeller` and MFO/IPG ownership.
- Both runtime instances have zero tasks, zero SLA schedules, and zero notification-outbox rows, matching Iteration 1 scope.
- Exact SQMS seed verifier passed again: 31 Opportunity transitions and 9 Bid transitions.
- API stderr was empty after the successful scenarios.
- One initial transport attempt returned HTTP 400 because Windows `curl.exe` stripped JSON quotes; it failed before workflow execution and was retried through .NET `HttpClient` with the exact public JSON.

## Remaining tasks

- No workflow-engine changes are required for SQMS Start/Context/Action integration.
- The actual SQMS UI/application must call these existing APIs or in-process services, persist the returned `workflowInstanceId`, render `availableActions`, and send only the selected `actionKey`.
- Select the next incremental capability before adding tasks, SLA, notifications, scheduling, or terminal-status synchronization.

## Known risks

- Runtime terminal status remains deferred: terminal states other than literal `Completed` have no outgoing transitions but retain `WorkflowInstance.Status = Active`.
- The two Iteration 2 runtime records intentionally remain in `workflowengine` as test evidence.
- API process PID 24400 was stopped during wrap-up; port 5058 was confirmed free.

## Next-session prompt

```text
Continue after SQMS Iteration 2 no-code runtime validation. Existing Workflow Start, Context, and Action APIs successfully drove Opportunity to OpenToBidding and Bid to UnderReviewBySeller using the seeded metadata and namespaced IDs. No engine code is needed for UI action discovery or transition validation. Preserve the approved matrices and select only one next SQMS capability; terminal status handling remains deferred.
```

# 2026-08-16 SQMS Iteration 3 Exact-Table Console Proof

## Current goal

- Completed the approved console-only mock of the SQMS application boundary using the user-supplied Opportunity and Bid DDL.
- Preserved the reusable engine and existing Portfolio Trade/SQMS behavior.

## Completed changes

- Added `sql/sqms/testing/004_create_sqms_mock_application_tables.sql`, a transactional and rerunnable setup script for exact-shape `SSQMS.Opportunity` and `SSQMS.Bid` tables in the current database.
- Added deterministic related fixtures at ID `10000` without updating existing rows.
- Added `sql/sqms/testing/005_verify_sqms_console_mock.sql`, which independently checks columns, SQL types, nullability, identity settings, PK/FK trust, workflow results, audit counts, absence of task/SLA/outbox rows, and application-row immutability.
- Added Console action envelopes for `SubmitOpportunity` and `SubmitBid`.
- Added an isolated SQL-backed integration test covering Start, role-filtered Context, Execute, refreshed Context, runtime persistence, and untouched application rows.
- No generic engine, public HTTP contract, EF migration, or UI code changed.

## Live database evidence

- Database: `DOLLY/workflowengine`; no `cp_sharepoint` database was created or written.
- Setup SQL ran twice successfully.
- Opportunity fixture `SSQMS.Opportunity.ID=10000` remained `Status=DRAFT`, `WorkflowState=Draft`, with null modification fields.
- Bid fixture `SSQMS.Bid.ID=10000`, related to Opportunity 10000, remained `Status=DRAFT`, `WorkflowState=Draft`, with null modification fields.
- `WF-SQMS-OPP-10000` started at Draft version 1, exposed only `SubmitOpportunity` to MFO, executed to Submitted version 2, then exposed `CancelOpportunity`, `MarkOpportunityPending`, and `ReturnOpportunityToDraft` to IPG.
- `WF-SQMS-BID-10000` started at Draft version 1, exposed only `SubmitBid` to MFO, executed to Submitted version 2, then exposed `MarkBidNotAllocated` and `MoveBidToIpgReview` to IPG.
- Each workflow has one matching audit and zero task, SLA schedule, and notification-outbox rows.
- Final independent SQL verifier printed `SQMS console mock verification passed.`

## Build and test status

- Focused TDD test: expected RED on the missing setup script, then 1/1 GREEN after implementation.
- Core warnings-as-errors build: 0 warnings, 0 errors.
- Core non-SMTP SQL-backed suite: 337 passed, 0 failed, 0 skipped.
- Workflow API warnings-as-errors build: 0 warnings, 0 errors.
- Workflow API suite: 63 passed, 0 failed, 2 existing dispatcher tests skipped.
- Console `verify-seed`: passed.
- Exact SQMS metadata verifier: Opportunity 11 states / 11 actions / 3 roles / 31 transitions; Bid 8 / 7 / 3 / 9.

## Remaining tasks and risks

- Iteration 3 is complete. Select exactly one capability before Iteration 4 planning.
- The production SQMS application still needs an approved convention to persist or deterministically derive `workflowInstanceId`.
- Application ownership/synchronization of `Status` and `WorkflowState` remains intentionally unresolved; the generic engine does not update them.
- UI, tasks, SLA, notifications, scheduling, aggregation, and generic non-`Completed` terminal-state synchronization remain deferred.

## Next-session prompt

```text
Resume after completed SQMS Iteration 3. Read AGENTS.md, PROJECT.md, .planning/SQMS-CONTEXT.md, and the 2026-08-16 console-mock design. The exact-shape SSQMS mock tables and deterministic ID 10000 fixtures exist in DOLLY/workflowengine. Both WF-SQMS-OPP-10000 and WF-SQMS-BID-10000 are Submitted version 2 with one audit, no task/SLA/outbox rows, and unchanged application fields. No generic engine code changed. Choose one narrowly scoped next SQMS capability before planning or coding.
```

# 2026-08-16 SQMS End-to-End Integration Complete

## Current goal

- Completed the approved SQMS end-to-end workflow integration while preserving the generic workflow engine, existing public Workflow API, Portfolio Trade behavior, and UI ownership boundary.
- The implementation is on local branch `feature/sqms-console-mock` in worktree `.worktrees/sqms-console-mock`; it has not been pushed.

## Completed changes

- Made generic Start participate in a caller-owned transaction and made terminal completion derive from destination-state metadata instead of the literal state key `Completed`.
- Added only the approved nullable SQMS business inputs to the mock application tables, plus JSON/category constraints and due-query indexes.
- Added the in-process `WorkflowEngine.SqmsIntegration` thin layer with deterministic identities, Start, Context, Action, atomic application projection, trusted notification input, classification, scheduled processing, and DI registration.
- Added taskless `SqmsSaleCycle` metadata and six exact notification routes: initial notice, reminder, seller approval, accepted, partially accepted, and not accepted.
- Added idempotent aggregate notice/reminder processing. A reminder waits for the initial outbox row to be `Sent`.
- Added automatic parent close, transactional child classification, retry after child failure, no-Bid `NotFulfilled`, seller-review orchestration, all three Bid decisions, and final Opportunity roll-up.
- Added action-only samples `03` through `06` and retained the existing console samples `01` and `02`.
- SQMS remains taskless and has no SLA. SMTP delivery continues through the existing durable generic outbox and sender.

## Commits

- `a2e73bd` - design the complete SQMS integration.
- `a75a40a` - write the executable implementation plan.
- `2009620` - generic transaction and terminal compatibility.
- `a82874f` - SQMS business fields and exact verification.
- `a0edbb0` - SQMS thin layer.
- `db96f1a` - taskless sale-cycle and notification metadata.
- `e8faf33` - scheduled notices and reminders.
- `c240d82` - automatic close and Bid classification.
- `b69d69c` - seller approval, disposition emails, and roll-up.
- `e1d8067` - sample and live SMTP acceptance coverage.

## Build and test status

- Core restore and warnings-as-errors build: passed, 0 warnings and 0 errors.
- Workflow API restore and warnings-as-errors build: passed, 0 warnings and 0 errors.
- Core serialized regression suite: 377 passed, 0 failed, 5 opt-in SMTP tests skipped.
- Workflow API suite: 68 passed, 0 failed, 0 skipped.
- Full opt-in SMTP suite: 5 passed, 0 failed, 0 skipped.
- Explicit SQMS console/sample selection: 7 passed; samples `01` through `06` were loaded and executed.
- Dedicated SQMS SMTP E2E: passed. Six SQMS outbox rows were `Sent` with retry count `0` and null `LastError`; all six subjects reached smtp4dev; every captured message had zero warnings and both plain and HTML bodies contained its configured SQMS link.

## Database verification

- Dedicated database: `DOLLY/workflowengine_sqms_final_verify_20260816`.
- `init-db`: passed.
- generic `verify-seed`: passed.
- `001_seed_sqms_transition_metadata.sql`: passed.
- `004_seed_sqms_end_to_end_metadata.sql`: passed.
- exact transition verifier: Opportunity 11 states / 11 actions / 3 roles / 31 transitions; Bid 8 / 7 / 3 / 9.
- exact end-to-end verifier: all six routes and taskless sale-cycle metadata passed.
- end-to-end seed-twice rollback verifier: passed and rolled back.

## Changed files

- Design/plan: `docs/superpowers/specs/2026-08-16-sqms-end-to-end-integration-design.md`, `docs/superpowers/plans/2026-08-16-sqms-end-to-end-integration.md`.
- Samples: `samples/sqms/03-request-seller-approval.json` through `06-reject-bid.json`.
- SQL: SQMS exact verifier, end-to-end seed/verifier/rollback, and application-table mock/verifier.
- Generic source: Start transaction ownership and metadata-terminal completion only.
- SQMS source: all files under `src/WorkflowEngine.SqmsIntegration` plus solution registration.
- Tests: SQMS identity, coordinator, metadata, schedule, classification, seller disposition, SMTP E2E, console mock, and generic regression coverage.

## Production substitutions and known risks

- Replace `.example.test` static recipients in workflow metadata with approved production distribution lists.
- Configure `OpenBiddingViewUrl`, `SellerApprovalViewUrlFormat`, and `BidViewUrlFormat` with real SharePoint routes.
- The SQMS UI/data layer must populate the approved scheduling and dynamic-recipient columns on each Opportunity/Bid row.
- The target SQMS API host must call `AddSqmsIntegration`, expose its own authenticated endpoints around the coordinator, and invoke `RunNoticesAndReminders` / `RunClosures` from its existing scheduler. No new generic scheduler or HTTP contract was added to this repository.
- The supplied schema has no explicit fund-name column. The current thin layer maps `FundConsideredSecondarySale` to the email `fundName` token. Confirm that field contains the display name; otherwise add a dedicated nullable application-owned Fund name and update only the SQMS repository mapping before production.
- smtp4dev PID `30004` remains on SMTP `3025` and UI/API `5000` using `.tools/smtp4dev/data/sqms-final-20260816`. Repeated diagnostic acceptance runs mean the mailbox contains more than one run; the automated test isolates the current six rows by the SQL database and exact subjects.

## Next-session prompt

```text
Continue from the completed SQMS end-to-end implementation on feature/sqms-console-mock. Read PROJECT.md, the 2026-08-16 SQMS end-to-end design, and this handoff. All generic/API regressions, exact SQL verifiers, samples 01-06, and live SMTP acceptance pass. Do not redesign the workflow. Production integration consists of wiring AddSqmsIntegration into the external SQMS API, scheduling SqmsScheduleRunner calls, applying the additive SQL/metadata, replacing example.test addresses and URLs, and confirming the source for the fundName email token. Push only when explicitly requested.
```
