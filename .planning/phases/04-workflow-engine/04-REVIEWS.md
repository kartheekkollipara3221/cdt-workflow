---
phase: 4
reviewers: [claude]
reviewed_at: 2026-05-26T21:27:06.9466017Z
plans_reviewed: [04-01-PLAN.md, 04-02-PLAN.md, 04-03-PLAN.md, 04-04-PLAN.md, 04-05-PLAN.md]
---

# Cross-AI Plan Review - Phase 4

## Claude Review

# Phase 4 Plan Review: Workflow Engine

## Overall Summary

The four execution plans plus one gap-closure plan form a well-structured, dependency-ordered decomposition of a conceptually heavy phase. The slicing matches the research recommendation (contract foundation → start/reset → validation core → side effects), and each plan cites locked decision IDs in `must_haves.truths`, which makes the decision-coverage gate enforceable. The plans are detailed enough to execute without further clarification, but a handful of concrete risks around transaction boundaries, idempotency race semantics, and the cross-plan reuse of `WorkflowExecutionService` deserve attention before Plan 04-04 begins.

---

## Plan 04-01: Result Contract Foundation

### Strengths
- Correct dependency placement (wave 1, no deps). Builder + canonical serializer land before any service can scatter result construction.
- Explicit separation of display JSON (`WorkflowJson.Options`) from canonical persistence JSON avoids the trap called out in research.
- `D-50` recursion concern (resultHash excluding diagnostics) is called out twice — in `must_haves` and Task 3 acceptance.
- Task 4 keeps the Phase 3 `AcceptedForPhase3Validation` path alive so existing tests keep passing, which is the right call for a foundation plan.

### Concerns
- **MEDIUM — Canonical JSON cross-cutting requirement underspecified.** Plan 04-01 builds `CanonicalWorkflowResultSerializer`, but Plan 04-03 reuses canonical JSON via `CanonicalRequestHasher` and may also need canonical envelope JSON. Task 3 only mandates canonical *result* JSON. If `CanonicalRequestHasher` already has a canonicalizer (Phase 3), the two implementations should share a primitive. The plan doesn't say whether to factor the canonicalizer out or duplicate it.
- **LOW — D-50 says `replay` is always present in diagnostics, but `validationErrors` is conditional.** The DTO design should make `replay` non-nullable (default `false`) and `validationErrors` nullable. The plan doesn't pin nullability of these fields, which can cause `null` vs missing-property test churn in Plan 04-04.
- **LOW — `existing*` fields are described as "absent for other result codes" (D-52).** With `System.Text.Json`, "absent" requires `JsonIgnoreCondition.WhenWritingNull` or per-property attributes. Task 1 says "appears only when populated" but doesn't say whether absent means JSON null or property missing. Pin one to avoid downstream test divergence.
- **LOW — Task 4 keeps `AcceptedForPhase3Validation` alive but Plan 04-03 removes it.** A note that this result code is transitional (will be deleted in 04-03) would prevent it being mistaken for a permanent contract.

### Suggestions
- Add a sub-task to Task 3 to extract a shared `CanonicalJsonWriter` primitive usable by both `CanonicalRequestHasher` and `CanonicalWorkflowResultSerializer`.
- Pin `JsonIgnoreCondition` strategy in Task 1 acceptance criteria.

### Risk: **LOW**

---

## Plan 04-02: Start Workflow and Reset

### Strengths
- The locked delete order (D-34) is reproduced verbatim in `must_haves`; the FK cascade hazard (delete instance before children would break) is structurally avoided.
- D-29 (concurrent unique violations propagate) is explicitly preserved — a common mistake is to translate *all* unique violations into `AlreadyExists`.
- Task 3 verification explicitly tests "runtime rows for other workflow instances remain untouched," which catches the bounded-delete invariant.
- `BusinessObjectType = workflowKey` decision in Task 4 is documented (not just implied).

### Concerns
- **MEDIUM — Reset transactional rollback test is hand-waved.** Task 3 says "transaction rollback if a failure is injected." Injecting a failure between deletes and the fresh insert with EF Core + SQL Server is non-trivial. Without a concrete mechanism (e.g., interceptor, partially seeded fixture causing FK violation), this acceptance criterion will likely be silently skipped or replaced with a weaker test. Specify the mechanism.
- **MEDIUM — D-31 requires `Version = 1` on Draft, but Phase 3 likely already has instances seeded for context tests at non-1 versions.** The plan should confirm whether `WF-PT-CTX-CHECKER` and friends (referenced in `03-02-SUMMARY.md`) are start-workflow created or seed-inserted. If seed-inserted, no impact; if start-workflow-created, fixtures may need a refresh.
- **LOW — `--reset` with `--format json` output shape is not pinned.** Task 4 mentions JSON output exists but doesn't say whether reset counts live under `result.reset.counts` or peer to `result`. This becomes a downstream test compatibility concern.
- **LOW — D-32 says start-workflow writes no audit, but the README quickstart shows `start-workflow … --reset` followed by `execute-json`.** The plan correctly forbids start audit, but the reset path may want a *single* operator log line (not audit row) for traceability. Worth confirming this is intentional.

### Suggestions
- Task 3: specify how to inject a failure between deletes and insert (e.g., a unique violation by seeding a same-key row mid-transaction, or an EF interceptor). Without a concrete mechanism the test won't be written.
- Task 4: add an acceptance criterion that reset JSON shape matches the same envelope shape as execute-json results (one stable result contract).

### Risk: **LOW–MEDIUM**

---

## Plan 04-03: Persisted Execution Validation

### Strengths
- The 14-step validation priority (D-05) is the highest-risk locked invariant in the phase; reproducing it verbatim in `must_haves` and dedicating Task 3 entirely to priority order is exactly right.
- Task 5 (idempotency race scaffold) is correctly deferred to Plan 04-04 for full activation but built here — sequencing matches the actual code dependency.
- The note that admin override does NOT bypass concurrency/idempotency/version is repeated three times (must_haves D-11, Task 4 action, Task 4 acceptance). This is a frequently-misimplemented rule.
- `WorkflowExecutionOutcome` (internal model) is called out as a separate artifact, which keeps the contract clean and lets Plan 04-04 add side-effect data without touching public result DTOs.

### Concerns
- **HIGH — Task 3 ordering of completed-workflow vs action-existence is ambiguous.** D-05 lists completed-workflow guard at step 5 and action existence at step 6, but the task acceptance says "Completed workflow returns InvalidTransition with the locked message" and "Action missing and transition missing both return InvalidTransition with distinct messages." This is fine for D-05 step 5, but if the action does not exist *and* the workflow is completed, the completed guard fires first — the test name should make that priority explicit (e.g., `CompletedWorkflowBeatsActionMissing`). Without that test, the priority order can drift undetected.
- **HIGH — Task 5 verification is too weak to catch the actual race.** "Add tests or test scaffolding proving unrelated unique violations are not swallowed" — but the actual race needs two concurrent connections, which requires either a real SQL barrier or test parallelism. The plan should commit to using SQL `WAITFOR DELAY` + parallel `Task.Run`, or a `DbCommandInterceptor` that holds before the insert. The race-recovery helper without a real concurrency test is unverified.
- **MEDIUM — `CanonicalRequestHasher` "hardening" in Task 2 is open-ended.** "If needed, harden its canonical serializer while preserving Phase 3 tests" — what counts as needed? Phase 3 already locked canonicalization (D-22). If Phase 3 already meets D-22, Task 2 should explicitly state "no changes expected" rather than leaving discretion that may unintentionally break Phase 3.
- **MEDIUM — Task 1 says request hash is "computed once from the effective envelope after overrides," but D-51 says `ValidationFailed` has `requestHash = null`.** Where does envelope-validation-failure short-circuit the hash computation? The plan should explicitly say: envelope validation runs first, and only if it passes is the hash computed. Otherwise Task 2's idempotency preflight will hash invalid envelopes.
- **MEDIUM — Idempotency preflight key derivation isn't specified.** D-20 enumerates request-hash inputs, but idempotency-record lookup uses `IdempotencyKey` (a separate field set from `workflow.idempotencyKey` or `clientActionId` fallback per CLAUDE.md). The plan should pin the lookup key formula in Task 2 (e.g., `IdempotencyKey = workflow.idempotencyKey ?? clientActionId`).
- **LOW — Definition-version mismatch (step 4) needs a clarification.** Does it compare the envelope's stated `workflowDefinitionVersion` against the instance's current version, or against the active definition version? D-05 only says "guard." Pin it in Task 3 acceptance.

### Suggestions
- Add an explicit "priority interaction" test matrix to Task 3: for each adjacent pair in D-05, construct a request that violates both and assert the earlier code wins.
- Task 5: commit to a concrete concurrency mechanism for the race test. Suggest `Task.WhenAll` of two independent `WorkflowDbContext` instances inserting the same idempotency key.
- Task 2: replace "if needed, harden" with an explicit pass/fail: "verify Phase 3 canonicalization meets D-22; do not modify unless a failing test demonstrates a gap."
- Add to Task 1 acceptance: "request hash is computed only after envelope validation passes; ValidationFailed never carries a request hash."

### Risk: **MEDIUM**

---

## Plan 04-04: Transition Side Effects and Context Refresh

### Strengths
- All ENG and PT requirements covered by Phase 4 are mapped to this single plan, which is correct — the side-effect step is where requirement satisfaction is provable.
- Task 2 verification covers terminal state (`Status=Completed`, `CompletedAtUtc`), which is easy to miss when focused on state transitions.
- Task 5 generating sample fixtures here (rather than waiting for Phase 6) matches the research recommendation and the locked Section 19 contract.
- The `ripgrep PortfolioTrade` check in verification is a smart guard against accidental hardcoding in application/domain layers.

### Concerns
- **HIGH — Transaction boundary across pre-validation and side effects is unclear.** D-01 says "one atomic transaction" for the action. Plan 04-03 runs validation *before* the transaction (idempotency preflight is explicitly outside it per D-07). But persisted validation steps 3–13 (instance load, version, completed, action/transition, concurrency, role) — do they run inside or outside the side-effect transaction? If outside, a transition can pass validation, then by the time the transaction opens, state has moved (TOCTOU). If inside, validation rolls back cleanly but the transaction is held longer. The plans don't pin this. Recommend: validation queries inside the transaction with READ COMMITTED (which is what D-26 references via PROJECT.md §15), and the Plan 04-04 task should make this explicit.
- **HIGH — Idempotency record insert race recovery (Task 3) interacts with already-committed side effects.** The loser of the race has already written task close, state move, version increment, new task, notification, and audit rows in the same transaction as the idempotency insert. If the idempotency insert fails with unique violation, the entire transaction rolls back (D-25 says "rolls back the loser transaction"). The plan correctly preserves this — but Task 3 acceptance says "Racing duplicate loser leaves no trace," which is only true if *all* side effects are in the same transaction as the idempotency insert. The plan must make this explicit. If the idempotency record is in a *separate* transaction (e.g., to honor "outside any transaction with fresh state" in D-25), the loser's side effects would leak.
- **MEDIUM — Task 4 says refreshed context is "a separate committed-data read" (D-39).** That implies a second EF DbContext or at least a separate transaction. The plan should pin which approach. A new DbContext per refresh is cleaner but adds DI/scope considerations not addressed.
- **MEDIUM — D-18 says replay does not recompute context refresh; Task 4 says replay returns stored context.** But the JSON envelope has top-level `workflowContext`. Where does the stored context live in `ResultJson`? D-15 says "including the original refreshed `workflowContext`." So the canonical result JSON must embed `workflowContext` *and* the result builder must round-trip it. Plan 04-01 Task 3 doesn't explicitly include `workflowContext` in canonical serialization. This needs cross-plan reconciliation.
- **MEDIUM — Notification queueing for `MakerReworkTask` with no SLA needs special handling.** The plan mentions "MakerReworkTask has no SLA and due date remains null" in research, but Task 2 doesn't enumerate this. The notification route should still be queued regardless of SLA, but the `DueAtUtc` on the created task must be nullable in the side-effect code path. Worth an acceptance criterion.
- **MEDIUM — Task 5 sample list omits SaveDraft and ambiguity fixtures.** D-09 says "tests should add an ambiguous fixture" but Task 5 only enumerates production-flow samples. The ambiguous fixture and a SaveDraft sample for `ReturnedToMaker` should be on the list, even if they live under a `tests/Fixtures/` directory rather than `samples/`.
- **LOW — Verification step "Checker Return to Maker quality gate from PROJECT.md section 21 passes" is opaque.** Specify the exact `dotnet run` command, like CLAUDE.md's quickstart format.
- **LOW — Status check constraint `Status ∈ {Active, Completed}` (per CLAUDE.md) means a terminal transition setting Status=Completed needs to happen before any further actions return `InvalidTransition` (step 5).** This is fine but worth a test: complete the workflow then attempt another action.

### Suggestions
- Add a task or sub-task to Task 2 explicitly pinning: "All persisted validation (steps 3–13) runs inside the side-effect transaction. Idempotency preflight (step 2) runs outside."
- Task 3: add acceptance criterion "Idempotency record insert is the final statement inside the side-effect transaction. Losing the unique-violation race rolls back all side effects atomically."
- Task 4: pin "Post-commit context refresh uses a new DbContext scope, never the transaction-scoped DbContext."
- Cross-plan: confirm Plan 04-01 canonical result JSON includes the `workflowContext` field; otherwise replay can't restore it.
- Task 5: add ambiguous-transition fixture and SaveDraft-from-`ReturnedToMaker` sample.

### Risk: **MEDIUM–HIGH**

---

## Plan 04-05: Inspection Commands Gap Closure

### Strengths
- Scope is tight: read-only commands, no schema changes, no third-party packages.
- Requirements list is reasonable but slightly over-claimed (see concerns).
- Verification commands form a complete UAT flow that proves all four commands work end-to-end.

### Concerns
- **MEDIUM — Requirements claim is suspicious.** This plan claims `ENG-03, ENG-04, ENG-05, PT-03, PT-04`. Those requirements should already be fully closed by Plan 04-04. A gap-closure plan that "re-completes" already-complete requirements suggests either (a) 04-04 was not actually complete when summarized, or (b) the requirements claim is wrong and should be DOC-related instead. Per the STATE.md status this is post-hoc, so cosmetic — but for future audits, the requirements field should reflect what the plan *newly* contributes.
- **LOW — No `--format json` requirement on inspection commands.** README references `show-context … --format json` heavily. The plan should at minimum acknowledge whether inspection commands need a JSON mode (likely yes, for consistency).
- **LOW — `WorkflowInspectionReader` (mentioned in CLAUDE.md) suggests the implementation chose to combine all four read services into one class.** The plan says "Add read-only inspection services" (plural). The final implementation diverged from the plan — fine for a gap-closure summary, worth noting.
- **LOW — "Branch UAT visibility" test is described in prose ("closed task, pending Maker rework task, returned notification, audit action are visible") but not pinned to a specific test name.** Acceptable for a gap-closure plan, but in a primary plan this would be too vague.

### Suggestions
- Add a sub-task: pin JSON output strategy (mirror `show-context` for consistency).
- Be explicit about whether the four commands share a single inspection reader or four separate readers.

### Risk: **LOW**

---

## Cross-Plan Concerns

### Dependency Ordering
- **`depends_on` is correct.** 04-01 → {04-02, 04-03} → 04-04 → 04-05. No cycles, no missing edges. Plan 04-03 correctly depends on both 04-01 (result builder) and 04-02 (start-workflow needed for test fixtures).
- **Parallelism opportunity missed.** Plans 04-02 and 04-03 are both wave-2 candidates after 04-01 (start-workflow doesn't depend on execution). Plan 04-02 is labeled wave 2 and 04-03 is wave 3 — but per the dependency graph, both could run in parallel after 04-01. If wave numbers drive scheduling, this serializes unnecessarily. Confirm wave numbers reflect intent.

### Idempotency Race Coverage
- The race-recovery logic is scaffolded in 04-03 Task 5 and activated in 04-04 Task 3, but neither plan commits to a concrete concurrency test mechanism. This is the highest-risk untested invariant in the phase. Without a real concurrent insert test, D-25 is a code review claim, not a tested behavior.

### Refreshed Context in Stored ResultJson
- Plan 04-01 Task 3 (canonical result serialization) and Plan 04-04 Task 3 (idempotency insert with original context) need to agree on whether `workflowContext` is part of the canonical serialization. If not, replay can't return the original context, breaking D-15/D-16/D-18. Resolve before Plan 04-04 starts.

### Transaction Boundary
- The plans don't pin whether persisted validation steps 3–13 run inside or outside the side-effect transaction. Under READ COMMITTED with validation outside, TOCTOU is possible. Inside the transaction is safer and matches D-01's "one atomic transaction" framing.

### Sample Fixtures vs. Test Fixtures
- Plan 04-04 Task 5 creates `samples/workflow-actions/*.json`, but Plan 04-03 tasks reference "injected ambiguous fixture" and similar. There's no explicit home for test-only fixtures (vs. operator samples). Recommend `tests/Fixtures/` for ambiguous/edge-case envelopes and `samples/` for happy-path operator demos.

---

## Final Risk Assessment

**Overall: MEDIUM**

Justification:
- The phase is well-structured and the decision set is fully encoded into `must_haves` blocks across all plans.
- The validation priority order (D-05), idempotency replay/conflict semantics, and admin-override scope are correctly propagated and repeated where they could be missed.
- The two material risks — transaction boundary and idempotency race testing — are both fixable by adding 1–2 sentences each to Plans 04-03 and 04-04. They are not structural problems.
- The minor risks (canonical JSON sharing, nullability conventions, sample fixture homes) are typical implementation-detail drift and won't block phase completion if addressed during execution.

**Recommended pre-execution fixes (highest leverage):**
1. **Plan 04-03 Task 5:** Commit to a concrete concurrency test mechanism for the idempotency race.
2. **Plan 04-04 Task 2:** Pin "persisted validation runs inside the side-effect transaction; idempotency preflight runs outside."
3. **Plan 04-01 Task 3:** Confirm `workflowContext` is part of canonical result JSON so D-15/D-18 replay actually works.
4. **Plan 04-03 Task 3:** Add adjacent-pair priority interaction tests for D-05.


---

## Consensus Summary

Only one independent external reviewer was available (claude); codex was detected but skipped because the current session is Codex. No multi-reviewer consensus can be claimed from this run.

### Agreed Strengths

Single-reviewer run; see Claude Review above.

### Agreed Concerns

Single-reviewer run; see Claude Review above.

### Divergent Views

No divergent views are available because only one external reviewer ran.
