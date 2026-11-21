# Atlas platform: code trace

This file connects the platform code to the specs, the reviews and the decisions. The code comments tell what the
code does and why. They do not hold these references (rules: `AGENTS.md`, section "Code comments (platform)").

- One row for each code member and reference. A row names the file (from `atlas-platform/`) and the member.
  It does not name a line number, because line numbers change.
- `(file)` means a comment for the full file.
- Spec: a module in `docs/atlas-platform/` (for example "spec 01 §5.2" = `01-*.md`, section 5.2), or a document
  such as `INTEGRATION.md`.
- Review or decision: the ID in the review files, in `PENDING_DECISIONS.html` or in `CODEX_HANDOFF.md`.
- The test `CodeCommentRulesTests` fails when a row names a file or member that does not exist. When you rename or
  delete a member, update its rows.
- This file is not in the target package.
- Not covered: test files, and the scripts `050` and `090`. Changes to `050` and `090` are stop-and-ask, so their
  comments did not change.

| File | Member | Spec or document | Review or decision | What the code does for it |
|---|---|---|---|---|
| `WebUi/src/app/app.routes.ts` | `routes` | INTEGRATION.md | — | Uses the target dashboard path, so the form goes back to the dashboard the same way. |
| `WebUi/src/app/platform/atlas-api.service.ts` | `(file)` | spec 07 §4.4 | — | Calls the P1 endpoints that take the string app code. |
| `WebUi/src/app/platform/atlas-api.service.ts` | `AtlasApiService` | — | review 3 RV-01 | Uses URLs with no v1/ prefix; the target base URL adds it. |
| `WebUi/src/app/platform/atlas-errors.ts` | `problemText` | — | NG-12 | Keeps the error text in its own file, so chunks do not share form code. |
| `WebUi/src/app/platform/atlas-history-dialog.ts` | `(file)` | spec 09 §4.5 | — | Shows the audit history of one record from v1/GetAuditLog. |
| `WebUi/src/app/platform/atlas-history-dialog.ts` | `AtlasHistoryDialog` | — | NG-11 | Uses OnPush change detection. |
| `WebUi/src/app/platform/atlas-history-dialog.ts` | `AtlasHistoryDialog` | — | NG-10 | Uses signal inputs; a new record or a close cancels the old load. |
| `WebUi/src/app/platform/atlas-models.ts` | `CPApplicationRow` | spec 07 §4.6 | — | Has the optional fields FormType and HasDashboard. |
| `WebUi/src/app/platform/atlas-navigation.ts` | `(file)` | spec 12 question 5 | — | Navigates by FormType and Target; returns null with no FormType. |
| `WebUi/src/app/platform/atlas-navigation.ts` | `AtlasDashboardNavigation` | — | review 3 RV-11 | Opens the platform form from the target dashboard with two guarded lines. |
| `WebUi/src/app/platform/atlas-notice.service.ts` | `AtlasNoticeService` | — | review R3 | Shows save and delete messages with ngx-toastr, as the target does. |
| `WebUi/src/app/platform/atlas-pbdev.ts` | `(file)` | PBDEV.md; PBDEV.md §3 | — | Only platform import of @pbdev/pbdev-core; uses only classes the target uses. |
| `WebUi/src/app/platform/atlas-platform.routes.ts` | `atlasPlatformRoutes` | spec 12 question 5 | — | Adds the form route; the current target routes do not change. |
| `WebUi/src/app/platform/atlas-platform.scss` | `(file)` | INTEGRATION.md §2 | review R6; decision D4 | Holds the platform styles under host elements; loads after styles.scss. |
| `WebUi/src/app/platform/atlas-platform.scss` | `atlas-sr-only` | — | review R2 | Hides the text on screen and keeps it for aria-describedby. |
| `WebUi/src/app/platform/dashboard/atlas-action-menu.ts` | `(file)` | INTEGRATION.md cp-atlas-dashboard edit 4 | — | The target dashboard can give its own ActionMenuComponent to columnDefs. |
| `WebUi/src/app/platform/dashboard/atlas-dashboard-columns.ts` | `(file)` | spec 03 §4.6 | H5 | Makes columns from tab Columns; builds links with DOM methods, never HTML text. |
| `WebUi/src/app/platform/dashboard/atlas-dashboard-columns.ts` | `cellDay` | — | Review 3 RV-16 | Gives the local day of a cell, so the date filter matches the shown day. |
| `WebUi/src/app/platform/dashboard/atlas-dashboard-columns.ts` | `columnDef` | — | NG-08 | The edit link has href, so it takes the focus and opens with Enter. |
| `WebUi/src/app/platform/dashboard/atlas-dashboard-columns.ts` | `sumsRow` | spec 03 §4.6 | — | Returns the totals row for the Sum columns, or null. |
| `WebUi/src/app/platform/dashboard/atlas-dashboard-columns.ts` | `formatCurrency` | spec 25 §12 | DR-09 | Formats a currency cell as USD with a thousands separator and 2 decimals. |
| `WebUi/src/app/platform/dashboard/atlas-dashboard.component.html` | `(file)` | — | review R5 | Layout and classes match the target cp-atlas-dashboard.html. |
| `WebUi/src/app/platform/dashboard/atlas-dashboard.component.ts` | `AtlasDashboardComponent` | — | NG-11 | Uses OnPush change detection. |
| `WebUi/src/app/platform/dashboard/atlas-dashboard.component.ts` | `historyRow` | spec 03 | NG-07 | The history row uses the tab Code as its app, not always the dashboard app. |
| `WebUi/src/app/platform/dashboard/atlas-dashboard.component.ts` | `onButton` | spec 03 §4.5 | — | A header button uses its Target first, else a new record of the app. |
| `WebUi/src/app/platform/dashboard/atlas-dashboard.component.ts` | `onTabKey` | — | NG-08 | Arrow keys, Home and End move between tabs; the new tab gets the focus. |
| `WebUi/src/app/platform/form/atlas-effect-cases.ts` | `(file)` | — | FABLE section 6.1 | Shared effect and Computed cases; client and server tests check the same list. |
| `WebUi/src/app/platform/form/atlas-effects.ts` | `(file)` | spec 01 §5.1 | B35 | Client effects; the server applies them again and sets Set/Clear/SetFromOption values. |
| `WebUi/src/app/platform/form/atlas-effects.ts` | `optionData` | spec 01 §5.2 | FABLE F14 | Returns Data of the selected option; the server also reads only Data. |
| `WebUi/src/app/platform/form/atlas-effects.ts` | `applyEffects` | spec 25 §6.5 | DR-01 | A control that the user cannot change sets no values (Set, Clear, SetFromOption). |
| `WebUi/src/app/platform/form/atlas-form.component.ts` | `canChange` | spec 25 §6.5 | DR-01 | The effects of a hidden, disabled or read-only control set no values. |
| `WebUi/src/app/platform/form/atlas-field-a11y.directive.ts` | `AtlasFieldA11yDirective` | — | NG-08 | Sets the accessible name, state and error on the rendered input. |
| `WebUi/src/app/platform/form/atlas-form-model.ts` | `AtlasValueKind` | — | review R2 | Date, bool and list values convert to the server text formats. |
| `WebUi/src/app/platform/form/atlas-form-model.ts` | `isShown` | — | NG-03 | Hidden fields and fields of hidden sections do not stop the save and are not sent. |
| `WebUi/src/app/platform/form/atlas-form-model.ts` | `AtlasFormRecord` | — | NG-11 | The section form is a FormRecord because its keys are dynamic. |
| `WebUi/src/app/platform/form/atlas-form-model.ts` | `buildOutputs` | spec 01 §5.9 step 4 | — | Sends only visible, enabled fields; a SingleRowFields grid sends one JSON row. |
| `WebUi/src/app/platform/form/atlas-form.component.html` | `(file)` | — | review R3 | Layout and classes match the target cp-atlas-sectionmaster.html. |
| `WebUi/src/app/platform/form/atlas-form.component.html` | `modal-confirm-body` | — | decision D2 | The mandatory-field message shows with one Ok button. |
| `WebUi/src/app/platform/form/atlas-form.component.ts` | `AtlasFormComponent` | spec 01 §5.5; spec 12 question 5 | NG-11 | Generic form on route form/:code/:id; OnPush, state in signals. |
| `WebUi/src/app/platform/form/atlas-form.component.ts` | `AtlasFieldView` | — | FABLE F17 | The template data of a control is found one time for each section. |
| `WebUi/src/app/platform/form/atlas-form.component.ts` | `invalidOpen` | — | decision D2 | Shows the mandatory-field message of the section master. |
| `WebUi/src/app/platform/form/atlas-form.component.ts` | `code` | — | NG-11 | The route state is in signals, so OnPush and zoneless see each change. |
| `WebUi/src/app/platform/form/atlas-form.component.ts` | `ngOnInit` | — | Review 3 RV-14 | A new route cancels the old load; no response is handled after destroy. |
| `WebUi/src/app/platform/form/atlas-form.component.ts` | `build` | — | Review 3 RV-17 | Disables the form when the user cannot save. |
| `WebUi/src/app/platform/form/atlas-form.component.ts` | `disableUnsupported` | — | NG-04 | Disables controls with no renderer, so they are not validated or sent. |
| `WebUi/src/app/platform/form/atlas-form.component.ts` | `wireEffects` | spec 01 §5.1 | — | Runs the effects at build and at each source value change. |
| `WebUi/src/app/platform/form/atlas-form.component.ts` | `effectsRunning` | — | Review 3 RV-15 | Stops a cycle of effects after one pass. |
| `WebUi/src/app/platform/form/atlas-form.component.ts` | `isShown` | — | NG-03 | Returns true when the control and its section are visible. |
| `WebUi/src/app/platform/form/atlas-form.component.ts` | `canSave` | — | Review 3 RV-17 | Read-only form and no Save button when CanEdit or CanCreate is false. |
| `WebUi/src/app/platform/form/atlas-form.component.ts` | `save` | — | Review 3 RV-L21; decision D2 | Touches only shown fields; shows the mandatory-field message when one is not valid. |
| `WebUi/src/app/platform/form/atlas-form.component.ts` | `showErrors` | spec 01 §5.11 | — | Field errors show at the field; other errors show at the top. |
| `WebUi/src/app/platform/form/atlas-grid-components.ts` | `ATLAS_GRID_COMPONENTS` | — | review R4 | The grid cell components by cellRenderer name, as frameworkComponents at the target. |
| `WebUi/src/app/platform/form/atlas-renderer-registry.ts` | `AtlasRendererRegistry` | spec 01 §5.5 | — | Maps a control Type to a component; a new type needs one entry. |
| `WebUi/src/app/platform/form/atlas-renderer-registry.ts` | `noInput` | — | NG-08 | Checkbox uses aria-labelledby, so it does not get two labels. |
| `WebUi/src/app/platform/form/atlas-renderer-registry.ts` | `drawsMessages` | — | review R2; decision D2 | Returns true when the pbdev control draws its own error text. |
| `WebUi/src/app/platform/form/atlas-renderer-registry.ts` | `resolve` | — | NG-04 | A type with no renderer gets the "unsupported" note. |
| `WebUi/src/app/platform/form/atlas-renderer-registry.ts` | `provideAtlasRenderers` | — | NG-04 | Adds the renderers of an app at start-up. |
| `WebUi/src/app/platform/form/atlas-validator-cases.ts` | `(file)` | — | SV-04 | Shared validator cases; client and server tests check the same list. |
| `WebUi/src/app/platform/form/atlas-validators.ts` | `(file)` | spec 01 §10.3 | SV-04 | Client validators use the same rules and texts as the server. |
| `WebUi/src/app/platform/form/renderers/atlas-grid-renderer.ts` | `AtlasGridRenderer` | spec 01 §10.2 | review R4 | The grid control matches the target grid; one row bound to form fields. |
| `WebUi/src/app/platform/form/renderers/atlas-renderers.ts` | `(file)` | — | review R2 | pbdev controls draw their own error text with the platform texts. |
| `WebUi/src/app/platform/form/renderers/atlas-renderers.ts` | `AtlasRendererBase` | — | NG-04 | The base class that app renderers extend. |
| `WebUi/src/app/platform/form/renderers/atlas-renderers.ts` | `AtlasAmountRenderer` | — | decision D1 | A text input with no mask, because maskF is not a pbdev input. |
| `WebUi/src/app/platform/form/renderers/atlas-renderers.ts` | `AtlasNgSelectRenderer` | — | decision D3 | ng-select with label/value options and the target settings. |
| `WebUi/src/app/platform/form/renderers/atlas-renderers.ts` | `AtlasDateRenderer` | — | review R2 | The form value is a JS Date; the server gets yyyy-MM-dd. |
| `WebUi/src/app/platform/form/renderers/atlas-renderers.ts` | `AtlasUnsupportedRenderer` | — | NG-04 | Shows a note for a control type with no renderer. |
| `WebUi/src/app/stand-in/cell-editors/cell-editors.ts` | `(file)` | — | snapshot of target code as of 2026-10-27 | Look-alikes of the target cell editors, found by cellRenderer name. |
| `WebUi/src/app/stand-in/pbdev-core/index.ts` | `(file)` | spec 12 §11.4 | — | tsconfig maps @pbdev/pbdev-core to this local stand-in. |
| `WebUi/src/app/stand-in/pbdev-core/pbdev-core.ts` | `(file)` | spec 12 §11.4; INTEGRATION.md | review R1; follows real source since 2026-10-27 | Copies the real library selectors, inputs, outputs and value behaviour. |
| `WebUi/src/app/stand-in/pbdev-core/pbdev-core.ts` | `InputComponent` | — | decision D1 | Accepts the mask input but does not format; the platform uses no mask. |
| `WebUi/src/app/stand-in/shell/stand-in.interceptor.ts` | `(file)` | spec 12 §11.3 | — | Sends the act-as header; it works only with the Development API. |
| `WebUi/src/environments/environment.ts` | `environment` | — | review 3 RV-01 | The base URL gives the v1/ prefix of the platform URLs. |
| `WebUi/src/styles.scss` | `(file)` | — | review R6 | Holds the local look-alikes of the target global styles. |
| `sql/release-p1/020-script-log.sql` | `CP_Atlas_ScriptLog` | spec 11 §4.4; spec 11 question 3; module 7 §4.8 | CD-04; DA-07 | Makes the script log table; each script logs inside its main transaction. |
| `sql/release-p1/030-active-user-roles-view.sql` | `vw_CP_Atlas_ActiveUserRole` | spec 06 §4.4 | SE-03; Review 3 RV-08 | Returns roles of active rows only; no role without LAN ID, code or name. |
| `sql/release-p1/040-ttdv2-app.sql` | `(file)` | spec 12 §3, §4 | DA-08; DA-12; Review 2 AR-05; DA-07; B12 | Makes TTDV2 schema, tables, app row, roles; upgrades earlier runs in place. |
| `sql/release-p1/040-ttdv2-app.sql` | `General` | spec 05 question 3; spec 06 §4.5; spec 01 §5.9 step 9 | — | Main table has typed columns, LAN ID person columns, UTC system dates. |
| `sql/release-p1/040-ttdv2-app.sql` | `IsDeleted` | — | DA-08 (B59, approved 2026-10-25) | Alters IsDeleted to bit NOT NULL so filtered indexes can be used. |
| `sql/release-p1/040-ttdv2-app.sql` | `WorkDescription` | — | DA-12 (B60, approved 2026-10-25) | Alters free-text columns to nvarchar so Unicode text is kept. |
| `sql/release-p1/040-ttdv2-app.sql` | `IX_General_ModifiedBy` | — | Review 2 AR-05 | Adds index so the OR filter of the timesheet tab uses an index union. |
| `sql/release-p1/040-ttdv2-app.sql` | `AuditLog` | spec 05 §6.6; spec 09 §4.2; spec 12 §2.2 | — | Makes the standard audit table with CreatedByLanId; no attachment table. |
| `sql/release-p1/040-ttdv2-app.sql` | `CP_Atlas_Application` | — | Review 3 RV-09 | Stops the script on a deleted or duplicate TTDV2 application row. |
| `sql/release-p1/040-ttdv2-app.sql` | `CP_Atlas_Entitlement` | spec 01 §10.5; spec 06 §4.3; spec 05 question 4 | — | Adds Read, Prepare, Owner roles with app-level flags. |
| `src/BBH.CP.Atlas.App/Platform/AtlasConfigLint.cs` | `AtlasConfigLint` | — | SV-07; B2 | Reports unknown JSON properties as warnings and unknown names, tokens, patterns, actions as errors. |
| `src/BBH.CP.Atlas.App/Platform/AtlasConfigLint.cs` | `EffectCycles` | — | Review 3 RV-15 | Reports Set/Clear/SetFromOption effect cycles that never end in the client. |
| `src/BBH.CP.Atlas.App/Platform/AtlasPlatformServiceCollectionExtensions.cs` | `AtlasPlatformServiceCollectionExtensions` | spec 07 §4.2; INTEGRATION.md §4 | Review 3 RV-L15 | Registers platform services; program.cs passes its own configuration root. |
| `src/BBH.CP.Atlas.App/Platform/AtlasPlatformServiceCollectionExtensions.cs` | `AddAtlasPlatform` | spec 07 module 7; spec 11; spec 05; spec 06 §4.1 | HO-08; HO-01; HO-06 | Host TimeProvider has priority; settings come from the given configuration; start-up runs as hosted service. |
| `src/BBH.CP.Atlas.App/Platform/AtlasPlatformServiceCollectionExtensions.cs` | `AddAtlasModule` | spec 07 §4.2 | question 1 | Registers one app per line; class must match the manifest Service. |
| `src/BBH.CP.Atlas.App/Platform/AtlasPlatformStartup.cs` | `AtlasPlatformStartup` | spec 05 §6.2; spec 07 §4.8 | HO-06 | Loads table metadata once at start-up and runs the start-up check. |
| `src/BBH.CP.Atlas.App/Platform/AtlasPlatformStartup.cs` | `OnStartupErrorKey` | — | HO-04; review 2 AR-01 | Setting name for the start-up error mode; DisableModule is the default. |
| `src/BBH.CP.Atlas.App/Platform/AtlasPlatformStartup.cs` | `StartAtlasPlatformAsync` | spec 07 §4.8 | HO-04; AR-01 | DisableModule disables apps with errors; Stop throws; any failure keeps the host running. |
| `src/BBH.CP.Atlas.App/Platform/AtlasPlatformStartup.cs` | `OnStartupError` | — | AR-01 | A missing or bad setting gives DisableModule; a bad setting also gives an error. |
| `src/BBH.CP.Atlas.App/Platform/AtlasPlatformStartup.cs` | `DisableApps` | — | HO-02; Review 3 RV-07 | Config file errors disable only their app; a failing config store disables all apps. |
| `src/BBH.CP.Atlas.App/Platform/AtlasPlatformStartup.cs` | `RunAtlasCheckAsync` | spec 11 §4.3 | Review 3 RV-06 | Runs the checks without a web server; any failure gives a report line and exit 1. |
| `src/BBH.CP.Atlas.App/Platform/AtlasPlatformStartup.cs` | `LoadAndCheckAsync` | — | HO-05; Review 3 RV-06 | Loads tables of all registered manifests; a bad table name is an error of its app only. |
| `src/BBH.CP.Atlas.App/Platform/AtlasPlatformStartup.cs` | `AtlasPlatformStartupService` | — | HO-06 | Runs the start-up as a hosted service before the server takes requests. |
| `src/BBH.CP.Atlas.App/Platform/AtlasProblemExceptionFilter.cs` | `AtlasProblemExceptionFilter` | — | SV-11 | Builds the problem body with the host ProblemDetailsFactory, same type and traceId as target errors. |
| `src/BBH.CP.Atlas.App/Platform/AtlasStartupCheck.cs` | `AtlasStartupCheck` | spec 07 §4.8; spec 11 §4.3 | — | Checks manifests, registrations, app row, tables, fields, providers, tabs and roles at start-up. |
| `src/BBH.CP.Atlas.App/Platform/AtlasStartupCheck.cs` | `Run` | — | Review 3 RV-06 | An exception in one app check is an error of that app only. |
| `src/BBH.CP.Atlas.App/Platform/AtlasStartupCheck.cs` | `CheckApp` | — | Review 3 RV-L04; SV-07 | Warns on Local SystemTime with Utc AuditTime; runs the JSON typing checks. |
| `src/BBH.CP.Atlas.App/Platform/AtlasStartupCheck.cs` | `CheckSettings` | — | DA-11 | Warns when a taken-over current app has no SystemTime setting. |
| `src/BBH.CP.Atlas.App/Platform/AtlasStartupCheck.cs` | `CheckAppRow` | spec 07 §4.5 | — | Checks the active dbo.CP_Atlas_Application row and its parent. |
| `src/BBH.CP.Atlas.App/Platform/AtlasStartupCheck.cs` | `CheckPermissions` | — | SV-02; Review 3 RV-05 | Reports a missing Permissions block or a missing Create, Edit or Delete key. |
| `src/BBH.CP.Atlas.App/Platform/AtlasStartupCheck.cs` | `CheckSoftDelete` | — | DA-04 | SoftDelete must name the IsDeleted bit column. |
| `src/BBH.CP.Atlas.App/Platform/AtlasStartupCheck.cs` | `CheckComputed` | — | SV-08; Review 3 RV-L02 | Uses the save function table; computed sources and targets must be number columns. |
| `src/BBH.CP.Atlas.App/Platform/AtlasStartupCheck.cs` | `CheckAuditColumns` | — | B85 | Refuses an AuditTable without ReferenceID or ReferenceType, and names 045 for the old RefID and RefType. |
| `src/BBH.CP.Atlas.App/Platform/AtlasStartupCheck.cs` | `CheckSharedAuditTables` | — | DA-03 | Warns when a shared AuditTable app does not limit History to its ReferenceType values. |
| `src/BBH.CP.Atlas.App/Platform/AtlasStartupCheck.cs` | `CheckDefaults` | — | DA-05 | Warns about NOT NULL value-type columns with a default that nothing sets. |
| `src/BBH.CP.Atlas.App/Platform/AtlasStartupCheck.cs` | `CheckDashboard` | spec 03 §4.7 | Review 3 | Reports a dashboard tab with a Procedure; only Source: Table tabs run. |
| `src/BBH.CP.Atlas.App/Platform/AtlasStartupCheck.cs` | `CheckRoles` | spec 04 §4.7 | — | Warns about a role in rules or Permissions that is not a role of the app. |
| `src/BBH.CP.Atlas.App/Platform/AtlasStartupCheck.cs` | `Table` | — | DA-01 | Reports a missing or non-int-IDENTITY key column before the model build fails. |
| `src/BBH.CP.Atlas.App/Platform/AtlasStartupCheck.cs` | `CheckGridCodes` | — | Review 3 RV-L12 | Warns when a grid cellRendererParams.code is not the app code. |
| `src/BBH.CP.Atlas.App/Platform/AtlasStartupCheck.cs` | `CheckControlTypes` | — | NG-04 | Warns about control types that the client does not render. |
| `src/BBH.CP.Atlas.App/Platform/AtlasStartupCheck.cs` | `CheckModel` | — | Review 3 RV-03 | Builds the EF model also when apps have errors; a model error is an error for all apps. |
| `src/BBH.CP.Atlas.App/StandIn/Controllers/V1/CPAtlasController.cs` | `(file)` | spec 07 §4.4; INTEGRATION.md §2 | — | Target change: code parameters become string; pass resolver and config store to CPAtlasService. |
| `src/BBH.CP.Atlas.App/StandIn/Program.cs` | `(file)` | INTEGRATION.md | — | Lists the platform lines that the target program.cs adds. |
| `src/BBH.CP.Atlas.App/StandIn/StandInAuthentication.cs` | `(file)` | spec 12 §11.3 | — | Local authentication; the target uses IIS Windows authentication. |
| `src/BBH.CP.Atlas.App/StandIn/StandInAuthentication.cs` | `StandInActAsMiddleware` | spec 01 §10.5 | — | Runs a request as a test user to test the access rules, Development only. |
| `src/BBH.CP.Atlas.App/StandIn/StandInHost.cs` | `(file)` | spec 12 §11.3 | — | The local demo host for Program.cs and the endpoint tests. |
| `src/BBH.CP.Atlas.App/StandIn/StandInHost.cs` | `Create` | — | HO-10 | Fixed authentication outside Development throws. |
| `src/BBH.CP.Atlas.Data/Platform/AtlasColumnMetadataReader.cs` | `AtlasColumnMetadataReader` | spec 05 §6.2, §6.8; spec 05 question 4 | — | Reads table columns from sys.columns; needs no VIEW DEFINITION grant. |
| `src/BBH.CP.Atlas.Data/Platform/AtlasDataExtensions.cs` | `NewRecord` | — | DA-08 | Gives NOT NULL value columns the type default in a new record. |
| `src/BBH.CP.Atlas.Data/Platform/AtlasDataExtensions.cs` | `FindActive` | spec 05 §6.5 | DA-10 | Loads one active record by key; read-only paths use no tracking. |
| `src/BBH.CP.Atlas.Data/Platform/AtlasPlatformContext.cs` | `AtlasPlatformContext` | spec 12 question 1 | — | Maps each registered table as a shared-type dictionary entity. |
| `src/BBH.CP.Atlas.Data/Platform/AtlasPlatformContext.cs` | `OnModelCreating` | — | Review 3 RV-03; DA-05 | Leaves unmappable tables out of the model; lets SQL defaults apply on insert. |
| `src/BBH.CP.Atlas.Data/Platform/AtlasSqlTypes.cs` | `AtlasSqlTypes` | spec 05 §6.2, §6.4 | — | Maps SQL Server types to CLR types and EF Core store types. |
| `src/BBH.CP.Atlas.Data/Platform/AtlasTable.cs` | `AtlasColumn` | spec 05 §6.2, §6.3 | DA-05 | Describes a column; tells if the database writes it or uses its default. |
| `src/BBH.CP.Atlas.Data/Platform/AtlasTable.cs` | `UsesDatabaseDefault` | — | DA-05 | True when an insert with null must use the database default. |
| `src/BBH.CP.Atlas.Data/Platform/AtlasTable.cs` | `IsValidFullName` | — | Review 3 RV-06 | Checks the Schema.Name form so a bad name gives a check error. |
| `src/BBH.CP.Atlas.Data/Platform/AtlasTableRegistry.cs` | `AtlasTableRegistry` | spec 05 §6.2 | — | Holds the tables loaded one time at start-up from database metadata. |
| `src/BBH.CP.Atlas.Data/Platform/AtlasTableRegistry.cs` | `IsLoaded` | — | HO-06 | True after the platform start-up loaded the tables. |
| `src/BBH.CP.Atlas.Data/Platform/AtlasValueConverter.cs` | `AtlasConversionResult` | spec 01 §5.11 | — | Holds a conversion result with the field error code on failure. |
| `src/BBH.CP.Atlas.Data/Platform/AtlasValueConverter.cs` | `AtlasValueConverter` | spec 05 §6.4 | — | Converts wire text to column values with the invariant culture. |
| `src/BBH.CP.Atlas.Data/Platform/AtlasValueConverter.cs` | `ToWire` | — | DA-11 | Writes date-times with no 'Z' for an app with SystemTime Local. |
| `src/BBH.CP.Atlas.Data/Platform/AtlasValueConverter.cs` | `SameValue` | — | DA-06 | Compares two wire values by their converted column value. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `(file)` | INTEGRATION.md §2; specs 01 §5.1-5.13, §10.2, §10.5; 03 §4.4-4.7; 06 §4.3; 07 §4.4; 09 §4.4; 12 | — | Adds optional platform properties to the partial target DTO classes. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `HeaderNameWithId` | spec 01 §5.7 | — | Header text for an existing record. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `Rules` | spec 01 §10.5 | — | App-level default rules for section controls with no rules. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `OwnerFields` | spec 01 §10.5; spec 04 §4.4 | — | Fields that name the record owner by LAN ID. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `CPPermissions` | spec 06 §4.3 | — | Operation permissions by role name or flag. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `Defaults` | spec 01 §5.7, §10.2 | — | Default values for a new record or an empty value. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `Computed` | spec 01 §10.2 | — | Fields that the server calculates on each save. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `SoftDelete` | spec 01 §10.2 | — | RemovePage sets the soft-delete field to true. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `AppCode` | spec 07 §4.4 | — | The app code as text; the server prefers it over Code. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `Version` | spec 01 §5.13 | — | Base64 rowversion token that the client sends back on save. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `OptionsSource` | spec 01 §10.2 | — | Options from a registered option provider. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `Effects` | spec 01 §5.1 | — | Client effects; the server applies show, hide and require again. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `KeepSavedValue` | spec 01 §10.2 | — | Adds a saved value that is not in the options at the top. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `Audit` | spec 09 §4.2 | — | False masks the field value as *** in the audit. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `Integer` | spec 01 §10.3 | — | The value must be a whole number. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `Data` | spec 01 §5.2 | — | Structured option data for SetFromOption. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `BindMode` | spec 01 §10.2 | — | SingleRowFields binds one grid row to record fields. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `CPButtonAction` | spec 01 §5.3 | — | Button action type; release P1 supports Save, Close, Delete. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `CPButtonTarget` | spec 03 §4.5 | — | Navigation target of a dashboard button. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `Errors` | spec 01 §5.11 | — | Save errors; ID is 0 and HTTP status stays 200. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `CreatedDateUtc` | spec 09 §4.5 | — | Audit time as ISO 8601 UTC text. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `Source` | spec 09 §4.4; spec 03 §4.7 | — | Audit source App or Workflow; Table tab source builds the list query. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `MaxRows` | — | DA-09 | Upper row limit of a tab; a full tab is logged. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `CPEffectCondition` | spec 01 §5.1 | — | Effect condition; the property Equals keeps the JSON name. |
| `src/BBH.CP.Atlas.Data/Platform/Dto/CPPlatformDto.cs` | `Any` | spec 12 §3.2 | — | A filter group joined with OR, one level only. |
| `src/BBH.CP.Atlas.Data/StandIn/DTO/CPAtlasApplicationConfig.cs` | `(file)` | INTEGRATION.md | — | Stand-in copy of the target DTOs; target classes need "partial". |
| `src/BBH.CP.Atlas.Data/StandIn/DTO/CPAtlasDashboardConfig.cs` | `(file)` | INTEGRATION.md; module 4 | — | Stand-in copy of the dashboard DTOs; rules use AtlasRuleEngine. |
| `src/BBH.CP.Atlas.Data/StandIn/DTO/CPAtlasPage.cs` | `(file)` | INTEGRATION.md | — | Stand-in copy of the page DTOs; CPAtlasPage and CPAuditLog are partial. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `ConfigSectionService` | spec 01 §5.7-5.13; spec 12 | — | Generic section service; a module class adds only option providers or hooks. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `SystemColumns` | spec 01 §5.9 step 9 | — | Lists the columns that only the server writes. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `OnAfterSave` | — | SV-06 | An exception after the commit is only logged; the save returns success. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `ClearRuleHiddenValues` | — | Review 3 RV-02 | Removes value and options of rule-hidden fields from the load response. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `RuleHiddenFields` | — | Review 3 RV-02 | Returns the rule-hidden fields for the History mask. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `LoadOptions` | spec 06 §4.5 | — | Shows a user field as a name; the value stays the LAN ID. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `ResolveDefault` | spec 01 §10.2 | — | Resolves the default tokens; other text is the value. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `LoadStored` | — | Review 3 RV-04; SV-02 | A denied create is always refused, also in LogOnly. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `ReadAndFilter` | — | SV-12 | Logs each refused field as a warning. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `ValidateFields` | spec 01 §10.3 | — | An empty grid day value is 0. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `CheckVersion` | — | DA-02 | Finds the rowversion column by type; RequireVersion refuses an update with no Version. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `ComputeAndSystemFields` | spec 01 §10.2 | DA-11 | Sets computed and system fields; times follow the SystemTime setting. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `Complete` | — | SV-06; Review 3 RV-02 | Logs an OnAfterSave failure; outputs leave out rule-hidden fields. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `OptionError` | — | SV-01; B10; Review 3 RV-L06; DA-06 | A changed option value must be a server option; an unchanged saved value stays valid. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `ReadOutputs` | — | SV-12 | Grid JSON that does not parse is an error. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `ReadGridRow` | — | SV-10 | Disposes the parsed JSON document. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `Map` | — | C8 | Skips a field with no column; the start-up check reports it. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `SaveWithAudit` | — | Review 3 RV-L01 | Releases the shared transaction so a second save can start a new one. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `LoadDashboard` | spec 03 §4.1-4.7 | — | Returns the dashboard of the app. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `AuditLog` | spec 01 §5.12 | — | Ignores the client audit call; the server writes the entry. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `GetAuditLog` | — | Review 3 RV-02 | Masks rule-hidden fields in the History with ***. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `Wire` | — | DA-11 | Writes column values as wire text with the SystemTime of the app. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `Failed` | — | SV-10 | Clears the change tracker so refused values cannot be saved later. |
| `src/BBH.CP.Atlas.Service/Platform/AtlasJson.cs` | `AtlasJson` | spec 11 §4.2 | — | Reads JSON with comments, trailing commas, case-insensitive names, enums by name. |
| `src/BBH.CP.Atlas.Service/Platform/AtlasLog.cs` | `AtlasLog` | INTEGRATION.md §4 | SV-12 | Holds all platform log events with stable IDs, names and templates. |
| `src/BBH.CP.Atlas.Service/Platform/AtlasPlatformServices.cs` | `Add` | module 6; module 4; module 9; module 1 §10.2 | — | Registers security, rules, audit, option provider and dashboard services. |
| `src/BBH.CP.Atlas.Service/Platform/AtlasProblemException.cs` | `AtlasProblemException` | spec 07 §4.3; spec 06 §4.3 | — | Carries 404, 400 or 403 status; the App filter makes a problem response. |
| `src/BBH.CP.Atlas.Service/Platform/AtlasProblemException.cs` | `Config` | — | SV-11 | Returns 500 for an app config defect, logged as Atlas.ConfigError. |
| `src/BBH.CP.Atlas.Service/Platform/Audit/AtlasAuditMask.cs` | `AtlasAuditMask` | — | Review 3 RV-02 | Shows *** for old and new values of fields the rules hide from the reader. |
| `src/BBH.CP.Atlas.Service/Platform/Audit/AtlasAuditWriter.cs` | `AtlasAuditWriter` | spec 09 §4.1-4.5 | — | Writes the audit entry in the save transaction; LogMessage is a JSON list. |
| `src/BBH.CP.Atlas.Service/Platform/Audit/AtlasAuditWriter.cs` | `Read` | — | DA-03 | Filters a shared audit table by ReferenceType so other apps' records are excluded. |
| `src/BBH.CP.Atlas.Service/Platform/Dashboard/AtlasDashboardService.cs` | `AtlasDashboardService` | spec 03 §4.1-4.7 | — | Checks View, applies tab rules before queries, loads rows, sets menus and add button. |
| `src/BBH.CP.Atlas.Service/Platform/Dashboard/AtlasDashboardService.cs` | `Load` | spec 03 §4.2 | SV-05 | Gives a hidden tab no rows; another app's tab needs that app enabled and viewable. |
| `src/BBH.CP.Atlas.Service/Platform/Dashboard/AtlasDashboardService.cs` | `LoadRows` | — | DA-09 | Gets the display names of all user columns with one query. |
| `src/BBH.CP.Atlas.Service/Platform/Dashboard/AtlasDashboardService.cs` | `Menus` | spec 03 §4.4 | — | Takes menus from JSON rules, else from Permissions. |
| `src/BBH.CP.Atlas.Service/Platform/Dashboard/AtlasTabQueryBuilder.cs` | `AtlasTabQueryBuilder` | spec 03 §4.7; spec 12 §3.2 | — | Builds a parameterized tab query from checked metadata names; always adds IsDeleted = 0. |
| `src/BBH.CP.Atlas.Service/Platform/Dashboard/AtlasTabQueryBuilder.cs` | `Build` | — | DA-08 | Uses "= 0" on a NOT NULL IsDeleted column so filtered indexes can seek. |
| `src/BBH.CP.Atlas.Service/Platform/Dashboard/AtlasTabQueryBuilder.cs` | `OrderBy` | — | DA-09 | Adds the key as the last order item so the order is stable. |
| `src/BBH.CP.Atlas.Service/Platform/Options/AtlasOptionProviders.cs` | `IAtlasOptionProvider` | spec 01 §10.2 | — | Defines a named option source; apps add providers in their module class. |
| `src/BBH.CP.Atlas.Service/Platform/Options/AtlasOptionProviders.cs` | `Check` | spec 25 §7.1 | — | Lets the start-up check report bad OptionsSource parameters of a provider. |
| `src/BBH.CP.Atlas.Service/Platform/Options/AtlasTableOptionProvider.cs` | `(file)` | spec 25 §7 | B3 | Reads options from a lookup table; names checked against the metadata, values as parameters. |
| `src/BBH.CP.Atlas.Service/Platform/Options/AtlasTableOptionProvider.cs` | `UserNameRows` | spec 25 §11 | B8 | Gives the display-name columns of the lookup rows for the name report. |
| `src/BBH.CP.Atlas.Service/Platform/Options/AtlasOptionProviders.cs` | `PBAccountsOptionProvider` | spec 01 §10.1; spec 01 §5.2 | — | Gives PB accounts as "number - name" with data for SetFromOption. |
| `src/BBH.CP.Atlas.Service/Platform/Options/AtlasOptionProviders.cs` | `UsersInRolesOptionProvider` | spec 06 §4.5 | — | Gives users with given app roles; value LAN ID, label display name. |
| `src/BBH.CP.Atlas.Service/Platform/Options/AtlasOptionProviders.cs` | `YearsOptionProvider` | — | FABLE F03 | Gives years from this year + After down to this year - Before. |
| `src/BBH.CP.Atlas.Service/Platform/Options/AtlasOptionProviders.cs` | `WeekEndingsOptionProvider` | — | FABLE F03 | Gives the last and next week-end days as yyyy-MM-dd. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasAppManifest.cs` | `AtlasAppManifest` | spec 07 §4.1 | — | Holds the app settings from <Code>.app.json, overridable per environment. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasAppManifest.cs` | `FormType` | spec 12 question 5 | — | FORM selects the platform form component. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasAppManifest.cs` | `AuditRefTypes` | — | DA-03 | Sets the ReferenceType values that History reads from a shared audit table. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasAppManifest.cs` | `AuditReadRefTypes` | — | DA-03 | Returns the ReferenceType filter of the History read, or null. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasAppManifest.cs` | `Service` | spec 07 §4.1 | — | Names a custom service class; null uses ConfigSectionService. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasAppManifest.cs` | `LookupTables` | spec 25 §7.1 | — | Names the tables that the Table provider of the app can read. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasAppManifest.cs` | `SystemTime` | — | DA-11 | Selects UTC or local time for system dates and wire date-times. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasAppManifest.cs` | `RequireVersion` | — | DA-02 | Requires the rowversion Version on update to prevent lost updates. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasAppManifest.cs` | `SchemaVersion` | — | SV-07 | Fails start-up when the config contract version is higher than 1. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasAppManifest.cs` | `IsValid` | spec 07 §4.4 | — | Checks the app code pattern. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasAppManifest.cs` | `FromRequest` | spec 07 §4.4 | — | Changes an old numeric app code to the enum name. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasAppManifest.cs` | `ToLegacy` | — | Review 3 RV-12 | Gives 404 for an unavailable platform app in place of Enum.Parse failure. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasApplicationMenu.cs` | `AtlasApplicationMenu` | spec 07 §4.6 | — | Adds FormType and HasDashboard to menu rows; removes unknown app rows. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasApplicationMenu.cs` | `Enrich` | — | AR-02 | Returns the procedure rows unchanged and logs Atlas.ConfigError on an exception. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasConfigStore.cs` | `IAtlasConfigStore` | spec 07 §4.7; spec 01 §5.6 | FABLE F04; HO-07; HO-02 | Reads config files one time; returns new form copies; one bad manifest disables only its app. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasConfigStore.cs` | `ConfigErrors` | — | HO-02 | Lists the manifest files that could not be read. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasConfigStore.cs` | `Build` | — | Review 3 RV-L09 | Reports a second manifest with the same Code as a config error. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasConfigStore.cs` | `Resolve` | — | HO-09 | Refuses a config file path outside the config folder. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasConfigStore.cs` | `ApplyEnvironmentOverrides` | spec 07 §4.1; spec 11 §4.5 | HO-03 | Applies environment overrides; an invalid value fails closed and is a setting error. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasDisabledApps.cs` | `AtlasStartupErrorMode` | spec 07 §4.8 | HO-04; AR-01 | Selects Stop or DisableModule (default) when the start-up check finds errors. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasDisabledApps.cs` | `AtlasDisabledApps` | — | HO-04 | Holds the apps that the start-up check disabled. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasModuleResolver.cs` | `AtlasModuleRegistration` | spec 07 §4.2 | — | Holds one AddAtlasModule line. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasModuleResolver.cs` | `IAtlasModuleResolver` | spec 07 §4.3 | HO-05 | Resolves the app service; 404 for an unknown, disabled or not-loaded app. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasPlatformConfiguration.cs` | `AtlasPlatformConfiguration` | — | HO-01 | Holds the configuration given to AddAtlasPlatform for the platform settings. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasPlatformConfiguration.cs` | `Options` | — | FABLE F05 | Reads the platform settings from the configuration. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasPlatformOptions.cs` | `AtlasPlatformOptions` | INTEGRATION.md §4 | FABLE F05; FABLE F04 | Reads the AppSettings platform settings in one place; ignores ConfigRefreshSeconds. |
| `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasPlatformOptions.cs` | `AtlasEnumText` | — | HO-03 | Accepts only enum names, not numbers, for setting text. |
| `src/BBH.CP.Atlas.Service/Platform/Rules/AtlasRuleEngine.cs` | `AtlasRuleContext` | spec 04 §4.1 | — | Holds role tokens, states and owner flag as rule inputs. |
| `src/BBH.CP.Atlas.Service/Platform/Rules/AtlasRuleEngine.cs` | `AtlasRuleOutcome` | — | FABLE F01 | Lists fields that the rules hid or disabled in a section. |
| `src/BBH.CP.Atlas.Service/Platform/Rules/AtlasRuleEngine.cs` | `AtlasRuleResult` | spec 04 §4.1 | — | Holds the rule result for one target. |
| `src/BBH.CP.Atlas.Service/Platform/Rules/AtlasRuleEngine.cs` | `AtlasRuleEngine` | spec 04 §4.1-4.3 | — | Evaluates rules with FirstMatch or MostPermissive; applies RuleDefault when none match. |
| `src/BBH.CP.Atlas.Service/Platform/Sections/AtlasAllowList.cs` | `AtlasAllowList` | spec 01 §5.9 step 4 | SV-08; DA-06; SV-01 | Ignores server-set fields; refuses changes to hidden or disabled fields with typed compare. |
| `src/BBH.CP.Atlas.Service/Platform/Sections/AtlasComputedFields.cs` | `AtlasComputedFields` | spec 01 §10.2 | SV-08 | Uses one function table for Computed fields in save and start-up check. |
| `src/BBH.CP.Atlas.Service/Platform/Sections/AtlasDefaultTokens.cs` | `AtlasDefaultTokens` | spec 01 §10.2 | SV-08 | Resolves default tokens with one list for load, save and start-up check. |
| `src/BBH.CP.Atlas.Service/Platform/Sections/AtlasFieldValidator.cs` | `AtlasFieldValidator` | spec 01 §10.3 | SV-04 | Checks a field value with the same rules and texts as the client. |
| `src/BBH.CP.Atlas.Service/Platform/Sections/AtlasFormFields.cs` | `AtlasFormField` | spec 01 §10.2 | — | Holds one form field: a control Field or a SingleRowFields grid column. |
| `src/BBH.CP.Atlas.Service/Platform/Sections/AtlasFormFields.cs` | `HiddenByRule` | — | SV-03 | Marks a field that a rule hid; an effect cannot show it again. |
| `src/BBH.CP.Atlas.Service/Platform/Sections/AtlasFormFields.cs` | `DisabledByRule` | — | SV-04 | Marks a field that a rule disabled; an effect cannot enable it. |
| `src/BBH.CP.Atlas.Service/Platform/Sections/AtlasFormFields.cs` | `ApplyOutcome` | — | SV-03, SV-04, FABLE F01 | Sets HiddenByRule and DisabledByRule from the rule outcome. |
| `src/BBH.CP.Atlas.Service/Platform/Sections/AtlasModuleServices.cs` | `AtlasModuleServices` | — | SV-13 | Bundles platform services; modules must not write app or audit tables directly. |
| `src/BBH.CP.Atlas.Service/Platform/Sections/AtlasServerEffects.cs` | `AtlasServerEffects` | spec 01 §5.2 | SV-03, SV-04 | Applies control effects on the server; rules have priority over effects. |
| `src/BBH.CP.Atlas.Service/Platform/Security/AtlasAccessService.cs` | `AtlasAccessService` | spec 06 §4.3 | SV-02; Review 3 RV-04 | Checks access per operation; LogOnly logs, Enforce gives 403; Delete always 403. |
| `src/BBH.CP.Atlas.Service/Platform/Security/AtlasAccessService.cs` | `ValueOf` | — | SV-09 | Finds an owner column value with a case-insensitive name. |
| `src/BBH.CP.Atlas.Service/Platform/Security/AtlasAccessService.cs` | `UserPermissions` | — | Review 3 RV-17 | Sets CanEdit to what the save accepts; LogOnly keeps the Save button. |
| `src/BBH.CP.Atlas.Service/Platform/Security/AtlasRoles.cs` | `AtlasRole` | spec 06 §4.4 | — | Holds one active role row from vw_CP_Atlas_ActiveUserRole. |
| `src/BBH.CP.Atlas.Service/Platform/Security/AtlasRoles.cs` | `AtlasAppRoles` | spec 01 §11.2; spec 06 question 6 | — | Holds roles of app and parent; IsAdmin includes write and read. |
| `src/BBH.CP.Atlas.Service/Platform/Security/AtlasUserContext.cs` | `IAtlasUserContext` | spec 06 §4.1 | — | Gives one user context per request keyed by LAN ID. |
| `src/BBH.CP.Atlas.Service/Platform/Security/AtlasUserContext.cs` | `IAtlasUserDirectory` | spec 06 §4.5 | DA-09 | Finds display names (batch) and users in roles of an app. |
| `src/BBH.CP.Atlas.Service/Platform/Security/AtlasUserContext.cs` | `MissTime` | — | Review 3 RV-L07 | Caches a LAN ID with no name for two minutes only. |
| `src/BBH.CP.Atlas.Service/Platform/Security/AtlasUserContext.cs` | `Entry` | — | Review 3 RV-L07 | Gives each cache entry a size so a SizeLimit cache accepts it. |
| `src/BBH.CP.Atlas.Service/Platform/Security/AtlasUserContext.cs` | `DisplayNames` | — | DA-09 | Reads missing names with one query for each 500 LAN IDs. |
| `src/BBH.CP.Atlas.Service/StandIn/CPAtlasService.cs` | `(file)` | INTEGRATION.md §2 | Review 3 RV-11; RV-12 | Target integration: optional constructor parameters, PLATFORM lines, string code, ToLegacy. |
| `src/BBH.CP.Atlas.Service/StandIn/CPAtlasService.cs` | `GetApplication` | spec 07 §4.6 | AR-02 | Adds menu data and hides unregistered apps; an error returns the rows unchanged. |
| `src/BBH.CP.Atlas.Service/StandIn/CPAtlasService.cs` | `AuditLog` | spec 01 §5.12 | — | With AuditMode Server the client audit call does nothing. |
| `src/BBH.CP.Atlas.Service/StandIn/CPAtlasService.cs` | `Legacy` | — | Review 3 RV-12 | ToLegacy gives 404 for a disabled platform app and 400 for no code. |
| `src/BBH.CP.Atlas.Service/Platform/Sections/AtlasServerEffects.cs` | `CanChange` | spec 25 §6.5 | DR-01 | Only a visible and enabled source field gives effect values; saved values stay. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `KeepOnlySavedOptions` | spec 25 §7.1 | DR-18 | A field that the user cannot change gets only its saved option, with no Data. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `MapRecord` | spec 25 §7.3 | — | Writes a SetFromOption target that is a column with no control (data-only target). |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` | `CheckOptionsSource` | spec 25 §7.1 | — | Returns the provider errors of a control for the start-up check. |
| `src/BBH.CP.Atlas.Service/Platform/Security/AtlasUserContext.cs` | `EmailOfDisplayName` | spec 25 §10.6 | D33, D34 | Finds the email of the one active user with a display name, or the reason. |
| `src/BBH.CP.Atlas.App/Platform/AtlasNameReport.cs` | `(file)` | spec 25 §11 | Q7 | Lists lookup names with no single user as Info lines (--atlas-check only). |
| `src/BBH.CP.Atlas.App/Platform/AtlasStartupCheck.cs` | `AtlasCheckSeverity` | spec 25 §11 | Q7 | Info: a report line that is not counted and does not change the exit code. |
| `src/BBH.CP.Atlas.App/Platform/AtlasConfigLint.cs` | `CheckForm` | spec 25 §7.3 | — | A SetFromOption target can be a non-system column of the main table. |
| `src/BBH.CP.Atlas.Service/Platform/Sections/AtlasServerEffects.cs` | `ApplyToControls` | spec 25 §8 | review 27 CR-02 | Applies Show, Hide, Require, Unrequire, Enable and Disable to the file pickers on the server. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.Attachments.cs` | `LoadAttachments` | spec 25 §8 | review 27 CR-02 | A file picker that the JSON hides keeps its files; only a rule removes them (load and download). |
| `src/BBH.CP.Atlas.App/Platform/AtlasConfigLint.cs` | `EnableTargets` | spec 25 §7.1 | review 27 CR-04 | Refuses an Enable effect on a control that the JSON disables, so a rule keeps its priority. |
| `src/BBH.CP.Atlas.App/Platform/AtlasConfigLint.cs` | `EngineBinding` | spec 25 §10.2 | review 27 CR-05 | A binding field must be a column, or a control with a column; the adapter reads only such a value. |
| `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.Workflow.cs` | `FieldValue` | spec 25 §10.6, §10.7 | review 27 CR-05 | Reads a binding value by column name, or through the column of the named control. |
