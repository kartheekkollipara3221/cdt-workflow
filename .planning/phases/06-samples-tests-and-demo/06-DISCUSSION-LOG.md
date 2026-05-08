# Phase 6: Samples, Tests, and Demo - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md; this log preserves the alternatives considered.

**Date:** 2026-05-08T10:35:24.0910272-04:00
**Phase:** 6-Samples, Tests, and Demo
**Areas discussed:** Demo command behavior, README shape and depth, Test coverage triage, Demo script artifacts, Demo failure simulation for tests, Coverage audit enforcement, README command parsing strictness, Fresh-clone walkthrough definition

---

## Demo Command Behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Happy path only | Run only section 25.1 using `PT-DEMO`. | |
| Happy path plus branch guidance | Run section 25.1 using `PT-DEMO`, then print manual branch commands. | Selected |
| Full demo suite | Run section 25.1, 25.2, and 25.3 automatically. | |

**User's choice:** Happy path plus branch guidance.
**Notes:** `demo` must reset only `PT-DEMO`, execute the happy path with overrides, assert each step's expectations, stop on failure without branch guidance, print final audit history on success, then print hardcoded branch guidance for `PT-1002` and `PT-1003`.

---

## README Shape and Depth

| Option | Description | Selected |
|--------|-------------|----------|
| Operator quickstart | Short command-first README only. | |
| Integration guide | Contract-first README only. | |
| Layered README | Quickstart first, integration guide second. | Selected |

**User's choice:** Layered README.
**Notes:** One root README serves operators and UI/API integrators. It indexes rather than duplicates the spec. No screenshots or diagrams in v1. Quickstart commands must be runnable; configuration snippets must be complete.

---

## Test Coverage Triage

| Option | Description | Selected |
|--------|-------------|----------|
| Exhaustive coverage ledger | Rebuild coverage from scratch for every section 20 bullet. | |
| Gap-focused finish | Audit existing tests, then implement only Partial/Missing rows. | Selected |
| Critical-path only | Cover only demo, samples, and README. | |

**User's choice:** Gap-focused finish.
**Notes:** Produce `tests/COVERAGE_AUDIT.md` before changing tests. Covered rows are untouched. Partial rows tighten existing tests. Missing rows get new tests. Engine gaps and spec contradictions require stop-and-ask.

---

## Demo Script Artifacts

| Option | Description | Selected |
|--------|-------------|----------|
| README-only scripts | README is the only place with branch commands. | |
| Checked-in script files | Add shell/PowerShell/cmd script files. | |
| README plus validation source | README shows commands; executable fixture arrays are authoritative. | Selected |

**User's choice:** README plus validation source.
**Notes:** No standalone scripts. Test fixtures define section 25.1, 25.2, and 25.3 as `ConsoleCommand` arrays. README tagged blocks must match fixture serialization.

---

## Demo Failure Simulation for Tests

| Option | Description | Selected |
|--------|-------------|----------|
| Injectable runner test double | Use `IDemoStepRunner` with production and failing test implementations. | Selected |
| Corrupt fixture copy | Mutate copied sample JSON files to force failures. | |
| Real database precondition failure | Create SQL state that makes a real step fail. | |

**User's choice:** Injectable runner test double.
**Notes:** The demo command has no test-only flags or branches. Failure tests inject `FailAtStepDemoStepRunner`; production uses the real command runner. A separate SQL-backed happy-path test verifies production wiring.

---

## Coverage Audit Enforcement

| Option | Description | Selected |
|--------|-------------|----------|
| Audit status test | Fail if audit rows are not Covered. | |
| Reviewer discipline only | Keep audit as human-only document. | |
| Strong audit test | Verify section 20 completeness, Covered status, and cited test existence. | Selected |

**User's choice:** Strong audit test.
**Notes:** `CoverageAuditTests` parses section 20 and `tests/COVERAGE_AUDIT.md`. The audit cannot paraphrase bullets, cannot leave non-Covered rows in final state, and cannot cite nonexistent tests except recognized non-test action prefixes.

---

## README Command Parsing Strictness

| Option | Description | Selected |
|--------|-------------|----------|
| Parse-only | Check command name and fixture/capability mapping. | |
| Parse plus handler validation | Validate README command arguments through pure production parsers. | Selected |
| Execute quickstart subset | Execute README commands under tests. | |

**User's choice:** Parse plus handler validation.
**Notes:** README validation catches argument shape drift, stale flags, sample filename typos, override-pair mismatches, reserved identifier misuse, and missing command examples without SQL side effects. Semantic execution remains covered by branch and quality-gate tests.

---

## Fresh-Clone Walkthrough Definition

| Option | Description | Selected |
|--------|-------------|----------|
| Manual quality gate only | Document and manually run the walkthrough. | |
| Automated fixture in current repo | Simulate the quickstart in the current workspace only. | |
| Both practical automation and manual note | Add opt-in FreshClone tests and require manual fresh-clone sign-off. | Selected |

**User's choice:** Both practical automation and manual note.
**Notes:** FreshClone tests are opt-in (`Category=FreshClone`) and destructive to the local database. Manual sign-off starts from a real clone and is required in the Phase 6 completion summary.

---

## the agent's Discretion

- Exact class names, file names, parser organization, fixture layout, and test grouping are implementation details as long as the locked behavior is preserved.

## Deferred Ideas

- Future `--dry-run` for demo could use the `IDemoStepRunner` structure, but is not in v1.
- Standalone scripts are rejected/deferred for v1.
