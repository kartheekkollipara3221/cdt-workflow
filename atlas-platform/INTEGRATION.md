# Integration at the target (outline)

Status: outline (C0). Each phase adds its steps. C10 completes this guide.

## 1. Files to copy

| From `atlas-platform/` | To the target |
|---|---|
| `src/BBH.CP.Atlas.Data/Platform/` | `BBH.CP.Atlas.Data/Platform/` |
| `src/BBH.CP.Atlas.Service/Platform/` | `BBH.CP.Atlas.Service/Platform/` |
| `src/BBH.CP.Atlas.Service/Application/` (new files only) | `BBH.CP.Atlas.Service/Application/` |
| `src/BBH.CP.Atlas.App/Platform/` | `BBH.CP.Atlas.App/Platform/` |
| `src/BBH.CP.Atlas.App/Config/TTDV2.*.json` | `BBH.CP.Atlas.App/Config/` |
| `WebUi/src/app/platform/` (C9) | `WebUi/Source/App/platform/` |

Do not copy: `StandIn/` folders, `BBH.CP.Atlas.Infra`, `*.csproj`, `appsettings*.json`, `tests/`, `local/`, the `@pbdev` stand-in and its `tsconfig` path.

## 2. Changes to existing target files

| File | Change | Why |
|---|---|---|
| `Data/DTO/CPAtlasApplicationConfig.cs` | Add `partial` to: `CPAtlasApplicationConfig`, `CPApplicationStep`, `CPApplicationSection`, `CPApplicationControl`, `CPValidator`, `CPKeyValue`, `CPGrid`, `CPGridColumn`, `CPCellRendererParam`, `CPAttachment`, `CPWorkflow`, `CPWorkflowCondition`, `CPWorkflowNotification`, `CPButton`, `CPSkipValidation`, `CPRule` | The platform adds optional properties in `Platform/` partial files. No behaviour change. |
| `Data/DTO/CPAtlasDashboardConfig.cs` | Add `partial` to: `CPAtlasDashboardConfig`, `CPAtlasDashboard`, `CPAtlasTab`, `CPParameter`, `CPMenu` | Same |
| `Data/DTO/CPAtlasPage.cs`, `CPAtlasLog.cs` | Add `partial` to `CPAtlasPage`, `CPAuditLog` | Same |
| `program.cs` | `builder.Services.AddAtlasPlatform(configuration);` and `builder.Services.AddAtlasModule<CPTTDV2Service>("TTDV2");` before `Build()`; `await app.Services.StartAtlasPlatformAsync();` after `Build()` (loads the table metadata before the first request) | Module 7 |
| `CPAtlasService.cs` | Add the optional constructor parameters `IAtlasModuleResolver? resolver = null, IAtlasConfigStore? configStore = null` at the end. At the start of `GetApplication` (after the procedure call), `LoadDashboard`, `LoadSectionPage`, `ManagePage`, `RemovePage`, `AuditLog`, `GetAuditLog`: copy the lines marked `PLATFORM` from `Service/StandIn/CPAtlasService.cs`. The `if` chains stay for the current codes. `ManagePage`/`AuditLog` keep `JsonConvert` (Newtonsoft reads the new optional properties). | Module 7, 4.3 |
| `Controllers/V1/CPAtlasController.cs` | `LoadDashboard(string code)`, `RemovePage(string code, int id)`; `Download`/`RemoveAttachment` stay. Inject `IAtlasModuleResolver` and `IAtlasConfigStore` and pass them to `new CPAtlasService(...)`. The current callers send `code=TTD` (binds the same); a number value maps to the enum name (`AtlasAppCode.FromRequest`). Inside the `if` chains use `Enum.Parse<CPApplicationCode>(AtlasAppCode.FromRequest(code), true)`. | Module 7, 4.4 |
| `app.routes.ts`, dashboard component | (C9) route `form/:code/:id`, `FormType` navigation | Module 12, question 5 |

## 3. SQL

Run `sql/release-p1/` in number order (DBA in QC and production). See spec 12, section 4.

## 4. Settings

(C2, C6) `AppSettings:ConfigFolder`, `AppSettings:Modules:TTDV2:*` in `appsettings.<Env>.json`.

## 5. Check at the target

| Item | Reason |
|---|---|
| `CPAtlasUser.LanId` / `Lanid` property name | Copy error in the read-only copy (model file and mapping differ) |
| EF Core version: local 10.0.7, target 10.0.13 | Same major version; build at the target confirms |
| `System.Security.Cryptography.Pkcs` | Locally pinned to 10.0.13 (vulnerability advisories on 9.0.4 via EF Core 10.0.7). Check that the target build has no NU1902/NU1903. |
| API login rights on schema `TTDV2` | `SELECT, INSERT, UPDATE, DELETE` (step 6 of `12-ttdv2-app.sql`). The metadata reader needs no extra grant. |
| `@pbdev/pbdev-core` look and feel, masks | The local stand-in only copies the behaviour (C9) |

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
