# Repository cleanup

Authorized: 2026-10-14. Scope: the user-approved folder cleanup plan.
Run inline under the GSD quick workflow. Preserve PROJECT.md milestones.

1. Preserve Git history, current tracked/untracked documents, and a file manifest in
   `C:/projects/cdt-workflow-archive/20261014-cleanup`. Archive old plans, review
   evidence, legacy snapshots, duplicate context files, and scratch artifacts.
   Move registered worktrees with Git after checking their status and ancestry.
2. Keep runtime source, tests, sample/scenario fixtures, SQL, scripts, acceptance
   tools, build configuration, current design contracts, and restore entrypoints.
   Consolidate the issue index and current handoff; repair archive references.
3. Verify archive hashes and unchanged protected files. Restore/build both
   solutions with warnings as errors, run core/API regression suites sequentially,
   seed/sample/demo coverage, documentation links, and restore-bundle extraction.
   Record results in SUMMARY.md and CODEX_HANDOFF.md; update GSD STATE.md.

No application behavior, package, database schema, or paused defect fixes are in scope.
Keep cleanup changes reviewable in the working tree; do not publish.
