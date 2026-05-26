---
phase: 6
reviewers: [claude]
reviewed_at: 2026-05-26T21:30:36.6574785Z
plans_reviewed: [06-01-PLAN.md, 06-02-PLAN.md, 06-03-PLAN.md, 06-04-PLAN.md]
---

# Cross-AI Plan Review - Phase 6

## Claude Review

# Cross-AI Plan Review: Phase 6 (Samples, Tests, and Demo)

## Overall Summary

The Phase 6 plan set is unusually well-scoped and disciplined. It correctly resists the temptation to redesign the engine and instead treats Phase 6 as a packaging-and-traceability exercise. The audit-first ordering (Plan 01) is the right call — it prevents duplicative test writing and creates a single source of truth (`tests/COVERAGE_AUDIT.md`) that all later plans converge on. The four-plan wave structure (audit → demo+fixtures → README+parsers → closure+FreshClone) has clean dependencies and minimal back-pressure. The locked decisions (D-01 through D-47) are specific enough that an implementing agent has very little room to drift, which is appropriate for a final POC milestone. Notable risks are concentrated in Plan 03 (mechanical parser extraction is rarely as mechanical as claimed) and Plan 04 (FreshClone tests are destructive to a shared local DB, and the audit-enforcement test is a load-bearing gate that itself needs to be correct).

---

## Plan 06-01: Coverage Audit Inventory

### Strengths
- Read-only first pass is the right architectural choice; it forces inventory before implementation.
- Locked four-column format and verbatim-bullet rule eliminate paraphrasing drift.
- Explicit stop-and-ask on `Engine gap`/`Spec contradiction` is well-placed before code changes.
- Acceptance criteria are checkable (count match, no orphan rows, one row per bullet).

### Concerns
- **MEDIUM** — Task 1 says "Use the locked four-column format: `??20 bullet | Status | Test or action | Notes`." The `??` appears to be a mojibake of `§20` from the Windows console. The actual `COVERAGE_AUDIT.md` correctly uses `§20`, but the plan file itself encodes corrupt text. If an agent literally copies the header from the plan, it will produce a corrupt audit file that fails D-33's whitespace-normalized equality check.
- **LOW** — No guidance on how to handle bullets that span multiple lines or contain pipe characters (`|`), which would break markdown table parsing. The actual section 20 bullets do contain backticked identifiers; a stray `|` would be a real bug.
- **LOW** — Acceptance criterion "Every section 20 bullet appears exactly once" implicitly assumes section 20 has no duplicate bullets. Worth a one-line check.

### Suggestions
- Fix the `??20` encoding in 06-01-PLAN.md to `§20` so the audit header is correct at the source.
- Add an explicit rule: "If a bullet contains `|`, escape it as `\|` in the markdown row."
- Add to verification: "Audit table parses cleanly as a 4-column markdown table (no rows with !=4 cells)."

### Risk: **LOW**
Read-only, reversible, no production code touched.

---

## Plan 06-02: Demo and Branch Fixtures

### Strengths
- Cleanly separates orchestration (`DemoCommandHandler`) from execution (`IDemoStepRunner`), which keeps the demo testable without `if (testing)` branches (D-10).
- Reuses one `ConsoleCommand`/`DemoStep` representation for README fixture, branch tests, and production demo (D-13) — this is the right call to prevent the happy path from existing in three places.
- Rejecting v1 flags (D-08) explicitly resists scope creep.
- Recursive-demo prevention is called out (Task 2 acceptance criteria).
- Production-path test plus failing-runner control-flow tests give both behavioral confidence and deterministic failure coverage.

### Concerns
- **MEDIUM** — Task 3 says the demo executes "section 25.1 sample/action/context/inspection steps with `PT-DEMO` overrides," but section 25.1 of the happy path includes `run-sla --now "2030-01-15T16:00:00Z"`. Running SLA monitor as part of the demo is fine, but if any sample step has SLA expectations tied to `PT-1001` due dates, the `PT-DEMO`-rewritten timing/IDs need to still satisfy them. The plan doesn't say how `testExpectations` written for `PT-1001` get rewritten or relaxed for `PT-DEMO`. Worth verifying expectations are identifier-agnostic before assuming this is a no-op.
- **MEDIUM** — `ProcessNotificationOutboxCommandHandler` writes directly to `Console.Out` (per research), but the demo step runner needs to capture step output to assert narratives. The plan acknowledges this in research but does not specify how the production runner captures stdout from handlers that write directly. If the answer is "redirect Console.Out per step," that has thread-safety implications for parallel test execution.
- **MEDIUM** — Task 5's `Section21QualityGateTests` runs `init-db`, `verify-seed`, both return branches, AND `demo` "through the command path." If these tests are in the default test run, they will execute against the configured `burrito` database (the same DB FreshClone destroys), causing test ordering coupling. The plan doesn't say whether these run against an isolated `burrito_phase6_*` DB or the configured one. xUnit collection serialization may not be enough.
- **LOW** — D-04 mandates a "100ms pause indicator" between steps. With 15+ happy-path steps, the demo adds ~1.5s of fixed sleep. Trivial in isolation, but `Section21QualityGateTests` re-running demo plus both branches multiplies this. Consider whether the pause indicator can be a printed marker without an actual `Task.Delay` in tests.
- **LOW** — No mention of how `WorkflowEngineDemoStepRunner` handles the `start-workflow --reset` step for `PT-DEMO`. If the runner dispatches through `CommandRegistry`, and `CommandRegistry` is the same instance that's mid-dispatch of `demo`, there's a reentrancy/scope question (especially with EF `DbContext` scoped lifetimes).

### Suggestions
- Explicitly state how sample `testExpectations` interact with `--business-object PT-DEMO` override mode (e.g., "expectations match on resultCode and state transitions, not on business object ID").
- Document the stdout-capture strategy for the production runner, or change handlers that write directly to `Console.Out` to also return their text in `CommandResult.Message`.
- For `Section21QualityGateTests`, name the test database explicitly (e.g., `burrito_phase6_quality_gate`) and apply the SQL collection-serialization pattern from Phase 4.
- For the 100ms pause, allow `IDemoStepRunner` to inject a no-op `IPauseStrategy` in tests, or use `TimeProvider` instead of `Task.Delay`.

### Risk: **MEDIUM**
Real workflow execution, real SQL, new orchestration. Mostly mitigated by the runner abstraction, but stdout capture and test-DB isolation are unspecified.

---

## Plan 06-03: README and Validation

### Strengths
- Layered README (Quickstart vs. Integration Guide) directly maps to two audiences (operators, integrators) with line budgets enforcing brevity.
- Tagged HTML-comment blocks (`<!-- BEGIN: HappyPath_Section25_1 -->`) give tests deterministic anchors instead of fragile prose-parsing.
- README validation operates at argument-shape level (D-36) — does not require SQL — which keeps it fast and CI-friendly.
- Capability coverage check (D-39) catches the case where a future command is added to `CommandRegistry` but never documented.
- Reserved-identifier scoping (PT-DEMO/PT-1001/PT-1002/PT-1003) is machine-enforceable.

### Concerns
- **HIGH** — Task 1 claims parser extraction is "mechanical and behavior-preserving" (D-40). In practice, mechanical extraction of parsers from handler-private nested types frequently changes:
  - Visibility of types (private → public/internal) — may force renaming or breaking changes in test code that referenced them via reflection.
  - Error message identity if `nameof()` was used on a now-renamed parameter.
  - The order of validation when validation was interleaved with side effects in the handler.
  The plan needs a lock-in step (golden tests on usage strings and error messages) **before** extraction, not "Update or update focused parser tests" after. The 06-03 summary lists 10 handlers modified, which is a large blast radius for "mechanical."
- **MEDIUM** — D-15 caps Quickstart at ~100 lines, D-16 caps Integration Guide at ~200-300 lines. The actual README shipped is 263 lines total — well within budget — but the plan has no test that enforces these caps. If a future change bloats Quickstart, nothing fails. Cheap to add a line-count assertion.
- **MEDIUM** — Task 3 says "README branch blocks match fixture serialization after whitespace normalization." The normalization rule isn't specified. Trailing whitespace? Tab/space? `\r\n` vs `\n` (Windows checkout)? Without spec, the test is fragile across contributor environments.
- **LOW** — Task 4 verifies "README spec section references resolve to real `PROJECT.md` headings." This is good, but `PROJECT.md` is a moving target — if section numbers shift in a future change, every README reference fails. Worth a note that this cross-document coupling is intentional.
- **LOW** — D-38's "no unknown flags" check needs a positive whitelist per command, not a global one. The plan doesn't specify whether the whitelist comes from the parser or is duplicated in the test.

### Suggestions
- Before Task 1, add a task: "Lock current parser behavior with character-exact usage-message tests and accepted-flag enumeration tests. Run them; they must pass against current handler code before any extraction."
- Add a line-count assertion test for README sections.
- Specify the normalization rule explicitly: e.g., "Trim trailing whitespace per line; strip blank lines at block boundaries; treat `\r\n` and `\n` equivalently."
- Derive the per-command flag whitelist from the parser, not from a duplicate in the test.

### Risk: **MEDIUM-HIGH**
Parser extraction across 10 handlers is the highest-risk change in Phase 6. README content itself is low-risk; the test coupling and the parser refactor are where regressions hide.

---

## Plan 06-04: Coverage Closure and Final Gates

### Strengths
- Audit-completeness test (`CoverageAuditTests.AuditDocumentIsValidAndComplete`) is the right enforcement: bullets parsed from spec, exact match required, cited tests must exist via reflection.
- FreshClone opt-in pattern mirrors the existing SMTP exclusion — consistent with established patterns.
- Manual fresh-clone sign-off is explicitly required in the completion summary (D-46), which prevents "automated tests pass, ship it" without human verification.
- `Manual:`, `Quality gate:`, and `README:` prefixes give an escape hatch for non-test action rows without weakening enforcement.

### Concerns
- **HIGH** — D-43 says FreshClone tests "drop/recreate the configured `burrito` database." This is the same database used by:
  - `init-db`/`verify-seed`/`demo` from the README quickstart.
  - The user's manual exploration via `start-workflow PortfolioTrade PT-1001 --reset`.
  - Potentially `Section21QualityGateTests` from Plan 06-02 if those don't use an isolated DB.
  Running FreshClone wipes the operator's working state. This is acceptable behavior for an opt-in destructive test, but the plan should require an explicit confirmation guard (env var like `RUN_FRESHCLONE_TESTS=1`, mirroring `RUN_SMTP_TESTS=1`) so that `dotnet test --filter Category=FreshClone` from muscle memory doesn't nuke a developer's DB.
- **MEDIUM** — `CoverageAuditTests` uses reflection to verify cited test names exist. Reflection-based test discovery in xUnit needs to handle `[Theory]` parameterization, fact attribute custom types (`SmtpFact`), and generic test classes. The plan says "using reflection or `dotnet test --list-tests`" — these are very different approaches with different failure modes. Pick one.
- **MEDIUM** — Task 1 says "tighten the named existing test to assert the missing section 20 behavior" for Partial rows. Tightening existing tests can break unrelated callers if the test is a `[Theory]` with shared test data. No guidance on when to add a new test vs. tighten.
- **MEDIUM** — The final audit shows several rows where the cited test seems generic ("Section21QualityGateTests.AllQualityGateCommandsExecute" cited for multiple distinct behaviors like `SaveDraft` semantics AND `workflow.options` non-suppression AND `RecordingNotificationSender`). A single test catching three independent invariants will not produce useful failure messages if one breaks. D-29 says don't duplicate, but the opposite extreme — one test asserting everything — undermines diagnosability.
- **LOW** — Task 4 is `manual` but listed under `autonomous: true` in the frontmatter. The completion summary section requires a real human signature (operator name, date, machine ID). This is fine, but the agent should explicitly pause for the user rather than fabricating a signature.
- **LOW** — No explicit verification that `xunit.runner.json` changes apply to all test runners (Visual Studio test explorer, `dotnet test`, CI). The exclusion mechanism should be tested.

### Suggestions
- Add `RUN_FRESHCLONE_TESTS=1` (or similar) gate in addition to the trait filter. Mirror the SMTP pattern exactly.
- Pick one test-name resolution mechanism (reflection is faster and doesn't require build artifacts; `--list-tests` is more authoritative). Document the choice.
- Add a rule: "If a single test would be cited for >2 unrelated section 20 bullets, split the test." Improves audit diagnosability.
- For Task 4, change `type` to `manual` to signal that human sign-off is required.
- Add a CI smoke check: `dotnet test --list-tests` shows FreshClone and Smtp tests are *not* in the default run.

### Risk: **MEDIUM**
The destructive FreshClone behavior and the load-bearing audit-validation test are the main risk concentrations. Both are mitigable with small additions.

---

## Cross-Plan Concerns

### Dependency Ordering
- Wave 1 → 2 → 3 → 4 is correct. Plan 02 depending only on 01 and Plan 03 depending only on 02 is right; Plan 04 depending on both 02 and 03 is right.
- **MEDIUM** — Plan 03's parser extraction modifies handlers that Plan 02 already wired into `demo`. If Plan 02 lands first and Plan 03 changes handler signatures, demo tests from Plan 02 may need re-verification in Plan 03. The plan doesn't explicitly call this out. Adding "re-run Plan 02 tests after parser extraction" to Plan 03 verification would close the loop.

### Scope Discipline
- Excellent overall. The "deferred ideas" section explicitly defers `--dry-run` and PowerShell scripts, both of which would have been easy scope creep.
- **LOW** — Plan 04 Task 1 ("Close remaining Partial and Missing audit rows") is open-ended. If the audit from Plan 01 leaves 30 Missing rows, Plan 04's scope balloons. Worth a fail-fast: "If Plan 01 produces more than N Missing rows, stop and split."

### Security / Performance
- No security-sensitive surfaces in Phase 6 (no auth changes, no input validation changes beyond parser extraction).
- Performance: the demo's fixed 100ms pause is negligible; FreshClone tests are slow but excluded by default. SQL-backed tests with `-m:1` serialization are already an established pattern. No new performance risks.

### Achieving Phase Goals
- All four `DOC-*` requirements are explicitly mapped to plans and the audit gate enforces section 20 completeness. The plans should achieve the phase goal.
- The one gap: section 21 quality gates are listed in Plan 04's verification checklist but are also exercised by Plan 02's `Section21QualityGateTests`. The relationship between "automated section 21 test" and "manual `dotnet run` of section 21 commands" is fuzzy. The plan implies both happen; this is fine but worth clarifying that the manual run is the canonical quality-gate run, and the test is supporting evidence.

---

## Overall Risk Assessment: **MEDIUM**

Justification:
- The locked decisions (D-01 through D-47) leave little room for implementation drift, which is a major risk reducer.
- The audit-first sequencing prevents the most common Phase-6-style failure (writing tests that duplicate or conflict with existing coverage).
- The two real risk concentrations are (a) the parser extraction in Plan 03 (mechanical refactors rarely are, and the blast radius is 10 handlers) and (b) FreshClone destructiveness in Plan 04 (mitigable with an env-var gate).
- One encoding bug in Plan 01 (`??20` vs `§20`) is small but worth fixing at the source.

If the suggested changes (parser-behavior lock-in before extraction, FreshClone env-var gate, stdout-capture spec for demo runner, isolated test DB for Section21 tests) are addressed, overall risk drops to **LOW-MEDIUM**.


---

## Consensus Summary

Only one independent external reviewer was available (claude); codex was detected but skipped because the current session is Codex. No multi-reviewer consensus can be claimed from this run.

### Agreed Strengths

Single-reviewer run; see Claude Review above.

### Agreed Concerns

Single-reviewer run; see Claude Review above.

### Divergent Views

No divergent views are available because only one external reviewer ran.
