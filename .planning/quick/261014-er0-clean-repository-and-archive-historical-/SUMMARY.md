---
status: incomplete
date: 2026-10-14
---

# Repository cleanup

Changed: archived 527 files across 22 paths; relocated four registered worktrees;
consolidated the current handoff and issue index; repaired archive references.
Why: reduce historical clutter while preserving runtime and maintenance functionality.

Archive: `C:/projects/cdt-workflow-archive/20261014-cleanup`.
The original working tree, including uncommitted handoffs/reviews, is in `snapshot/`.
Git history is in the verified `repository.bundle`. The moved-file SHA-256 inventory
is `manifest.csv`. This local archive is not distributed with a Git clone.

## Verification

- Core restore/build using existing restore state: pass; zero build warnings/errors.
- API restore/build using existing restore state: pass; zero build warnings/errors.
- Core tests excluding SMTP: 477 passed, 2 failed, 0 skipped (479 total).
- API tests: 68 passed, 0 failed, 0 skipped.
- Local smtp4dev tests: 5 passed, 0 failed, 0 skipped. Helper stopped afterward.
- Total: 550 passed, 2 failed of 552 tests.
- Final scenario-document check: 16 passed after restoring fixture bytes exactly.
  These are a focused rerun of tests included in the total above.
- The failures are FreshCloneWalkthroughTests.BuildSucceedsWithWarningsAsErrors
  and QuickstartSequence_ProducesExpectedOutputs. Their isolated restore/build
  reports NU1902 and NU1903 for the unchanged System.Security.Cryptography.Pkcs
  10.0.0 dependency. No package updates, audit suppression, or warning-policy edits.
- Seed verification run: SQL-backed seed tests and fresh-clone VerifySeed test passed.
- Sample JSON executed: all ten sample files through the existing regression tests;
  existing branch and demo coverage passed. Dedicated suite databases only.
- Both historical restore packages extracted successfully and verified their payloads.
  Outputs are retained outside the repository under archive verification/.
- Final checks: all 527 archived file hashes match; all 561 protected files match
  the pre-cleanup snapshot byte-for-byte. Local Markdown/HTML file links checked:
  no missing targets. Git diff whitespace check passed. Browser rendering was not tested.

## Retained functionality and support

Keep src, workflow-api, tests, tools, scripts, sql, samples, current design specs,
scenario packs, shared build settings, CI, and restore entrypoints. The acceptance
tool is referenced by core tests; scenario documents are executable test fixtures.
Keep paused decisions and defect tracking under .planning; use docs/OPEN-ISSUES.md
as the current index. Do not resume any defect work from cleanup authorization.

Files updated: .gitignore, README.md, CODEX_HANDOFF.md, docs/README.md,
docs/OPEN-ISSUES.md, historical references in retained documentation/GSD files,
and this GSD task. Archived paths are listed in archived-paths.json outside the repo.

Open issues: clean-build dependency advisories remain unresolved. Cleanup is
implemented, but the full quality gate is not green; task status remains incomplete
for that reason. The user authorized committing and pushing this cleanup to
origin/console-code-first-0518 with the documented limitations, provided source
remains untouched. All 561 protected files were reverified before publication.
Milestones and application behavior unchanged.
