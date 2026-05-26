---
phase: 5
reviewers: [claude]
reviewed_at: 2026-05-26T21:28:55.4142554Z
plans_reviewed: [05-01-PLAN.md, 05-02-PLAN.md, 05-03-PLAN.md]
---

# Cross-AI Plan Review - Phase 5

## Claude Review

# Cross-AI Plan Review: Phase 5 — SLA Monitor and Notification Senders

## Overall Summary

The three Phase 5 plans are tightly scoped, well-aligned with the locked decisions in `05-CONTEXT.md`, and correctly partition the work into (1) SLA queueing, (2) sender abstraction + selection, and (3) outbox processing + SMTP test boundary. The `must_haves.truths` blocks faithfully restate the D-## locks, and the dependency graph (`05-03 depends_on [05-01, 05-02]`) reflects real coupling — the outbox processor consumes both the SLA-queued rows and the sender abstraction. The plans are appropriate in size, avoid forbidden patterns (CQRS, brokers, `DateTime.UtcNow`, role-email SQL tables), and stay within the MailKit-only package constraint. The main weaknesses are a few unhandled edge cases around fixed-time `TimeProvider` scoping, transaction semantics for SQL Server / EF Core, parallel execution risk between 05-01 and 05-02, and a `files_modified` overlap that could cause merge churn.

---

## Plan 05-01: SLA Monitor

### Strengths
- Faithfully encodes the D-01..D-09 locks as `truths`, including the strict `<` boundary (D-02) and the "Pending OR Sent" suppression rule (D-03).
- Correctly separates the SLA scan (no transaction, materialize first) from per-row queueing transactions (D-06).
- Test acceptance criteria cover the genuinely tricky cases: boundary, null due dates, completed task/workflow exclusion, Pending+Sent duplicate suppression, and rerun idempotency.
- Reuses the existing `WorkflowExecutionValidationTests.CreateSeededContext()` + `FixedTimeProvider` patterns rather than inventing new infrastructure.

### Concerns

- **[MEDIUM] `--now` fixed-time TimeProvider mechanism is under-specified.** D-05 says `--now` "registers a fixed-time `TimeProvider` for command duration." But `TimeProvider` is registered as a singleton in `Program.cs` (per CLAUDE.md). Swapping the DI registration mid-process for one command is fragile and could leak into other resolved services. The plan should specify the mechanism: either (a) pass `TimeProvider` as a constructor argument that the command handler overrides via a scoped/factory injection, (b) use an ambient `FixedTimeProvider` wrapper that the handler sets and the service reads, or (c) pass `now` as an explicit parameter into `IRunSlaService.ScanAsync(DateTimeOffset now, ...)`. Option (c) is cleanest and avoids DI mutation.
- **[MEDIUM] No `RouteSlaTaskOverdue` definition is referenced.** Task 2's `<action>` says "using route `RouteSlaTaskOverdue` metadata," but Phase 5 context never confirms a seeded `NotificationRoute` row exists for `SlaTaskOverdue`. Phase 4's existing flow seeds workflow-transition notifications; an SLA-overdue route may or may not exist. The plan should add a verification step ("confirm `NotificationRoutes` contains a `SlaTaskOverdue` route, else add to seed") or specify the recipient-resolution fallback. Without this, the queue insert will either fail FK validation or insert NULL `RecipientRoleKey`.
- **[MEDIUM] Race between `NOT EXISTS` query filter and unique index insert.** The query excludes tasks with an existing `SlaTaskOverdue` row, but if two `run-sla` processes run concurrently, both can pass the filter and both attempt to insert. The DB unique filtered index will reject the second, but the plan does not state that the per-row transaction catches the unique constraint violation gracefully — it should either (a) accept `DbUpdateException` on unique-index conflict as a "skipped" outcome and continue, or (b) document concurrent `run-sla` as unsupported (parallel to D-15 for the outbox processor). Without this, a concurrent run will throw a hard exception mid-batch.
- **[LOW] `files_modified` declares `tests/WorkflowEngine.Tests/CommandRegistryTests.cs`, which is also declared by plan 05-03 Task 3.** Both plans modify the same registry test file. Since they run in different waves this is sequenceable, but the second plan must read what the first wrote — flag it explicitly to avoid an overwrite.
- **[LOW] Output format spec ("Now:", "Command: RunSla", etc.) is informal.** Tests that assert on output strings will couple to wording. Consider naming a constant or capturing output via a structured result and rendering separately, so output cosmetic changes don't break assertions.
- **[LOW] No test for `--now` parsing failure cases.** Acceptance says "missing/invalid `--now` returns a non-zero result," but the test file list doesn't enumerate parse-error coverage. Add a `RunSlaCommand_InvalidNow_ReturnsNonZero` case explicitly.

### Suggestions
- Make `IRunSlaService.ScanAsync` take `DateTimeOffset now` (or a `TimeProvider`) as a parameter, sidestepping DI mutation entirely.
- Add an explicit task: "Verify or seed a `NotificationRoute` row for `SlaTaskOverdue` mapped to the task's assignee role."
- Add an acceptance criterion: "Concurrent `run-sla` invocations either skip-on-conflict via `DbUpdateException` or are documented as unsupported."

### Risk: **MEDIUM**
The query and locks are correct on paper, but the `TimeProvider` mechanism and `NotificationRoute` dependency are concrete implementation gaps that will likely surface during execution.

---

## Plan 05-02: Sender Abstraction and Configuration

### Strengths
- Cleanly separates the contract (Application layer) from implementations (Infrastructure), preserving the layering invariant.
- Locks SMTP failure modes at construction (`Smtp:Host` missing, role mapping missing) per D-23, which is the right place to fail-fast.
- Tier 1 sender unit tests stay network-free and SMTP credentials are explicitly excluded — matching `PROJECT.md` §22.
- Configuration precedence (env var → environment json → base json) is correctly stated per D-20.

### Concerns

- **[HIGH] Wave 1 parallel execution with 05-01 over `InfrastructureServiceCollectionExtensions.cs` and `Program.cs`.** Both 05-01 and 05-02 declare `src/WorkflowEngine.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` and `src/WorkflowEngine.Console/Program.cs` in `files_modified`. If executed in parallel they will produce merge conflicts in DI wiring. Either (a) serialize 05-01 and 05-02 (introduce `depends_on: [05-02]` on 05-01 so the sender is available when SLA wires DI), (b) carve out a separate "wiring" sub-plan that runs after both, or (c) coordinate the two execution agents to touch disjoint regions of the same file.
- **[MEDIUM] D-22 says tests "remove `INotificationSender` and register `RecordingNotificationSender`," but `RecordingNotificationSender` is not in the plan's deliverables.** It's mentioned in `must_haves.truths` and in 05-03 Task 5, but no plan explicitly creates it. Add it to 05-02 Task 2 alongside Console/NoOp, since it's a test-only sender that lives in the test project — the plan should name `tests/WorkflowEngine.Tests/RecordingNotificationSender.cs` as a deliverable.
- **[MEDIUM] `NotificationDelivery` record shape is under-specified for SMTP.** The plan says it carries "outbox id, workflow instance id, task id, notification type, recipient role/user, subject, payload JSON, and created time." But SMTP must resolve role → email via `Smtp:RoleEmailMap` and apply `Smtp:RecipientOverride`. If `NotificationDelivery` carries only the role key, the SMTP sender must inject `IOptions<SmtpOptions>` — fine, but the plan should state this so the contract isn't accidentally redesigned to push email addresses through (which would leak SMTP concerns into the abstraction).
- **[MEDIUM] No test for "what happens if `Notifications:Sender` is empty string vs. missing key."** D-18 says "default Console" — does default apply only when the key is missing, or also when value is `""` or whitespace? After "trimming" per D-18, an all-whitespace value becomes empty; should that be "Console" (treat-as-missing) or fail-fast (unknown)? Acceptance criteria should pin this.
- **[LOW] `Smtp:From` and `Smtp:RoleEmailMap` validation timing is inconsistent.** D-23 names only `Smtp:Host` for fail-fast at construction. Task 3 acceptance also requires missing-role-mapping to throw — but "at construction or send time" leaves it ambiguous. If role map is validated at construction, an empty map fails startup even when no notifications need sending; if at send time, the failure happens mid-batch and aborts the command (D-12). The plan should pick one and state it. Construction-time is more aligned with D-23's spirit.
- **[LOW] No assertion that MailKit is the *only* new dependency added.** A grep-style check ("`dotnet list package` adds only MailKit") would lock down the `PROJECT.md` §22 constraint.

### Suggestions
- Move `Program.cs` and `InfrastructureServiceCollectionExtensions.cs` edits to a dedicated wave-2 wiring plan, OR add `depends_on: [05-02]` to 05-01.
- Explicitly add `RecordingNotificationSender` to 05-02 Task 2 deliverables.
- State that `NotificationDelivery` carries the role key (not email); SMTP resolves email internally.
- Pin the empty-string and whitespace behavior of `Notifications:Sender`.

### Risk: **MEDIUM-HIGH**
The parallel execution conflict on DI/Program.cs files is a near-certain merge problem unless coordination is explicit. The substantive design is sound.

---

## Plan 05-03: Outbox Processor + SMTP Test Boundary

### Strengths
- Captures the trickiest lock (D-12 fail-fast mid-batch with earlier rows preserved) and tests it explicitly.
- Optimistic-update `WHERE Status='Pending'` with the zero-row warning (D-14) is encoded in both `truths` and acceptance criteria.
- Tier 2 SMTP exclusion is configured via `xunit.runner.json` and `Category=Smtp`, matching D-28.
- Includes a real end-to-end test (Task 5) that walks the full SLA → outbox → sent path, which is the highest-value smoke for the phase.
- `user_setup` correctly flags the Tier 2 SMTP prerequisite, which is the right place per AGENTS.MD.

### Concerns

- **[HIGH] Per-row `TransactionScope` / EF transaction + sender call has a partial-failure window.** D-11 says "open a transaction, call `INotificationSender.SendAsync`, update Status=Sent, commit." If `SendAsync` succeeds (email actually sent) but the commit fails (connection drop, timeout), the row stays `Pending` and the next run will resend — duplicate external delivery. This is partially acknowledged in D-15 ("duplicate external sends are possible under concurrent SMTP invocations") but D-15 only addresses concurrency, not commit failure. The plan should either (a) acknowledge that commit-failure-after-send is also an at-least-once boundary for v1, or (b) reorder to update-then-send within the transaction (which has the opposite failure mode: sent=true but email never went out). Either is defensible for v1, but the plan should state the choice. Recommend explicitly: "Send-then-update; at-least-once delivery semantics are accepted for v1."
- **[MEDIUM] SQL Server + EF Core: `WHERE Status='Pending'` optimistic update isn't quite a clean primitive.** EF Core's `SaveChanges` against a tracked entity won't emit `WHERE Status='Pending'` unless the entity has a concurrency token or you use `ExecuteUpdate`/raw SQL. The plan should specify the mechanism — most likely `context.NotificationOutbox.Where(o => o.Id == id && o.Status == "Pending").ExecuteUpdate(...)` — otherwise an implementer might use tracked-entity updates and silently lose the D-14 guard.
- **[MEDIUM] Task 2's "abort the command, leaves current/later rows Pending, and exits non-zero" — but exceptions in a command handler don't cleanly map to exit codes.** Task 3 says "sender exceptions surface as non-zero command results or propagated exceptions consistently with the console command pattern." The plan should pick one: catch in the handler and return non-zero (cleaner output, no stack trace), or let it propagate and rely on `Program.cs` exit-code mapping. The current "either/or" wording invites inconsistency between this command and `run-sla`.
- **[MEDIUM] Task 4 doesn't specify how `xunit.runner.json` excludes `Category=Smtp` by default.** xUnit's runner config doesn't directly support category exclusion via JSON — it requires assembly-level `[Trait]` discovery and `--filter` at runtime, *or* a `.runsettings` file for VSTest, *or* `xunit.runner.visualstudio` with method-discovery rules. The implementer needs concrete guidance. A common working pattern is: tests are tagged `[Trait("Category","Smtp")]`, and CI / dev scripts pass `--filter Category!=Smtp`. The plan should reflect that and update `CLAUDE.md`'s documented filter accordingly (note: `CLAUDE.md` already shows `--filter "Category!=Smtp&Category!=FreshClone&Category!=Performance"` for CI).
- **[MEDIUM] Task 5 end-to-end test assumes the outbox processor will accept any sender — but `RecordingNotificationSender` is registered by replacing DI per D-22.** The test must explicitly construct a WebApplicationFactory-like override or use a manual service-provider build. The acceptance criteria should specify how the recording sender is wired in (e.g., "test builds a service collection, calls `AddWorkflowInfrastructure`, then `services.RemoveAll<INotificationSender>(); services.AddSingleton<INotificationSender, RecordingNotificationSender>();`").
- **[LOW] Tier 2 SMTP test "per-send connection lifecycle" is vague.** What does "per-send" mean? MailKit's `SmtpClient` is typically connected, authenticated, sent-on, then disconnected per message in low-volume scenarios — but it can also be reused. The acceptance should pin which behavior is verified.
- **[LOW] `tests/WorkflowEngine.Tests/README.md` is the documentation target, but `PROJECT.md` Phase 6 (DOC-04) calls for the main README to cover sender configuration.** The test-project README is fine for setup, but Phase 6 should also update the root README. Note this as a Phase 6 hand-off rather than expanding 05-03 scope.
- **[LOW] No explicit verification that `process-notification-outbox` honors the sender's `SenderName` banner (D-24) when SMTP is selected.** Add an acceptance row: "Banner correctly prints SenderName for Console, NoOp, and Smtp."

### Suggestions
- Pin the send-then-update at-least-once semantic explicitly in the plan.
- Specify `ExecuteUpdate` (EF Core 7+) for the optimistic status update — it's available in .NET 10 EF Core and is the only clean way to emit `WHERE Status='Pending'`.
- Choose one exception-handling pattern (catch + non-zero return) and apply it to both `run-sla` and `process-notification-outbox`.
- Replace the `xunit.runner.json` mechanism with a documented `--filter Category!=Smtp` invocation, and update repo scripts/CI to match. (CLAUDE.md already does this — align the plan to it.)
- Add a 6th task or expand Task 4: "Update root README to document `Notifications:Sender` config keys and Tier 2 opt-in command." If kept in Phase 6, link it explicitly.

### Risk: **MEDIUM-HIGH**
The transactional semantics around send + commit, the EF Core mechanism for the optimistic update, and the Tier 2 exclusion mechanism each have a meaningful chance of being implemented wrong without further specification.

---

## Cross-Plan Concerns

- **[HIGH] Wave 1 parallel execution conflict.** 05-01 and 05-02 both modify `InfrastructureServiceCollectionExtensions.cs` and `Program.cs`. Either serialize the plans or carve wiring into a separate sequencing step.
- **[MEDIUM] `CommandRegistry.cs` and `CommandRegistryTests.cs` are touched by both 05-01 and 05-03.** Sequence-safe but worth flagging — 05-03 must read 05-01's state, not rewrite the SLA registration.
- **[MEDIUM] `RecordingNotificationSender` is referenced by 05-02 (D-22 truth) and 05-03 (Task 5) but not deliverably owned by either.** Assign to 05-02 Task 2.
- **[MEDIUM] No plan covers updating `appsettings.Development.json` or documenting that the dev environment must NOT set `Notifications:Sender=Smtp`.** D-21 says "production enables SMTP by configuration only," but the dev/test guardrail belongs in the committed `appsettings.Development.json` defaults or a comment. Worth a one-line note.
- **[LOW] `PROJECT.md` §20 calls for tests covering "notification senders, ... SLA" — the test names across plans are good, but no plan explicitly cross-references §20 to confirm every required test name is present.** A consolidation check at end of Phase 5 would catch gaps before Phase 6.

---

## Overall Risk Assessment: **MEDIUM**

The plans correctly capture the locked decisions and decompose work along clean seams. The risk is concentrated in three areas that need clarification before execution rather than in fundamental design flaws:

1. **Parallel execution conflicts** on shared wiring files (HIGH likelihood, easy fix: serialize or split).
2. **Transactional and TimeProvider mechanisms** that are stated in prose but not pinned to a concrete .NET/EF Core implementation pattern (MEDIUM likelihood of inconsistent implementation).
3. **A few cross-plan ownership gaps** (`RecordingNotificationSender`, `NotificationRoute` seed for `SlaTaskOverdue`, README updates) that could cause execution stalls.

None of these are showstoppers — they're tighten-the-spec items. With the suggested adjustments (especially serializing wave 1 or splitting wiring out, specifying `ExecuteUpdate` for the optimistic status update, and clarifying the `--now` `TimeProvider` mechanism), Phase 5 should land cleanly within its existing scope and satisfy SLA-01 through SLA-04.


---

## Consensus Summary

Only one independent external reviewer was available (claude); codex was detected but skipped because the current session is Codex. No multi-reviewer consensus can be claimed from this run.

### Agreed Strengths

Single-reviewer run; see Claude Review above.

### Agreed Concerns

Single-reviewer run; see Claude Review above.

### Divergent Views

No divergent views are available because only one external reviewer ran.
