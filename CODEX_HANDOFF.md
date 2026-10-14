# Current handoff

Updated: 2026-10-14. Branch: `console-code-first-0518`.

## Current goal

Complete the user-approved repository cleanup. Keep `src/` and `workflow-api/`
in place and preserve all supported behavior. No defect fixes are authorized.

## Completed changes

- Moved 527 historical/scratch files across 22 paths to the external archive.
- Moved all four registered worktrees with Git; retained branches and untracked runtime data.
- Kept source, tests, SQL, scripts, samples, executable scenario packs, current
  design specifications, acceptance tooling, and restore entrypoints.
- Consolidated current findings in [docs/OPEN-ISSUES.md](docs/OPEN-ISSUES.md).
- Replaced the growing handoff history with this checkpoint. Original content
  and all pre-existing uncommitted documents are preserved in the snapshot.

## Archive and recovery

Archive: `C:/projects/cdt-workflow-archive/20261014-cleanup`.

- `snapshot/`: pre-cleanup files, including the original handoff and review links.
- `archived/` and `manifest.csv`: moved files and their SHA-256 hashes.
- `repository.bundle`: verified Git history; patches and status capture local changes.
- `worktrees/`: relocated Git worktrees, still registered with this repository.
- `verification/`: command logs, TRX results, and cleanup checks.

The archive is local and does not travel with a Git clone. Restore selected
paths from `snapshot/` after reviewing current edits. Do not overwrite later
work. To reverse a worktree move, use `git worktree move` with the original
path recorded in `worktrees-before.txt`. The snapshot excludes rebuildable
bin/obj/IDE output, `.git`, `.tools`, and separately relocated worktrees.

## Changed files

Historical paths removed from the working tree; README, docs index, archive
references, .gitignore, GSD state, and this handoff updated. See the cleanup
[plan](.planning/quick/261014-er0-clean-repository-and-archive-historical-/PLAN.md)
and summary in that same directory. Application source and build settings are unchanged.

## Build/test status

Both solutions restore/build from existing restore state with zero warnings/errors.
Core: 477 passed, 2 failed (fresh-clone builds detect NU1902/NU1903 for the existing
System.Security.Cryptography.Pkcs 10.0.0 dependency). API: 68 passed.
SMTP: 5 passed. Total: 550 passed, 2 failed. Seed verification, all ten samples,
demo coverage, and both restore-package extractions passed. All 527 moved files
passed SHA-256 checks; 561 protected files match the original snapshot exactly.
Local documentation file links and Git diff whitespace checks passed.
No audit suppression or package changes applied. See the cleanup summary;
do not claim a fully passing clean-build gate.

## Remaining tasks and risks

Review the package-advisory build failure separately. All paused engine, SQMS,
and CDT topics remain paused; see [open issues](docs/OPEN-ISSUES.md).
The user authorized publication of this cleanup to `origin/console-code-first-0518`,
accepting the local archive and existing test limitations, provided source remains
untouched. All 561 protected files were rechecked byte-for-byte before publication.
Do not resume defect implementation automatically.

## Next-session prompt

Read PROJECT.md, AGENTS.MD, this handoff, the cleanup SUMMARY.md, and
docs/OPEN-ISSUES.md. Check Git status. Review the cleanup diff and its external
archive. Fetch and verify local/remote commit equality. Resolve the clean-build dependency advisory separately within an
authorized scope. Preserve all paused decisions and public compatibility.
