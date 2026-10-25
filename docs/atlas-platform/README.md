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
