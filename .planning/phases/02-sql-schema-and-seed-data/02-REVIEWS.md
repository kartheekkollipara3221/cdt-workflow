---
phase: 2
reviewers: [claude]
reviewed_at: 2026-05-26T21:23:45.0296672Z
plans_reviewed: [02-01-PLAN.md]
---

# Cross-AI Plan Review - Phase 2

## Claude Review

# Phase 2 Plan Review: SQL Schema and Seed Data

## 1. Summary

The plan is a single-file, four-task execute-plan that cleanly covers the Phase 2 scope: generic EF entities + migration, deterministic Portfolio Trade seed, real `init-db`/`verify-seed` handlers, and focused schema/seed tests. It traces well to requirements (SQL-01..04, PT-01) and the D-01..D-14 decisions, calls out the right canonical references, and respects the reusable-engine boundary. The main gaps are around concrete SQL-fidelity details (status check constraints, the literal index filter strings, version-scoped uniqueness wording), test infrastructure (how SQL-backed tests reach a database without `WORKFLOW_ENGINE_TEST_CONNECTION_STRING` being mentioned), and a missing explicit `dotnet ef` tool-manifest step that the research file flagged as a risk.

## 2. Strengths

- **Tight requirement traceability.** The frontmatter maps requirements → must_haves → decisions → artifacts, so each task is anchored.
- **Reusable-engine boundary enforced.** Verification includes `rg "PortfolioTrade" src/WorkflowEngine.Application src/WorkflowEngine.Domain` returning no matches, plus the AdminDemoOverride grep — these are the right guardrails given D-02/D-07.
- **Idempotent seed framing is correct.** D-05/D-10 ("upsert by stable keys, don't delete runtime data") is captured both as a constraint and as a task action.
- **Filtered-index literals are called out.** The plan explicitly names `WHERE Status = 'Pending'` and the `SlaTaskOverdue` filter — both of these are exactly the strings that Phase 4/5 will depend on, and getting them wrong silently is a classic foot-gun.
- **Test discipline.** Explicit "no EF InMemory for persistence/seed checks" and "no Phase 4 side-effect assertions" prevents scope creep into transition execution.
- **Phase boundary clarity.** Deferred ideas are spelled out so a downstream agent won't pull engine work into Phase 2.

## 3. Concerns

### HIGH

- **SQL-backed tests have no documented connection strategy.** D-12 forbids InMemory but the plan never says where the test DB comes from. The Phase 2 research file flags this exact risk ("local SQL Server named in development config may be unavailable from the sandbox") and Phase 1's note says local builds need `-m:1`. The plan should commit to a concrete approach: env-var override (`WORKFLOW_ENGINE_TEST_CONNECTION_STRING` per CLAUDE.md), per-test unique database name suffix, and a clean teardown path. Without this, tests will either be flaky, share state across runs, or be skipped without a documented reason — which contradicts the "document the exact failure" guidance.
- **No status check constraints on `WorkflowInstance.Status` / `WorkflowTask.Status`.** CLAUDE.md describes these as DB-level check constraints (`∈ {Active, Completed}` / `∈ {Pending, Completed}`), and D-04 says "use exact v1 status strings." The plan only mandates the strings in the filtered index — but the filtered index does not stop someone inserting `Status='Open'`. The migration should add `CHECK` constraints; the plan's acceptance criteria do not require that.
- **`verify-seed` validation surface is under-specified.** D-11 demands validation of (a) full transition matrix content, (b) notification route resolution, (c) SLA policy resolution, (d) uniqueness assumptions, (e) version-scoped key references that composite FKs do not enforce, and (f) action presentation hints / Maker button hints. The Task 3 action says "run SQL-backed validations and report all failures clearly" but the acceptance criteria only check for "actionable missing/mismatched seed details" generically. There is no concrete checklist — risk that `verify-seed` ships as a happy-path probe and Phase 4 finds gaps the verifier should have caught.

### MEDIUM

- **EF migration tooling step is missing.** Research §Risks explicitly says "If `dotnet ef` is unavailable, add a local tool manifest and `Microsoft.EntityFrameworkCore.Design`." The plan mentions adding the design package "only if needed" but never owns the tool-manifest decision. CLAUDE.md confirms a `dotnet-tools.json` with `dotnet-ef` 10.0.7 ended up being the right answer — Task 1 should call this out as a deliverable, not leave it conditional.
- **`WorkflowInstance.Version` initial value not enforced in schema.** PROJECT.md constraint "Version starts at 1 and increments only on state-changing transitions" — Phase 4 owns the increment, but Phase 2 owns the column default. The plan does not list this in Task 1's acceptance criteria.
- **Definition-key uniqueness wording is ambiguous.** D-03 says "definition-key uniqueness" — for nested defs (states, actions, roles, tasks, transitions) this must be scoped to `WorkflowDefinitionVersionId`, not globally. Research §"EF Core mappings should enforce" is precise. Task 1's acceptance criteria just says "required unique constraints are configured" — a careless reading could produce globally unique keys that break a second workflow definition.
- **No mention of cascade delete behavior.** CLAUDE.md (which post-dates the plan but reflects what was built) says runtime FKs cascade off `WorkflowInstance`. This is a real schema decision Phase 4 depends on, and the plan does not surface it. If left to the default convention, the migration may end up with `Restrict` and Phase 4 will hit FK errors during instance lifecycle tests.
- **`NotificationOutbox` unique filter uses the literal string `'SlaTaskOverdue'`.** This is correct, but the plan does not say the literal must match the runtime `NotificationType` value the seed emits. A typo (`SLATaskOverdue`, `SlaOverdue`, `'SlaTaskOverdue '`) wouldn't fail the build or seed but would silently disable the duplicate-protection guarantee SLA-02 depends on. Worth elevating to a verify-seed check.
- **`TimeProvider` is mentioned (D-14) but Phase 2 has no place that needs it.** Seed data is deterministic; if any `CreatedAtUtc`/`UpdatedAtUtc` columns are timestamped at seed time, they must come from injected `TimeProvider` rather than `DateTime.UtcNow`. The plan should either explicitly say "no seed timestamps in Phase 2" or "all timestamps via injected TimeProvider" — currently it's just a reminder with no enforcement hook.

### LOW

- **Wildcard file globs in `files_modified`.** `src/WorkflowEngine.Infrastructure/Persistence/Entities/*.cs` and similar are pragmatic but make the plan's diff scope hard to bound. Listing the entity names explicitly would also serve as a checklist.
- **Verification grep is too narrow.** `rg "PortfolioTrade" src/WorkflowEngine.Application src/WorkflowEngine.Domain` is good but doesn't catch leakage into `WorkflowEngine.Infrastructure` outside `Persistence/Seed`. A more targeted grep — e.g., excluding the seed folder — would catch a Portfolio-Trade-named service or entity in infrastructure.
- **No assertion that `init-db` is rerunnable.** D-10 demands idempotence. There is no verification step like "run `init-db` twice and confirm seed rows are unchanged."
- **Task 3 description "Refactor command dispatch enough to let..."** is fuzzy. What "enough" means is left to the implementer; a one-sentence description of the target shape (handler interface + DI lookup vs. switch statement) would prevent dispatcher churn in later phases.
- **Output deliverable is a single summary file.** Phase 2 has four tasks and a non-trivial schema; a brief schema diagram or a textual ERD in the summary would help downstream phases — but this is a nice-to-have.

## 4. Suggestions

1. **Add a "Test infrastructure" subsection** specifying: env-var lookup order, per-test unique database name suffix, teardown, and the documented skip path when SQL Server is unavailable (matching CLAUDE.md's `WORKFLOW_ENGINE_TEST_CONNECTION_STRING`).
2. **Promote check constraints to Task 1 acceptance criteria**: `WorkflowInstance.Status IN ('Active','Completed')`, `WorkflowTask.Status IN ('Pending','Completed')`, `WorkflowInstance.Version >= 1` with default 1.
3. **Make `verify-seed` validations a numbered checklist** in Task 3 — one bullet per check from D-11, plus a "notification-type literal matches filtered index" assertion.
4. **Add a tool-manifest task or step** to Task 1: `dotnet new tool-manifest` + `dotnet tool install dotnet-ef`. Don't leave it conditional.
5. **Tighten definition-key uniqueness wording** in Task 1's acceptance criteria: "all nested definition keys (state/action/role/task/transition) are unique within `WorkflowDefinitionVersionId`, not globally."
6. **Specify cascade behavior** for runtime FKs off `WorkflowInstance` in Task 1 — this avoids Phase 4 rework.
7. **Add an idempotency verification step**: run `init-db` twice and assert row counts unchanged. Also assert that a pre-existing dummy runtime row in `WorkflowInstance` survives a second `init-db`.
8. **Tighten Portfolio-Trade leakage grep** to include `src/WorkflowEngine.Infrastructure` minus the seed directory.
9. **List entities explicitly** in `files_modified` to act as a 15-item checklist matching the research's schema scope.

## 5. Risk Assessment

**Overall: MEDIUM.**

The plan's structure is sound and the right guardrails are named, but several schema-fidelity details that Phase 4/5 silently depend on (status check constraints, version-scoped uniqueness, notification-type filter literal alignment, cascade behavior, `Version` default) are not in the acceptance criteria. The test-infrastructure question is genuinely high-risk — without a concrete approach, Phase 2 either ships with disabled tests (violating the "explain exactly" rule) or with a fragile shared-DB test setup. `verify-seed`'s under-specified validation surface is the second material concern: it's framed as a guardrail but the plan doesn't enforce that it actually catches each class of seed defect. The reusable-engine boundary, idempotent-seed framing, and deferred-work discipline are all strong — those are the failure modes that would have made this a HIGH-risk plan, and the plan handles them correctly.


---

## Consensus Summary

Only one independent external reviewer was available (claude); codex was detected but skipped because the current session is Codex. No multi-reviewer consensus can be claimed from this run.

### Agreed Strengths

Single-reviewer run; see Claude Review above.

### Agreed Concerns

Single-reviewer run; see Claude Review above.

### Divergent Views

No divergent views are available because only one external reviewer ran.
