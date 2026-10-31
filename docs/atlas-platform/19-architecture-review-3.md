# Architecture review 3: the complete atlas-platform solution before the move to the target

Status: Review 3, 2026-10-31. Fix wave 4 done (module 20). 43 findings: 3 High, 15 Medium, 25 Low. No code changed. Verdict: not ready for the move today; 5 items block the dev integration, about one day of work.

## 1. Purpose and method

This review examines the complete solution one more time before the one-time integration in the target dev region. Review 1 (module 13) found 47 findings; review 2 (module 16) found 11. Both are fixed or answered. This review does not repeat them. Where an earlier fix is not complete in the code, this review says so.

Method:

- Four independent readers, one for each area: Service (`BBH.CP.Atlas.Service/Platform`, `Application`); Data, App, Config and SQL; the Angular `WebUi/src/app/platform`; the integration seam (`INTEGRATION.md`, the target package, the hand edits) against the real target copies in `target-implementation-read-only/`.
- Each reader read every file of its area in full, read reviews 13 and 16 first, and gave file and line for each finding.
- The author of this report opened each High and Medium finding at the cited line and confirmed it. Line numbers refer to the code at commit `00193b7`.
- Regression at the start of the review: `dotnet build` 0 warnings; `dotnet test` 277 passed; `ng build` 0 warnings; `ng test` 66 passed (vitest).
- Authentication is out of scope (IIS Windows authentication at the target) and is not a finding.

Severity: High = blocks the move or breaks a promise that the design makes (all apps stay up; a hidden field stays hidden). Medium = a defect that a user or an operator meets, or a hand edit that cannot be made as written. Low = quality, consistency, or a latent defect with no path today.

## 2. Verdict

The platform is sound in its core: parameterized SQL, one transaction for record and audit, rowversion concurrency, server-side rules and effects, IsOwner from the stored record, no XSS surface, DTO partials with no collision, a route that cannot shadow a target route. Sections 3 and 7 list what was checked and found correct.

The move cannot start today as the code is. Five items block the dev integration:

| # | Blocker | Why it blocks | Effort |
|---|---|---|---|
| RV-01 | Every platform API URL becomes `/v1/v1/...` at the target | All TTDV2 calls give 404 on the first smoke test | 15 minutes |
| RV-11 | The `CPAtlasService` hand edit needs two signature changes that INTEGRATION.md does not list; the dashboard hand edit is under-specified | The integrator cannot compile the PLATFORM lines as written; this is the AR-03 risk | Half a day (documents and reference diffs) |
| RV-08 | The role view can return NULL `RoleName` or `ApplicationCode`; the reader throws | One bad entitlement row at the target locks a user out of all platform apps | 15 minutes |
| RV-09 | Script 050 maps TTD roles by name and revokes all TTD roles with no name check; 040 ignores a soft-deleted app row | A name mismatch at the target removes TTD users from both apps | 1 hour |
| RV-10 | No rollback script for the rows that 040 and 050 write | After a branch revert the TTDV2 app row stays in the menu for every former TTD user, and the reverted controller rejects the code | 1 hour |

RV-12 (a disabled app gives 500, not the documented 404) should go in the same wave because it is the rollback switch.

The three High findings that are not blockers today (RV-02 hidden-field leak, RV-03 one bad table takes all platform apps down, RV-04 LogOnly Create) have no path in TTDV2 as configured. They break the platform promise for app #2 and must be fixed before the second app. RV-02 is cheap and should go in the same wave.

Recommendation: fix wave 4 (section 9, group A) today, run the regression and rebuild the target package, then move. Group B before app #2. Group C is backlog.

## 3. Checked and found correct

| Area | Checked | Evidence |
|---|---|---|
| SQL injection | Table and column names come only from `sys.columns` metadata and are bracket-quoted; values and `TOP` are parameters; no procedure name comes from config or request | `AtlasTabQueryBuilder.cs:74-75, 102, 107, 111-132, 166`; `AtlasUserContext.cs:69-77, 116-120, 143-158`; `AtlasStartupCheck.cs:277-281` |
| Save transaction | Record and audit row in one transaction, one commit; rollback on concurrency and update errors; `ChangeTracker.Clear()` in `finally`; `OnAfterSave` wrapped | `ConfigSectionService.cs:540-574, 386-394, 702` |
| Concurrency | Rowversion found by type and mapped with `IsRowVersion()`; `VersionRequired` when missing; a malformed token gives Conflict, not 500 | `ConfigSectionService.cs:306-315, 375-379, 672`; `AtlasPlatformContext.cs:56-57` |
| IsOwner | Rules and `Demand` always receive the stored record, never client values; `OwnerFields` lookup is case-insensitive | `ConfigSectionService.cs:242, 256, 595, 626`; `AtlasAccessService.cs:86-94` |
| Earlier fixes in code | SV-01, SV-03, SV-04, SV-05, SV-06, SV-09, SV-10, DA-02, DA-03, HO-09, AR-01 default, AR-02 guard, NG-02, NG-03, NG-04, NG-07, NG-08, NG-10, NG-11, NG-12 | See the readers' lists; two residuals are RV-05 (SV-02) and RV-L06 (DA-06) |
| DI lifetimes | `IAtlasModuleResolver` scoped; singletons hold no scoped service; no `DateTime.UtcNow` in the Service project; `TimeProvider` from the host is respected | `AtlasPlatformServiceCollectionExtensions.cs:24, 35, 68` |
| Legacy isolation | The exception filter returns for any exception that is not `AtlasProblemException`; the resolver short-circuits on registered codes before the config store; the menu guard returns the procedure rows on any error | `AtlasProblemExceptionFilter.cs:19`; `AtlasModuleResolver.cs:53`; `AtlasApplicationMenu.cs:23-32` |
| Path traversal | Form path resolved with `GetFullPath` and compared with the folder plus a separator | `AtlasConfigStore.cs:165-175` |
| Culture | All parsing and formatting with `InvariantCulture` | `AtlasValueConverter.cs`; `AtlasFieldValidator.cs:42`; `AtlasDefaultTokens.cs:33` |
| XSS | No `innerHTML`, no `bypassSecurityTrust`; ag-grid cells built with DOM methods; JSON classes go only into `class` attributes | `atlas-dashboard-columns.ts:82-85, 100-113`; `atlas-form.component.html:36-41` |
| API contract | GET responses wrapped `{ result }` as the target controller does; `ManagePage` sends `FormData` key `input`; PascalCase DTO names match | `atlas-api.service.ts:23-50`; `CPAtlasController.cs.txt:40-70, 101-127` |
| Client to server | Hidden fields and sections are not validated or sent; the server applies Show, Hide, Enable, Disable, Set, Clear again; Delete and History hidden on the client are enforced on the server | `atlas-form-model.ts:107-118, 163-170`; `AtlasServerEffects.cs:26-38`; `AtlasDashboardService.cs:183-186` |
| Styles | Scoped under `atlas-form` and `atlas-dashboard` hosts and `.atlas-*` classes; `.btn`, `h1`, `.nav-tabs` are not redefined | `atlas-platform.scss:11, 130, 206, 303-354` |
| Routes | `form/:code/:id` is lazy and cannot shadow `dashboard/:code`, `sectionmaster/...`, `stepmaster/...`, `page/...` | `atlas-platform.routes.ts:5-11`; target `app.routes.ts.txt:8-38` |
| DTO partials | Only new names are added; same namespace and assembly; the 12 classes are in the partial list | `CPPlatformDto.cs:8-142`; `INTEGRATION.md:24-26` |
| Service seam | Base class, abstract and virtual members, interfaces and `IUserInfo` match the target; the constructor append keeps all call sites | `CPApplicationService.cs.txt:30-49, 529, 587, 630`; `CPAtlasService.cs.txt:29-32` |
| DI at the target | `TimeProvider` registered by the host is kept (`TryAddSingleton`); `AddProblemDetails` exists; `AtlasPlatformContext` reuses the `CPSharepointContext` connection | `program.cs.txt:29, 72`; `AtlasDataExtensions.cs:19-20` |
| SQL scripts | 050 is one transaction with `XACT_ABORT ON` and rollback in CATCH; 040 and 050 do not modify TTD tables; indexes only on the new empty tables; view column names match the real tables | `050:24, 128-199`; `040:96-106, 121-123`; `030:18-37` |
| Config hygiene | No secret in `appsettings*.json` (Integrated Security); stand-in overrides equal the manifest values | `appsettings.Development.json:3`; `TTDV2.app.json:13, 18` |

## 4. High findings

### RV-01 Every platform API URL becomes `/v1/v1/...` at the target (High, integration)

Evidence:

- `atlas-platform/WebUi/src/app/platform/atlas-api.service.ts:23, 28, 34, 40, 45, 50`: `'v1/Application'`, `'v1/Dashboard'`, `'v1/SectionPage'`, `'v1/ManagePage'`, `'v1/RemovePage'`, `'v1/GetAuditLog'`.
- Target `WebUi/WebUi/Source/Environments/environment.ts.txt:3`: `apiBaseUrl: '/v1/'`. Target `Shared/Interceptors/apiinterceptor.ts.txt:5`: `const apiUrl = environment.apiBaseUrl + req.url;`. The interceptor is global.
- The target service uses bare names: `cp-atlas-service.ts.txt:43` `this.http.get('Application')`.
- The local environment masks the defect: `atlas-platform/WebUi/src/environments/environment.ts:5` `apiBaseUrl: 'http://localhost:5080/'`.

Why it matters: after the copy, the TTDV2 form, the dashboard rows, save, delete and history all give 404. No local test and no build finds it. `INTEGRATION.md` does not mention it.

Fix: remove the `v1/` prefix in `atlas-api.service.ts` and set the stand-in environment to `http://localhost:5080/v1/`. Add a spec that asserts the request URL has no `v1/` prefix.

### RV-02 A field that a rule hides is hidden on the client only; the server sends its value in three places (High, authorization)

Evidence:

- `ConfigSectionService.cs:148` `BindValues(fields, table, record);` runs before `:153` `Services.Rules.ApplyToSection(...)`.
- `AtlasRuleEngine.cs:116` `control.Visible = control.Visible && result.Visible;` and `:127` `child.Visible = visible;` set only the flag. `Control.Value` and the grid rows stay.
- `ConfigSectionService.cs:398-400`: `result.Outputs = [.. fields.All.Where(x => table.HasColumn(x.Field))...]` returns every column after a save.
- `ConfigSectionService.cs:519-530`: `Changes(...)` records old and new values of every field; only `field.Control.Audit == false` masks (`:529`). History (`:621-628`) shows them.
- Test `Ttdv2SectionTests.cs:517-543` asserts `Visible == false` only.

Why it matters: `HideControls` by role is an authorization statement in the config model. SV-03 closed the write side. The read side still sends the value in the LoadPage JSON, in the SavePage `Outputs` and in History. TTDV2 hides nothing by role, so it is not affected today. App #2 with `{ Roles: [Prepare], HideControls: [Salary] }` sends Salary to Prepare through three endpoints.

Fix: after `ApplyToSection`, clear `Control.Value`, `Control.Options` and the grid rows for each control with `Visible == false`; filter `result.Outputs` to visible fields; in `GetAuditLog`, apply the reader's rule result and write `***` for a hidden field. Add one test that asserts the value is absent in all three places.

### RV-03 One bad table disables one app, but its table stays in the shared EF model and every platform app gives 500 (High, data and start-up)

Evidence:

- `AtlasColumnMetadataReader.cs:86` `registry.Add(table);` adds the table before the check decides.
- `AtlasPlatformContext.cs:34` `foreach (AtlasTable table in Registry.Tables)` and `:45-47` `?? throw new InvalidOperationException($"Column '{table.FullName}.{column.Name}' has the type ... does not support.")`.
- `AtlasStartupCheck.cs:352-353` `if (items.Any(x => x.Severity == AtlasCheckSeverity.Error)) return;` skips the model build when per-app errors exist.
- `AtlasTableRegistry.cs` has no Remove method.

Why it matters: with the default `DisableModule` (AR-01), an app whose table has an unsupported column type is disabled, but the one EF model is built on the first `Records()` call of a healthy app, `OnModelCreating` throws for the bad table, and every platform app returns 500. HO-04 and AR-01 say "only the bad apps are off". For this error class the code does not keep that promise. TTDV2 alone is not affected; app #2 with one `sql_variant` or `geography` column takes TTDV2 down.

Fix: in `OnModelCreating`, skip a table that fails the check (or map it without the bad column), or let `DisableApps` remove the tables of disabled apps from the registry. Run `CheckModel` also when per-app errors exist. Add a test with two apps, one bad table.

## 5. Medium findings

### Authorization and the save pipeline

### RV-04 In LogOnly, a user with no Create permission or no role can insert a record (Medium, authorization)

Evidence: `AtlasAccessService.cs:67` `if (manifest.AccessMode == AtlasAccessMode.Enforce || operation == AtlasOperation.Delete)`: only Delete is always enforced. `ConfigSectionService.cs:284` required validators run for `x.Visible && !x.Disabled` only; `:328-336` Defaults are mapped for every field. `TTDV2.app.json:13` `"AccessMode": "LogOnly"`; `040-ttdv2-app.sql:30-49` every business column is NULL.

Why it matters: a `@Read` user, or a user with no TTDV2 role, posts `ID = 0` with no outputs. Create is denied but only logged; the rules disable every field, so nothing is validated; Defaults set TaxYear, WeekEnding and `TaxPreparerLanId`; the insert succeeds. The result is junk rows and audit noise in the app that ships in LogOnly. SV-02 covered Delete; Create was not examined.

Fix: treat Create as Delete in `Demand` (always 403), or refuse a create when the effective section has no enabled field. Add the test "LogOnly: Read user posts ID 0 with no outputs; no row".

### RV-05 A missing Permissions key fails open; the start-up check requires only the block (Medium, authorization; SV-02 residual)

Evidence: `AtlasAccessService.cs:45` `permissions?.View == null || ...`; `:51-52` same for Create and Delete; `:107-108` `if (edit == null) return true;`. `AtlasStartupCheck.cs:144-145` checks `form.Permissions == null` only.

Why it matters: `"Permissions": { "View": ["@Read"] }` passes `--atlas-check` and gives Create, Edit of all records and Delete to every role, `@Read` included. Delete destroys data.

Fix: a missing key for Create, Edit or Delete means "nobody", or the start-up check gives an error for each missing key. Record the choice in spec 06.

### Start-up and isolation

### RV-06 Per-app faults escape the per-manifest loop and disable every platform app; `--atlas-check` crashes on them (Medium, start-up)

Evidence: `AtlasTable.cs:78-79` throws `ArgumentException` for a `Table` such as `"General"` (no schema), reached from `AtlasColumnMetadataReader.cs:28`. `AtlasStartupCheck.cs:187` `GetRequiredKeyedService<IAtlasModule>(manifest.Code)` has no try. `AtlasPlatformStartup.cs:53-56` the catch-all gives `new(AtlasCheckSeverity.Error, "*", ex.Message)`, which disables all apps. `:124` `catch (Exception ex) when (ex is SqlException or InvalidOperationException)` does not catch `ArgumentException`, so the check command ends with a stack trace.

Why it matters: app #17 with a typo in `Table` takes apps #1 to #16 off at start-up, and the release gate prints a stack trace instead of the named error.

Fix: validate the `Schema.Name` form of `Table`, `AuditTable` and `AttachmentTable` for each manifest before `LoadAsync` (per-app error); wrap the module resolution per app; widen the catch at `:124` to `Exception` except `OperationCanceledException`.

### RV-07 The DisableModule guard does not cover `DisableApps`, and there is no recovery after a transient SQL failure (Medium, start-up; AR-01 completeness)

Evidence: `AtlasPlatformStartup.cs:52-56` the catch wraps `LoadAndCheckAsync` only; `:67` `DisableApps(services, errors)` runs after it and `:99` calls `services.GetRequiredService<IAtlasConfigStore>().ConfigErrors`, which lists the folder (`AtlasConfigStore.cs:88-95`, outside the store's own try). `:190` `StartAsync(...) => services.StartAtlasPlatformAsync(...)`: an exception here stops the host. `:68` `registry.MarkLoaded();` after a `"*"` SQL failure; `AtlasDisabledApps.cs:17` "A restart after the fix makes them available again."

Why it matters: if the config folder cannot be listed (an ACL change), the same call fails twice and the exception leaves the hosted service, which is the case AR-01 was meant to close. A failover during an IIS recycle gives 404 for every platform app until the next recycle, while the current apps recover on the next request.

Fix: in the catch, disable all codes from `GetServices<AtlasModuleRegistration>()` without the config store; for the `"*"` SQL case do not `MarkLoaded` and let the resolver run one gated retry after a back-off, or expose a health-check item.

### SQL scripts and the shared tables

### RV-08 The role view can return NULL `RoleName` or `ApplicationCode`; the reader throws and locks the user out of all platform apps (Medium, SQL and security)

Evidence: `030-active-user-roles-view.sql:23` `a.Code AS ApplicationCode,` and `:26` `e.Name AS RoleName,`; the WHERE clause (`:34-37`) filters `IsDeleted` only. `AtlasUserContext.cs:75` `reader.GetString(0)` and `reader.GetString(2)`. Both columns are nullable in the target shape (`local/sql/000-cp-atlas-core-tables.sql:11, 25`).

Why it matters: the target owns the `CP_Atlas_*` data. One row with a NULL `Code` or `Name` gives `SqlNullValueException` on every platform request of each user who holds that row. The current apps use `Get_Application`, which tolerates it.

Fix: add `AND a.Code IS NOT NULL AND e.Name IS NOT NULL` to the view, and read with `NullableString` and skip NULL rows.

### RV-09 Script 050 maps TTD roles by name and revokes all TTD roles with no name check; 040 ignores a soft-deleted app row (Medium, SQL; touches TTD users)

Evidence: `050-ttdv2-copy-from-ttd.sql:138` `INNER JOIN dbo.CP_Atlas_Entitlement AS e2 ON ... AND e2.Name = e1.Name`; `:147` `UPDATE ue SET ue.IsDeleted = 1 ...` for all active TTD roles. The TTDV2 roles are `Read`, `Prepare`, `Owner` (`040:137-139`); the TTD role names at the target are not in this repository. `040:128` `IF NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_Application WHERE Code = 'TTDV2')` and `:132` have no `IsDeleted` filter; `AtlasStartupCheck.cs:371` takes `x.First()` of duplicate codes.

Why it matters: a TTD role named `Reader` gets no TTDV2 role and is still revoked, so the user loses both apps with no message. A soft-deleted TTDV2 row in dev blocks the insert and attaches the entitlements to the deleted row.

Fix: in 050 Part 1 add a `#Problem` row for each active TTD role name with no TTDV2 role of the same name, so the script stops before Part 2. In 040 filter `ISNULL(IsDeleted, 0) = 0` and `THROW` on a deleted or duplicate row.

### RV-10 No rollback script for the rows that 040 and 050 write (Medium, rollback)

Evidence: `040:128-129` inserts the `TTDV2` row in `CP_Atlas_Application`; `:141` inserts the entitlements; `050:145-147` soft-deletes all TTD user entitlements with only a comment as rollback. Report 18 (`18-whole-redesign-integration.md:294`) gives "revert the branch" as the second rollback.

Why it matters: after a branch revert the app row stays; `dbo.Get_Application` returns `TTDV2` to every user with a TTDV2 role (after 050: all former TTD users); the menu shows `dashboard/TTDV2`; the reverted controller `LoadDashboard(CPApplicationCode code)` rejects the value. TTD users have no roles until a person writes the UPDATE by hand.

Fix: add `045-ttdv2-rollback.sql` (soft-delete the TTDV2 app row and entitlements; restore the TTD `CP_Atlas_UserEntitlement` rows by `ModifiedBy` and `ModifiedDate`) and name it in `sql/release-p1/README.md` and INTEGRATION.md section 8.

### The integration seam

### RV-11 The `CPAtlasService` and dashboard hand edits are under-specified against the real target files (Medium, integration; AR-03)

Evidence:

- Target `Service/CPAtlasService.cs.txt:69` `LoadDashboard(CPApplicationCode Code)`, `:100` `RemovePage(CPApplicationCode code, int id)`, `:127` `LoadApplicationService(CPApplicationCode code)`. The reference `StandIn/CPAtlasService.cs:37, 66` take `string code`. `INTEGRATION.md:28` says only "copy the lines marked PLATFORM; the `if` chains stay". The `Enum.Parse` change is listed for the controller only (`:29`).
- Target `cp-atlas-dashboard.ts.txt:109` `onClick(controlName: string)` is called from `cp-atlas-dashboard.html.txt:12` `(click)="onClick(btn.Name)"`, so `button.Target` (`INTEGRATION.md:31`) needs a template change too. `editRow` (`:316`) uses `this.applicationCode`, which `setGridData` overwrites with `tab?.Code` (`:135`). `formTypeOf(apps, code)` needs the `v1/Application` rows; the dashboard has no `apps` field and the service exposes them as a stream only (`cp-atlas-service.ts.txt:18` `cpApplications$`).

Why it matters: the PLATFORM lines do not compile against the enum parameters. The integrator must change two public signatures and add `Enum.Parse` after each PLATFORM line with no guidance. Hand edit 8 touches `onClick`, `editRow`, the template, a new `apps` subscription and `GenerateColumns`, with no reference file and no spec. This is the "error in an existing if chain" risk that AR-03 names.

Fix: list the two signature changes and the `Enum.Parse<CPApplicationCode>(AtlasAppCode.FromRequest(code), true)` lines in the `CPAtlasService.cs` row; ship a full diff of `CPAtlasService.cs` and of `cp-atlas-dashboard.ts` in `reference\`; add a spec that runs the edited target dashboard with one legacy tab and one `Columns` tab.

### RV-12 A disabled platform app gives 500 at the target, not the documented 404 (Medium, rollback)

Evidence: `StandIn/CPAtlasService.cs:101-104` `Platform()` returns null when `IsPlatformApp` is false; `AtlasModuleResolver.cs:53` is false for `Enabled: false`. The stand-in then throws `NotFound` (`:108-111`), but at the target the legacy chain runs `Enum.Parse<CPApplicationCode>("TTDV2")` (`CPAtlasService.cs.txt:114` pattern), which throws `ArgumentException`; `Middlewares/GlobalExceptionHandler.cs.txt:12-16` logs it as 500. `INTEGRATION.md:48-49` promises 404.

Why it matters: with `TTDV2:Enabled=false` (the rollback switch), a bookmarked `dashboard/TTDV2` or `form/TTDV2/5` logs an unhandled exception on every hit; the menu guard is the only thing that hides it.

Fix: after the PLATFORM line use `Enum.TryParse` and throw `AtlasProblemException.NotFound` when it fails and `configStore.FindManifest(code) != null`. Add an HTTP test for a disabled app.

### RV-13 The host target framework is not verified, and the package writes the WebUi files to a folder that does not exist at the target (Medium, build and package)

Evidence: `Atlasapp/Atlasapp/BBH.CP.Atlas.App.csproj.txt:4` `net9.0`, `:11` EF SqlServer `9.0.14`; `Service/BBH.CP.Atlas.Service.csproj.txt:4` `net9.0`; `Data/.../BBH.CP.Atlas.Data.csproj.txt:4` `net10.0`, `:11` EF `10.0.13`. Platform `Directory.Build.props:4` `net10.0`, `:5` `LangVersion latest`. `INTEGRATION.md:76` checks "EF Core 10.0.13" only. `make-target-package.py:29` `'copy/WebUi/Source/App/platform/'` and every COPY header say `WebUi/Source/App/platform/`; the real target has `angular.json.txt:14` `"sourceRoot": "src"` and `INTEGRATION.md:32` itself says `src/app/platform/atlas-platform.scss`.

Why it matters: a net9.0 project cannot reference a net10.0 Data project, so the copy is inconsistent and the real framework is unknown. Platform code compiles under the host's framework and language version, not the platform's. `Source/App` is a capitalisation artefact of the copy; the integrator gets two different paths in one document.

Fix: add "confirm App and Service TFM = net10.0, or build the Platform folders under net9.0 once locally" to INTEGRATION.md section 5; state the minimum LangVersion. Package to `copy/WebUi/src/app/platform/` and use `src/app/...` in INTEGRATION.md and the COPY headers.

### Angular form and dashboard

### RV-14 Form load, save and remove responses are not tied to the component lifetime; a late response on a destroyed form throws NG0911 (Medium, Angular)

Evidence: `atlas-form.component.ts:58-62` the `paramMap` subscription calls `this.load()` (no `switchMap`); `:69` `this.api.section(...).subscribe({` has no `takeUntilDestroyed`; `:107, :117` `takeUntilDestroyed(this.destroyRef)` runs inside `build()`, which runs from that response; `:240-261` (save) and `:266-276` (remove) are not guarded. Target `CustomRouteReuseStrategy.ts.txt:34-45` recreates the component on a parameter change.

Why it matters: if the user clicks Close while `v1/SectionPage` is in flight, `build()` runs on a destroyed component and `takeUntilDestroyed` on a destroyed `DestroyRef` throws `NG0911` into the global error handler. Locally (`app.config.ts:16` `onSameUrlNavigation: 'reload'`) a slow response for record A can overwrite the form of record B, so a save sends B's ID with A's values. Module 13 section 11 says "an old request cannot overwrite a newer one"; that holds at the target only.

Fix: `paramMap.pipe(map(...), switchMap(({ code, id }) => this.api.section(code, id).pipe(catchError(...))), takeUntilDestroyed(this.destroyRef))`; pipe save and remove through `takeUntilDestroyed` as well. Add a spec that destroys the fixture before the response is flushed.

### RV-15 Effect `Set`, `Clear` and `SetFromOption` re-enter `valueChanges` with no cycle guard, and the lint does not check for a cycle (Medium, Angular and config)

Evidence: `atlas-form.component.ts:165` `this.form.get(name)?.setValue(...)` emits by default; `:107` each control with Effects subscribes to `valueChanges` and calls `applyEffects`; `atlas-effects.ts:67-72` `Set`, `Clear` and `SetFromOption` call `target.setValue`. `AtlasConfigLint.cs:75-87` checks only that the names exist. TTDV2 uses `SetFromOption` and `Clear` (`TTDV2.json:53, 57`) with no cycle.

Why it matters: a config where A sets B and B sets A, or a control that sets itself, recurses until a stack overflow and the form page is dead. Angular emits `valueChanges` on `setValue` even when the value is unchanged. App #17 finds this at run time, not in `--atlas-check`.

Fix: in `setValue` use `{ emitEvent: false }` and run `applyEffects` for the target once with a visited set per cascade; add a lint error for a cycle in the effect graph.

### RV-16 Dashboard date columns convert a UTC date-time to the browser's local day; the form and the grid filter use the UTC day (Medium, Angular)

Evidence: `atlas-dashboard-columns.ts:24-28` `const date = new Date(text); ... date.getDate()` for any value longer than 10 characters; `:37` the filter comparator uses the `yyyy-MM-dd` prefix; `atlas-form-model.ts:48-49` reads the prefix. The server writes `datetime` columns as `yyyy-MM-dd'T'HH:mm:ss.fff'Z'` (`AtlasValueConverter.cs:103`).

Why it matters: a `datetime` column that holds a date at 00:00 UTC shows 17-Oct on the dashboard for a US user and 18-Oct in the form, and a filter for 17-Oct does not match the row shown as 17-Oct. Two values for one record on two screens is a defect that users report.

Fix: in `formatDate`, when the column type is `date`, format from the prefix (as the comparator already does); keep the local conversion for a true timestamp type only. Add `formatDate('2026-10-18T00:00:00.000Z')` to the spec.

### RV-17 The client ignores `CanEdit` and `CanCreate`: Save stays visible and all fields editable on a view-only record; the user learns at save time with a 403 (Medium, Angular)

Evidence: `atlas-form.component.ts:193-203` `visibleButtons` tests `UserPermissions?.CanDelete` only; `atlas-models.ts:98-103` has `CanCreate` and `CanEdit`; the server fills them (`ConfigSectionService.cs:162`) and enforces Edit (`AtlasAccessService.cs:53`).

Why it matters: the server check is present, so this is not a security gap. The UI shows a Save button and editable fields to a View-only user, who types for minutes and then gets "Access denied". The target section master hides what the user cannot do; the platform must match.

Fix: hide the save buttons when `id() > 0 ? !CanEdit : !CanCreate` and call `this.form.disable({ emitEvent: false })` in `build()` for that case.

### Tests

### RV-18 Critical paths with no test (Medium, tests)

Evidence (.NET): `EndpointTests.cs` runs against the stand-in `CPAtlasService` (`StandIn/CPAtlasService.cs:107-111`), so the legacy `if` chain plus the PLATFORM lines is never executed. No test loads a dashboard tab with `Procedure` on the platform path (a grep over the tests finds `Procedure` in `EndpointTests.cs:38` only), although all current tabs are procedure tabs. No test for `Enabled=false` through the host HTTP path (RV-12). No test for the `--atlas-check` argument removal in `program.cs`.

Evidence (Angular): `platform.component.spec.ts:62` sets `Version: 'AAAA'` but no spec asserts the `ManagePage` body `Version` (one hit for `Version` in both specs); no spec for a save click while `saving()` is true, an Effects `Set` chain (RV-15), Close during load (RV-14), a dashboard load error, a `date` column with a `Z` value (RV-16), or the edited target dashboard (RV-11). `platform.component.spec.ts:216-219` passes when `Zone` is undefined, so the only `OnPush` under zone test never runs locally.

Why it matters: the rowversion round trip is the concurrency contract (DA-02); a regression that drops `Version` from the body turns every update into `VersionRequired` with no red test. The two riskiest hand edits (AR-03) have only the smoke test.

Fix: assert `page.Version === 'AAAA'`; add the specs named above; add a `Procedure` tab test in `Ttdv2DashboardTests`, an HTTP test for `Enabled=false`, and a spec for the edited target dashboard; add `zone.js` as a devDependency for the zone spec.

## 6. Low findings

| ID | Finding | Evidence | Fix |
|---|---|---|---|
| RV-L01 | The platform context keeps a reference to the finished shared transaction; a second save in one scope would throw (latent; one save per request today) | `AtlasDataExtensions.cs:52-53`; `ConfigSectionService.cs:540, 571-574` | `platform.Database.UseTransaction(null)` in the `finally`; test with two saves in one scope |
| RV-L02 | A Computed field over a non-numeric source column gives a 500 on every save; the start-up check tests the column name only | `AtlasComputedFields.cs:56`; `AtlasStartupCheck.cs:174-176` | Start-up error for a non-numeric source or target; catch and return `ComputedInvalid` |
| RV-L03 | `user` controls bypass the option check; a LAN ID is never validated | `ConfigSectionService.cs:412` | Require the value in `CP_Atlas_User` or the provider list |
| RV-L04 | `AuditTime` and `SystemTime` have separate defaults; History can differ from the record by the offset | `AtlasAuditWriter.cs:60-62`; `ConfigSectionService.cs:670`; `AtlasAppManifest.cs:58, 64` | Default `AuditTime` to `SystemTime`; start-up warning when they differ |
| RV-L05 | A config-folder read failure is not isolated; every platform request gives 500 until the next refresh | `AtlasConfigStore.cs:88-95` | Wrap the scan; return the previous snapshot on error |
| RV-L06 | The option check uses the old ordinal `SameValue` (DA-06 residual) | `ConfigSectionService.cs:411, 685-686` | Use `AtlasValueConverter.SameValue(value, previous, column)` |
| RV-L07 | Display-name cache: 30 minutes, caches a miss, no size, no invalidation | `AtlasUserContext.cs:82, 96, 129` | Do not cache a miss; TTL as a setting; `entry.Size = 1` |
| RV-L08 | A default shown in a disabled field is refused if a client echoes it on create | `ConfigSectionService.cs:209-211`; `AtlasAllowList.cs:46-51` | Treat a sent value equal to the default as `Unchanged` on create |
| RV-L09 | Two manifest files with the same `Code`: the first wins silently | `AtlasConfigStore.cs:137` | Start-up error for a duplicate code |
| RV-L10 | The script log stores the module file names, not the release file names | `040:154` `'12-ttdv2-app.sql'`; `020:37`; `050:192` | Log the release file name |
| RV-L11 | Platform scripts write `GETUTCDATE()` into the shared `dbo.CP_Atlas_*` tables; the current apps write local time | `040:130, 142`; `050:133, 147`; `CPTTDService.cs.txt:134` | `GETDATE()` for rows in `dbo.CP_Atlas_*`, or state the rule |
| RV-L12 | The app code is hard-coded 8 times in the TTDV2 grid `cellRendererParams`; the lint does not see it | `TTDV2.json:168-175` | Lint rule, or the client fills it from the route |
| RV-L13 | The role query joins `CP_Atlas_User.LanID`, which has no index in the target shape (not a regression) | `000-cp-atlas-core-tables.sql:39-53`; `AtlasUserContext.cs:71-73` | Ask the target team for a filtered index |
| RV-L14 | The start-up check opens a second `SqlConnection` from the connection string | `AtlasPlatformStartup.cs:160` | Use `context.Database.GetDbConnection()` |
| RV-L15 | `AddAtlasPlatform` comment says `builder.Configuration`; INTEGRATION.md says `configuration` (the `appsettings.json`-only root) | `AtlasPlatformServiceCollectionExtensions.cs:13`; `INTEGRATION.md:27, 55-57` | Correct the comment |
| RV-L16 | The read-only copy has more divergences than the one that INTEGRATION.md lists | `INTEGRATION.md:75`; `CPAtlasController.cs.txt:31` vs `CPAtlasService.cs.txt:29`; `app.component.ts.txt:84` vs `cp-atlas-service.ts.txt:18`; `app.routes.ts.txt:37` | Add them to section 5; keep the first target build as the acceptance step |
| RV-L17 | Target TypeScript has `strictNullChecks: false`; the platform is strict | `tsconfig.json.txt:8`; platform `tsconfig.json:6` | Run `tsc` once with the target flags on the copied folder |
| RV-L18 | `CPAtlasDashboard.Code` serialises as `0` (`CDT`) for TTDV2 | `CPAtlasDashboardConfig.cs.txt:19`; `program.cs.txt:70`; `CPPlatformDto.cs:117-121` | Document it; clients use `AppCode` when present |
| RV-L19 | History fallback time `yyyy-MM-dd HH:mm:ss` goes through `DatePipe`; Safari rejects it | `atlas-history-dialog.ts:102, 36`; `AtlasAuditWriter.cs:120-123` | Parse into a local `Date` |
| RV-L20 | Reference dashboard: uncancelled `forkJoin`, plain fields `code` and `apps`, full grid rebuild after delete | `atlas-dashboard.component.ts:71-72, 86, 164` | `switchMap`; signals; remove the row without `loading` |
| RV-L21 | `markAllAsTouched()` touches hidden controls; a field shown later shows its error before input | `atlas-form.component.ts:231, 233, 185-191` | Touch shown controls only |
| RV-L22 | Radio group has no accessible group name; `aria-*` lands on the first radio only | `atlas-renderers.ts:164, 167`; `atlas-field-a11y.directive.ts:17` | `aria-labelledby` on the group; add `option` to `noInput` |
| RV-L23 | The a11y directive runs a DOM query on every change-detection cycle for every field | `atlas-field-a11y.directive.ts:16-17`; `atlas-form.component.html:42, 51` | Query once; update in an `effect()` |
| RV-L24 | The form route path is hard-coded in three places | `atlas-platform.routes.ts:7`; `atlas-navigation.ts:18`; `atlas-dashboard.component.ts:173` | One exported constant or an `ATLAS_FORM_LINK` token |
| RV-L25 | The zone-mode test passes when `zone.js` is not loaded | `platform.component.spec.ts:216-219` | Add `zone.js` as a devDependency, or mark the case pending |

## 7. Test coverage

168 .NET test methods in 20 files; 45 Angular cases in 2 specs. All pass.

| Critical path | Test |
|---|---|
| Save pipeline with permissions (Read, Prepare, Owner; Enforce) | `Sections/Ttdv2SectionTests.cs`; `Security/AccessServiceTests.cs` |
| Rowversion conflict; missing Version | `Sections/Ttdv2SectionTests.cs`; `Data/AtlasPlatformContextTests.cs` |
| Module disabled (resolver level) | `Registry/RegistryTests.cs`; `Api/EndpointTests.cs` |
| Start-up error path (DisableModule, Stop, bad setting, SQL down) | `Api/AtlasCheckTests.cs` |
| Dashboard `Source: Table` tabs, permissions, 403 | `Dashboard/Ttdv2DashboardTests.cs`; `Dashboard/TabQueryBuilderTests.cs` |
| Rules on save (role-hidden field, effects, disabled field) | `Sections/SectionStepTests.cs`; `Rules/RuleEngineTests.cs` |
| Menu guard (AR-02) | `Registry/ApplicationMenuTests.cs` |
| Validators, converter, SQL scripts | `ValidatorParityTests.cs`; `AtlasValueConverterTests.cs`; `Ttdv2AppScriptTests.cs`; `CopyScriptTests.cs`; `ReleaseScriptTests.cs` |
| Module disabled through the target `CPAtlasService` legacy fall-through (RV-12) | NONE |
| Dashboard `Procedure` tab on the platform path | NONE |
| Legacy `if` chains plus PLATFORM lines in one `CPAtlasService` (RV-11) | NONE |
| Edited target `cp-atlas-dashboard.ts` (RV-11) | NONE |
| `--atlas-check` argument removal in `program.cs` | NONE |
| Platform API URLs against the target `apiBaseUrl` (RV-01) | NONE |
| LogOnly Create by a Read user (RV-04); hidden-field value absent (RV-02); two apps with one bad table (RV-03) | NONE |

## 8. Rollback with `Enabled=false`

The resolver treats TTDV2 as not a platform app (`AtlasModuleResolver.cs:53`), the menu row is removed (`AtlasApplicationMenu.cs:42-55`), and the legacy chains run for the 4 apps. These items stay different:

- The Angular route `form/:code/:id` and `atlas-platform.scss` stay in the bundle (scoped; no effect on the existing pages).
- `AtlasProblemExceptionFilter` stays registered (no-op for other exceptions).
- The hosted start-up service still reads `sys.columns` at every start (`AtlasPlatformStartup.cs:47-57`).
- Database objects remain: `dbo.CP_Atlas_ScriptLog` (020), `dbo.vw_CP_Atlas_ActiveUserRole` (030), the `TTDV2` schema and tables, the `CP_Atlas_Application` row and 3 entitlements (040), and after 050 the soft-deleted TTD user entitlements (RV-10).
- A stale `dashboard/TTDV2` link gives 500, not 404 (RV-12).

The 4 apps run exactly as before only if hand edits 5, 6 and 8 kept the `if` chains intact; no test covers that (RV-18).

## 9. Recommended order

### Group A: before the move (today)

1. RV-01: remove the `v1/` prefix; stand-in environment `http://localhost:5080/v1/`; one spec.
2. RV-08: `IS NOT NULL` in the view; `NullableString` in the reader.
3. RV-09: name check in 050 Part 1; `IsDeleted` filter and `THROW` in 040.
4. RV-10: `045-ttdv2-rollback.sql`.
5. RV-12: `TryParse` plus `NotFound` in the reference `CPAtlasService`.
6. RV-11 and RV-13: INTEGRATION.md rows for the signature changes, the framework check and the `src/app` path; full reference diffs of `CPAtlasService.cs` and `cp-atlas-dashboard.ts`; package path.
7. RV-02: clear hidden values on load, filter `Outputs`, mask History; one test.
8. Regression; rebuild the target package; then the dev integration.

### Group B: before app #2

RV-03, RV-04, RV-05, RV-06, RV-07, RV-14, RV-15, RV-16, RV-17, RV-18, RV-L02, RV-L05, RV-L09, RV-L12.

### Group C: backlog

The remaining Low findings, with RV-L13 as a request to the target team.

## 10. References

- Module 13: architecture review 1 (47 findings, fixed in waves 1 to 3).
- Module 16: architecture review 2 (11 findings; AR-01, AR-02, AR-05 fixed).
- Report 18: whole redesign and integration (files to move, hand edits, order).
- `atlas-platform/INTEGRATION.md`; `atlas-platform/local/make-target-package.py`.
- Target copies: `target-implementation-read-only/` (Atlasapp API, Service, Data, WebUi).

## 11. Fix status

| Item | Status |
|---|---|
| RV-01 to RV-18 | Fixed in wave 4 (2026-10-31); RV-04, RV-07, RV-16, RV-17 in part. See module 20 for each verdict. |
| RV-L01 to RV-L25 | 12 fixed, 2 documented, 6 deferred, 3 not agreed, 2 for the target team. See module 20, section 5. |
