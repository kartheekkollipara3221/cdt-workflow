# Integration at the target

Status: complete for release P1 (C10, 2026-10-25). Build: 0 warnings. Tests: 140 .NET (xUnit, SQL Server) + 13 Angular (Vitest).
Local review: `local/README.md`.

## 1. Files to copy

| From `atlas-platform/` | To the target |
|---|---|
| `src/BBH.CP.Atlas.Data/Platform/` | `BBH.CP.Atlas.Data/Platform/` |
| `src/BBH.CP.Atlas.Service/Platform/` | `BBH.CP.Atlas.Service/Platform/` |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs`, `CPTTDV2Service.cs`, `TTDV2/` | `BBH.CP.Atlas.Service/Application/` |
| `src/BBH.CP.Atlas.App/Platform/` | `BBH.CP.Atlas.App/Platform/` |
| `src/BBH.CP.Atlas.App/Config/TTDV2.app.json`, `TTDV2.json`, `TTDV2.Dashboard.json` | `BBH.CP.Atlas.App/Config/` (Copy to Output: PreserveNewest) |
| `WebUi/src/app/platform/` | `WebUi/Source/App/platform/` (the `dashboard/` folder is a reference: see section 2) |
| `sql/release-p1/` | the DBA release folder (section 3) |

Do not copy: `StandIn/` folders, `WebUi/src/app/stand-in/`, `WebUi/src/app/app.*`, `WebUi/src/environments/`, `BBH.CP.Atlas.Infra`, `*.csproj`, `appsettings*.json`, `tests/`, `local/`, the `@pbdev` stand-in and its `tsconfig` path.

## 2. Changes to existing target files

| File | Change | Why |
|---|---|---|
| `Data/DTO/CPAtlasApplicationConfig.cs` | Add `partial` to: `CPAtlasApplicationConfig`, `CPApplicationStep`, `CPApplicationSection`, `CPApplicationControl`, `CPValidator`, `CPKeyValue`, `CPGrid`, `CPGridColumn`, `CPCellRendererParam`, `CPAttachment`, `CPWorkflow`, `CPWorkflowCondition`, `CPWorkflowNotification`, `CPButton`, `CPSkipValidation`, `CPRule` | The platform adds optional properties in `Platform/` partial files. No behaviour change. |
| `Data/DTO/CPAtlasDashboardConfig.cs` | Add `partial` to: `CPAtlasDashboardConfig`, `CPAtlasDashboard`, `CPAtlasTab`, `CPParameter`, `CPMenu` | Same |
| `Data/DTO/CPAtlasPage.cs`, `CPAtlasLog.cs` | Add `partial` to `CPAtlasPage`, `CPAuditLog` | Same |
| `program.cs` | `builder.Services.AddAtlasPlatform(configuration);` and `builder.Services.AddAtlasModule<CPTTDV2Service>("TTDV2");` before `Build()`; `await app.Services.StartAtlasPlatformAsync();` after `Build()` (loads the table metadata and runs the start-up check; an error stops the start-up). `--atlas-check`: remove the switch from `args` before `CreateBuilder`, and after `Build()` add `if (check) return await app.Services.RunAtlasCheckAsync(Console.Out);` (see `StandIn/Program.cs`; exit code 0/1, no web server). | Modules 7, 11 |
| `CPAtlasService.cs` | Add the optional constructor parameters `IAtlasModuleResolver? resolver = null, IAtlasConfigStore? configStore = null` at the end. At the start of `GetApplication` (after the procedure call), `LoadDashboard`, `LoadSectionPage`, `ManagePage`, `RemovePage`, `AuditLog`, `GetAuditLog`: copy the lines marked `PLATFORM` from `Service/StandIn/CPAtlasService.cs`. The `if` chains stay for the current codes. `ManagePage`/`AuditLog` keep `JsonConvert` (Newtonsoft reads the new optional properties). | Module 7, 4.3 |
| `Controllers/V1/CPAtlasController.cs` | `LoadDashboard(string code)`, `RemovePage(string code, int id)`; `Download`/`RemoveAttachment` stay. Inject `IAtlasModuleResolver` and `IAtlasConfigStore` and pass them to `new CPAtlasService(...)`. The current callers send `code=TTD` (binds the same); a number value maps to the enum name (`AtlasAppCode.FromRequest`). Inside the `if` chains use `Enum.Parse<CPApplicationCode>(AtlasAppCode.FromRequest(code), true)`. | Module 7, 4.4 |
| `app.routes.ts` | Add `...atlasPlatformRoutes` (route `form/:code/:id`, `platform/atlas-platform.routes.ts`). The current routes stay. | Module 12, question 5 |
| `cp-atlas-dashboard.ts` | In `onClick` and `editRow`: first `const route = formRoute(formTypeOf(apps, code), code, id); if (route) { this.router.navigate(route); return; }` (`platform/atlas-navigation.ts`; `apps` = the `v1/Application` rows), then the current `if` chain. The add button uses `button.Target` when it is there. For a tab with `Columns`, use `columnDefs()` and `sumsRow()` from `platform/dashboard/atlas-dashboard-columns.ts`. | Module 3, 4.5-4.6 |
| `angular.json` | Add `"src/app/platform/atlas-platform.css"` (the copied `platform/atlas-platform.css`, target path) to `styles`, after `src/styles.scss` (build and test sections). | Review NG-06 |
| `package.json` | No new package: the platform uses `ag-grid-community`/`ag-grid-angular` 33, `@ng-select/ng-select` 21 and `@pbdev/pbdev-core` (all already at the target). | Spec 12, 11.1 |
| Menu (app component) | Show an app only when `HasDashboard` is not `false`. | Module 7, 4.6 |

## 3. SQL

Run `sql/release-p1/` in number order (DBA in QC and production). See `sql/release-p1/README.md` and spec 12, section 4.
`050` runs only on the change-over day. Before: part 1 of `050` is read only and lists each value that cannot be converted.

## 4. Settings

| Key | Value | Note |
|---|---|---|
| `AppSettings:ConfigFolder` | `Config` (default) | Folder of the manifests and JSON files, relative to the content root |
| `AppSettings:Modules:TTDV2:Enabled` | `false` in production until the business approves | Rollback switch (spec 12, section 7) |
| `AppSettings:Modules:TTDV2:AccessMode` | `LogOnly` (P1) | `Enforce` after one release of log review |
| Log events | `Atlas.AccessDenied` (41001), `Atlas.FieldIgnored` (41010), `Atlas.SaveFailed` (41011), `Atlas.StartupCheck` (41020) | Search the log for these names |

Release check: `BBH.CP.Atlas.App.exe --atlas-check --environment QC` returns 0 (no errors) or 1.

## 4.1 Acceptance (spec 12, section 8)

| Item | Result |
|---|---|
| Section 5 tests | Pass: load defaults and saved values; refusals (extra name, disabled field, `1.5`, `1:30`, `-5`, `1441`); `WeeklyTotal` calculated again; conflict; audit `Create`/`Update`/`Delete`, no entry for no change; access matrix in `Enforce`; `Atlas.AccessDenied` in `LogOnly`; copy script on TTD sample data. |
| `--atlas-check` | 0 for TTDV2; 1 for a manifest with no registration line (test `AtlasCheckTests`). The current apps have no manifests and are not checked. |
| Onboarding items for TTDV2 | SQL `040`, `TTDV2.app.json`, `TTDV2.json`, `TTDV2.Dashboard.json`, role grants, one `AddAtlasModule` line. No TypeScript change. |
| TTDV2 module code | One class with one hook (`CPTTDV2Service.cs`, 31 lines) + two option providers (`TTDV2OptionProviders.cs`, 38 lines). JSON: 19 + 210 + 92 lines. Baseline for the next 13 apps. |

## 5. Check at the target

| Item | Reason |
|---|---|
| `CPAtlasUser.LanId` / `Lanid` property name | Copy error in the read-only copy (model file and mapping differ) |
| EF Core version: local 10.0.7, target 10.0.13 | Same major version; build at the target confirms |
| `System.Security.Cryptography.Pkcs` | Locally pinned to 10.0.13 (vulnerability advisories on 9.0.4 via EF Core 10.0.7). Check that the target build has no NU1902/NU1903. |
| API login rights on schema `TTDV2` | `SELECT, INSERT, UPDATE, DELETE` (step 6 of `12-ttdv2-app.sql`). The metadata reader needs no extra grant. |
| `@pbdev/pbdev-core` look and feel, masks | The local stand-in only copies the behaviour (C9) |
| `@pbdev/pbdev-core` names and inputs | `PBDEV.md` section 3 lists the 4 bindings to check in the first target build. Best: copy the package `*.d.ts` files (`PBDEV.md` section 4). |

## 6. Findings during the build (for the review)

| # | Phase | Finding | Handling |
|---|---|---|---|
| B1 | C1 | EF Core property bags use the sentinel null; a new record sent `ID = 0` to the identity column. | Sentinel = type default for non-nullable value columns (`AtlasPlatformContext`). |
| B2 | C2 | `System.Text.Json` enforces the C# `required` keyword; `Newtonsoft.Json` does not. `SQMSO.json`, `SQMSB.json` (no `Workflow.Roles`) and `CDT.json` (controls with no `Label`) fail with a plain STJ loader. | The platform loader turns `required` off (`AtlasJson`). Test: all current form files and the dashboard file load. |
| B3 | C4 | Design choice for the new path: a rule can hide or disable a child control, but a control that the JSON disables (for example `AccountNumber`, filled by `SetFromOption`) stays disabled unless a rule names it in `EnableControls`. The legacy `ApplyRules` enables it. Only apps on the new path use this. | `AtlasRuleEngine.ApplyToSection`. For review. |
| B4 | C4 | Button rules use `RuleDefault: Hidden` when the app uses `ReadOnly` or `Hidden` (a read-only button has no use). | For review. |
| B5 | C4 | The form JSON gets an optional app-level `Rules` list (module 1, 10.5 example): the default for each section-level control with no own rules. | `CPAtlasApplicationConfig.Rules`. |
| B6 | C5 | The legacy `LoadWeekEnding` loop adds `nextSunday + 7` three times (the same date 3 times). | `WeekEndingsOptionProvider`: the last Sunday (today if Sunday) + the next 3 different Sundays. |
| B7 | C5 | Allow list detail (spec 01, 5.9 step 4 says "ignore"; spec 12, section 5 says "refuse"). | An unknown output name = `NotAllowed`. A hidden or disabled field with a changed value = `NotAllowed`; the same value is ignored (a client can send all values). System columns, `Computed` fields and `SetFromOption` targets are set by the server; a client value is ignored (log `Atlas.FieldIgnored`). |
| B8 | C5 | Hook order. | `OnLoad` runs after the values, options and defaults (so TTDV2 sees the default week) and before the rules. |
| B9 | C5 | TTD sets `ModifiedBy`/`ModifiedDate` only on update. | The platform also sets them on create, so the dashboard order `ModifiedDate DESC` shows new records first. |
| B10 | C5 | `SetFromOption` targets (`AccountNumber`, `AccountName`) are disabled controls. | The server sets them from `Data` of the selected option. If the option is not in the list (an old saved value), the stored values stay. |
| B11 | C5 | "Empty = 0" for the day minutes (spec 01, 10.3). | A grid field with `Validator.Integer` and an empty value saves `0`. |
| B12 | C6 | `sqlcmd` runs with `QUOTED_IDENTIFIER OFF` by default. `12-ttdv2-app.sql` makes filtered indexes and failed with Msg 1934 under `sqlcmd` (SSMS has `ON`). The same risk is in `05-new-app-template.sql`. | Both scripts now start with `SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;`. DBAs who use `sqlcmd` can also add `-I`. |
| B13 | C6 | `Microsoft.AspNetCore.Authentication.Negotiate` 10.0.6 has high-severity advisories (NU1903). | The stand-in host uses 10.0.13. Only the local host uses it (the target uses IIS Windows authentication). The local default is `StandIn:Authentication = Fixed` (the Windows login of the API process), so any browser works without a Windows sign-in. |
| B14 | C7 | Dashboard rules on the new path. | A tab with rules where no rule matches is hidden (`Hidden` default) and gets no rows. A JSON menu `Rule` keeps the legacy `FirstMatch` meaning. A tab with no `Menus` gets `Edit`/`History`/`Delete` from `Permissions` (`Edit` visible if the user can edit own or all records; the server checks each record). The add button with no `Rules` is hidden when the user has no `Create` permission. |
| B15 | C7 | Row shape of a `Source: Table` tab. | `KeyValues` rows are keyed by the column `Name` (the client shows `Label`); `ID` is always in the row (for `Link: Edit`). Dates are text (`yyyy-MM-dd`, or ISO UTC with `Z` for `datetime`). `KeyLength` is not set (the client uses `Width`). |
| B16 | C8 | Start-up check scope in P1. | Errors: manifest code, registration line (both directions), app row and `Parent`, tables, form fields/`OwnerFields`/`SoftDelete`/`Computed`/`Defaults` against the columns and controls, option providers, dashboard tabs (`Procedure` xor `Source: Table`, names, operators, values). Warnings: a disabled manifest, an unknown rule or `Permissions` role, a `Service` name that does not match the class. The check covers platform apps only; the current apps are not checked (no manifests). |
| B17 | Review | Chrome Issues: "A form field element should have an id or name attribute" (6) and "Incorrect use of <label for=FORM_ELEMENT>" (7) on the TTDV2 form. | Each renderer passes the id `ctl_` + Name to its input: `inputId` on `pbdev-select`/`pbdev-textarea`/`pbdev-date-picker`/`pbdev-checkbox` (as on `pbdev-input`), `labelForId` on `ng-select`. Types with no input (`user`, `grid`) get a label with no `for` (`AtlasRendererRegistry.hasInput`; `register(type, component, hasInput = true)` stays compatible). Changed by B18: the platform no longer binds `inputId` on library controls (the `atlasControlId` directive sets the id). |
| B18 | Review NG-01 | The `@pbdev` stand-in had other export names (`PbdevSelect`, ...) and inputs (`open`/`closed`, `label`) than the real library, so the copied platform did not compile at the target. | The stand-in now has the target class names and only the members that the target WebUi uses (evidence: `PBDEV.md`). All platform imports go through `platform/atlas-pbdev.ts`. Dialogs use `[visible]`/`(visibleChange)`; sections use `<pbdev-section [title]>`; `pbdev-select` gets `{label, value}` options (`toSelectOptions`); `atlasControlId` puts the field id on the inner element. Placeholders of select and textarea are not bound (not proven). |
| B19 | Review NG-02 | The form went back to `/apps/:code`, a route that only the local shell had. | The form uses `ATLAS_DASHBOARD_LINK` (`platform/atlas-navigation.ts`). Its default is the target route `['/dashboard', code]`, so the target needs no provider. The local shell now uses the same `dashboard/:code` path (`apps/:code` redirects). |
| B20 | Review NG-05 | `platform/platform.spec.ts` imported `vitest` and used `it.each`; the target runs Karma with Jasmine. | No runner import, a plain loop instead of `it.each`, and only APIs that both runners have (`describe`, `it`, `expect`, `TestBed`, `toBe`, `toEqual`, `toBeNull`, `toBeTruthy`). The spec is copied with `platform/` and runs in the target `ng test`. |
| B21 | Review NG-06 | The platform styles were only in the local `styles.css` (not copied), the platform used `btn--danger` (not proven at the target), and Tailwind classes that are only in server JSON are not generated by the target Tailwind scan. | `platform/atlas-platform.css` (copied) has the `atlas-*` rules with `--atlas-*` variables and `.grid-column-start-end-1-2` (used by `TTDV2.json`, not defined at the target). Buttons use `btn`/`btn--primary` (target `pbdev-core.min.css`) and `atlas-btn-danger`. `platform/atlas-tailwind-classes.ts` lists the Tailwind classes of the JSON, so the target scan generates them with no config change; a new class goes in that list. `src/styles.css` is now stand-in only. |
| B22 | Review SV-01 (challenges B10 in part) | The server did not check that a value is one of the options, so a direct API call could save any Category, WeekEnding, TaxYear or Account, and any LAN ID as `TaxPreparer` (an `OwnerField`). | Step 4 of the save: a changed value of a control with `Options` or `OptionsSource` must be in the server list for this user (multi-select: each comma-separated value), else the field error `NotInOptions`. An unchanged saved value stays valid (B10 and `KeepSavedValue` unchanged). A field with an error gets no second validator error. Tests: `A_value_that_is_not_an_option_is_refused` (5 cases), `A_saved_value_that_is_no_longer_an_option_can_stay`. The API test now sends the week and year defaults of the loaded page (it depended on the date). |
| B23 | Review SV-02 (changes spec 06 section 5 in part) | In `LogOnly` mode a user without the Delete permission (also a user with no role) could soft-delete any record; a form without `Permissions` allowed all operations. | `Demand` refuses a Delete with 403 in `LogOnly` too (the denial is still logged). View, Create, Edit and History keep the `LogOnly` behaviour, so a user whose role grant is missing can still work during the change-over. The start-up check gives an error for a platform form with no `Permissions` block. Tests: `LogOnly_mode_still_refuses_a_delete_with_403`, `LogOnly_does_not_let_a_user_without_the_Delete_permission_delete`, `A_form_without_a_Permissions_block_is_an_error`. |
| B24 | Review HO-03 | An environment override that could not be parsed was ignored, so a typo left the app on (`Enabled`) or access not enforced (`AccessMode`); a number such as `2` was accepted as an enum value. | Only the enum names (any case) and `true`/`false` are accepted. A value that is present but not valid fails closed: `Enabled` = false, `AccessMode` = `Enforce`; the other modes keep the manifest value. Each one is a start-up check error (`AtlasAppManifest.SettingErrors`), so `--atlas-check` returns 1. A warning reminds that `AccessMode` is `LogOnly` outside Development. Valid settings work as before. |
| B25 | Review DA-01 | A table with a column type that the platform does not support (`xml`, `time`, `float`, ...) or with a key that is not `int IDENTITY` passed `--atlas-check`; the first request then failed while the one EF model of all platform tables was built, so all platform apps failed. | The start-up check reports, for each Table/AuditTable/AttachmentTable: no `ID` column, an `ID` that is not `int IDENTITY`, and each unsupported column. When there is no other error it builds the EF model once and reports a failure. Test: `A_table_with_an_unsupported_column_or_key_type_is_an_error`. |
