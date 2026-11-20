# ClientInflows (P2, `CFPINFLOW`): gap report

Date: 2026-11-19. Input: `ONBOARDING_ANSWERS.md` (D1 to D34, closed). Code read: `atlas-platform/` at commit 80bbb74,
the engine in `src/`, and the target copy of 2026-11-18 (`cp sharepoint.txt`). No code and no SQL is changed by this
report.

Each requirement is in one group:

- **A** = metadata only (JSON files and SQL scripts). The platform code of today does it.
- **B** = new generic platform code. No `CFPINFLOW` name in C# or TypeScript.
- **C** = workflow engine or engine adapter (module 10).

Paths are relative to the repository root. `Service/` = `atlas-platform/src/BBH.CP.Atlas.Service/`,
`Data/` = `atlas-platform/src/BBH.CP.Atlas.Data/`, `App/` = `atlas-platform/src/BBH.CP.Atlas.App/`,
`WebUi/` = `atlas-platform/WebUi/src/app/platform/`.

## 1. Summary

| Group | Count | Largest items |
|---|---|---|
| A metadata only | 17 | App rows and roles, form and dashboard JSON, additive `CFP.Inflow` columns, `CFP.AuditLog`, engine workflow metadata |
| B new generic platform code | 12 | Attachments (module 8), `richtext`, table option provider (AIN lookup), state in the rules, action `Set`, `MustBeTrue` validator, `--atlas-check` name report |
| C engine or adapter | 8 | Engine adapter (module 10, not coded), 2 emails for one transition (engine has one route for each transition), recipients by display name, list by Investment Type |

Result: P2 is the first app that needs modules 8 and 10. Most of the work is group B and C code that the next apps
use again. The engine itself needs no change if question Q2 gets answer A.

## 2. Group A: metadata only

| # | Requirement | What exists today | What P2 adds |
|---|---|---|---|
| A1 | D1, D3: app `CFPINFLOW`, top-level menu, label | `CP_Atlas_Application` row + manifest; model `atlas-platform/sql/release-p1/040-ttdv2-app.sql`, `docs/atlas-platform/sql/05-new-app-template.sql`; menu from `Service/Platform/Registry/AtlasApplicationMenu.cs` | App row, `CFPINFLOW.app.json` |
| A2 | D21, D22: roles `Read`, `Write`; all users see all records | Role rows + `Permissions` block (`Data/Platform/Dto/CPPlatformDto.cs` `CPPermissions`); `Service/Platform/Security/AtlasAccessService.cs` | Role rows; `View: [@Read]`, `Create/Edit.All/Delete: [Write]`, `History: [@Read]`; no `OwnerFields` |
| A3 | D24, D26: Add New for Write only; menu Edit, Delete, History; Delete hidden for Read; Read user sees the form disabled | `UserPermissions` drives the buttons and the row menu (`WebUi/dashboard/atlas-action-menu.ts`, `Service/Platform/Dashboard/AtlasDashboardService.cs`); app rule `{ Roles: [@Read], Disabled: true }` as in `App/Config/TTDV2.json` | JSON only |
| A4 | D25, D26: soft delete in each state, no email | `ConfigSectionService.RemovePage` + `SoftDelete` setting; a nullable `IsDeleted` is read correctly (`Data/Platform/AtlasDataExtensions.cs`) | JSON only. Note N3 |
| A5 | Fields 2 to 5, D11: 4 fields filled from the account, read-only | Effect `SetFromOption` runs on the client (`WebUi/form/atlas-effects.ts`) and again on the server (`Service/Platform/Sections/AtlasServerEffects.cs`); a server-set field is not accepted from the client (`ConfigSectionService.IsServerSet`) | JSON only (the option source is B3) |
| A6 | Field 6, D10: Investment Type fixed list | `select` with `Options`; the server checks the value (`ConfigSectionService.OptionError`) | JSON only |
| A7 | Field 7: Amount, USD, 2 decimals, more than 0 | Control `currency` (`WebUi/form/atlas-renderer-registry.ts`), validator `Min` (`Service/Platform/Sections/AtlasFieldValidator.cs`); column `decimal(19,4)` | `Validator: { Required, Min: 0.01 }` |
| A8 | Field 8: Inflow Date | Control `date`; column `datetime` | JSON only |
| A9 | Field 11: Submitted By (part 1) = current user at the first save | `Defaults: { "...": "{User.LanID}" }` for a new record + type `user` (`Service/Platform/Sections/AtlasDefaultTokens.cs`) | JSON only. Part 2 is B6 |
| A10 | D23, D25: tabs "Inflows" and "Inflow Pipeline", columns, order, link on ID | `Source: Table` tabs with `Filter`, `OrderBy`, column types `currency`, `date`, `user`, `Link: Edit` (`Service/Platform/Dashboard/AtlasTabQueryBuilder.cs`, `WebUi/dashboard/atlas-dashboard-columns.ts`) | `CFPINFLOW.Dashboard.json` |
| A11 | D14, D28: Date of destruction = created + 7 years, dashboard only | A SQL computed column is read and never written (`Data/Platform/AtlasTable.cs` `IsDatabaseGenerated`) | Script: `DestructionDate AS DATEADD(year, 7, CreatedDate)` |
| A12 | D2 and hand-off 5: gaps of `CFP.Inflow` | Table metadata is read at start-up (`Data/Platform/AtlasColumnMetadataReader.cs`); the version check finds a `rowversion` column by type | Additive script, new nullable columns: `RowVersion`, `WorkflowState`, `Status`, `FundsReceived`, `CashSubmittedDate`, `RMCC` (see Q8), `DestructionDate` |
| A13 | D27: History of all fields | `Service/Platform/Audit/AtlasAuditWriter.cs` (writes `ReferenceID`, `ReferenceType`). No `CFP.AuditLog` table exists at the target | Script: new `CFP.AuditLog` (template part of `05-new-app-template.sql`); manifest `AuditTable` |
| A14 | D15: states Draft (placeholder), Submitted, Funds Received; T1, T2; no SLA (D20) | Engine workflow metadata is SQL (states, actions, roles, transitions, routes); a workflow with no task and no SLA policy is permitted | Engine metadata script for a new workflow key. Depends on C1 |
| A15 | D19: old subjects and body text, link | Engine route templates: `SubjectTemplate`, `BodyTemplate`, `HtmlBodyTemplate` with `{{token}}` (PROJECT.md section 14) | Route rows in the same script |
| A16 | D6, D7, D17, D18: fixed lists (ICG, CS Team, IMRG, Star FAS IFI Cash Team) | Engine rule `StaticAddress`, kind To or Cc (`src/WorkflowEngine.Infrastructure/Notifications/NotificationRecipientResolver.cs`) | Rule rows; placeholder addresses in the local script |
| A17 | D32: new AIN column `RMCC` | none | Local script `ALTER TABLE AIN.AccountInventory ADD RMCC`; the target team does the same |

## 3. Group B: new generic platform code

| # | Requirement | What exists today | Gap |
|---|---|---|---|
| B1 | Fields 10, 15: `filepicker`, many files, required, read-only after submit; D27 files in History | Module 8 is design only (`docs/atlas-platform/08-attachments.md`, section 10 decisions). Code: manifest key `AttachmentTable` exists and is not read (`Service/Platform/Registry/AtlasAppManifest.cs`); `ConfigSectionService.SavePage(input, files)` gets `files` and ignores them; the client has no `filepicker` renderer (`WebUi/form/atlas-renderer-registry.ts`; a spec expects `AtlasUnsupportedRenderer`) | All of module 8 for platform apps: attachment service, save in the record transaction, removal on save (new optional request field), limits (10 MB a file, 25 MB a save, type and signature check), `Required` = at least one file after the save, audit entries, controller `GET v1/Attachment/{appCode}/{id}`, client control, start-up check |
| B2 | Fields 9, 14, D12, D31: `richtext`, no pictures | Not coded. Target `package.json` has `ckeditor5` 41.4.2 packages (in the CVE-2024-45613 range) | Client renderer with CKEditor 5 (tables, lists, links; no picture plugin); server-side HTML clean-up on save (Q6); plain text for the dashboard and History |
| B3 | D8, D29: account list from `AIN.AccountInventory`, "number - name", reusable in 4+ apps | `PBAccountsOptionProvider` reads another source (PB accounts) (`Service/Platform/Options/AtlasOptionProviders.cs`); `IAtlasOptionProvider` is the extension point | One generic provider that reads a table named in `OptionsSource.Parameters` (table, value column, label format, data columns, not-deleted filter). The table and column names are checked against the table metadata at start-up. No AIN name in code |
| B4 | D4, D16: part 2 shows after the first submit; part 1 read-only after T1; all read-only after T2 | `CPRule.States` exists and the rule engine compares it, but the service always sends an empty state list (`ConfigSectionService.RuleContext` -> `new(..., [], ...)`; `Service/Platform/Rules/AtlasRuleEngine.cs` `Matches`) | Read the state of the record (the `StateField` column) into the rule context, on load and on save. A new record has the first state |
| B5 | Fields 12, 13, 17: check box must be ticked to submit | `Required` passes for the value `false` (`AtlasFieldValidator.Validate` tests only for empty; the wire value of a bit is `true`/`false`) | New optional validator `MustBeTrue`, on the server and the client (`WebUi/form/atlas-validators.ts`, shared cases file) |
| B6 | Field 16, D23: part 2 Submitted By and "Funds confirmed date" set at T2 | `Defaults` apply to a new record only; `CPButtonAction.Set` is reserved and not read (`Data/Platform/Dto/CPPlatformDto.cs`) | Read `Action.Set` on save: field to token (`{User.LanID}`, `{Now}`); the fields are server-set. Also used for `FundsReceived` -> `Status` if needed |
| B7 | T1, T2 buttons | The form reads `Action.Type` Save, Delete, Close (`WebUi/form/atlas-form.component.ts`); `Execute` is reserved | Button action `Execute` with a workflow action key: save + engine action in one request (the server part is C1) |
| B8 | D34: `--atlas-check` lists AIN accounts whose RM or RMCC name does not match one user | `App/Platform/AtlasStartupCheck.cs` has errors and warnings only; the gate demands `0 error(s), 0 warning(s)` | A generic check for a "name must match one user" field of a binding, and a way to report data findings (Q7) |
| B9 | Dashboard and History show rich text as plain text | none | Column type or flag for HTML-to-text in `AtlasDashboardService` and `AtlasAuditWriter` |
| B10 | D23: export | No export code was found in the platform dashboard or in the target `cp-atlas-dashboard.ts` copy | See Q9 (not blocking) |
| B11 | Hidden part 2 must not send values or files before T1 | The allow list refuses fields that are hidden or disabled (`Service/Platform/Sections/AtlasAllowList.cs`) | Exists for fields. The attachment upload must use the same check (part of B1) |
| B12 | Local test | `atlas-platform/local/` has no CFP or AIN tables | Local scripts for `CFP.*`, `AIN.AccountInventory` (sample rows), users and roles (A035313 Write, A023456 Read) |

## 4. Group C: workflow engine and engine adapter

| # | Requirement | What exists today | Gap |
|---|---|---|---|
| C1 | D15, T1, T2: workflow on a section form | Module 10 is design only (`docs/atlas-platform/10-engine-adapter.md`). The platform has no reference to the engine (`Service/BBH.CP.Atlas.Service.csproj`). `CPPermissions.Execute` is reserved. The target registers the engine services in `Program.cs` | The adapter: `Workflow` binding in the form JSON, start and execute, engine call last then commit, result check, state and status written to the record, dispatch after commit, state check on load, `RequestId`. **Stop-and-ask item (Q1)** |
| C2 | D17: 2 emails at T1 (E1 to ICG, E2 to the Investment Type lists and RM), different subject and text | The engine has one `NotificationRouteKey` for each transition and makes one outbox row (`src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs`, the block that reads `transition.NotificationRouteKey`) | Not possible with metadata alone. Options in Q2 |
| C3 | D6, D7: To list by Investment Type; "Fixed Income & Equity" = 2 lists | Engine rules are fixed for a route. The only filter is `AssignedRoleKey`, and it is valid only when the transition makes a task | The choice by field value must be made before the engine call. Options in Q3 |
| C4 | D33: RM and RMCC email from the display name | The engine takes addresses only (`RequestInput` rule, PROJECT.md section 14). Module 10 section 4.6 finds recipients by LAN ID and stops the action when a user is not found | Adapter: recipient source "display name field" -> `CP_Atlas_User.DisplayName` -> `Email`; many names with `;`. This is a second mode next to LAN ID |
| C5 | D34: no match or 2+ matches: leave out, send to the others, log a warning | Module 10: a missing recipient is a field error. Engine: a route must have one To address at least; a rule can be not required | Adapter option "optional recipient" + warning log event. The engine rule rows for RM and RMCC are `IsRequired = 0`; the lists keep one To address |
| C6 | D19: Amount as `$1,250,000.00`, date as `MM/DD/YYYY`, link to the record | The engine renders `{{token}}` from `templateData` text; it does not format | Adapter: template data from record fields with a format, and a record link token (base URL from configuration) |
| C7 | D22: any Write user does T2 | Engine transition has `AllowedRoleKey`; module 10 sends the app roles of the user (`ActorRoles: "User"`) | Adapter (part of C1) + engine role rows (A14) |
| C8 | D13: Status values on the dashboard | Engine state keys have no spaces (`FundsReceived`) | Adapter writes `StatusField` with the label from the binding (module 10 section 4.3 step 5) |

## 5. Notes (no question)

- N1. `CFP.Inflow` person columns hold LAN IDs and the dates are UTC (platform rule). The target team must use the same
  values when it moves the 500 old records. This goes into `INTEGRATION.md`.
- N2. `CFP.Outflow` and `CFP.OutflowAttachment` are not changed (CFPOUTFLOW is a later app).
- N3. A deleted record in state Submitted keeps an active engine instance (no cancel action, D15). No email goes out,
  because no SLA exists. The design records this as accepted.
- N4. The target `package.json` changes for CKEditor (new version). This is a target file change; it was approved with
  the CKEditor decision and becomes an integration step.
- N5. The account list sends all not-deleted AIN accounts with each form load, as the TTDV2 account list does today.
- N6. Engine self-transition (same from and to state) must be proved with a test before the design uses it (Q2 option A).

## 6. Questions that block the design

One question at a time. The order is the order of effect on the design.

| # | Question | Options (first = recommended) |
|---|---|---|
| Q1 | Stop-and-ask: do you approve the build of the engine adapter (module 10) for section forms in P2? The engine code does not change. The platform gets a project reference to the engine, and the local database gets the engine tables. | A yes. B no (P2 then has no engine: state and emails are not possible in a generic way). |
| Q2 | Two emails at T1. The engine sends one email for each transition. | A: no engine change. The binding gives a list of engine actions for one button; the adapter runs `Submit` (email E1) and then a second action that keeps the state (email E2). Two engine commits; if the second fails, the record is saved and the log has an error. B: engine change: many routes for one transition (new table, engine release at the target). C: one email to all. |
| Q3 | Where does the list for each Investment Type live? | A: in the platform: a binding map (field value -> list keys) and the addresses in app settings for each environment; the adapter sends them as `recipientInputs`. B: in the engine: 3 actions and 3 routes for each email (same text 3 times), addresses as static rules. |
| Q4 | Attachment table. | A: keep `CFP.InflowAttachment` (D2) and add nullable columns (`ContentType`, `SizeBytes`, LAN ID columns); the manifest maps `InflowId` = record and `Type` = control name. B: new `CFP.Attachment` with the module 8 names; `CFP.InflowAttachment` stays unused. |
| Q5 | Stop-and-ask: change `Description` and `Comment` from `varchar(max)` to `nvarchar(max)` (type change of 2 existing columns; the data stays)? | A yes. B no (characters outside the code page are lost). |
| Q6 | Server-side HTML clean-up for `richtext`. | A: own small allow-list code (no new package; tags and attributes that CKEditor makes without pictures). B: NuGet package `HtmlSanitizer` (new package, target approval). |
| Q7 | `--atlas-check` and the AIN name report. The gate demands 0 warnings. | A: new severity "Info": listed, not counted, exit 0. B: warnings, and the local sample data has only clean names. C: a switch `--atlas-check-data` for data reports. |
| Q8 | RM and RMCC for E3 (T2 can be days after T1). | A: the values of T1: `RelationshipManager` and a new column `RMCC` in `CFP.Inflow`, filled from the account. B: read the AIN row again at T2. |
| Q9 | Not blocking: export. No export code was found in the target dashboard copy. Which export does the target use? | A: the ag-grid CSV export, one button, generic. B: none in P2. |
