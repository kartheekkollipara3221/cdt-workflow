# Module 12: TTDV2 pilot build (release P1)

Status: Draft 2, 2026-10-25. All open questions answered (section 10). This spec is the coding plan for `atlas-platform/`.

Build status (2026-10-25): C0-C10 done in `atlas-platform/`. Findings B1-B16 and the integration steps: `atlas-platform/INTEGRATION.md`. Local run: `atlas-platform/local/README.md`.

## 1. Purpose

Release P1 builds **TTDV2**: the Tax Time Database made again as a **new app** on the platform. The current TTD code is the reference for what the app does (module 1, 10.1 and 10.5). The current TTD app does not change.

TTDV2 is designed and coded as if it were app #17. Thus the pilot proves two things together:

- The platform parts work (registry, rules, access, generic service, audit, form component, dashboard).
- A new app needs only the onboarding steps of module 7, section 5: SQL template, manifest, form JSON, dashboard JSON, role grants and one registration line.

Rules for P1:

- The platform is built **beside** the current code (module 11, 4.7). The current Section Master component and the current services stay frozen.
- TTD, SQMSO, SQMSB, AIN and CDT do not change in P1: no route, endpoint, table, column, JSON or service change.
- The workflow engine does not change.
- "Vertical slice" is the build order of P1, not a code structure. P1 builds each layer (SQL, Data, Service, API, UI) only as far as TTDV2 needs it. All apps share one platform; each later release adds the parts that its app needs.

## 2. Scope

### 2.1 In P1

| # | Part | Source |
|---|---|---|
| 1 | App manifest, `AddAtlasPlatform`, one `AddAtlasModule` line, resolver, string app code (`AppCode`, not in the enum) | Module 7, 4.1-4.4 |
| 2 | Config folder, JSON cache with copy for each request, tolerant `System.Text.Json` loader | Module 7, 4.7; module 11, 4.2 |
| 3 | `ConfigSectionService` with hooks (`OnLoad`, `OnBeforeSave`, `OnAfterSave`) | Module 1, 5.7 |
| 4 | Save pipeline steps 1-9, 11, 12 (no step 10, no workflow) | Module 1, 5.9 |
| 5 | `Errors` list in the `ManagePage` response (HTTP 200); `Version` token (`RowVersion`) | Module 1, 5.11, 5.13 |
| 6 | Option providers `PBAccounts`, `UsersInRoles` (platform); `TaxYears`, `WeekEndings` (TTDV2) | Module 1, 10.2 |
| 7 | Default tokens, `KeepSavedValue`, `Computed` (`Sum`), `SoftDelete`, grid `SingleRowFields`, `CPKeyValue.Data`, `Effects` with `SetFromOption` | Module 1, 5.1, 5.2, 10.2 |
| 8 | `Validator` properties `Integer`, `Min`, `Max` (server and client) | Module 1, 10.3 |
| 9 | `IAtlasRuleEngine` with `MostPermissive`, `RuleDefault: ReadOnly`, `OwnerFields`; same rules on save | Module 4, 4.1-4.6 |
| 10 | `IAtlasUserContext` (LAN ID, AD cache, roles for each app) | Module 6, 4.1 |
| 11 | `IAtlasAccessService` with `Permissions` and role flags (`@Read`, `@Write`, `@Admin`); `AccessMode: LogOnly` | Module 6, 4.3 |
| 12 | Active roles view `dbo.vw_CP_Atlas_ActiveUserRole` | Module 6, 4.4 |
| 13 | People as LAN IDs (`Value` = LAN ID, `Label` = display name) | Module 6, 4.5 |
| 14 | `AtlasPlatformContext`: shared-type entities for `TTDV2.General` and `TTDV2.AuditLog`, same connection and transaction as `CPSharepointContext` | Module 5, 6.2 (question 1) |
| 15 | Column metadata from `sys.columns`; value conversion by column type, invariant culture; load by key | Module 5, 6.2, 6.4, 6.5 |
| 16 | Server audit (`AuditMode: Server`): `Create`, `Update`, `Delete`; `_Event`, `Label`, `Old`; UTC; `GetAuditLog` access check, sort, `CreatedDateUtc` | Module 9, 4.1-4.5 |
| 17 | TTDV2 dashboard: own file, rules before load, `Permissions` menus, `Columns`, `Target` button; tab data from the table (`Source: Table`) with the new `Any` filter group and column `Type: "user"` (question 6) | Module 3, 4.1-4.7 |
| 18 | `v1/Application` adds `FormType`, `HasDashboard`; hides apps that are not registered | Module 7, 4.6 |
| 19 | Start-up check and `--atlas-check` command | Module 7, 4.8; module 11, 4.3 |
| 20 | Environment files change-over, log event names, log path setting | Module 11, 4.1, 4.1.1, 4.6 |
| 21 | `dbo.CP_Atlas_ScriptLog` and `sql/release-p1/` | Module 11, 4.4 |
| 22 | New generic form component in the client, with a renderer registry; route `form/:code/:id`; `FormType: FORM`; dashboard navigation by `FormType` and `Target` (question 5) | Module 1, 5.5; module 3, 4.5; module 11, 4.7 |

### 2.2 Not in P1

- Attachments (module 8), workflow adapter (module 10), `Relations` (module 4, 4.4), `Visibility` (module 6, 4.6).
- `Load: Lazy` and server paging (module 3, 4.2, 4.2.1).
- `Redirects`, `Action` on buttons other than `Save` and `Close`, families (module 1, 5.3, 5.4, section 11).
- `Enforce` mode. It comes after one release of log review (module 6, question 5).
- Rule preview endpoint (module 4, 4.8).
- Any change to TTD, SQMSO, SQMSB, AIN or CDT. The LAN ID scripts for the existing tables stay in P4.

## 3. TTDV2 app

TTDV2 does what TTD does (module 1, 10.1 and 10.5), with the new-app rules.

| Item | Setting |
|---|---|
| Code | `TTDV2` (pattern `^[A-Z][A-Z0-9]{1,19}$`). Not in `CPApplicationCode`; the client sends `AppCode`. |
| Schema | `TTDV2`: `General`, `AuditLog`. No attachment table in P1. |
| Service | `CPTTDV2Service : ConfigSectionService`, one hook: `OnLoad` (grid column dates). Changed 2026-10-31 (FABLE F03): no class; the platform providers `Years` and `WeekEndings` with `Parameters`. |
| Option providers | `TaxYears`, `WeekEndings` (TTDV2, with `TimeProvider`); `PBAccounts`, `UsersInRoles` (platform) |
| Rule mode | `MostPermissive`, `RuleDefault: ReadOnly` |
| Owner | `OwnerFields: ["CreatedBy", "TaxPreparerLanId"]`, LAN ID match |
| Roles | `Read` (`IsRead`) view; `Prepare` (`IsWrite`) create and edit own; `Owner` (`IsAdmin`) all, also delete |
| Day minutes | `int` columns, `CHECK 0-1440`; `Validator: { "Integer": true, "Min": 0, "Max": 1440 }`; empty = 0 |
| Total | `WeeklyTotal int`; `Computed: { "WeeklyTotal": "Sum(...7 day columns...)" }` |
| Dates | `WeekEnding date`; `TaxYear smallint`; system dates UTC |
| Delete | `SoftDelete: { "Field": "IsDeleted" }` |
| Header | `HeaderName: "Tax Time Database Time Sheet"`, `HeaderNameWithId: "{Name} - {ID}"` |
| Audit | `AuditMode: Server`, `TTDV2.AuditLog`, UTC, `CreatedByLanId` |
| Access | `AccessMode: LogOnly` |

### 3.1 Field map from TTD to TTDV2

| TTD column (text) | TTDV2 column | Control |
|---|---|---|
| `Account varchar(1000)` | `Account nvarchar(1000)` (review DA-12) | `Account` (`ngselect`, `PBAccounts`, `SetFromOption`) |
| `AccountNumber`, `AccountName` | `AccountNumber varchar(100)`, `AccountName nvarchar(1000)` (review DA-12) | `AccountNumber`, `AccountName` |
| `TaxYear varchar(200)` | `TaxYear smallint` | `TaxYear` (`TaxYears`, default `{Options[1]}`, `KeepSavedValue`) |
| `Category`, `WorkDescription` | `Category` same, `WorkDescription nvarchar(max)` (review DA-12) | same |
| `WeekEnding varchar(100)` | `WeekEnding date` | `WeekEnding` (`WeekEndings`, default `{Options[0]}`, `KeepSavedValue`) |
| `MondayMinute` ... `SundayMinute varchar(100)` | `int NULL`, `CHECK 0-1440` | grid `WeeklyHour`, `SingleRowFields` |
| `WeeklyTotal varchar(100)` | `WeeklyTotal int` | `Computed` |
| `TaxPreparerName varchar(1000)` (display name) | `TaxPreparerLanId nvarchar(50)` | `TaxPreparer` (`UsersInRoles` `["Prepare", "Owner"]`, default `{User.LanID}`) |
| `CreatedBy`, `ModifiedBy` (display name) | same, LAN ID | system |
| `CreatedDate`, `ModifiedDate` (local) | same, UTC | system |
| `IsDeleted` | `bit NOT NULL DEFAULT 0` (review DA-08) | system |
| — | `RowVersion rowversion` | system |

### 3.2 Dashboard tabs (`Source: Table`, no procedure)

The 3 TTD tabs (`CPAtlasDashboardConfig.json.txt:235-294`, procedures `TTD.Get_TTD_*`) become 3 table tabs in `TTDV2.Dashboard.json`. The platform always adds `IsDeleted = 0`.

| Tab `ID` | Filter | Order | Columns |
|---|---|---|---|
| `CurrentTaxTimesheet` | `Any`: `CreatedBy`, `TaxPreparerLanId`, `ModifiedBy` `Eq {LanId}` | `ModifiedDate DESC`, `CreatedDate DESC` | ID (hidden), Account Number (`Link: Edit`), Client Name, Tax Year, Category, Week Ending (date), Weekly Total, Modified By (user), Created (date) |
| `TaxTimeAllReport` | none | `CreatedBy`, `WeekEnding DESC` | as TTD + Saturday and Sunday Minutes; `Sums` on the 7 days and Weekly Total |
| `TaxTimeReport` | `TaxPreparerLanId Eq {LanId}` | `WeekEnding DESC`, `CreatedDate DESC` | as `TaxTimeAllReport` |

Two additions to module 3 (4.7), needed by the next apps too:

- **`Any` filter group:** `{ "Any": [ { "Column": "CreatedBy", "Op": "Eq", "Value": "{LanId}" }, ... ] }`. The items in the group are joined with `OR`; the group and the other filters with `AND`. One level only.
- **Column `Type: "user"`:** the column holds a LAN ID. The server returns the display name from `CP_Atlas_User` (user cache, module 6, 4.1). Sort and search use the display name only with `Paging: Client`.

Defect fixed: the TTD "All Report" sums Saturday and Sunday, but its procedure does not return them. TTDV2 shows all 7 days.

### 3.3 Files for TTDV2

| File | Content |
|---|---|
| `sql/12-ttdv2-app.sql` | Schema, tables, app row, roles (from the new-app template) |
| `Config/TTDV2.app.json` | Manifest |
| `Config/TTDV2.json` | Form |
| `Config/TTDV2.Dashboard.json` | Dashboard |
| `program.cs` | `services.AddAtlasModule<CPTTDV2Service>("TTDV2");` |
| `appsettings.<Env>.json` | `AppSettings:Modules:TTDV2:Enabled` (`false` in production until the business approves) |
| Role grants | `sql/06-grant-revoke-role-template.sql`, or the copy script (section 4) |

## 4. SQL for release P1

Folder `sql/release-p1/`. The DBA runs the scripts in number order. Each script is additive, idempotent, and ends with the script log block.

| # | Script | Source |
|---|---|---|
| 010 | Engine idempotent migrations (H9) | Module 11, 4.4 |
| 020 | `dbo.CP_Atlas_ScriptLog` | `sql/11-script-log.sql` |
| 030 | `dbo.vw_CP_Atlas_ActiveUserRole` | `sql/06-active-user-roles-view.sql` |
| 040 | TTDV2 app: schema, `General`, `AuditLog`, app row, roles with flags | [sql/12-ttdv2-app.sql](sql/12-ttdv2-app.sql) |
| 050 | Copy TTD data to TTDV2 (run at the change-over only, section 10, question 4) | [sql/12-ttdv2-copy-from-ttd.sql](sql/12-ttdv2-copy-from-ttd.sql) |

The copy script keeps the record IDs, converts the text values, finds LAN IDs where the display name is unique, copies the audit entries, gives TTDV2 roles to the users with the same TTD roles, revokes all TTD roles (decision, question 4), and lists each value that it cannot convert.

## 5. Tests

The tests come from the TTD reference behaviour (module 1, 10.1, 10.3 and 10.5).

- Load: a new record gets the defaults (`TaxYear` = second option, `WeekEnding` = first option, `TaxPreparer` = current user) and the grid column dates. An existing record keeps a saved value that is not in the options.
- Save: an extra output name, a hidden or disabled field, and a bad minute value (`1.5`, `1:30`, `-5`, `1441`) are refused with a field error. A wrong `WeeklyTotal` from the client saves the correct sum.
- Access matrix: each cell of module 1, 10.5, with one user for each role, for an own record and for another user's record. In `LogOnly`, a denied check writes `Atlas.AccessDenied`; the matrix test runs in `Enforce` in the test environment.
- Concurrency: two users save the same record; the second user gets a conflict error.
- Audit: a create, an update and a delete each write one entry with `_Event`; an update with no change writes no entry.
- Copy script: row counts agree; each converted value agrees with the TTD value; the not-converted list is empty or approved.
- Regression: the current TTD, SQMSO, SQMSB, AIN and CDT pass their current checks with no change. The current JSON files load with the old and the new loader with the same result (module 11, 4.2).

## 6. Code location: `atlas-platform/`

A separate solution in this repository, not in `WorkflowEngine.sln`. The user copies the code into the target.

Decision 2026-10-25: keep the 4 target projects. Add a `Platform/` folder to each. Use the target names (`CP` prefix, `BBH.CP.Atlas.*` namespaces). Move no existing file.

| Target project | New code (added only) |
|---|---|
| `BBH.CP.Atlas.App` (`Atlasapp/`) | `Platform/` (`AddAtlasPlatform`, start-up check, `--atlas-check`); one `AddAtlasModule` line in `program.cs`; `Config/TTDV2.app.json`, `Config/TTDV2.json`, `Config/TTDV2.Dashboard.json`. Controllers: the `code` parameter accepts the string code (module 7, 4.4). |
| `BBH.CP.Atlas.Service` | `Platform/` (registry, resolver, rule engine, user context, access service, audit writer, option providers, save pipeline); `Application/ConfigSectionService.cs`; `Application/CPTTDV2Service.cs` and the providers `TaxYears`, `WeekEndings`. |
| `BBH.CP.Atlas.Data` | `Platform/AtlasPlatformContext.cs`, column metadata reader; new optional properties on the existing DTO classes. |
| `WebUi` | `Platform/` new generic form component and renderer registry, beside the current `sectionmaster` component. `app.routes.ts`: add `form/:code/:id`. Dashboard: `FormType` and `Target` navigation before the current `if` chains. |

- `ConfigSectionService` inherits from `CPApplicationService` and implements `ISectionService`, as `CPTTDService` does. `CPAtlasService` keeps its public methods and calls the resolver for a registered code; the `if` chains stay for the current codes.
- `atlas-platform/` has the same 4 project names and folder paths. Stand-ins replace the target parts that are not in this repository (`TPAMAPIAccess`, `CPPBContext`, `CPUser`, the existing DTOs, `@pbdev`). Integration: copy the new folders and files into the same paths. Do not copy the stand-ins.
- Later (after P4): a separate project or a NuGet package for the 16 apps, if necessary.

## 7. Backward compatibility and rollback

- TTD and all current apps do not change. All new DTO properties and response fields are optional.
- The database changes are new objects only: the `TTDV2` schema, the script log table and the active roles view.
- Rollback before the change-over: set `AppSettings:Modules:TTDV2:Enabled = false` and restart. TTD is not affected.
- Rollback after the change-over: set `Enabled = false`, restart, and give the TTD roles back (the revoked rows have `IsDeleted` = 1 and `ModifiedBy` = the change-over run). Limit: records that users made or changed in TTDV2 after the change-over are not in TTD. Copy them back by hand, or roll forward with a fix.

## 8. Acceptance

- The tests of section 5 pass.
- `--atlas-check` returns 0 for the current apps and TTDV2, and 1 for a manifest with no registration line.
- TTDV2 needs only the onboarding items of section 3.3. Record each step and its time (baseline for the next 13 apps).
- The TTDV2 module code is one class with one hook and two option providers. Record the number of lines.

## 9. Notes

- P2 (the first of the 13 apps) uses the same parts. It adds only what that app needs.
- Template gap: module 9 (4.2) says new audit tables get `CreatedByLanId`. `sql/05-new-app-template.sql` does not have it yet. `sql/12-ttdv2-app.sql` has it. Add it to the template before P2.

## 10. Open questions

1. TTD data access: the legacy class `TTDGeneral` and shared-type entities cannot map one table in one context.
   Answered (2026-10-25): option A. A second context, `AtlasPlatformContext`, holds only shared-type entities and uses the same connection and transaction as `CPSharepointContext`. Note (question 3): TTDV2 has its own schema, so there is no conflict; the platform context stays the place for all shared-type entities.
2. Owner match by LAN ID or display name.
   Answered (2026-10-25): option A, LAN ID. Note (question 3): TTDV2 keeps people as LAN IDs from the start. The LAN ID scripts for `TTD.General` move to P4 with the other existing tables.
3. How does P1 change TTD (switch, rollback, client selection, version token)?
   Answered (2026-10-25): it does not. P1 builds `TTDV2` as a new app with its own schema `TTDV2` and typed columns. The current TTD code is the reference. A copy script moves the TTD data at the change-over. This replaces the questions about the service switch, the client selection for TTD and the `RowVersion` column on `TTD.General`.
4. Change-over: after the copy, do TTD and TTDV2 run in parallel for a period, or does TTD stop at once?
   Answered (2026-10-25): option B. TTD stops at once. On the change-over day: (1) run the copy script; its part 2d revokes all TTD roles (soft delete) in the same transaction; (2) set `AppSettings:Modules:TTDV2:Enabled = true` and restart. TTD then leaves the menu (no active roles). The TTD rows stay unchanged.
5. Client route: how does the client open the new form component for TTDV2?
   Answered (2026-10-25): option A. New route `form/:code/:id` opens the new component (`WebUi/Platform/`). `v1/Application` `FormType` gets the new value `FORM` (with `SECTION`, `STEPPER`). The dashboard `onClick` and `editRow` use `FormType` first; with no value they use the current `if` chain. The add button uses `Target` (module 3, 4.5). The old routes and `CPAtlasSectionMaster` do not change. The dashboard component gets this small additive change in P1.
6. TTDV2 dashboard: tab data from the table (`Source: Table`, module 3, 4.7) in P1, or a stored procedure as in TTD today?
   Answered (2026-10-25): option A. `Source: Table` for the 3 tabs, no procedure (3.2). Two platform additions: the `Any` filter group and the column `Type: "user"`. The "All Report" shows all 7 days.

## 11. Coding plan for `atlas-platform/`

### 11.1 Solution layout

```text
atlas-platform/
  AtlasPlatform.slnx
  Directory.Build.props            net10.0, Nullable, TreatWarningsAsErrors (same as this repository)
  INTEGRATION.md                   copy list, program.cs lines, settings, SQL order
  src/
    BBH.CP.Atlas.Data/
      Platform/                    COPY: AtlasPlatformContext, column metadata reader, value converter
      StandIn/                     DO NOT COPY: CPSharepointContext (members used only), DTOs, models
    BBH.CP.Atlas.Service/
      Platform/                    COPY: registry, resolver, rules, user context, access, audit, options, pipeline, dashboard query
      Application/ConfigSectionService.cs, CPTTDV2Service.cs, TTDV2/ providers     COPY
      StandIn/                     DO NOT COPY: CPApplicationService, CPAtlasService, ISectionService and the other interfaces, CPUser
    BBH.CP.Atlas.App/
      Platform/                    COPY: AddAtlasPlatform, AddAtlasModule, start-up check, --atlas-check
      Config/TTDV2.app.json, TTDV2.json, TTDV2.Dashboard.json                      COPY
      StandIn/                     DO NOT COPY: program.cs host, controllers (only the endpoints that P1 uses), TPAMAPIAccess
    WebUi/
      src/app/platform/            COPY: form component, renderer registry, effects, route, dashboard navigation helper
      src/app/stand-in/            DO NOT COPY: @pbdev stand-in, old dashboard shell
  tests/
    BBH.CP.Atlas.Platform.Tests/   xUnit (as in this repository)
  sql/release-p1/                  010-050 in run order (copies of the module scripts)
```

Rules:

- Stand-ins (decision 2026-10-25, coding question 1): small hand-written classes with only the members that the platform uses, with the same names, namespaces and signatures as the `.txt` copies. Each stand-in file starts with `// STAND-IN: do not copy to the target.` A compile at the target confirms the signatures.
- Packages: only packages that the target already uses (EF Core SqlServer 10, the ASP.NET Core framework, Angular 21, AG Grid 33), plus the test packages of this repository (xUnit, Test SDK, coverlet). No new third-party package.
- `TimeProvider` everywhere; no `DateTime.Now` or `DateTime.UtcNow` outside the composition root.
- The workflow engine (`src/`, `WorkflowEngine.sln`) does not change.
- SQL Server tests use `localhost` with Windows authentication, as the engine tests do, with `[Trait("Category", "SqlServer")]`. They create and drop their own test database.

### 11.2 Build phases

Each phase ends with `dotnet build` (no warnings) and `dotnet test` green. Stop after each phase for a review.

| Phase | Content | Tests |
|---|---|---|
| C0 | .NET solution skeleton, `Directory.Build.props`, .NET stand-ins, empty `Platform/` folders, `INTEGRATION.md` and `local/README.md` outlines | Build; one smoke test |
| C1 | Data: `AtlasPlatformContext` (shared-type), `sys.columns` reader, value converter (6.4), load by key | Conversion unit tests; SQL tests on a test database made by `12-ttdv2-app.sql` |
| C2 | Config: manifest model, tolerant JSON loader, cache with copy, `AddAtlasPlatform`, `AddAtlasModule`, resolver, string app code | Loader parity test with all current JSON files; resolver 404/400 |
| C3 | Security: `IAtlasUserContext`, active roles (view), role flags, `IAtlasAccessService`, `Permissions`, `LogOnly`/`Enforce`, `Atlas.AccessDenied` log | Access matrix (module 1, 10.5) |
| C4 | Rules: `IAtlasRuleEngine` (`FirstMatch`, `MostPermissive`, `RuleDefault`, `OwnerFields`) | Rule matrix; `FirstMatch` gives the current results |
| C5 | Section: `ConfigSectionService` load (providers, tokens, `KeepSavedValue`, grid `SingleRowFields`, header) and save pipeline (allow list, validators, `Computed`, version, system fields, audit, `Errors`); `CPTTDV2Service`, `TaxYears`, `WeekEndings` | Section 5 load, save, concurrency and audit tests |
| C6 | Local database script and demo host (11.3). API: resolver hook in `CPAtlasService` stand-in, string `code`, `v1/Application` `FormType`/`HasDashboard`, `GetAuditLog` | Endpoint tests with a test host |
| C7 | Dashboard: TTDV2 dashboard file, rules before load, `Source: Table` query builder, `Any`, `Type: "user"`, `Permissions` menus | Query builder tests (names only from metadata, values only as parameters); 3 TTDV2 tabs |
| C8 | Start-up check (all modules) and `--atlas-check` | Exit code 0 / 1 cases |
| C9 | WebUi skeleton and `@pbdev` stand-in (11.4). Route `form/:code/:id`, form component, renderer registry, `Effects`, `Validator`, dashboard navigation by `FormType` and `Target` | Angular unit tests |
| C10 | Release package: `sql/release-p1/`, `INTEGRATION.md` complete, acceptance list (section 8) | Full test run; copy-script test on a copy of TTD sample data |

### 11.3 Local working copy

Decision 2026-10-25: the user runs TTDV2 on the local computer (.NET 10, Node, Angular CLI 21, SQL Server 2022 with Windows authentication) before the integration.

- Database: `atlas-platform/local/setup-local-db.sql` makes the database `CPAtlasPlatformDev` with the `dbo.CP_Atlas_*` tables (target shape), runs `sql/release-p1/` 020-040, and adds test users (the local Windows login and one user for each role `Read`, `Prepare`, `Owner`).
- API: the stand-in host runs on Kestrel with Windows authentication (`AddNegotiate`), with Swagger for the TTDV2 endpoints.
- UI: `ng serve` shows the dashboard and the form `form/TTDV2/{id}`.
- Stand-ins for internal services: PB accounts (Oracle) = sample accounts; Active Directory = the Windows login + a settings file for the test users; TPAM = a local connection string.
- Development-only user switch: a setting lets the local user act as each test user, to test the access table (module 1, 10.5). It is off outside `Development`.
- Demo points: after C6, the API with Swagger; after C9, the full app in the browser.
- `atlas-platform/local/README.md`: the steps to run it.

### 11.4 `@pbdev/pbdev-core` stand-in

The internal library is not available on this computer. Decision 2026-10-25:

- A local package `@pbdev/pbdev-core` in `WebUi/src/app/stand-in/pbdev-core/`, with the same selectors, inputs, outputs and export names. The form controls implement `ControlValueAccessor`, so `formControlName` works as with the real library. Plain HTML and simple CSS; the same style file names.
- One switch point: a `tsconfig` path maps `@pbdev/pbdev-core` to the stand-in. At the target, do not copy the mapping or the folder; the same import goes to the real package. The new component code does not change.
- The new component uses only the elements and inputs that the current `cp-atlas-sectionmaster.html` uses with the real library (`pbdev-input` `inputType`, `pbdev-select` `[options]`, `pbdev-textarea` `[rows]`, `pbdev-date-picker`, `pbdev-checkbox`, `pbdev-section`, `pbdev-section-label` `[label]`, `pbdev-loader`, `pbdev-dialog`). No stepper and no file picker in P1.
- `INTEGRATION.md` lists each item to check at the target (look and feel, masks).
- The WebUi skeleton and the stand-in package are made in C9 (C0 makes the .NET skeleton only).

Update 2026-10-27 (rework R1-R6, `atlas-platform/PBDEV.md` level-set 2): the real pbdev-core 6.0.1 source and the section master as of 2026-10-27 are now read-only in `target-implementation-read-only/`. The stand-in follows the real source (inputs, value types: Date, boolean, raw option value; own error text). The new form and dashboard follow `cp-atlas-sectionmaster` and `cp-atlas-dashboard` of 2026-10-27: page layout and classes, the mandatory-field modal (D2), toastr after a save, `ng-select` with `label`/`value` (D3), no mask on currency/percent (D1), the `grid` control on `ag-grid-angular` with the target cell components (`NumericEditor`, `TextEditor`, `SelectEditor`, given with `provideAtlasGridComponents`), and pbdev-core styles (D4). `pbdev-section-label` is not in the library and is not used.
