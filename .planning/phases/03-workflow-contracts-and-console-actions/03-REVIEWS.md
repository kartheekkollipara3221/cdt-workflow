---
phase: 3
reviewers: [claude]
reviewed_at: 2026-05-26T21:25:11.2503884Z
plans_reviewed: [03-01-PLAN.md, 03-02-PLAN.md]
---

# Cross-AI Plan Review - Phase 3

## Claude Review

# Cross-AI Plan Review: Phase 3 — Workflow Contracts and Console Actions

## Plan 03-01: Workflow Action Contracts, Validation, Normalization, and `execute-json`

### Summary
This plan cleanly separates Phase 3 contract/validation/normalization scope from Phase 4 execution semantics, splits the work into four well-bounded tasks, and explicitly forbids `execute-json` from pretending a transition occurred. The boundary between "validate now" and "defer until Phase 4" is crisp and matches D-04/D-07. The plan is implementable as written, but a few specifics — what a "deferred execution" `WorkflowActionResult` looks like on the wire, how `--admin-override` and `--reset` interact with Phase 3, and canonical-hashing edge cases — are under-specified.

### Strengths
- **Phase boundary discipline.** Task 4 explicitly says "return a clear Phase 3 deferred-execution result shape rather than pretending a transition ran." This is exactly the right framing and prevents downstream confusion.
- **Override ordering is locked down.** D-12 puts overrides before validation, hashing, and idempotency derivation, which is the only correct ordering (otherwise re-targeted samples would collide on the original `workflowInstanceId`).
- **Hashing rules are explicit about exclusions.** Excluding `clientActionId`, `idempotencyKey`, `correlationId`, and `requestedAtUtc` from the hash matches `PROJECT.md` §9 and is the right call.
- **Generic-engine guardrail enforced.** The `rg "PortfolioTrade"` check in the verification block is a smart automated guard against regression into engine-specific code.
- **Reusability hook for Phase 4.** Calling out that normalization + hash output must be reusable by Phase 4 (rather than rewritten) prevents an expensive rework.

### Concerns

**HIGH — `WorkflowActionResult` shape for "deferred execution" is undefined.**
Task 4 says return "a clear Phase 3 deferred-execution result shape" but doesn't say *what that shape is*. `PROJECT.md` §8.3 likely enumerates the valid `resultCode` values (the Phase-6 README lists `Success`, `ValidationFailed`, `Forbidden`, `InvalidTransition`, etc.) — none of which obviously map to "deferred." If you invent a new code like `AcceptedForPhase3Validation` (which is exactly what the existing 03-01-SUMMARY shows happened), that's a contract shape change that should have been a stop-and-ask trigger per D-07 / §22. The plan should either (a) explicitly enumerate the result code, or (b) flag this as needing user approval before Task 4 starts.

**MEDIUM — `--admin-override` flag is not addressed.**
`PROJECT.md` and the CLAUDE.md mention `--admin-override` as a recognized flag for `execute-json`. Plan 03-01 only lists `--business-object`, `--workflow-instance`, and `--format json`. If `--admin-override` is parsed in Phase 4 only, that's fine, but the plan should say so. Otherwise an operator passing it in Phase 3 will get a silent ignore or an "unknown flag" error with no documented behavior.

**MEDIUM — `toState` rejection is described vaguely.**
Task 2 says "no authoritative toState field if detectable." "If detectable" is hand-wavy. The DTO either has a `toState` property (and silently drops it, which violates D-02's spirit) or doesn't (in which case `System.Text.Json` deserialization silently ignores unknown properties by default — also not a rejection). The plan should specify: use `JsonSerializerOptions` with `UnmappedMemberHandling = Disallow` on the `workflow` block, or write an explicit validator rule that scans the parsed `JsonElement` for `toState` and emits `ValidationFailed`. As written, two engineers would implement this two different ways.

**MEDIUM — Canonical hashing of `dataSave.payload` JSON arrays.**
Task 3 says "canonicalize JSON object property order so semantically identical payloads hash consistently." It doesn't say what to do with arrays. Arrays are order-significant in JSON semantics, so the right answer is "preserve array order," but the plan should state this explicitly so no one tries to sort arrays. It should also specify number normalization (e.g., `1` vs `1.0`, trailing zeros, exponent form) — canonical JSON specs (RFC 8785) handle this carefully and it bites people who roll their own.

**LOW — No mention of file-not-found / unreadable file handling for `execute-json`.**
Task 4 lists "invalid file path" in `<verify>` but the `<action>` doesn't say what the result is. Is it an exit code? A `WorkflowActionResult` with `ValidationFailed`? A bare error message? Inconsistent handling here will surface in Phase 6 demo scripts.

**LOW — Test fixtures for canonical hash are not specified.**
"Property order does not affect hash" needs concrete fixtures (e.g., `{"a":1,"b":2}` vs `{"b":2,"a":1}` plus a deeply nested case). Without that, a passing test could be one where the input was already canonical and the implementation is broken.

### Suggestions
1. **Pin the deferred result shape.** Before Task 4, add an explicit decision: either reuse an existing `resultCode` (e.g., return `Success=false` with a documented marker, or treat valid-but-not-executed as a 03-specific code that the user has approved) or stop-and-ask. Don't let the implementer invent it.
2. **Make `toState` rejection a positive validation rule.** Add to Task 2: "Re-parse the raw `workflow` block as `JsonElement` and emit `ValidationFailed` with code `toStateNotAuthoritative` if a `toState` property is present." This is testable and unambiguous.
3. **Specify canonical hash semantics fully.** Add to Task 3: arrays preserve order; numbers serialize as `JsonElement.GetRawText()` (or specify a normalization); strings are not Unicode-normalized; null vs missing are distinct. Reference RFC 8785 if you want a citation.
4. **Document `--admin-override` Phase 3 behavior.** One sentence: "`--admin-override` is parsed in Phase 4; in Phase 3 it is accepted but has no effect since no authorization is performed."
5. **Add a Phase 4 hand-off note.** Task 3's `<done>` says "Phase 4 can reuse normalization." Make that concrete: the normalizer and hasher must be `public` services registered in DI, not internal helpers inside `ExecuteJsonCommandHandler`.

### Risk Assessment: **MEDIUM**
The plan structure is sound and the boundary is well-chosen, but the under-specified "deferred execution" result shape risks a contract decision being made silently by the implementer (and per the existing 03-01-SUMMARY, that's exactly what happened — `AcceptedForPhase3Validation` was invented). For a project where `PROJECT.md` explicitly stop-and-asks on contract shape changes (§22), that's not a small thing. The hashing edge cases are a smaller risk but will cause Phase 4 idempotency-replay tests to flake if not nailed down.

---

## Plan 03-02: SQL-Backed `show-context` and Workflow Context DTOs

### Summary
This plan correctly puts the read model in Infrastructure behind an Application interface (`IWorkflowContextReader`), preserves read-only behavior, and exercises a wide range of states (Draft, CheckerReview, ReturnedToMaker, wrong-role, completed, no-SLA, on-track, overdue). The dependency on Plan 03-01 is appropriately declared. The main weakness is the lack of a strategy for *creating* the test runtime rows — there's no `start-workflow` yet, so tests must hand-craft rows, and the plan glosses over how.

### Strengths
- **Clean Application/Infrastructure split.** `IWorkflowContextReader` in Application, `WorkflowContextReader` in Infrastructure — matches the established Clean Architecture pattern and keeps EF out of Application.
- **SLA computed from clock, not outbox.** "Overdue is computed from `DueAtUtc` and `TimeProvider`, not NotificationOutbox" — this is the right invariant and prevents a subtle Phase-5 coupling.
- **Wrong-role behavior is explicit.** Returning state/task details with empty `availableActions` and `canAct=false` (rather than a 403-style refusal) matches the form-open contract intent.
- **`sla` is always an object.** Explicitly modeling `NoSla` / `OnTrack` / `Overdue` rather than nullable avoids a known pitfall called out in §28.

### Concerns

**HIGH — Test fixture strategy for runtime rows is undefined.**
Research §Risks calls this out: "Phase 3 needs a minimal test fixture to create `WorkflowInstance` and pending `WorkflowTask` rows because Phase 4 `start-workflow` execution does not exist yet." The plan acknowledges seeded-then-hand-created runtime rows in Task 2's `<verify>` but doesn't specify *how*. Options: (a) raw EF `Add` in test setup, (b) a temporary test-only seed helper, (c) wait for Phase 4. Option (a) is the right choice, but the plan should say so — otherwise the implementer either invents a `start-workflow` helper that bleeds Phase 4 behavior into Phase 3, or skips the SQL-backed tests entirely. Also: will these fixtures be reusable in Phase 4 tests, or thrown away? Decide now.

**MEDIUM — `WorkflowInstanceId` not-found behavior is under-specified.**
Task 3 mentions "unknown workflow instance output" in `<verify>` but the `<action>` doesn't say what the response is. JSON with an empty/null workflow block? An error result with non-zero exit code? A `WorkflowContextResponse` with `workflow=null` would break the contract (it says `sla` is always an object — symmetry suggests `workflow` is too). The plan should specify: command returns non-zero exit with a clear message, *or* the reader returns a documented sentinel.

**MEDIUM — `canAct=false` is referenced but never defined in DTO task.**
Task 2's acceptance criteria says "wrong-role actors see state/task details but empty availableActions and `canAct=false`." But Task 1's DTO definition doesn't list `canAct` as a field on `actorAccess` or anywhere else. Either Task 1's DTO list is incomplete, or `canAct` is derived implicitly from `availableActions.Count == 0` (in which case stop calling it a field). Pick one.

**MEDIUM — Multi-role transition resolution is ambiguous.**
The reader description says "matching transitions for the actor's roles" and uses set intersection ("`AllowedRoleKey` intersects actor roles"). But if an actor has roles `[Maker, Checker]` and there are transitions for both from the current state (which shouldn't happen in single-state v1, but the metadata could allow it), what shows up in `availableActions`? Deduped? Both? The `AmbiguousRoleMatch` result code exists in the result vocabulary — but that's an execution-time concern, not a context-time one. The plan should say context aggregates all matching transitions and lets Phase 4 worry about ambiguity, OR that context already filters to a single role. Don't leave it to the implementer.

**LOW — `serverTimeUtc` is in the DTO list but its purpose isn't justified.**
Task 1's acceptance criteria includes `serverTimeUtc`. Fine — it's in the contract per §8.1 presumably. But the plan should note it must use `TimeProvider.GetUtcNow()` for testability, otherwise tests assert against a moving target.

**LOW — `verify-seed` regression check is good but `init-db` isn't checked.**
Verification block runs `verify-seed` but not `init-db`. If the read model touches schema (it shouldn't, but a stray migration could land here by accident), a Phase 4 author starting from `init-db` would discover it the hard way. Cheap to add.

**LOW — No mention of EF `AsNoTracking()`.**
Task 2 is explicitly read-only. Using `AsNoTracking()` (or `AsNoTrackingWithIdentityResolution()` for the joined query) prevents accidental writes and is also faster. Worth a one-line note in the action.

### Suggestions
1. **Specify the test fixture pattern explicitly.** Add to Task 2: "Test setup uses `WorkflowDbContext` directly to insert `WorkflowInstance` and `WorkflowTask` rows for each scenario. These helpers may be reused by Phase 4 tests but are not promoted to production seed data."
2. **Define not-found behavior.** Add to Task 3: "If `WorkflowInstanceId` does not exist, return non-zero exit code with JSON `{ "error": "...", "workflowInstanceId": "..." }` to stderr." (Or whatever the team prefers — just decide.)
3. **Resolve `canAct` ambiguity.** Either add `canAct` to Task 1's DTO field list, or rephrase Task 2 as "`availableActions` is empty for wrong-role actors" and drop the `canAct` framing.
4. **Document multi-role behavior.** One sentence: "Available actions deduplicate by `actionKey`; if multiple roles authorize the same action, the action appears once. Role-ambiguity resolution is a Phase 4 execution concern."
5. **Pin `TimeProvider` for `serverTimeUtc`.** Add to Task 2: "`serverTimeUtc` and SLA computations both come from the injected `TimeProvider`."
6. **Add `AsNoTracking()`.** One-line addition to Task 2's action.

### Risk Assessment: **MEDIUM**
The architecture is right and the read-only invariant is well-protected, but the test-fixture gap is real — without `start-workflow`, every contributor will solve this differently, leading to fragile or skipped SQL-backed tests. The not-found and multi-role ambiguities are smaller but will cause inconsistency between Phase 3 and Phase 4 if not resolved up front.

---

## Overall Phase 3 Risk Assessment: **MEDIUM**

Both plans are well-structured and respect the Phase 3/4 boundary, which is the hardest thing to get right here. The wave-1/wave-2 dependency (`03-02` depends on `03-01`) is correct because the context DTO file lives in the same `Contracts/` folder and may share JSON options. However:

- The biggest cross-plan risk is **contract-shape drift**: Plan 03-01 invents a "deferred execution" result without an enumerated code, and Plan 03-02 references `canAct` without defining it. `PROJECT.md` §22 lists contract changes as stop-and-ask; both plans should explicitly route those decisions back to the user before implementation.
- **Test-fixture coupling**: Phase 3 read-model tests need runtime rows that Phase 4 hasn't built yet. A short, explicit fixture-helper convention upfront prevents two divergent test setups.
- **Hashing rigor**: The canonical hash will be exercised hard in Phase 4 (idempotency-replay tests). Nailing array/number/null semantics in Phase 3 saves debugging later.

None of these are fatal — the plans are good enough to execute, and the existing `03-01-SUMMARY.md` shows they did execute successfully. But future Phase 3-style plans in this repo would benefit from explicit contract-decision callouts and test-fixture conventions baked into the plan template.


---

## Consensus Summary

Only one independent external reviewer was available (claude); codex was detected but skipped because the current session is Codex. No multi-reviewer consensus can be claimed from this run.

### Agreed Strengths

Single-reviewer run; see Claude Review above.

### Agreed Concerns

Single-reviewer run; see Claude Review above.

### Divergent Views

No divergent views are available because only one external reviewer ran.
