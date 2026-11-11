# Independent assessment of the implicit state machine findings

Date: 2026-11-10. Reviewed local source: `65adf7942daee4cf0bfadc50862903d64337fa91`.

Input: [IMPLICIT_STATE_MACHINES.md](IMPLICIT_STATE_MACHINES.md). The original inventory is unchanged.

**Verdict: I agree with most of the risk descriptions. I partly agree with the classification. I disagree with a blanket state-machine refactor.**

The important defects concern concurrent writes, incomplete validation, and different owners of the same outcome. Status constants can improve readability. They cannot make those writes atomic.

This assessment uses the current C#, TypeScript, templates, JSON metadata, SQL, and test source. It does not use the inventory as proof. The inspected runtime folders have no Git differences from the inventory's baseline, `c729452`. The corrections below therefore concern the analysis, rather than later runtime changes.

**Evidence limit:** No application test, build, browser test, HTTP request, SQL script, seed command, or sample was executed for this assessment. A source-supported concurrency sequence is not a reproduced incident. Existing test names below identify inspected coverage, not a fresh passing test run. Target code and deployed databases were not inspected.

## 1. What the inventory gets right, and where I disagree

The inventory is useful as a map of lifecycle rules. E01 to E06, P01, P06, P08, and the asynchronous client operations have real lifecycle behaviour.

However, several entries are decisions or calculations, rather than separate state machines that need a transition class:

- E07 selects a request path.
- E09 translates a result into a response. Its commit-versus-delivery problem is real, but a result-code enum does not solve it.
- E13 calculates available actions from persisted state and actor roles.
- P02 is already an ordered save pipeline with a `SaveState` object.
- P05 calculates app availability from several inputs.
- P07 evaluates access policy. P09 already centralizes the active-role predicate in a SQL view.

Separate Boolean values can also describe independent facts. A history dialog and a page save need not become one large enum. A single enum for every combination could make the component harder to understand.

The engine already has important explicit enforcement: SQL status checks, filtered unique indexes, version-guarded instance updates, and outbox claim tokens. The issue is where those protections stop. See [instance update](../src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs#L675), [task constraint](../src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs#L310), [schedule constraints](../src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs#L352), and [outbox claim](../src/WorkflowEngine.Infrastructure/Notifications/ProcessNotificationOutboxService.cs#L81).

## 2. Verdict for each engine finding

| Finding | Verdict | My assessment and source |
|---|---|---|
| E01 Instance lifecycle | **Agree with the defects.** | SaveDraft skips the final version-guarded update. Terminal completion depends on task metadata, without an independent check that no pending task remains. The accepted `Draft` and `SaveDraft` identifiers are not new defects. [Execution](../src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs#L644), [terminal and task writes](../src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs#L675), [start](../src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs#L387). |
| E02 Task lifecycle | **Agree with the description. Low refactoring priority.** | One service creates and completes tasks. SQL enforces at most one pending task. The different query methods do not create a defect when that constraint holds. A mismatched close-task key does return `ConcurrencyConflict`, even when metadata caused the mismatch. [Constraint](../src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs#L296), [guard](../src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs#L1043). |
| E03 SLA schedule lifecycle | **Agree.** | The no-task exception path and the task-completion race are supported by source. Schedule rowversion protects conflicting edits to the same schedule. It does not enforce the relationship between a completed task and a newly inserted schedule. [Synchronization](../src/WorkflowEngine.Infrastructure/Sla/WorkflowSlaSynchronizationService.cs#L25), [rowversion](../src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs#L352). |
| E04 Reminder cursor | **Partly agree.** | Repeated warnings and an unchanged cursor are real operational concerns. Deriving reminder kind from time and the saved policy is not itself a defect. `Blocked` is an analytical label for a failed attempt, not necessarily a permanent lifecycle state. The primary task email dependency is real. [Scan](../src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs#L172), [primary email](../src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs#L219), [kind calculation](../src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs#L354). |
| E05 Outbox delivery | **Agree. Highest-value lifecycle review area.** | Delivery logic is duplicated. However, worker recovery and immediate API delivery have different purposes. Their differences need individual decisions. Cancellation has two race orderings, described below. [Worker](../src/WorkflowEngine.Infrastructure/Notifications/ProcessNotificationOutboxService.cs#L25), [dispatcher](../workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionNotificationDispatcher.cs#L25), [cancellation](../src/WorkflowEngine.Infrastructure/Sla/WorkflowSlaSynchronizationService.cs#L219). |
| E06 Idempotency | **Agree with specific gaps. Partly agree with classification.** | Row absence/presence is a reasonable small protocol. The request hash omits `AssignedRoleKey`, while the action API generates a new key on each call. Those are more important than naming the protocol states. One inventory citation is wrong: the SLA unique-violation check uses SQL error numbers, not message text. [Hasher](../src/WorkflowEngine.Application/Normalization/CanonicalRequestHasher.cs#L21), [API key](../workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs#L40), [SLA exception check](../src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs#L539). |
| E07 Request paths | **Agree with the paths. Disagree that they need a separate machine.** | These are request variants and caller options. The most important question is whether each path applies the necessary validation and concurrency checks. [SLA-only branch](../workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs#L79), [SaveDraft branch](../src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs#L675). |
| E08 Activation and metadata flags | **Agree, with a narrower description.** | There is no unique active-version constraint. Start ignores the definition-level flag. Task readers apply `IsActive` differently. Version activation procedures do exist, so “no publication procedure” is too broad. The unresolved problem is generic enforcement and safe treatment of published metadata. [Version mapping](../src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs#L73), [start lookup](../src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs#L441), [activation method](../src/WorkflowEngine.Infrastructure/Persistence/Seed/WorkflowSeedService.cs#L97). |
| E09 Result and HTTP mapping | **Agree with the committed-action ambiguity. Disagree with the machine label.** | The API can return failure after the engine commits successfully. This is an outcome contract issue. Constants do not reconcile the two outcomes. These findings apply to the standalone workflow API code inspected here. They do not prove target in-process caller behaviour. [Post-commit dispatch](../workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs#L121), [failure mapping](../workflow-api/src/WorkflowEngine.WorkflowApi/Controllers/WorkflowActionController.cs#L24). |
| E10 SQMS scheduler | **Partly agree.** | SQMS-specific timers belong in this caller layer. They do not violate the generic engine boundary. Version `1` and the dependence on audit/projection rows are real coupling. Reopening duplication is conditional on future metadata changes. It is not reachable through the current opening transition. [Scheduler](../src/WorkflowEngine.Sqms/Scheduling/SqmsScheduledService.cs#L41), [fixed version](../src/WorkflowEngine.Sqms/Scheduling/SqmsScheduledService.cs#L243), [audit join](../src/WorkflowEngine.Sqms/Persistence/SqlSqmsDataStore.cs#L66), [current transitions](../sql/sqms/002_sqms_workflow_metadata_dml.sql#L393). |
| E11 SQMS projection | **Agree with the integration risk, with a qualification.** | Scheduled writes have an explicit owner and share the engine transaction. User-action projection belongs to the target. That boundary is not evidence of a broken transaction at the target. Drift can also hide candidates completely, without an `InvalidTransition` warning. [Ownership](../src/WorkflowEngine.Sqms/SqmsWorkflowService.cs#L172), [projection](../src/WorkflowEngine.Sqms/SqmsWorkflowService.cs#L514), [transaction](../src/WorkflowEngine.Sqms/SqmsWorkflowService.cs#L564), [candidate filter](../src/WorkflowEngine.Sqms/Persistence/SqlSqmsDataStore.cs#L66). |
| E12 Participant capture | **Agree. No refactor justified by this finding.** | Capture and duplicate rejection are local to one service. Immutable participant rows are an intentional snapshot. [Capture](../src/WorkflowEngine.Infrastructure/WorkflowStart/WorkflowParticipantCaptureService.cs#L17). |
| E13 Actor access | **Agree with the calculation. Disagree with treating it as a lifecycle defect.** | This is a read model. Available actions depend on active transitions, active actions, roles, and completion status. A new persisted access state would add another value to synchronize. [Reader](../src/WorkflowEngine.Infrastructure/WorkflowContext/WorkflowContextReader.cs#L86). |

## 3. Verdict for each platform finding

| Finding | Verdict | My assessment and source |
|---|---|---|
| P01 Record lifecycle | **Partly agree. One factual correction.** | Delete has no client version token. No restore path is present. Neither fact alone proves a requirement violation. The assertion that deleted records are also unavailable to history is false. History deliberately remains readable for a permitted user. A business no-op save can still update system fields. [Delete](../atlas-platform/src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs#L725), [history](../atlas-platform/src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs#L758), [system fields](../atlas-platform/src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs#L468). |
| P02 Save pipeline | **Agree with the description. No separate state-machine refactor needed.** | The ordered steps and `SaveState` are already explicit. Returning validation errors in the result and transport errors as exceptions is a protocol choice. The client handles both. Logging an `OnAfterSave` failure preserves the committed outcome. [Pipeline](../atlas-platform/src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs#L263), [after-save handling](../atlas-platform/src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs#L514), [client](../atlas-platform/WebUi/src/app/platform/form/atlas-form.component.ts#L315). |
| P03 Field state | **Agree with the rule/effect mismatch. Narrow the impact.** | The server preserves rule locks. Client setters do not. This can create an editable field whose update the server refuses. The server also removes rule-hidden values from load responses. This finding does not establish a data disclosure or server authorization bypass. Initial effects run before the final whole-form disable, which limits the report's read-only-form claim. [Server locks](../atlas-platform/src/BBH.CP.Atlas.Service/Platform/Sections/AtlasServerEffects.cs#L32), [client order](../atlas-platform/WebUi/src/app/platform/form/atlas-form.component.ts#L108), [client setters](../atlas-platform/WebUi/src/app/platform/form/atlas-form.component.ts#L170). |
| P04 Unconnected workflow state | **Agree. Missing capability, not a hidden working machine.** | Rule contexts receive no states. `Execute` follows the client's default close path. The lint warns about unsupported actions. The adapter is needed before a workflow-based platform app can rely on these features. This is not evidence that TTDV2 requires an adapter. [Empty state list](../atlas-platform/src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs#L800), [state match](../atlas-platform/src/BBH.CP.Atlas.Service/Platform/Rules/AtlasRuleEngine.cs#L185), [button handling](../atlas-platform/WebUi/src/app/platform/form/atlas-form.component.ts#L270), [lint](../atlas-platform/src/BBH.CP.Atlas.App/Platform/AtlasConfigLint.cs#L166). |
| P05 App availability | **Agree with the cross-app gap. Partly agree with the refactor scope.** | The resolver already performs the full check. The menu calls it. Those are not seven independent implementations. Foreign dashboard tabs do bypass the startup-disabled set. Centralizing that decision is useful, provided legacy fall-through and error behaviour remain intact. [Resolver](../atlas-platform/src/BBH.CP.Atlas.Service/Platform/Registry/AtlasModuleResolver.cs#L45), [menu](../atlas-platform/src/BBH.CP.Atlas.Service/Platform/Registry/AtlasApplicationMenu.cs#L35), [foreign tab](../atlas-platform/src/BBH.CP.Atlas.Service/Platform/Dashboard/AtlasDashboardService.cs#L87). |
| P06 Startup | **Agree with the lifecycle. Low refactoring priority.** | A gate and an `IsLoaded` check already coordinate startup. In `DisableModule` mode, `MarkLoaded` means the startup attempt finished, including failures that disabled apps. It does not mean all tables loaded successfully. The restart policy is deliberate. [Gate](../atlas-platform/src/BBH.CP.Atlas.App/Platform/AtlasPlatformStartup.cs#L29), [failure handling](../atlas-platform/src/BBH.CP.Atlas.App/Platform/AtlasPlatformStartup.cs#L48). |
| P07 Access and rollout | **Agree with the decision table. Disagree that asymmetry is automatically a defect.** | Create-at-save and Delete enforce denial even in `LogOnly`. Those exceptions are explicit in code. A shared helper must preserve them. These are policy modes, not record lifecycle states. [Access policy](../atlas-platform/src/BBH.CP.Atlas.Service/Platform/Security/AtlasAccessService.cs#L42), [create guard](../atlas-platform/src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs#L329). |
| P08 Cut-over and rollback | **Agree. High priority before operational use.** | Rollback infers which role grants to restore from actor and time. Re-cutover skips existing business rows and later history. The first log timestamp remains the rollback reference after another cut-over. The data remains in TTD, so “stale active data and missing copied history” is more precise than physical data loss. [Copy](../atlas-platform/sql/release-p1/050-ttdv2-copy-from-ttd.sql#L178), [log](../atlas-platform/sql/release-p1/050-ttdv2-copy-from-ttd.sql#L205), [rollback](../atlas-platform/sql/release-p1/090-ttdv2-rollback.sql#L25). |
| P09 User roles | **Agree with the facts. Disagree with the implication that another machine is needed.** | The SQL view already defines the active-role predicate in one place. The platform reads it once per request. Mutation ownership is outside that reader. [View](../atlas-platform/sql/release-p1/030-active-user-roles-view.sql#L30), [reader](../atlas-platform/src/BBH.CP.Atlas.Service/Platform/Security/AtlasUserContext.cs#L56). |
| P10 Form | **Agree with the missing delete guard. Partly agree with the proposed redesign.** | `remove()` has no busy state. Save and delete can overlap. Add an operation guard before considering a broader component rewrite. Independent dialogs are not themselves evidence of invalid state. [Save guard](../atlas-platform/WebUi/src/app/platform/form/atlas-form.component.ts#L287), [delete](../atlas-platform/WebUi/src/app/platform/form/atlas-form.component.ts#L331), [button disable](../atlas-platform/WebUi/src/app/platform/form/atlas-form.component.html#L71). |
| P11 Dashboard, history, cache | **Partly agree. Narrow the deployment claim.** | The local dashboard delete lacks a busy guard. Its source header says it is a target reference, while the target keeps its current dashboard. This does not prove a target dashboard defect. History already cancels superseded loads. The navigation promise cache is normal caching. [Dashboard scope](../atlas-platform/WebUi/src/app/platform/dashboard/atlas-dashboard.component.ts#L1), [delete](../atlas-platform/WebUi/src/app/platform/dashboard/atlas-dashboard.component.ts#L164), [history](../atlas-platform/WebUi/src/app/platform/atlas-history-dialog.ts#L84), [cache](../atlas-platform/WebUi/src/app/platform/atlas-navigation.ts#L60). |

## 4. Deeper analysis of the important paths

### 4.1 SaveDraft: a second read does not close the write gap

**Verdict: agree. Known issue. High priority when engine fixes resume.**

The engine re-reads the instance and pending task inside the transaction. It checks the request guards and the selected transition. This is useful protection. However, SaveDraft performs no conditional write to the instance after those reads.

A supported interleaving is:

1. Request A reads `Draft` and passes the transaction's guards.
2. Request B changes the workflow state and commits.
3. Request A writes its data, audit, and idempotency record, then commits without a final instance-version check.

The narrow gap is after A's last check. It is not true that every SaveDraft submitted after a transition succeeds. A transition committed before A's re-read is detected.

The console path can persist `MockSectionData`. The inspected workflow API sets `PersistSectionData = false`. Therefore this review does not establish a target business-table overwrite. It does establish a gap in the generic action's concurrency boundary.

Evidence: [transaction and guards](../src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs#L640), [conditional branch](../src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs#L675), [audit and commit](../src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs#L858), [API caller options](../workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs#L121).

**Confirming test:** Use two SQL connections. Pause SaveDraft after its final guard, commit a transition, then release SaveDraft. Inspect the result, audit, section data, and idempotency row. A fix must preserve the approved rule that SaveDraft does not increment the workflow version.

### 4.2 SLA: distinguish a simple missing-task error from the completion race

**Verdict: agree with both, but they need different tests and fixes.**

An actionless API request with `slaTargetDate` calls synchronization directly. `ResolvePendingTask()` calls `Single()`. No task therefore causes `InvalidOperationException`. The local catch handles only `WorkflowSlaValidationException`. The global exception handler maps the other exception to a generic 500.

This path is source-supported for a validly shaped request naming an unknown instance, a Draft instance without a task, or a completed instance without a task. It needs no concurrency to fail.

Evidence: [API branch](../workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs#L79), [task lookup](../src/WorkflowEngine.Infrastructure/Sla/WorkflowSlaSynchronizationService.cs#L171), [exception mapping](../workflow-api/src/WorkflowEngine.WorkflowApi/Infrastructure/GlobalExceptionHandler.cs#L47).

The completion race has a more specific ordering:

1. Synchronization reads the pending task.
2. Another transaction completes that task and its schedule, then commits.
3. Synchronization reads no active schedule and inserts a new active schedule for the task it already loaded.

Synchronization does not recheck task status at the final write. It also does not reject a completed instance. `WorkflowTask` has no configured rowversion. The schedule's rowversion cannot protect this new schedule from a relationship violation. The unique active-schedule index also permits it because the old schedule is completed.

The next SLA scan excludes the completed task. The result can therefore be an active schedule that no scan processes.

Evidence: [read and insert order](../src/WorkflowEngine.Infrastructure/Sla/WorkflowSlaSynchronizationService.cs#L29), [completion](../src/WorkflowEngine.Infrastructure/Sla/WorkflowSlaSynchronizationService.cs#L142), [task mapping](../src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs#L296), [scan filters](../src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs#L49).

**Confirming tests:** Test the three no-task API cases separately. For the race, pause after the task read and complete the task on another connection before the active-schedule query. Existing synchronization tests cover sequential completion and rescheduling, not this ordering.

### 4.3 Outbox cancellation: the inventory understates the write race

**Verdict: agree with the reported ordering. Add a second ordering for investigation.**

The reported case is valid: the sender runs, completion cancels the outbox row and clears its token, then the sender succeeds. The worker's conditional `Sent` update matches no row. The message can be delivered while the row says `Cancelled`.

A second source-supported ordering is more direct:

1. `CancelUndeliveredOccurrences()` reads a `Processing` row into EF tracking and changes its local status to `Cancelled`.
2. Before those changes are saved, the worker completes delivery and updates that database row to `Sent`.
3. The cancellation transaction saves its tracked changes.

The outbox has no configured concurrency token. The cancellation path does not issue a conditional update that rechecks status or the processing token at write time. Its earlier query predicate does not become a final update guard. Thus the stale cancellation can overwrite the `Sent` status. `SentAtUtc` may remain populated, because cancellation does not change that property.

This second ordering is a source inference. No concurrent SQL test was executed here. Existing tests that preserve an already-sent row query that row after it is sent. They do not prove protection against this stale tracked write.

Evidence: [tracked cancellation](../src/WorkflowEngine.Infrastructure/Sla/WorkflowSlaSynchronizationService.cs#L219), [save after cancellation](../src/WorkflowEngine.Infrastructure/Sla/WorkflowSlaSynchronizationService.cs#L160), [worker completion](../src/WorkflowEngine.Infrastructure/Notifications/ProcessNotificationOutboxService.cs#L138), [outbox mapping](../src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs#L413), [existing sequential test](../tests/WorkflowEngine.Tests/WorkflowSlaSynchronizationTests.cs#L49).

An in-memory transition guard would not solve either race. A conditional database update can protect the persisted status. It cannot recall a message already accepted by the sender. The cancellation policy must distinguish those two concerns.

**Confirming tests:** Use a controlled sender for cancellation during send. Use a second connection and a barrier between cancellation's query and save for the stale-write ordering. Check status, token, `SentAtUtc`, `CancelledAtUtc`, sender calls, and warnings.

### 4.4 Two delivery callers can produce a failure without an SMTP failure

**Verdict: the inventory's E05/E09 combination has this additional consequence.**

The action can commit a pending notification. The worker can then claim or send it before the API dispatcher queries pending rows. The dispatcher finds zero pending rows and returns failure. The API changes the committed action result to `NotificationDeliveryFailed`.

If both callers read the pending row first, the worker can win the atomic claim. The API dispatcher then also returns failure. These paths do not require a broken SMTP server.

This is conditional on the background worker running at the same time as immediate dispatch. It is not evidence that the target uses this dispatcher.

Evidence: [worker claim](../src/WorkflowEngine.Infrastructure/Notifications/ProcessNotificationOutboxService.cs#L81), [empty selection and lost claim](../workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionNotificationDispatcher.cs#L36), [result mutation](../workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs#L155).

**Confirming test:** Let the worker claim the action's row immediately after the action commits. Then run the dispatcher and inspect both the persisted workflow and the HTTP result. A helper extraction alone does not decide what the API should report.

### 4.5 Metadata validity is the real issue behind several engine machines

**Verdict: agree with generic validation, rather than more lifecycle classes.**

The generic executor can mark an instance completed while metadata leaves a task pending. It can also create a task on a terminal transition. Neither is prevented by the instance status check or the one-pending-task index.

The primary-email requirement is another cross-entity dependency. The SLA scan requires exactly one non-SLA outbox row for the task. It copies that row's body and HTML body. Reminder-specific recipient snapshots do not remove this dependency.

The first skipped scan might report missing recipients before it reaches the missing-primary-email check. Therefore “no primary email” does not imply that exact warning will always appear first.

Evidence: [terminal/task writes](../src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs#L675), [recipient selection](../src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs#L172), [primary-email dependency](../src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs#L219).

The existing nonliteral-terminal-state test proves terminal behaviour for a taskless workflow. It does not prove that invalid task metadata is refused. [Test source](../tests/WorkflowEngine.Tests/WorkflowTerminalStateTests.cs#L20).

**Confirming tests:** Supply a terminal transition that leaves an existing task open, another that creates a task, and an SLA task without a primary notification. Assert rollback or validation under the chosen future contract. A check-only command can report these conditions without changing runtime contracts.

### 4.6 Platform effects and availability: conditional, but valid gaps

**Rule locks:** Choose an editable form where a rule disables one field. Add an `Enable` effect from another field. The client can enable the target. The server retains its rule lock and refuses a changed submitted value. This is a consistency defect for metadata that uses these effects.

TTDV2's inspected effects are `SetFromOption` and `Clear`. Its present JSON does not exercise that `Enable`/`Show` case. Also, `build()` runs initial effects before it disables a form with `canSave = false`. The broader claim that initial effects leave every read-only form editable is not supported.

Evidence: [TTDV2 effects](../atlas-platform/src/BBH.CP.Atlas.App/Config/TTDV2.json#L53), [client initialization](../atlas-platform/WebUi/src/app/platform/form/atlas-form.component.ts#L108), [client effect application](../atlas-platform/WebUi/src/app/platform/form/atlas-effects.ts#L54), [server refusal](../atlas-platform/src/BBH.CP.Atlas.Service/Platform/Sections/AtlasAllowList.cs#L45).

**Cross-app availability:** App A can have a dashboard tab for app B. Suppose B's table loaded, B's setting is enabled, and a separate B validation error caused startup to disable B. The foreign-tab code checks the setting and View access. It does not check `AtlasDisabledApps`. A's startup check validates the tab table and query, rather than every B validation result.

That is a credible path to showing rows from a startup-disabled app. “Low risk” cannot be concluded without the actual metadata and the reason B was disabled. The View check still runs, so this finding does not by itself prove an authorization bypass.

Evidence: [foreign-app checks](../atlas-platform/src/BBH.CP.Atlas.Service/Platform/Dashboard/AtlasDashboardService.cs#L87), [tab startup checks](../atlas-platform/src/BBH.CP.Atlas.App/Platform/AtlasStartupCheck.cs#L328), [full resolver check](../atlas-platform/src/BBH.CP.Atlas.Service/Platform/Registry/AtlasModuleResolver.cs#L74).

**Confirming tests:** Test rule-disabled and rule-hidden targets in the actual form component. For availability, load B's table, disable B through the startup-disabled collection, and request A's foreign tab with otherwise valid View access.

### 4.7 Cut-over: a script-log row is not a complete record of the operation

**Verdict: agree with R01, R02, and the second-rollback inference.**

- **R01:** An administrator revokes a TTD grant within the preceding hour. The same administrator then runs `050`. Later, `090` can restore that deliberately revoked grant. The actor/time predicate cannot distinguish it from grants changed by `050`.
- **R02:** After rollback, users edit an existing TTD record and add history. A later `050` skips the record because its ID already exists in TTDV2. The audit copy skips later entries when any copied TTD audit entry exists for that record. It then revokes TTD access again.
- **Second rollback:** `050` does not add another log row. A later cut-over gives the grants a modification timestamp after the first recorded cut-over. `090` still searches the first timestamp's window. It can leave those TTD grants revoked while disabling TTDV2 grants too.

Evidence: [grant copy and revoke](../atlas-platform/sql/release-p1/050-ttdv2-copy-from-ttd.sql#L147), [record and history filters](../atlas-platform/sql/release-p1/050-ttdv2-copy-from-ttd.sql#L178), [single log row](../atlas-platform/sql/release-p1/050-ttdv2-copy-from-ttd.sql#L205), [restore window and TTDV2 disable](../atlas-platform/sql/release-p1/090-ttdv2-rollback.sql#L25).

R01 and R02 already have independent review test source. I inspected those tests but did not execute them. Their existence is not evidence of a fresh passing or failing run. [Existing reproductions](../atlas-platform/review/2026-11-01/IndependentReviewTests.cs#L31).

**Confirming tests:** Run only in an isolated disposable database. Include intentional prior revocation, re-cutover after business edits, and a second rollback. Use distinct timestamps for each operation. Reconciliation must also consider edits made in TTDV2 before rollback. Blindly replacing TTDV2 rows would not be a safe general correction.

### 4.8 Record history, delete concurrency, and business no-op saves

The P01 history claim needs correction. `GetAuditLog()` passes a possibly null active record to the History permission check and continues reading audit. It does not throw NotFound. An existing test deletes a record and then reads its Create, Update, and Delete history.

Evidence: [history method](../atlas-platform/src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs#L758), [test after deletion](../atlas-platform/tests/BBH.CP.Atlas.Platform.Tests/Sections/Ttdv2SectionTests.cs#L235).

Delete lacks protection against changes since the user's form load. It still uses a tracked record with rowversion mapping, so it can detect a conflicting write after the delete request's own read. Those are different concurrency windows. The report should not be read as saying Delete has no database concurrency protection at all.

Evidence: [tracked delete read](../atlas-platform/src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs#L730), [rowversion mapping](../atlas-platform/src/BBH.CP.Atlas.Data/Platform/AtlasPlatformContext.cs#L63), [conflict handling](../atlas-platform/src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs#L692).

A business no-op save updates `ModifiedBy` and `ModifiedDate`. If either tracked value changes, a database update can change rowversion even when no business audit entry is added. If those values remain identical, the source alone does not justify saying an update always occurs.

Evidence: [tracked mapping](../atlas-platform/src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs#L436), [system fields](../atlas-platform/src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs#L476), [audit omission](../atlas-platform/src/BBH.CP.Atlas.Service/Platform/Audit/AtlasAuditWriter.cs#L30).

**Confirming tests:** Separate stale-form deletion from a write concurrent with the delete request. For a no-op save, advance the injected clock and check the rowversion and the audit count independently.

## 5. Corrections to the claimed invariants

| Inventory rule | Correction or qualification |
|---|---|
| I1: Completed instance has no pending task | A desired invariant, not generic enforcement. The terminal transition's metadata can violate it. |
| I2: Completed task has no active schedule | Sequential completion enforces it. The late synchronization sequence can violate it. |
| I3: Inactive schedule has no pending/processing reminders | Intended cancellation rule. It does not prove that no email was sent. Concurrent delivery and tracked cancellation need separate checks. |
| I4: Pending SLA task has one active schedule | Normal creation is transactional. The filtered index enforces **at most one**, not existence. Metadata or direct data changes can still leave none. |
| I5: Task due date equals active schedule overdue time | Synchronization writes both. This is not an independent database relationship constraint. |
| I6: One pending task for each instance | Correct wording: **at most one pending task**. Draft, terminal, and taskless workflows can have zero. |
| I7: One active version | No database constraint enforces it. Inactive workflows can also intentionally have no active version. |
| I8: SLA task has a primary email | Required by the current scan, but not a generic metadata invariant checked before execution. |
| I9: SQMS projections equal engine state | Scheduled transitions enforce the writes together. User-action consistency depends on target integration, which this review did not inspect. |
| I10: Client/server field state matches | Shared effect and validator cases cover parts of the behaviour. They do not prove parity of rule locks, whole-form permission handling, and component operation lifetimes. |

The supporting constraints and write paths are linked in sections 2 to 4. These statements distinguish enforced limits from desired relationships.

## 6. My disposition of W1 to W13

These are recommendations, not an implementation plan or approval to change code.

| Work item | My take |
|---|---|
| W1 Transition tests | **Support, narrowed.** Start with SQL interleavings and cross-entity invariants for the selected fix. Do not require a new class for every named machine before fixing a specific defect. Existing behaviour tests already cover many normal transitions. |
| W2 Status constants | **Optional, later.** Preserve every persisted string. Make small edits when touching the affected service. The literal count is not a defect count. Constants do not provide concurrency safety. |
| W3 Shared delivery helper | **Support after behaviour decisions.** Share proven common operations. Keep recovery selection, retry fairness, and API delivery-result semantics explicit. Do not automatically give the API every worker responsibility. |
| W4 SaveDraft race | **High priority when engine fixes resume.** Reproduce the final-check gap. Preserve SaveDraft's existing version semantics. |
| W5 Cancel during send | **Support and expand.** Include stale tracked cancellation overwriting `Sent`, as well as cancellation while the sender runs. Agree on delivery versus cancellation semantics first. |
| W6 SLA-only no-task result | **Support.** Deterministic invalid-state handling is worthwhile. Decide the public response and status behaviour before implementation. |
| W7 Generic metadata validation | **Support before additional workflow onboarding.** Include terminal task rules, first-state validity, SLA primary-email requirements, active metadata references, and active-version selection. A check command can be separate from runtime behaviour changes. |
| W8 SLA completion race | **High priority alongside W4.** Protect the task/schedule relationship at the database write boundary. Schedule rowversion alone is insufficient. |
| W9 Availability function | **Support a narrow correction.** Test foreign dashboard tabs first. Preserve legacy menu behaviour, fall-through, and public error results. The current full resolver check need not become a new framework. |
| W10 Client operation state | **Support the delete guard first.** Make Save and Delete mutually exclusive while a request is active. A full page-state enum is optional. The dashboard issue is confirmed in the local reference component only. |
| W11 Client rule locks | **Support before using conflicting Show/Enable effects.** Preserve server authority. Test the full component and rule context, not only the pure effect helper. TTDV2's current effects do not exercise the reported case. |
| W12 Engine adapter | **Keep as planned feature work.** Its missing state/action support is real. It is not a prerequisite for every cleanup item or for the current workflow-free pilot. |
| W13 Cut-over state | **High priority before cut-over/rollback use.** Record exact affected grants or define another reliable reconciliation method. Define repeat cut-over behaviour. Changes to `050` or `090` require the existing scoped approval. |

Within approved scope, I would prioritize W13 before operational migration use. For engine correctness, I would prioritize W4 and W8, then the W5 delivery races. W6 is a smaller deterministic correction. W10 is a focused client correction. W7, W9, and W11 matter before broader metadata use. W2 is not a prerequisite for any of these.

The existing E09 committed-action result ambiguity and E06 assignment-role hash omission also remain substantive concerns. They should not disappear behind a status-constant refactor.

## 7. Completion record

Changed: Added this independent assessment and a review checkpoint in `CODEX_HANDOFF.md`. No runtime or SQL implementation changed.

Why: Give a source-backed verdict for all 24 findings, correct overstated claims, and identify the most useful confirmation tests.

Tests run: None. This was a source review. The concurrency sequences and conditional UI paths remain unexecuted. Documentation links and the final diff were checked separately.

Seed verification run: Not run. No engine seed changed. Not applicable for the platform documentation changes.

Sample JSON executed: Not run. No workflow execution was requested. Not applicable for the platform documentation changes.

Files updated: `docs/IMPLICIT_STATE_MACHINES_ASSESSMENT.md`, `CODEX_HANDOFF.md`.

Open issues: The defects remain unfixed. Runtime confirmation, target behaviour, and implementation decisions remain open. The review does not resume paused engine implementation.

Package rebuilt: No. No copied platform file or `INTEGRATION.md` changed.
