# CP Atlas reusable platform: design specs

## Goal

CP Atlas must become a reusable platform. The next 16 business apps must integrate with low-code changes: configuration, metadata and database scripts. A new app must not need new branches in shared code.

## Scope and rules

- This folder contains design specs only. It does not contain Atlas code.
- The workflow engine (`src/`, `WorkflowEngine.sln`) does not change for this work.
- The target code belongs to the target team. Each spec states requirements and a design for that team.
- Evidence comes from the copies in `target-implementation-read-only/`. Line numbers refer to those `.txt` files. The copies have copy errors; see the copy-error rule in [TARGET_ATLAS_ISSUES_BY_SEVERITY.html](../TARGET_ATLAS_ISSUES_BY_SEVERITY.html).
- All changes must be backward compatible. New JSON properties are optional. Existing routes, endpoints, request and response shapes, control names and the four current modules (SQMSO, SQMSB, TTD, AIN) and CDT must continue to work with their current JSON.
- Supply each database change as a SQL script.

## Spec template

Each module spec has these sections:

1. Current behaviour, with evidence.
2. The cost to add app #17 today.
3. Findings (hardcoded or per-app items).
4. Reusable design.
5. Backward compatibility.
6. Migration steps.
7. Regression and acceptance checks.
8. Open questions.

## Modules

| # | Module | Spec | Status |
|---|--------|------|--------|
| 1 | Section Master (single-page forms: SQMSO, SQMSB, TTD, AIN) | [01-section-master.md](01-section-master.md) · [HTML report](01-section-master.html) · SQL: [rowversion](sql/01-section-master-rowversion.sql), [role scope check](sql/01-section-master-role-scope-check.sql) | Draft 3, all questions answered, ready for the target team |
| 2 | Step Master / Step Page (multi-step forms: CDT) | — | Not started |
| 3 | Dashboard (tabs, grids, menus, procedures) | [03-dashboard.md](03-dashboard.md) · [HTML report](03-dashboard.html) | Draft 2 (all questions answered) |
| 4 | Rules engine (`CPRule`) | [04-rules-engine.md](04-rules-engine.md) · [HTML report](04-rules-engine.html) · SQL: [multi-role users](sql/04-multi-role-users.sql) (read-only) | Draft 2 (all questions answered) |
| 5 | Data mapping (`MapData` / `BindData`, per-app tables) | [05-data-mapping.md](05-data-mapping.md) · [HTML report](05-data-mapping.html) · SQL: [new-app template](sql/05-new-app-template.sql) | Draft 3 (all questions answered) |
| 6 | Security and entitlements | [06-security.md](06-security.md) · [HTML report](06-security.html) · SQL: [active roles view](sql/06-active-user-roles-view.sql), [LAN ID columns](sql/06-lanid-columns-ddl.sql), [LAN ID backfill](sql/06-lanid-backfill-dml.sql), [visibility template](sql/06-record-visibility-template.sql), [grant/revoke role](sql/06-grant-revoke-role-template.sql) | Draft 2 (all questions answered) |
| 7 | Module registry and service factory | [07-registry.md](07-registry.md) · [HTML report](07-registry.html) | Draft 2 (all questions answered) |
| 8 | Attachments and download | [08-attachments.md](08-attachments.md) · [HTML report](08-attachments.html) · SQL: attachment table in [new-app template](sql/05-new-app-template.sql) part 3a | Draft 2 (all questions answered) |
| 9 | Audit log and history | [09-audit-history.md](09-audit-history.md) · [HTML report](09-audit-history.html) | Draft 2 (all questions answered) |
| 10 | Workflow engine adapter (start, execute, notify, SLA) | [10-engine-adapter.md](10-engine-adapter.md) · [HTML report](10-engine-adapter.html) | Draft 2 (all questions answered) |
| 11 | Configuration and deployment | [11-config-deployment.md](11-config-deployment.md) · [HTML report](11-config-deployment.html) · SQL: [script log](sql/11-script-log.sql) | Draft 2 (all questions answered) |
| 12 | TTDV2 pilot build (release P1: TTD rebuilt as a new app; coding plan) | [12-ttd-pilot-build.md](12-ttd-pilot-build.md) · [HTML report](12-ttd-pilot-build.html) · SQL: [TTDV2 app](sql/12-ttdv2-app.sql), [copy from TTD](sql/12-ttdv2-copy-from-ttd.sql) | Draft 2 (all questions answered) |
| 13 | Architecture review of the atlas-platform code (Angular, ASP.NET Core, EF Core, general principles) | [13-architecture-review.md](13-architecture-review.md) · [HTML report](13-architecture-review.html) | Review 1: 47 findings (9 High), 3 fix waves; no code changed |
| 16 | Architecture review 2 (after rework R0-R8 and the dry run): ease for the next apps, scale, risk to the existing system | [16-architecture-review-2.md](16-architecture-review-2.md) · [HTML report](16-architecture-review-2.html) | Review 2: 11 findings (2 High: AR-01 start-up stops host, AR-02 menu step); no code changed |
| 17 | Section Master: target and new platform compared (better, different, benefit of the move) | [17-section-master-comparison.md](17-section-master-comparison.md) · [HTML report](17-section-master-comparison.html) | Report, 2026-10-29 |
| 18 | Whole redesign: big picture (improved, same, pending) and integration at the target (new files to move, target files to edit, SQL, settings, order) | [18-whole-redesign-integration.md](18-whole-redesign-integration.md) · [HTML report](18-whole-redesign-integration.html) | Report, 2026-10-29 |
| 19 | Architecture review 3 (the complete solution before the move): Service, Data/App/SQL, Angular, integration seam against the real target copies | [19-architecture-review-3.md](19-architecture-review-3.md) · [HTML report](19-architecture-review-3.html) | Review 3: 43 findings (3 High: RV-01 `/v1/v1/` URLs, RV-02 hidden-field value leak, RV-03 one bad table takes all platform apps down); 5 blockers for the move; no code changed |
| 20 | Review 3: second opinion on each finding of module 19, and fix wave 4 | [20-review-3-assessment-and-fixes.md](20-review-3-assessment-and-fixes.md) · [HTML report](20-review-3-assessment-and-fixes.html) · SQL: [TTDV2 rollback](sql/12-ttdv2-rollback.sql) | Done: 30 of 43 fixed (5 in part), 2 documented, 6 deferred, 3 not agreed, 2 for the target team; new RV-N1; 294 + 73 tests pass |
| 21 | Independent code review by Claude Fable 5.1 (simplification: ub-mode, Angular 21, .NET guidance, "16 apps by JSON"); 19 findings, 18 delete-or-merge items, 14-step refactor plan; Phase A, no code changed | [FABLE_REVIEW.md](FABLE_REVIEW.md) | Decided 2026-10-31: steps 1-14 without 10; step 9 yes; step 6 delete the pilot files. Work order: [FABLE_IMPLEMENTATION_PLAN.md](FABLE_IMPLEMENTATION_PLAN.md) |
| 22 | Phase B: the refactor of FABLE_REVIEW.md steps 1-14 (no step 10), one commit for each step, by Claude Opus 5.5 | [FABLE_IMPLEMENTATION_PLAN.md](FABLE_IMPLEMENTATION_PLAN.md) · [INTEGRATION.md](../../atlas-platform/INTEGRATION.md) rows B76, B77 and the `tailwind.config.js` row | Done 2026-10-31: commits 2a9710b to 8cab414, package 3577d2e (92 files). dotnet test 305, ng test 87, --atlas-check 0/0. Not pushed. |
