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
| 3 | Dashboard (tabs, grids, menus, procedures) | — | Not started |
| 4 | Rules engine (`CPRule`) | — | Not started (Section Master spec extends it) |
| 5 | Data mapping (`MapData` / `BindData`, per-app tables) | [05-data-mapping.md](05-data-mapping.md) · [HTML report](05-data-mapping.html) · SQL: [new-app template](sql/05-new-app-template.sql) | Draft 3 (all questions answered) |
| 6 | Security and entitlements | [06-security.md](06-security.md) · [HTML report](06-security.html) · SQL: [active roles view](sql/06-active-user-roles-view.sql), [LAN ID columns](sql/06-lanid-columns-ddl.sql), [LAN ID backfill](sql/06-lanid-backfill-dml.sql), [visibility template](sql/06-record-visibility-template.sql), [grant/revoke role](sql/06-grant-revoke-role-template.sql) | Draft 2 (all questions answered) |
| 7 | Module registry and service factory | — | Not started (Section Master spec introduces it) |
| 8 | Attachments and download | — | Not started |
| 9 | Audit log and history | — | Not started |
| 10 | Workflow engine adapter (start, execute, notify, SLA) | — | Not started |
| 11 | Configuration and deployment | — | Not started |
