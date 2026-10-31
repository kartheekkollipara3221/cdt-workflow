# CP Atlas redesign: big picture and integration at the target

Status: Report, 2026-10-29. Covers the whole redesign in `atlas-platform/` (release P1, pilot app TTDV2), not only the section master. For the form only, see [17-section-master-comparison](17-section-master-comparison.html). No code is changed by this report.

## 1. Purpose

This report gives the big picture for the integration of the whole redesign at the target:

1. What the redesign improves, compared with what the target does today (section 3).
2. What stays the same (section 4).
3. The new files to move to the target (section 5).
4. The existing target files to update (section 6), and the SQL and settings (section 7).
5. The order of the integration (section 8).
6. What is pending (section 9).

Sources: `atlas-platform/INTEGRATION.md`, specs 01-12, reviews 13 and 16, `CODEX_HANDOFF.md`, and the read-only target copy `target-implementation-read-only/`.

## 2. Big picture

### 2.1 Target today

CP Atlas is one host (`BBH.CP.Atlas.App`, Angular `WebUi`) for SQMSO, SQMSB, TTD, AIN and CDT. Each app is code in shared files:

- An enum value in `CPApplicationCode` and `if` branches in `CPAtlasService` and `CPAtlasController`.
- One service class for each app (150 to 740 lines), with its own load, save, defaults, roles and workflow calls.
- An EF entity, a `DbSet` and a DTO for each app table. Stored procedures for each dashboard tab.
- App and field branches in the shared Angular components (`cp-atlas-sectionmaster`, `cp-atlas-dashboard`).
- The browser applies the rules and writes the audit entry. The server trusts the browser.

### 2.2 The redesign

The redesign adds a **generic platform** beside the current code. A new app is **configuration**: a manifest, a form JSON, a dashboard JSON, SQL from a template, and one registration line. The platform reads these files and does the work that each app class does today.

| Layer | Platform part |
|---|---|
| Registry | `<CODE>.app.json` manifest, `AddAtlasModule<T>("CODE")`, resolver by string code, config cache, menu enrichment |
| Form | Generic form component (route `form/:code/:id`), renderer registry, `Effects`, `Computed`, validators |
| Server | `ConfigSectionService`: load, 12-step save pipeline, delete, history; hooks for app code |
| Rules and security | Rule engine (roles, states, owner), user context (LAN ID, roles of the own app), access service (`Permissions`, `LogOnly`/`Enforce`) |
| Data | Table registry from `sys.columns`, typed value conversion, `rowversion`, one EF model for platform tables |
| Dashboard | Tabs from JSON (`Source: Table`, `Filter`, `MaxRows`, `Columns`), row permissions, totals row |
| Audit | Server audit in the save transaction, History dialog |
| Operations | Start-up check, `--atlas-check`, `Enabled` switch per app, `OnStartupError = DisableModule`, named log events |

The first app on the platform is **TTDV2**: TTD built again as a new app. TTD stays as it is (the reference) until the change-over.

### 2.3 In one table

| | Improved | Same | Pending |
|---|---|---|---|
| New app | Config + SQL + 1 line | - | Features that the next apps need (section 9.3) |
| Current apps | - | No change (SQMSO, SQMSB, TTD, AIN, CDT) | Migration to the platform (P3, P4) |
| Look | - | pbdev controls, layout, styles, toastr | `pbdev-select` and exact version proven at the target |
| Contracts | Optional new fields (`Errors`, `Version`, `FormType`, ...) | Routes, endpoints, request and response shapes, IDs | - |
| Security | Server checks each save | Windows authentication | `Enforce` mode after one release |
| Data | Typed, LAN IDs, versions, server audit | Current tables | TTD change-over (`050`) |
| Operations | Release check, switch, isolation | Same host and app pool | Performance numbers (PF9) |

## 3. What improves, area by area

### 3.1 Adding an app

| Step | Target today | Redesign |
|---|---|---|
| App code | Enum value (C#) | Manifest `<CODE>.app.json` |
| Server wiring | `if` branches in `CPAtlasService` (13 branches) and controller | One `AddAtlasModule` line |
| Service class | New class, 150-740 lines | None, or a small hook class (TTDV2: 31 lines + 38 lines option providers) |
| Table | DDL, EF entity, `DbSet`, DTO | SQL template `05-new-app-template.sql`; columns read from `sys.columns` |
| Form | JSON + branches in the section master | JSON only |
| Dashboard | JSON + branches in the dashboard + stored procedures | JSON only (`Source: Table`) |
| Menu and navigation | `if` chains in dashboard `onClick` / `editRow` | `FormType` from `v1/Application` |
| Test | Manual | `--atlas-check` + platform tests |
| Shared files changed | 5 to 7 | 0 |

Result: app #17 does not change shared code, so it cannot break the other apps. 13 apps are waiting (the unused codes in `CPApplicationCode`), and all 13 use the section form.

### 3.2 Registry and configuration (module 7, 11)

| Target today | Redesign |
|---|---|
| `if` chain on the code; a code with no branch returns `null` → `NullReferenceException` (SM-06) | Resolver by code; a code with no module → clear 404 |
| JSON read from disk on each request; one `<Code>ConfigPath` key for each app | One `ConfigFolder`; cache; reload when a file changes; copy for each request |
| A typo in the JSON is found by users | Lint at start-up and in `--atlas-check`: names, tokens, patterns, duplicates, tables, column types |
| A menu item for an app that the API cannot open (RG-06) | The menu hides platform apps that are not available |
| One bad config stops the host | `DisableModule`: only that app is off; the current apps work (AR-01) |

### 3.3 Form (module 1)

| Target today | Redesign |
|---|---|
| App codes, role names and more than 12 field names in the shared component | No app or field name; behaviour from `Effects`, `Rules`, `Computed` |
| 5-second timer for conditional fields | Effects run at once |
| Always "Record saved successfully" | Errors shown on the field or at the top |
| Unknown control type hidden with no message | "Unsupported" note; check before deployment |
| `*ngIf` chain for each type | Renderer registry; a new type is one provider line |
| No history, no delete, no conflict message | History, soft delete with confirm, conflict message |

### 3.4 Save and security (modules 1, 4, 6)

| Target today | Redesign |
|---|---|
| The server saves each name that the browser sends (H3) | Allow list: only fields shown and enabled for this user |
| The save does not check roles, state or owner (H4) | Rules and access check on the server |
| Validators only in the browser | Same validators on the server |
| Any value accepted for a list field | Value must be in the server option list for this user |
| Roles from all apps in some modules (SM-08) | Roles only from the own app (and its parent); deleted entitlements do not count (`vw_CP_Atlas_ActiveUserRole`) |
| System fields can come from the browser | `CreatedBy`, `ModifiedBy`, dates, computed columns set by the server |
| Errors caught and hidden (H7, SM-16) | Roll back, log, return the error |
| Access change is all or nothing | `AccessMode = LogOnly` first (log only), then `Enforce` |

### 3.5 Data (module 5)

| Target today | Redesign |
|---|---|
| Text columns for numbers and dates | Typed columns; conversion by column type, invariant culture |
| Display names for people | LAN IDs (name in the label only) |
| Last write wins (SM-18) | `rowversion`; the second save gets a conflict |
| Full table read to find one row (SM-12) | Key lookup, no tracking for reads |
| EF entity and DTO for each table | Tables registered from metadata; no entity class |
| `DateTime.Now` | `TimeProvider`; UTC or local per app (`SystemTime`) |

### 3.6 Dashboard (module 3)

| Target today | Redesign |
|---|---|
| A stored procedure for each tab | `Source: Table` with `Filter`, `Any` groups, `MaxRows`, `Columns` |
| Row menus in code | `Permissions` from the server |
| Totals in code | `sumsRow()` from the column JSON |
| A tab of another app read with no check | The other app must be enabled and the user must have `View` |
| Non-sargable `ISNULL([IsDeleted],0)` | `[IsDeleted] = 0` with filtered indexes |

### 3.7 Audit and history (module 9)

| Target today | Redesign |
|---|---|
| The browser writes the audit entry after each `POST` | The server writes it in the save transaction |
| A failed save still gets an entry; a user can skip or forge it | No entry without a commit; no entry when nothing changed |
| No history view in the form | History dialog with old and new values |

### 3.8 Operations

| Target today | Redesign |
|---|---|
| No release check | `--atlas-check` (exit code 0 or 1) |
| Rollback = code change and deployment | `AppSettings:Modules:<CODE>:Enabled = false` |
| Errors lost | Named events 41001-41024 |
| Hand-run SQL | Numbered scripts with `dbo.CP_Atlas_ScriptLog` |
| Few tests | 277 .NET tests (SQL Server) and 66 Angular tests; 0 build warnings |

## 4. What stays the same

- **Current apps.** SQMSO, SQMSB, TTD, AIN and CDT keep their code, JSON, tables, procedures and routes. Their `if` branches stay.
- **Contracts.** Routes `sectionmaster/:code/:id` and `stepmaster/...`, endpoints `v1/Application`, `v1/SectionPage`, `v1/ManagePage`, `v1/Execute`, `v1/AuditLog`, request and response shapes. New fields are optional. HTTP status 200 for save errors (phase 1).
- **Look.** The same pbdev-core controls, section master and dashboard layout, CSS classes, `div_{Name}` / `btn_{Name}` IDs, toastr settings, ag-grid with the target cell editors. The local look built from the real pbdev-core source was confirmed ("spot on", 2026-10-28).
- **Host.** Same .NET 10 host, same app pool, same IIS Windows authentication, same database and connection.
- **Packages.** No new package. The platform uses the packages that the target already has.
- **Workflow engine.** Not changed. CDT and SQMS keep their engine calls.
- **Database.** Current tables are not changed. TTDV2 has its own schema. The `rowversion` and LAN ID scripts for the current tables are P4 only.

## 5. New files to move to the target

The target package `atlas-platform/local/package/atlas-target-package.ps1` has all these files in the target layout (`copy\`), with SHA-256 checks. Do not copy: `StandIn/` folders, `WebUi/src/app/stand-in/`, `WebUi/src/app/app.*`, `WebUi/src/environments/`, `BBH.CP.Atlas.Infra`, `*.csproj`, `appsettings*.json`, `tests/`, `local/`.

### 5.1 Data project → `BBH.CP.Atlas.Data/Platform/`

| File | Purpose |
|---|---|
| `AtlasPlatformContext.cs` | EF Core context for platform tables; same connection and transaction as `CPSharepointContext` |
| `AtlasTableRegistry.cs`, `AtlasTable.cs` | Registered tables and their columns |
| `AtlasColumnMetadataReader.cs` | Reads columns, keys, defaults from `sys.columns` |
| `AtlasSqlTypes.cs`, `AtlasValueConverter.cs` | Supported SQL types; text ↔ typed value, same-value compare |
| `AtlasDataExtensions.cs` | Registration and `FindActive` (key lookup, soft delete) |
| `Dto/CPPlatformDto.cs` | Optional new DTO properties (partial classes): `Effects`, `Computed`, `Version`, `Errors`, `FormType`, ... |
| `README.md` | Folder notes |

### 5.2 Service project → `BBH.CP.Atlas.Service/Platform/`

| File | Purpose |
|---|---|
| `AtlasPlatformServices.cs`, `AtlasJson.cs`, `AtlasLog.cs`, `AtlasProblemException.cs` | Shared services, JSON loader, log events 41001-41024, problem errors |
| `Registry/AtlasAppManifest.cs`, `AtlasConfigStore.cs`, `AtlasPlatformConfiguration.cs` | Manifest model, config cache and reload, settings |
| `Registry/AtlasModuleResolver.cs`, `AtlasDisabledApps.cs` | Code → module; apps that are off |
| `Registry/AtlasApplicationMenu.cs` | Adds `FormType`, `HasDashboard` to `v1/Application`; guarded |
| `Sections/AtlasFormFields.cs`, `AtlasAllowList.cs`, `AtlasFieldValidator.cs` | Effective fields, allow list, server validators |
| `Sections/AtlasServerEffects.cs`, `AtlasComputedFields.cs`, `AtlasDefaultTokens.cs` | Server effects, `Computed`, default tokens |
| `Sections/AtlasModuleServices.cs` | Parameter object for module classes |
| `Rules/AtlasRuleEngine.cs` | Roles, states, owner → show, hide, enable, disable |
| `Security/AtlasUserContext.cs`, `AtlasRoles.cs`, `AtlasAccessService.cs`, `AtlasSql.cs` | LAN ID, roles of the own app, `Permissions`, `LogOnly`/`Enforce` |
| `Dashboard/AtlasDashboardService.cs`, `AtlasTabQueryBuilder.cs` | Tabs from JSON; parameterized SQL |
| `Options/AtlasOptionProviders.cs` | `PBAccounts`, `UsersInRoles` |
| `Audit/AtlasAuditWriter.cs` | Server audit and history read |
| `README.md` | Folder notes |

### 5.3 Service project → `BBH.CP.Atlas.Service/Application/`

| File | Purpose |
|---|---|
| `ConfigSectionService.cs` | The generic app service: load, save pipeline, delete, dashboard, history, hooks |
| `CPTTDV2Service.cs` | TTDV2 module (one hook) |
| `TTDV2/TTDV2OptionProviders.cs` | `TaxYears`, `WeekEndings` |

### 5.4 App project → `BBH.CP.Atlas.App/Platform/` and `Config/`

| File | Purpose |
|---|---|
| `Platform/AtlasPlatformServiceCollectionExtensions.cs` | `AddAtlasPlatform`, `AddAtlasModule` |
| `Platform/AtlasPlatformStartup.cs` | Start-up (hosted service), `OnStartupError` |
| `Platform/AtlasStartupCheck.cs`, `AtlasConfigLint.cs` | Start-up check and `--atlas-check` |
| `Platform/AtlasProblemExceptionFilter.cs` | Problem body for platform errors |
| `Platform/UserInfoCurrentUserSource.cs` | Current user from the target `UserInfo` |
| `Platform/README.md` | Folder notes |
| `Config/TTDV2.app.json`, `TTDV2.json`, `TTDV2.Dashboard.json` | TTDV2 manifest, form, dashboard (Copy to Output: PreserveNewest) |

### 5.5 WebUi → `WebUi/src/app/platform/`

| File | Purpose |
|---|---|
| `atlas-platform.routes.ts` | Route `form/:code/:id` (lazy) |
| `atlas-api.service.ts`, `atlas-models.ts`, `atlas-errors.ts` | API calls, models, error text |
| `atlas-navigation.ts` | `formRoute()`, `formTypeOf()`, dashboard link |
| `atlas-notice.service.ts` | Toastr messages |
| `atlas-history-dialog.ts` | History dialog |
| `atlas-pbdev.ts` | All pbdev-core imports in one place |
| `atlas-platform.scss`, `atlas-tailwind-classes.ts` | Platform styles; Tailwind classes used only in JSON |
| `form/atlas-form.component.ts`, `.html` | Generic form |
| `form/atlas-form-model.ts`, `atlas-effects.ts`, `atlas-validators.ts`, `atlas-validator-cases.ts` | Form model, effects, validators, shared test cases |
| `form/atlas-renderer-registry.ts`, `renderers/atlas-renderers.ts`, `renderers/atlas-grid-renderer.ts` | Type → renderer; control renderers; ag-grid renderer |
| `form/atlas-grid-components.ts`, `atlas-field-a11y.directive.ts` | Cell editor registration; accessibility |
| `dashboard/atlas-dashboard-columns.ts` | `columnDefs()`, `sumsRow()` for the target dashboard |
| `dashboard/atlas-dashboard.component.ts`, `.html` | Reference only (see section 6) |
| `platform.spec.ts`, `platform.component.spec.ts` | Tests; run in the target `ng test` (Karma) |

### 5.6 SQL → DBA release folder

| File | Purpose |
|---|---|
| `020-script-log.sql` | `dbo.CP_Atlas_ScriptLog` |
| `030-active-user-roles-view.sql` | `dbo.vw_CP_Atlas_ActiveUserRole` (active roles only) |
| `040-ttdv2-app.sql` | Schema `TTDV2`, tables, indexes, app row, roles, grants |
| `050-ttdv2-copy-from-ttd.sql` | Change-over day only: copy TTD data, revoke TTD roles |
| `README.md` | Order and checks |

## 6. Existing target files to update (hand edits)

About 11 edits. All are additive. The package folder `reference\` has the local version of each file with the `PLATFORM` lines.

| # | Target file | Change | Risk if wrong |
|---|---|---|---|
| 1 | `Data/DTO/CPAtlasApplicationConfig.cs` | Add `partial` to 16 classes (`CPAtlasApplicationConfig`, `CPApplicationSection`, `CPApplicationControl`, `CPButton`, `CPKeyValue`, `CPGrid`, `CPRule`, ...) | Build error only |
| 2 | `Data/DTO/CPAtlasDashboardConfig.cs` | Add `partial` to 5 classes | Build error only |
| 3 | `Data/DTO/CPAtlasPage.cs`, `CPAtlasLog.cs` | Add `partial` to `CPAtlasPage`, `CPAuditLog` | Build error only |
| 4 | `program.cs` | `AddAtlasPlatform(configuration)`, `AddAtlasModule<CPTTDV2Service>("TTDV2")`; optional `StartAtlasPlatformAsync()`; `--atlas-check` switch | Host start |
| 5 | `Service/CPAtlasService.cs` | 3 optional constructor parameters; `PLATFORM` lines at the start of `GetApplication`, `LoadDashboard`, `LoadSectionPage`, `ManagePage`, `RemovePage`, `AuditLog`, `GetAuditLog`. The `if` chains stay. | Current apps (test them) |
| 6 | `Controllers/V1/CPAtlasController.cs` | `LoadDashboard(string code)`, `RemovePage(string code, int id)`; inject resolver and config store (and optional logger); `Enum.Parse` through `AtlasAppCode.FromRequest` | Current apps (test them) |
| 7 | `app.routes.ts` | Add `...atlasPlatformRoutes` | TTDV2 only |
| 8 | `cp-atlas-dashboard.ts` | `formRoute(...)` first in `onClick` and `editRow`; `button.Target`; `columnDefs()` / `sumsRow()` for a tab with `Columns` | Current dashboards (test them) |
| 9 | `angular.json` | Add `src/app/platform/atlas-platform.scss` to `styles` (build and test) | Look only |
| 10 | `app.config.ts` | `provideAtlasGridComponents({ TextEditor, SelectEditor, NumericEditor })` | TTDV2 grid only |
| 11 | Menu (app component) | Show an app only when `HasDashboard` is not `false` | Menu |

No change to `package.json`. No change to the workflow engine.

## 7. SQL and settings

### 7.1 SQL

Run `sql/release-p1/` in number order: `020`, `030`, `040`. Run `050` only on the change-over day (part 1 of `050` is read-only and lists values that cannot be converted).

### 7.2 Settings in the target `appsettings.json`

| Key | Value |
|---|---|
| `AppSettings:ConfigFolder` | `Config` (default) |
| `AppSettings:ConfigRefreshSeconds` | `2` (default) |
| `AppSettings:Atlas:OnStartupError` | `DisableModule` (default) |
| `AppSettings:Modules:TTDV2:Enabled` | `false` until the smoke test passes |
| `AppSettings:Modules:TTDV2:AccessMode` | `LogOnly` (P1) |

The target reads only `appsettings.json` in the app folder. Restart the app pool after a change.

## 8. Integration order (development region)

1. Run the package: `powershell -ExecutionPolicy Bypass -File .\atlas-target-package.ps1 -Destination C:\temp\atlas-package`.
2. Smoke test of the current apps **before** the change: menu, each dashboard, open, save (SQMSO, SQMSB, TTD, AIN, CDT).
3. Copy the files of section 5 on one branch.
4. Do the hand edits of section 6.
5. Build: `dotnet build` (0 warnings) and `ng build`. This also proves `pbdev-select` and the exact pbdev-core version (AR-10).
6. Run SQL `020`, `030`, `040`. Add the settings with `TTDV2:Enabled = false`.
7. Run `BBH.CP.Atlas.App.exe --atlas-check` → exit code 0.
8. Smoke test of the current apps **after** the change. Same results as step 2.
9. List the `CP_Atlas_Application` codes (AR-02 check).
10. Set `TTDV2:Enabled = true`. Test TTDV2: add, save, edit, history, conflict, roles (Owner, Prepare, Read, none).

Rollback: `TTDV2:Enabled = false` (minutes), or revert the branch. The current apps do not use the platform paths.

## 9. What is pending

### 9.1 Integration (next)

| Item | Owner |
|---|---|
| One-time integration in the development region (section 8) | User at the target; errors are fixed here and the package is built again |
| Smoke test before and after; TTDV2 off until it passes | User |
| List of `CP_Atlas_Application` codes (AR-02) | User |
| `pbdev-select` and exact pbdev-core version (AR-10) | Closed by the first target build |
| Check `CPAtlasUser.LanId` / `Lanid` name, EF Core 10.0.13, no NU1902/NU1903 | Target build |

### 9.2 Decisions and reviews

| Item | Status |
|---|---|
| Walk-through of design choices B3, B4, B7, B9, B10, B14, B15 | Not started (one at a time) |
| Review with the target team (reviews 13, 16, INTEGRATION.md) | Not started |
| Owner of the `Platform/` folders and an author guide + JSON Schema (AR-08) | To agree before app #2 |
| `MaxRows` 5000 on `TaxTimeAllReport` | To confirm |
| `AccessMode` `Enforce` | After one release of log review |
| Split `050` into copy and revoke (AR-11) | Decide at go-live |

### 9.3 Platform features not in P1 (needed by some next apps, AR-09)

| Feature | Design |
|---|---|
| Attachments and file upload (`filepicker`) | Module 8 |
| Workflow engine adapter (start, execute, notify, SLA) | Module 10 |
| Multi-row grids; grid headers from a value (target `WeekEnding`) | Not yet designed |
| Button actions other than `Save`, `Close`, `Delete`; `Redirects` | Module 1, 5.3-5.4 |
| Parent and child apps (families, SQMSO → SQMSB bid) | Module 1, section 11 |
| Record visibility, `Relations` | Modules 6, 4 |
| Lazy load and server paging on dashboards | Module 3 |
| Dashboard row actions as the target `ActionMenuComponent` | Module 3 (N4) |
| Stepper forms (CDT, one more app) | Step Master stays; module 2 not started |
| Feature table of the next 13 apps, to order P2-P5 | To do before P2 |

### 9.4 Performance and later releases

| Item | Status |
|---|---|
| Slow-request log (PF9), then decide async I/O (PF1, AR-04) | After integration |
| Start-up time baseline (PF5, AR-06); parsed form cache (PF6, AR-07) | When numbers show a need |
| P2: second new app; P3: SQMS family on the platform; P4: current apps moved in place (LAN ID and `rowversion` scripts for current tables) | Plan (pilot-first) |
| Local look gaps (fonts, pbdev-icons, logo): local preview only | Not needed at the target |

## 10. Recommendation

Integrate now in the development region, in the order of section 8. The risk to the current apps is in the hand edits 4, 5, 6, 8 and 11 of section 6; the smoke test before and after covers them. Keep TTDV2 off until the smoke test passes. Then make the feature table of the next 13 apps and add each missing feature once, as metadata.
