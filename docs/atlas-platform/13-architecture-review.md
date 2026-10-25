# Module 13: Architecture review of the atlas-platform code

Status: Review 1 (2026-10-25). Read-only review of commit `056be47`. No code changed for this review. Each finding has file:line evidence that was read and checked.

## 1. Purpose and method

This review examines the new atlas-platform code (C0 to C10 and B17) for these areas:

- Angular: the angular.dev guides and the official Angular style guide (signals, zoneless, typed forms, dynamic components, testing, accessibility).
- ASP.NET Core: the Microsoft guidance for dependency injection, options, the Generic Host, error handling (ProblemDetails), logging and performance.
- EF Core and SQL Server: the Microsoft EF Core guidance for the model, raw SQL, transactions, concurrency, generated values, query filters and performance.
- General principles: SOLID, fault isolation, fail-fast, one source of truth, least privilege, and backward compatibility.

The goal of the platform is the measure: **can app #17 go live with JSON, one SQL table and one registration line, and can the code go into the target without a break?**

Method:

1. Four parallel reviews, one for each layer (Angular, host, data, service design).
2. A check of each High finding and the key Medium findings in the code, and in `target-implementation-read-only/` for target facts.
3. A merge of duplicate findings into one item. The ID prefix shows the layer: `NG` Angular, `HO` host and registry, `SV` service design, `DA` data and SQL.

Scope: `atlas-platform/src/**/Platform`, `Service/Application/ConfigSectionService.cs`, `WebUi/src/app/platform`, `sql/release-p1`, and the tests. The `StandIn/` folders are not copied to the target. This review examines them only where they set a contract.

Out of scope: authentication (the target does it with IIS Windows authentication).

## 2. Overall assessment

The design is sound. The main structure is correct for a metadata-driven platform:

- keyed module registration;
- a property-bag EF model with a correct model-cache key;
- an access service with one decision for each operation;
- server-run rules on save;
- an allow list;
- metadata-quoted SQL;
- one transaction for the record and its audit.

The code is clean and consistently uses `TimeProvider`. The tests are behavioural and run on a real SQL Server.

The gaps are in three groups:

1. **Integration with the target (release P1 blockers).** The `@pbdev` stand-in does not have the export names of the real library. The form goes to a route that the target does not have. The copied spec file uses a test runner that the target does not have. The styles are not delivered. In this condition, the copied WebUi code does not compile at the target (NG-01, NG-02, NG-05, NG-06).
2. **Trust and fault isolation.** The server does not check that a value is one of the options (SV-01). In `LogOnly` mode a user with no role can delete any record of a new app (SV-02). One bad config file, a typo in an environment override, or an unsupported column type can stop all platform apps (HO-01, HO-02, DA-01).
3. **The "config only" promise for app #17.** Some JSON features are accepted but not done on the server, or done differently on the client: `Hide` effects, `Enable`/`Disable`/`Set`/`Clear`, `SoftDelete.Field`, unknown control types, and patterns. An app author finds these in production, not at `--atlas-check` (NG-03, NG-04, SV-04, SV-07, DA-04).

Most fixes are small and additive. None of them needs a change to a public contract.

| Severity | Count | Meaning |
|---|---|---|
| High | 9 | Stops integration, opens a security gap, or stops all platform apps. Fix before the copy to the target. |
| Medium | 24 | Wrong behaviour for app #17, a performance limit, or a missing control. Fix before the second platform app. |
| Low | 14 | Hygiene and future-proofing. |

## 3. What is done well

- **Composition root.** `AddAtlasModule<T>(code)` validates and normalizes the code and registers a keyed scoped `IAtlasModule` (`App/Platform/AtlasPlatformServiceCollectionExtensions.cs:44-55`). The resolver gives 404 for an unknown or disabled app and 400 for a missing feature (`Registry/AtlasModuleResolver.cs:44-58`). This replaces the per-app `if` chains.
- **No shared mutable config.** The config store caches only the file text and makes new objects on each call (`Registry/AtlasConfigStore.cs:50,74,84`). Test: `RegistryTests.Each_form_request_gets_its_own_copy`. Rules and effects change only per-request copies.
- **Server authority on save.** The server runs the rules again for this user and the stored record (`ConfigSectionService.cs:269`). It refuses unknown names and changed values in hidden or disabled fields (`:291-297, 419-433`). It sets system, `Computed` and `SetFromOption` columns itself (`:285, 336-368`).
- **Safe dynamic SQL.** Identifiers come only from `sys.columns` and are bracket-quoted (`Dashboard/AtlasTabQueryBuilder.cs:141-145`). Operators and sort directions come from a fixed list (`:19-22, 132-134`). Values are typed parameters (`:97-119`). Test: `A_value_with_SQL_text_stays_a_parameter`.
- **Correct EF Core model for runtime tables.** Shared-type `Dictionary<string,object?>` entities with `ReplaceService<IModelCacheKeyFactory, AtlasModelCacheKeyFactory>` keyed on a table signature (`Data/Platform/AtlasPlatformContext.cs:29,73-79`). Exact store types (`varchar(n)`, `decimal(p,s)`), so parameters have the correct facets (`AtlasSqlTypes.cs:37-49`).
- **Concurrency and audit in one transaction.** The client version is the EF original value (`ConfigSectionService.cs:386-391`). The record and the audit share one connection and one transaction (`AtlasDataExtensions.cs:44-55`, `ConfigSectionService.cs:640-679`). Test: `A_save_of_an_old_version_gives_a_concurrency_error`.
- **Act-as is safe.** It needs Development, a setting, an authenticated user and a listed LAN ID, and it keeps the real identity in a claim (`App/StandIn/StandInAuthentication.cs:40-47`). No platform code reads the header.
- **Angular structure.** Logic is in pure functions behind a narrow interface (`atlas-effects.ts:7-12`, `atlas-form-model.ts`), and these functions have tests without TestBed. The code uses standalone components, `inject()`, built-in control flow with stable `track` keys, `takeUntilDestroyed`, a lazy `loadComponent` route, strict TypeScript with `strictTemplates`, and DOM cells made with `textContent` (no `innerHTML`).
- **Backward-compatible extension points.** `formRoute()` returns `null` so the target `if` chain still runs. `register(type, component, hasInput = true)` keeps old callers. All new DTO members are optional.

## 4. Integration blockers (fix before the copy to the target)

### NG-01 The @pbdev stand-in does not match the real library, so the copied code does not compile (High, Angular)

**Evidence.** The platform imports `PbdevDialog`, `PbdevLoader`, `PbdevSectionLabel`, `PbdevSelect`, `PbdevTextarea`, `PbdevDatePicker`, `PbdevCheckbox` and `PbdevInput` (`platform/form/atlas-form.component.ts:9`, `renderers/atlas-renderers.ts:7`, `atlas-history-dialog.ts:5`, `dashboard/atlas-dashboard.component.ts:9`). The real library exports `SelectComponent`, `TextareaComponent`, `SectionComponent`, `LoaderComponent`, `DialogComponent` and others (target `cp-atlas-sectionmaster.ts.txt:6`, `app.component.ts.txt:4-11`). The target dialog uses `[(visible)]` and `[header]` (`app.component.html.txt:63`). The stand-in has `open`/`closed`. The target checkbox uses `inputLabel`. The stand-in uses `label`. `inputId` on the select, textarea, date picker and checkbox (B17) has not been checked.

**Guidance.** Spec 12 section 11.4 ("the same selectors, inputs, outputs and export names"). angular.dev strict templates: an unknown input is error NG8002.

**Impact.** The target build fails with TS2305 in four files. When the names are fixed, NG8002 errors follow. The B17 note "the form still works" is not correct: the build stops first.

**Fix.**

1. Rename the stand-in exports to the real class names.
2. Make the stand-in dialog `visible` + `visibleChange` + `header`.
3. Use `inputLabel` on the checkbox.
4. Put all `@pbdev` imports in one file, `platform/atlas-pbdev.ts`, with a table of the inputs that the platform uses. A remaining mismatch is then a one-file fix.
5. Add a row to INTEGRATION.md section 5 for each input that is not verified.

Effort S-M.

### NG-02 The form goes to a route that only the local shell has (High, Angular)

**Evidence.** `atlas-form.component.ts:203, 234, 252`: `this.router.navigate(['/apps', this.code])`. The target route is `dashboard/:code` (target `app.routes.ts.txt:10`).

**Guidance.** A reusable feature must not hard-code the routes of its host.

**Impact.** At the target, Close, Save and Delete give NG04002 "Cannot match any routes", or go to the wrong page.

**Fix.** Add an `InjectionToken<(code: string) => unknown[]>` named `ATLAS_DASHBOARD_LINK`, with the default factory `c => ['/dashboard', c]`. The stand-in `app.config` provides `['/apps', c]`. No target change is necessary. Effort S.

### NG-03 The client validates hidden fields, so a form with a Hide effect cannot save (High, Angular)

**Evidence.** `setVisible` changes only a map (`atlas-form.component.ts:123-125`). The validators stay on the control (`atlas-form-model.ts:58`). `save()` checks `this.form.invalid` for all controls (`atlas-form.component.ts:213`). `buildOutputs` checks the field visibility but not the section visibility (`atlas-form-model.ts:73`). The server validates only visible and enabled fields (`ConfigSectionService.cs:304`). It resolves an effect target as a field or as all children of a section (`:524`).

**Guidance.** angular.dev reactive forms: disabled controls are not validated and are not in `value`. One source of truth for validation.

**Impact.** App #17 with `{When:{Equals:'No'}, Hide:['Reason']}` and a required `Reason` cannot save. The user sees "Correct the marked fields." and no field is marked. With `Hide:['SectionB']`, the children are still sent, and an edited value gives `NotAllowed`. TTDV2 is not affected because it has no Show/Hide effects.

**Fix.** Resolve effect targets as the server does. Compute validity and outputs only for controls that are visible and whose section is visible. Use a pure helper `visibleInvalid(...)` in `save()`. Add tests for both cases. Effort S-M.

### NG-04 Unknown control types fail silently, and there is no supported way to add a renderer (High, Angular)

**Evidence.**

- The template shows a field only when `registry.get(child.Type)` returns a component (`atlas-form.component.html:38, 56`). `buildFormGroup` still makes a control with `Validators.required` (`atlas-form-model.ts:54-59`), and `buildOutputs` still sends it.
- The current target JSON uses `option`, `check` and `filepicker`, and none of them is registered (`atlas-renderer-registry.ts:17-29`).
- A `grid` without `BindMode: SingleRowFields` has no form group, but `AtlasGridRowRenderer` binds `[formGroup]` to `undefined` (`atlas-renderers.ts:87, 100-102`). The form crashes.
- The base class `AtlasRenderer` is not exported, `register()` takes `Type<unknown>`, and there is no provider function.

**Guidance.** angular.dev "Programmatically rendering components": NgComponentOutlet inputs are checked only at run time. The DI guide recommends a multi-provider or `InjectionToken` for plug-in registries.

**Impact.** An app with a radio field shows no field and can never save. A custom renderer with wrong input names fails only at run time. A typo in a grid config crashes the form.

**Fix.** These steps are additive:

- Export `AtlasRendererBase` with `control = input.required<...>()` and `group = input.required<FormGroup>()`.
- Add the overload `register(type, component: Type<AtlasRendererBase>, hasInput?)`.
- Add `provideAtlasRenderers(map)` (`provideEnvironmentInitializer`).
- Add an `AtlasUnsupportedRenderer` that shows "Unsupported control type X".
- Add `check` as an alias of `checkbox`, and a radio renderer for `option`.
- Add `@if (rowGroup)` in the grid renderer.
- Let `--atlas-check` warn when a type is not in a shared list.

Effort M.

### NG-05 The copied spec file uses Vitest, but the target runs Karma with Jasmine (Medium, Angular)

**Evidence.** `platform/platform.spec.ts:2` imports from `vitest`, and line 76 uses `it.each`. The target uses `@angular-devkit/build-angular:karma` with Jasmine (`angular.json.txt:88`, `package.json.txt:57-63`).

**Impact.** The spec is in `platform/`, which is copied, so the target `ng test` does not compile.

**Fix.** Use only the global `describe`/`it`/`expect` (no import) and a plain loop instead of `it.each`. Or remove the spec from the copy list. Effort S.

### NG-06 The platform styles are not delivered, and target Tailwind removes classes that are only in JSON (Medium, Angular)

**Evidence.** All `atlas-*` rules are only in the local `src/styles.css`. That file is not in the copy list (INTEGRATION.md section 1), and no platform component has `styleUrl`. `styles.css:81` defines `.btn` again, but the target gets it from Bootstrap. Target Tailwind scans only `./src/**/*.{html,ts}` (`tailwind.config.js.txt:5`). The layout classes come from server JSON (`DivClass`, `Class`).

**Guidance.** angular.dev "Styling components": a component supplies its own styles. Tailwind: dynamic class names must be in the safelist.

**Impact.** At the target the form has no style: error text is not red and the field layout breaks. App #17 with `grid-cols-3` gets no CSS.

**Fix.** Move the `atlas-*` rules to `platform/atlas-platform.css` (copied, and added to `styles`). Keep `.btn` and the other Tailwind look-alikes in a stand-in file. Add a Tailwind `safelist` pattern to the INTEGRATION.md steps. Effort S.

### HO-01 The platform reads settings from the DI configuration, not from the parameter of AddAtlasPlatform (Medium, host)

**Evidence.** `AddAtlasPlatform(configuration)` only checks the parameter for null (`AtlasPlatformServiceCollectionExtensions.cs:18-21`). `AtlasConfigStore` reads `AppSettings:ConfigFolder` and `AppSettings:Modules:*` from the injected `IConfiguration` (`AtlasConfigStore.cs:33-37, 113`). At the target, that `IConfiguration` is replaced by a root that reads only `appsettings.json` (target `program.cs.txt:18-21, 27`). It has no environment file, no environment variables and no reload. The local tests use in-memory overrides (`EndpointTests.cs:49`).

**Guidance.** Options pattern: bind from the configuration that you get. Explicit dependencies.

**Impact.** Overrides that work locally (environment variables, `appsettings.QC.json`) have no effect at the target. `--atlas-check --environment QC` reports "QC" but checks the `appsettings.json` that is next to the exe.

**Fix.** Bind `AtlasPlatformOptions` from the parameter (`AddOptions<>().Bind(...).ValidateOnStart()`), and inject `IOptionsMonitor<AtlasPlatformOptions>` into the store. Keep the old constructor. In INTEGRATION.md, write which configuration root the target must give, and check the `ConfigFolder` base path (content root against base directory). Effort S-M.

## 5. Security and data integrity

### SV-01 The server does not check that a value is one of the options (High, service)

**Evidence.** `Validate` checks only Required, length, Pattern, Integer, Min and Max (`ConfigSectionService.cs:542-572`). No step compares a value with `control.Options` or `OptionsSource`. When the option is not found, `DerivedValues` emits nothing, so the stored values stay (B10, `:509-518`). The handoff lists this as an open point.

**Guidance.** Do not trust client data. The option list is the domain of the field.

**Impact.** A direct API call can save `Category = "ANYTHING"`, a `WeekEnding` that is not a Sunday, or an `Account` that is not in the user's PB account list. It can also set `TaxPreparer` to any LAN ID. `TaxPreparerLanId` is an `OwnerField`, so this gives ownership of the record to any user. All 16 apps get the same gap.

**Fix.** In the allow-list step, for each control with `Options` or `OptionsSource`: when the value changed and is not in the server-side list, add the field error `NotInOptions`. Keep `KeepSavedValue`: an unchanged saved value stays valid. Split the value first for a multi-select `ngselect`. Add tamper tests for Category, WeekEnding and TaxPreparer. Effort S-M.

### SV-02 In LogOnly mode, a user with no role can delete any record of a new app (High, service; challenges spec 06 section 5)

**Evidence.** `AccessMode` defaults to `LogOnly` (`Registry/AtlasAppManifest.cs:44`), and `TTDV2.app.json:13` sets `"LogOnly"`. `Demand` throws only in `Enforce` mode (`Security/AtlasAccessService.cs:66`). `RemovePage` has no second guard (`ConfigSectionService.cs:692-716`). A `Permissions` entry that is not there means "allow" (`AtlasAccessService.cs:49-50`).

**Guidance.** Least privilege. Spec 06 made `LogOnly` to protect existing users during a migration. TTDV2 is a new app with no existing users, so that reason does not apply.

**Impact.** Edit stays blocked (the rules give `ReadOnly` and the allow list refuses). Delete is not blocked: any Windows user can call `RemovePage` for each ID and soft-delete all records. The delete is logged but it still occurs. `GetAuditLog` and the dashboard ("All Report" tab) are also open. App #17 gets this condition when it leaves out the keys.

**Fix.**

- In `LogOnly`, always enforce `Delete` and the decision "no role in app". Or add `LogOnlyOperations` with the default `View,History`.
- For new apps, make `Enforce` the default, or make the key mandatory.
- Make a missing `Permissions` block a start-up check error for platform apps.
- Add the test: in `LogOnly`, `RemovePage` by a user with no role gives 403.

Effort S.

### SV-03 A value-driven effect can show a field that a role rule hid, and then the field is writable (Medium, service)

**Evidence.** The rules run first (`ConfigSectionService.cs:269`). For a hidden field, the disabled value is not calculated (`AtlasRuleEngine.cs:129-130`). Then `ApplyServerEffects` sets `target.Visible = true` with no condition (`ConfigSectionService.cs:484`), from values that the client sent (`:273-274`). The allow list then accepts the field (`:291`).

**Guidance.** Least privilege: client input must not loosen a role restriction.

**Impact.** For app #17 with a rule `{Roles:[Prepare], HideControls:[Salary]}` and an effect `When Type=Employee, Show [Salary]`: a Prepare user sends `Type=Employee` and a Salary value, and the server saves it.

**Fix.** Keep `RuleVisible` and `RuleDisabled` on `AtlasFormField`. An effect can then only make the rule result narrower (`Visible = RuleVisible`). Add a test. Effort S.

### SV-04 The client and the server use different effect and validator rules (Medium, service and Angular)

**Evidence.**

- The client applies `Enable`, `Disable`, `Set`, `Clear` and `OptionsFromChild` (`atlas-effects.ts:65-68`). The server applies only Show, Hide, Require, Unrequire and `SetFromOption` (`ConfigSectionService.cs:476-488`).
- Pattern: the client `Validators.pattern(string)` adds `^...$`. The server `Regex.IsMatch` (`:555`) accepts a match anywhere in the value.
- A `RegexMatchTimeoutException` is not caught, so it gives a 500.
- Required: the client accepts `"   "`. The server uses `IsNullOrWhiteSpace`.
- The message rules are the opposite of each other (`atlas-validators.ts:73-80` against `ConfigSectionService.cs:547-553`).

**Guidance.** One source of truth for validation. The server is the authority, and the client must mirror it.

**Impact.**

- `Enable:[X]` on a field that the JSON disables: the user can edit it, and the server answers `NotAllowed` on each save.
- `Disable:[X]` is done only by the client.
- `Set` values from the client are trusted.
- Pattern `\d{4}` accepts `abc1234xyz` through the API.

**Fix.** Do `Enable`/`Disable`/`Set`/`Clear` on the server in the same pass, with `Set` targets made by the server. Until then, `--atlas-check` gives an error for these keys. Anchor the server pattern (`^(?:p)$`). Change a regex timeout into a field error. Use a trimmed Required on the client. Make one JSON fixture of cases that both xUnit and Vitest run. Effort M.

### SV-05 A dashboard tab can read the table of another app without the checks of that app (Medium, service)

**Evidence.** `LoadRows` finds `tab.Code` with `FindManifest(tabCode)` and reads that table (`Dashboard/AtlasDashboardService.cs:81-85`). It does not check `Enabled`, and it does not call `access.Demand` for that app.

**Impact.** A tab with `Code: "TTDV2"` in the dashboard of another app shows TTDV2 data to users who have no TTDV2 role. This occurs also when TTDV2 has `Enabled=false`, which is the rollback switch.

**Fix.** For a foreign `tabCode`: the app must be enabled, and the `View` check must pass on that manifest and form. If not, hide the tab. Effort S.

### NG-07 Dashboard History and Delete use the code of the dashboard, not the code of the tab (Medium, Angular)

**Evidence.** `open()` uses `this.tab()?.Code || this.code` (`atlas-dashboard.component.ts:94-95`), but `confirmRemove()` calls `this.api.remove(this.code, ...)` (`:98-101`), and the history dialog gets `[appCode]="code"`. Spec 03 says the tab `Code` is "the app of the rows".

**Impact.** On a dashboard with rows from another app, Delete goes to the same ID in the wrong app, and History shows the log of a different record.

**Fix.** Keep `{ code, id }` in `historyRow` and `deleteRow`, from `this.tab()?.Code || this.code`. Effort S.

### DA-02 Optimistic concurrency is optional, so a client that does not send Version overwrites changes (Medium, data)

**Evidence.** `ConfigSectionService.cs:324, 386`: the check runs only when `input.Version` is not empty. The column name is fixed (`RowVersionColumn = "RowVersion"`, `:71`), but the model marks any `timestamp` column as the row version (`AtlasPlatformContext.cs:56`). For another column name, the check stops without a signal.

**Guidance.** EF Core "Handling concurrency conflicts": the token must be the value that the user saw (disconnected scenario).

**Impact.** A script, a second client or a future UI that does not send `Version` overwrites changes from other users on all apps, with no error.

**Fix.** On the platform path, an update of a table with a rowversion and no `Version` gives `VersionRequired` (the TTDV2 UI already sends it). Find the column by `IsRowVersion`, not by name. If necessary, make it a manifest setting with the default `true` for new apps. Effort S.

### DA-03 The audit history read does not filter on RefType (Medium, data)

**Evidence.** `AtlasAuditWriter.cs:73-76` filters only `RefID == refId`. The legacy read also filters `RefType == Code` for child apps (target `CPApplicationService.cs.txt:639`). The writer stores `RefType` (`:57`). Two manifests can name the same `AuditTable`.

**Impact.** In a parent/child family with one shared `AuditLog` (the SQMSO/SQMSB pattern, P3 H1/H2), History of record 42 of app A also shows record 42 of app B. The user can see values without the right to see them.

**Fix.** When the audit table is shared or `Parent` is set, filter `RefType IN (...)`. Add an optional `AuditRefTypes` (for example `["TTDV2","TTD"]`). Add a start-up warning for a shared `AuditTable` without that setting. Effort S.

### DA-04 SoftDelete.Field is configurable, but all reads use IsDeleted (Medium, data and service)

**Evidence.** `RemovePage` writes `form.SoftDelete.Field` (`ConfigSectionService.cs:701-706`). `FindActive` (`AtlasDataExtensions.cs:98-103`), the dashboard filter (`AtlasTabQueryBuilder.cs:42-43`), `SystemColumns` (`ConfigSectionService.cs:65`) and the create default (`:364`) use the constant `IsDeleted`. The start-up check only checks that the column exists.

**Guidance.** EF Core global query filters (soft delete is the reference example). One source of truth.

**Impact.** App #17 with `"SoftDelete": {"Field":"IsArchived"}` passes `--atlas-check`. Delete reports success and writes a Delete audit entry, but the record stays visible and editable everywhere. A client can also send `IsArchived`.

**Fix.** P1: the start-up check gives an error when `Field` is not `IsDeleted` or the column is not `bit`. Later: keep the soft-delete column on `AtlasTable` and use it in a model-level `HasQueryFilter`, in the query builder and in `SystemColumns`. Effort S.

### DA-05 Database default constraints are ignored, so inserts send NULL (Medium, data)

**Evidence.** `AtlasColumnMetadataReader.cs:12-21` does not read `default_object_id`. `NewRecord` sets all columns to null (`AtlasDataExtensions.cs:57-71`). The model marks only identity, rowversion and computed columns as generated (`AtlasPlatformContext.cs:56-61`), so EF sends all columns in the INSERT.

**Guidance.** EF Core "Generated values / default values": configure `HasDefaultValueSql` so EF does not send the sentinel.

**Impact.** App #17 has an existing table with `Status varchar(20) NOT NULL DEFAULT 'Draft'`, and the form does not set it. Each create fails with a general `SaveFailed`. A nullable column with a default gets NULL and no error.

**Fix.** Read `HasDefault` and the definition. For nullable or reference-type columns, set `.HasDefaultValueSql(definition)`. Do not do this for non-nullable value types: a `bit DEFAULT 1` column would drop a `false` from the user. Give a start-up warning for those columns. Effort M.

### DA-06 Change detection and the allow list compare text, not typed values (Medium, data)

**Evidence.** `SameValue` is an ordinal string compare (`ConfigSectionService.cs:782-783`). It is used for disabled and hidden fields (`:293`) and for the audit diff (`:628-631`). `ToWire` gives the stored decimal scale and the `.fff'Z'` time format (`AtlasValueConverter.cs:97-103`).

**Guidance.** EF Core compares typed values (`ValueComparer`). The platform diff does not agree with EF.

**Impact.** Stored `1250000.50` and sent `1250000.5`: EF sends no UPDATE, but the audit records a change. This breaks the acceptance item "no entry for no change". A client that formats numbers in a different way gets `NotAllowed` on a disabled field.

**Fix.** Normalize before the compare: `ToWire(FromWire(x))`, or compare the converted CLR values. Use the same helper in `SameValue` and in `Changes`. Effort S.

### SV-06 An OnAfterSave failure after the commit gives a 500 for a saved record (Medium, service)

**Evidence.** `OnAfterSave(saveContext)` (`ConfigSectionService.cs:397`) runs after the commit, with no try/catch.

**Impact.** A module hook that sends a notification throws an exception. The record is saved, but the client sees a 500. When the user tries again with `ID = 0`, a duplicate record is created.

**Fix.** Catch the exception, log `Atlas.AfterSaveFailed` with a new EventId, and return success with the ID. Write in the module guide that hooks must not throw. Effort S.

### SV-09 IsOwner compares field names case-sensitively, but the start-up check does not (Low, service)

**Evidence.** `IsOwner` uses `record.TryGetValue(field, ...)` with the JSON name (`AtlasAccessService.cs:77`). The EF dictionary is case-sensitive. The start-up check uses `table.HasColumn(owner)`, which ignores case (`AtlasStartupCheck.cs:123`).

**Impact.** `"OwnerFields": ["TaxPreparerLanID"]` passes the check, but it never matches. The owner cannot edit the record. This fails closed, but the cause is hard to find.

**Fix.** Resolve each owner field with `table.Find(field)?.Name` before the lookup. Effort S.

### SV-10 Failure paths leave the tracked entity changed (Low, service)

**Evidence.** `record = stored` is the tracked dictionary, and `Map` writes into it (`ConfigSectionService.cs:331-338`) before an early `return Failed(...)` (`:351-352, 382-383`). `ChangeTracker.Clear()` runs only in `SaveWithAudit` (`:677`). `JsonDocument.Parse(json)` is not disposed (`:447`).

**Impact.** This is latent. A later `SaveChanges` in the same request (from a hook or a future feature) writes refused values.

**Fix.** Call `ChangeTracker.Clear()` in `Failed(...)`, and use `using JsonDocument`. Effort S.

### DA-12 varchar free-text columns lose Unicode characters without an error (Low, data)

**Evidence.** `040-ttdv2-app.sql:28-33`: `Account`, `AccountName` and `WorkDescription` are `varchar`. They are mapped as AnsiString (`AtlasSqlTypes.cs:42-43`). The length check counts UTF-16 characters (`AtlasValueConverter.cs:171`).

**Impact.** Pasted CJK text or symbols are stored as `?`, and the audit does not show the loss.

**Fix.** Use `nvarchar` for free text in `05-new-app-template.sql`. TTDV2 mirrors TTD, so keep it or decide explicitly. Effort S.

## 6. Reliability and operations

### HO-02 One bad config file stops all platform apps and the shared application menu (High, host)

**Evidence.** `GetManifests()` deserializes all `*.app.json` files on each call and throws at the first bad file (`Registry/AtlasConfigStore.cs:48-55`). The resolver (`AtlasModuleResolver.cs:41,50`), `RolesFor` (`AtlasUserContext.cs:54`) and the menu (`AtlasApplicationMenu.cs:20-22`) all call it.

**Guidance.** Fault isolation (bulkhead). `IOptionsMonitor` keeps the last valid value when a reload fails validation.

**Impact.** App #17 deploys a manifest with a typo, or a file copy is not complete. Then all TTDV2 requests give 500, and the full `v1/Application` response gives 500 for each user who has a platform role. The legacy menu items are lost too.

**Fix.** Parse each file separately. Keep the last good manifest for each file. A bad file removes only its own app (404 and a log entry `Atlas.ConfigInvalid`, new EventId). Show the errors in the start-up check. Do this together with HO-05 and HO-07 as one immutable snapshot (see section 9). Effort M.

### HO-03 Environment overrides fail open: a typo leaves the app on and access not enforced (High, host)

**Evidence.** `AtlasConfigStore.cs:117-120` ignores values that it cannot parse (`bool.TryParse`, `Enum.TryParse`). The manifest ships `"Enabled": true`. `AccessMode` defaults to `LogOnly`. INTEGRATION.md section 4 makes `Enabled=false` the production rollback switch.

**Guidance.** Options validation (`Validate`, `ValidateOnStart`). Security settings fail closed.

**Impact.** `Enabled = "0"` or `"flase"` is ignored, so TTDV2 goes live before approval. `"Enfroce"` is ignored, so access stays `LogOnly`. The value `"2"` parses to an enum value that is not defined. The start-up check does not report these cases.

**Fix.** Use `Enum.IsDefined` after the parse. A value that is present but not valid is a start-up error. A bad `Enabled` value means `false`. Add a warning when `AccessMode == LogOnly` outside Development. Valid settings do not change. Effort S.

### DA-01 An unsupported column type or a non-int ID stops all platform apps, and the start-up check does not find it (High, data)

**Evidence.** `OnModelCreating` throws for a table with no `ID` and for a column with no CLR type (`AtlasPlatformContext.cs:36-47`). The start-up check only checks that the table exists (`AtlasStartupCheck.cs:229-242`). It never reads `ClrType` or the key type. `Data/Platform/README.md:21-22` says that the check reports these columns, but it does not. The key is cast to `int` (`AtlasDataExtensions.cs:96`, `ConfigSectionService.cs:399, 654`).

**Guidance.** EF Core: one model for each context type, built on first use. Fail-fast validation of model inputs.

**Impact.** A DBA adds a `float`, `time`, `datetimeoffset`, `xml` or `varbinary(max)` column to the table of app #12. `--atlas-check` returns 0 and the API starts. The first platform request fails while the model is built. All tables share one model, so all platform apps fail. A `bigint` key gives `InvalidCastException` on save.

**Fix.** In the start-up check, give an error when `Key` is null, when the key is not `int` identity, or when a column has `ClrType == null`. Then build the model in the check (`_ = context.Model`), so that `--atlas-check` returns 1. Optional: ignore unsupported nullable columns in the model. Effort S.

### HO-04 A start-up check error stops the whole host, legacy apps included (Medium, host; challenges spec 07 section 4.8)

**Evidence.** `StartAtlasPlatformAsync` throws for any error (`AtlasPlatformStartup.cs:24-26`). The metadata load has no try/catch.

**Guidance.** Blast-radius containment. Fail-fast is correct for platform wiring, but not for the data of one app. Health checks (Degraded/Unhealthy) let the host start and report a defective part.

**Impact.** With 17 config-only apps, one bad form field, or SQL that is not available when the app pool starts, gives 500.30 for all of CP Atlas (TTD, CDT, SQMS). After repeated failures, IIS Rapid-Fail Protection can stop the app pool.

**Fix.** Add the optional setting `AppSettings:Atlas:OnStartupError = Stop | DisableModule`, with the default `Stop` (no change). `DisableModule` removes only the failing apps (404, hidden in the menu) and logs Critical. Optionally register an `IHealthCheck` that reports Degraded. Effort M.

### HO-05 A config hot reload does not run the validation and table load (Medium, host)

**Evidence.** The files are read again when their time stamp changes (`AtlasConfigStore.cs:99-107`). The tables load only at start-up, and only for apps that are enabled then (`AtlasPlatformStartup.cs:64-68`). Disabled manifests are not checked (`AtlasStartupCheck.cs:64-68`). A table that is not loaded throws (`AtlasTableRegistry.cs:59-62`).

**Impact.** An app deployed with `Enabled: false` and then switched on by a live edit goes live without a check. Each request then gives 500 "Table ... is not registered". A live form edit with a new column has the same result.

**Fix.** On a manifest or form change, run the checks for that module. If they fail, keep the last good version. Small first step: the resolver treats a manifest whose table is not loaded as "not available" (404 and a log entry). Effort M.

### HO-06 Platform start-up is a manual call, with no guard when it did not run (Medium, host)

**Evidence.** The registry loads only when the integrator adds `await app.Services.StartAtlasPlatformAsync()` (INTEGRATION.md section 2). Nothing checks that it ran. The token is `default`, not `ApplicationStopping`.

**Guidance.** Generic Host: start-up work goes in an `IHostedService`, which runs before the server accepts requests.

**Impact.** If the line is not there, or a target `WebApplicationFactory` test does not call it, the host starts. Each platform request then gives a general 500 with "Table ... is not registered".

**Fix.** `AddAtlasPlatform` registers a hosted service that calls the same idempotent load (with a `Loaded` flag). `StartAtlasPlatformAsync` stays public and does nothing if the load is complete. Or the resolver throws a clear "platform not started" error. Effort S.

### DA-07 The release scripts log success when the main batch failed (Medium, SQL)

**Evidence.** In `040-ttdv2-app.sql` the "Log this script" batch (`:126-130`) is after a `GO`, after the main batch that can `THROW`. In `050-ttdv2-copy-from-ttd.sql`, `THROW 55006` (`:117`) ends only its own batch, and the log batch (`:191-193`) still runs. SSMS and `sqlcmd` without `-b` continue to the next batch.

**Guidance.** A migration records its success in the same transaction as the change (as `__EFMigrationsHistory` does).

**Impact.** `CP_Atlas_ScriptLog` says that 040 or 050 is applied when nothing was created or copied. A DBA who trusts the log does not run it again. TTDV2 then fails at start-up, or users lose their TTD history.

**Fix.** Move the log `INSERT` into the TRY block, before `COMMIT`, or add a success condition. Add `:on error exit` and write `-b` in the README. Use the same fix in the `11-script-log.sql` template. Also in 050: revoke the TTD roles before the `#Src` snapshot, or take the snapshot in the copy transaction. Effort S.

### SV-07 The config contract is not typo-safe, and the start-up check does not check references (Medium, service)

**Evidence.** `AtlasJson` ignores case and ignores unknown members (`Platform/AtlasJson.cs:263-288`). An unknown default token is saved as literal text (`ConfigSectionService.cs:234`). `CheckForm` (`AtlasStartupCheck.cs:116-164`) does not check these items:

- effect target names and `SetFromOption` keys;
- the control names in rules;
- token syntax;
- whether a pattern compiles;
- duplicate names;
- `Type` values and button `Action.Type`.

There is no schema and no version marker.

**Impact.**

- `"Requried": true` turns off validation without a message.
- `"{Todya}"` saves the text `{Todya}`.
- `HideControls:["Sallary"]` hides nothing. This is also a security gap when the rule must restrict a field.

**Fix.** In `--atlas-check` only, for platform apps only: deserialize again with `UnmappedMemberHandling = Disallow` and report unknown members as warnings. Add the missing name, token and pattern checks as errors. Add an optional `"SchemaVersion": 1`. Publish a JSON Schema in `docs/` for editor help (no package needed). Effort M.

### NG-08 Accessibility gaps on the platform side (Medium, Angular)

**Evidence.**

- The breadcrumb is `<a (click)>` with no `href` (`atlas-form.component.html:12`).
- Grid edit links have no `href` or `tabindex` (`atlas-dashboard-columns.ts:82-85`).
- Field errors have no `id`, no `aria-live` and no `aria-describedby` (`atlas-form.component.html:47-49`). The required `*` is visual only.
- The tabs have `role="tab"` but no `tabpanel`, no `aria-controls` and no arrow keys.
- The checkbox has two labels.

**Guidance.** angular.dev accessibility guide; WAI-ARIA tabs pattern; WCAG 2.1.1, 1.3.1 and 3.3.1.

**Fix.**

- Use `href="#"` + `preventDefault` or `routerLink` (NG-02).
- Give grid links `href` and an Enter key handler.
- Use `<span [id]="'err_'+Name" aria-live="polite">`, plus `aria-describedby` and `aria-required` where `@pbdev` allows them.
- Add a tabpanel with roving tabindex.
- Give `checkbox` no outer `for` (as for `hasInput=false`).

Effort S-M.

### HO-08 Registration hygiene: TimeProvider is replaced, and a duplicate module code gives an unclear crash (Low, host)

**Evidence.** `services.AddSingleton(TimeProvider.System)` (`AtlasPlatformServiceCollectionExtensions.cs:23`). The target already registers one, and the last registration wins. A second `AddAtlasModule` for the same code fails later in `ToDictionary` with "An item with the same key has already been added" (`AtlasModuleResolver.cs:30-31`).

**Guidance.** Library DI convention: `TryAdd*`, so the choice of the app wins. Fail-fast with a clear message.

**Fix.** `TryAddSingleton(TimeProvider.System)`. Throw `"App code 'X' is registered twice."` in `AddAtlasModule`. Effort S.

### HO-09 The config-folder containment check compares string prefixes only (Low, host)

**Evidence.** `AtlasConfigStore.cs:91-93`: `path.StartsWith(ConfigFolder, ...)`. `..\ConfigOld\x.json` passes.

**Fix.** Compare with the folder plus a directory separator, or use `Path.GetRelativePath`. Effort S.

### HO-10 The stand-in Fixed authentication is not limited to Development (Low, host; stand-in only)

**Evidence.** `StandIn/StandInHost.cs:34-38` uses the Fixed handler in all environments unless `Negotiate` is set.

**Impact.** If the stand-in host runs on a shared QC or demo machine, each caller is the service account, with its roles.

**Fix.** Throw at start-up when Fixed is selected outside Development. Effort S.

### SV-11 Config defects use status codes that are not consistent, and the problem body is built by hand (Low, service and host)

**Evidence.** Some config defects give 400 "Bad request" (no Section, no Table, no SoftDelete: `ConfigSectionService.cs:143, 268, 702, 740`). Others give a raw 500 (unknown provider, `:750`). `AtlasProblemExceptionFilter.cs:15-24` makes `new ProblemDetails {...}` without `ProblemDetailsFactory`, so there is no `traceId` or `type`. The target calls `AddProblemDetails()`.

**Guidance.** ASP.NET Core error handling: `ProblemDetailsFactory` / `IProblemDetailsService`.

**Fix.** Add `AtlasProblemException.Config(message)` (500, logged as `Atlas.ConfigError`). Make the body with `ProblemDetailsFactory.CreateProblemDetails`. This adds fields only. Effort S.

### SV-12 Weak logging of refusals and ignored data (Low, service and host)

**Evidence.** `Atlas.FieldIgnored` is logged at Debug level (`ConfigSectionService.cs:287`), but INTEGRATION.md section 4 tells operators to search for it. `NotAllowed` refusals are not logged (`:296, 424, 433`). Grid JSON that does not parse is dropped without an error (`:449-452`). `Atlas.ClientAuditIgnored` has no EventId. The log calls use extension methods, and the event IDs are in different files.

**Guidance.** .NET high-performance logging (`[LoggerMessage]`, CA1848).

**Fix.**

- Log `FieldIgnored` at Information.
- Log `Atlas.FieldRefused` at Warning, with LanId, App, Record and Field.
- Grid JSON that does not parse gives a field error.
- Make one `AtlasLog` partial class with `[LoggerMessage]`, which keeps the same IDs and templates.

Effort S.

## 7. Performance

### HO-07 The config store reads and parses all manifests again for each lookup (Medium, host)

**Evidence.** `FindManifest` is `GetManifests().FirstOrDefault(...)` (`AtlasConfigStore.cs:63`). Each call lists the folder, reads the time stamp of each file and deserializes each manifest. One `LoadPage` makes approximately 10 calls: `IsPlatformApp`, `Get`, `Manifest`, `GetForm`, `RolesFor`, and `UserPermissions` (4 calls). `GetForm` also deserializes the form again at each call site (`ConfigSectionService.cs:127, 245, 695, 728`).

**Guidance.** ASP.NET Core performance best practices: avoid blocking file I/O on hot paths; cache, and invalidate with change tokens.

**Impact.** With 17 apps, there are approximately 170 deserializations and 170 file time-stamp reads for each request, and the cost increases with each app. On a UNC share it is slower. A file edit during a request can give "enabled" and "disabled" in the same request.

**Fix.** Keep an immutable manifest snapshot that a `PhysicalFileProvider.Watch("*.app.json")` change token invalidates. Keep a copy on read only for `GetForm` and `GetDashboard`, which the request changes. Optionally use a per-request snapshot. Effort M.

### DA-08 The soft-delete condition cannot use the filtered indexes (Medium, data)

**Evidence.** `AtlasTabQueryBuilder.cs:43` writes `ISNULL([IsDeleted], 0) = 0`. The indexes are filtered `WHERE [IsDeleted] = 0` (`040-ttdv2-app.sql:63, 66`). The column is `bit NULL` (`:48`).

**Guidance.** SQL Server filtered-index matching: the query condition must imply the index filter. `ISNULL(x,0)=0` also matches NULL, and it is not sargable.

**Impact.** The "My records" tab and the preparer tab cannot seek `IX_General_CreatedBy` or `IX_General_TaxPreparerLanId`. Each dashboard load scans the full table for each visible tab. Each app made from the template gets the same problem.

**Fix.** Template: `IsDeleted bit NOT NULL DEFAULT 0`. The platform and the copy script never write NULL. Builder: write `[IsDeleted] = 0` when the column is NOT NULL. For TTDV2, if 040 already ran in an environment, an `ALTER COLUMN ... NOT NULL` after a NULL check is in place and loses no data, but it needs approval under the compatibility rules. Effort S.

### DA-09 Dashboard queries return all rows in an order that is not stable, and display names load one at a time (Medium, data)

**Evidence.** There is no `TOP` or `OFFSET/FETCH` (`AtlasTabQueryBuilder.cs:59-65`). The "All Report" tab has no filter. `OrderBy` has no unique tie-breaker. `AtlasDashboardService.cs:111-114` calls `DisplayName(lanId)` for each row. With an empty cache, that is one query for each LAN ID (`AtlasUserContext.cs:84-91`).

**Guidance.** EF Core performance: limit the result size; avoid N+1. Paging needs a unique order.

**Impact.** With tens of thousands of rows, each dashboard open sends all rows of each tab. After the cache expires, 200 preparers give 200 extra round trips. When paging is added later, the order gives duplicate or missing rows.

**Fix.** Always add `[ID] DESC` as the last ORDER BY item (the current order stays). Add an optional `MaxRows` setting that writes `TOP (@max)` and logs when it is reached. Load the display names in one `IN (...)` query. Effort S-M.

### DA-10 Synchronous database I/O, and tracked reads (Low, data and host)

**Evidence.** `FindActive`, `SaveChanges`, `AtlasSql.Query` (`ExecuteReader()`), `AtlasAuditWriter.Read` and the start-up check are synchronous. `FindActiveAsync` and `BeginSharedTransactionAsync` exist but are not used. `LoadPage` and `GetAuditLog` load a tracked record that they never save. `RequestAborted` is not passed down.

**Guidance.** ASP.NET Core: avoid blocking calls. EF Core: use no-tracking for read-only paths.

**Impact.** This agrees with the synchronous target `CPApplicationService`, so it is not a defect now. With 17 apps in one app pool, blocked threads limit throughput under load.

**Fix.** Now: a `FindActive(..., tracking: false)` overload for `LoadPage` and `GetAuditLog`. Later: additive `...Async(CancellationToken)` overloads. Effort M (deferred).

### NG-11 Mixed signal and plain state, template method calls, and loose form types (Low, Angular)

**Evidence.** `code`, `id`, `form` and `controlsByName` are plain fields (`atlas-form.component.ts:42-45`). The template calls `visibleButtons()`, `canViewHistory()`, `isVisible()`, `error(child)`, `registry.get()` and `registry.hasInput()` for each field on each change-detection cycle. No component uses `OnPush`. The form is `FormGroup<Record<...>>`, not `FormRecord`. A multi `ng-select` puts a `string[]` in a `FormControl<string|null>`.

**Guidance.** angular.dev signals (derive with `computed`); style guide (prefer `OnPush`, keep logic out of templates); typed forms (`FormRecord` for dynamic keys).

**Impact.** It works now because each async path also writes a signal. It is fragile: a future async option load that changes only `form` does not repaint under `OnPush` or zoneless.

**Fix.** Make `id` and `code` signals. Make `visibleButtons` and `canViewHistory` `computed`. Make a per-field view model `{control, renderer, labelFor}` in `build()`. Use `FormRecord` and `OnPush`. Effort S-M.

### NG-12 problemText is in the form component, so the lazy chunks are mixed, and it shows a developer message (Low, Angular)

**Evidence.** `export function problemText` is in `atlas-form.component.ts:305-317`, and the dashboard and the home page import it. The build output shows that the form chunk is 125 bytes and its code is in a 131 kB shared chunk. Line 314 tells the user to "Start the API (see atlas-platform/local/README.md)".

**Fix.** Move it to `platform/atlas-errors.ts`, and use a neutral status-0 message, such as "The service is not available. Try again later." Effort S.

## 8. Design and maintainability

### SV-08 ConfigSectionService has too many responsibilities and is closed to extension (Medium, service)

**Evidence.** One class of approximately 800 lines, which each module inherits (`ConfigSectionService.cs:60`). It does binding, options, defaults, tokens, server effects, the allow list, validation, number parsing, computed fields, system fields, concurrency, transactions, the audit diff, delete and the dashboard. Each extension point is a fixed switch:

- computed functions (`:603-610`, repeated in `AtlasStartupCheck.cs:135`);
- tokens (`:220-234`);
- the `"user"` type (`:212`);
- validator kinds (`:542-572`).

The logic is `private static`, so only the SQL harness can test it.

**Guidance.** SRP, OCP, DRY (the function list is in two places).

**Impact.** To add `Avg()`, a `{User.Email}` token or a `Date` validator for app #17, a developer must change the base class of all apps and test all apps again. The list in the check and the list in the service will drift apart.

**Fix.** Move the pure steps to `Platform/Sections` as separate classes: `AtlasFieldValidator`, `AtlasServerEffects`, `AtlasComputedFields` (one function table for the save and the check), `AtlasDefaultTokens` and `AtlasAllowList`. `ConfigSectionService` stays the orchestrator with the same public and protected surface. Add fast unit tests. This also makes SV-03, SV-04 and SV-01 easier. Effort M.

### NG-09 There are no component or integration tests (Medium, Angular)

**Evidence.** The only spec file tests pure functions. There are no TestBed tests for `AtlasFormComponent`, the renderers, the dashboard or the history dialog. The local app is zoneless, but the target uses `provideZoneChangeDetection({ eventCoalescing: true })` (target `app.config.ts.txt:16`).

**Guidance.** angular.dev "Testing components", component harnesses.

**Impact.** No test covers NG-02, NG-03, NG-04, the server error mapping, the NgComponentOutlet input contract or the dialog flow. No test runs in the change-detection mode of the target.

**Fix.** Add approximately five TestBed tests with `provideHttpClientTesting` and `RouterTestingHarness`:

1. Each registered type renders.
2. A hidden required field lets the form save.
3. A server field error appears on its field.
4. Close, Save and Delete go to the injected link.
5. The dialog opens and closes.

Run one test with `provideZoneChangeDetection`. Effort M.

### NG-10 The history dialog uses the old decorator API and a fetch that is not cancelled (Low, Angular)

**Evidence.** The dialog has `@Input() open`, `@Output() closed = new EventEmitter`, and an `ngOnChanges()` that subscribes on each change with no cancellation (`atlas-history-dialog.ts:48-72`). On an error it sets `entries([])`, so the user sees "No history."

**Guidance.** angular.dev: `input()` / `output()`; RxJS interop: `toObservable` + `switchMap` or `rxResource`.

**Impact.** Open record 5, close it, and open record 7. If the answer for record 5 arrives last, it shows under "History of record 7". A 403 or 500 shows as an empty history.

**Fix.** Use `input()`/`output()` (no template change), load with `switchMap`, and show `problemText()` on an error. Effort S.

### SV-13 The module bundle gives each module the raw contexts (Low, service)

**Evidence.** `AtlasModuleServices` (`Platform/Sections/AtlasModuleServices.cs:5-33`) gives each module `CPSharepointContext`, `AtlasPlatformContext` and `IConfiguration`. It also builds the dashboard service graph on each save.

**Assessment.** The bundle is a correct, deliberate parameter object: a new platform service does not break 16 module constructors. It is not a service locator.

**Fix.** Write in the module guide that a module must not write through the raw contexts, because that skips the allow list and the audit. Optionally make `Dashboards` lazy. Effort S.

### DA-11 System time stamps are always UTC with 'Z', with no setting for each app (Low for P1, Medium for P4, data)

**Evidence.** `ConfigSectionService.cs:359, 363, 368, 708` use `GetUtcNow().UtcDateTime`. `AtlasValueConverter.cs:98` always adds `Z`. Legacy writes local time (target `CPApplicationService.cs.txt:611`). Only the audit has a setting (`manifest.AuditTime`).

**Impact.** In P4 (in-place migration of existing apps), old rows have local times and new saves write UTC to the same columns. The dashboard shows the old values as UTC, and `ORDER BY ModifiedDate` mixes the two conventions.

**Fix.** Add an optional manifest `SystemTime: Utc | Local` (default `Utc`). Use it on save and delete, and in `ToWire`. Warn in the start-up check for an onboarded legacy table without the setting. Effort S.

## 9. Recorded decisions that this review challenges

| Decision | Challenge | Proposal |
|---|---|---|
| Spec 06 section 5: `LogOnly` for change-over | Correct for a migration, but not for a new app (SV-02). | Enforce `Delete` and "no role" in `LogOnly`. Make `Enforce` the default for new apps. |
| Spec 07 section 4.8: a start-up check error stops the host | One app can stop all of CP Atlas, legacy apps included (HO-04). | Optional `OnStartupError = DisableModule`. The default stays `Stop`. |
| B10: `SetFromOption` keeps the stored values when the option is not found | Together with SV-01, a client can save a value that is not an option. | Keep B10 for unchanged values only. Refuse changed values that are not options. |
| B17: "the form still works when an inputId binding fails" | Under strict templates the build fails first (NG-01). | Correct the note, and verify each `@pbdev` input. |
| Spec 12 section 11.4: the stand-in has "the same export names" | Not met (NG-01). | Rename the stand-in exports to the real class names. |

## 10. Recommended order of work

**Wave 1: before the copy to the target (release P1).** Approximately 3 to 4 days.

1. NG-01, NG-02, NG-05, NG-06: compile and run correctly in the target WebUi.
2. SV-01, SV-02: close the two security gaps.
3. HO-03, DA-01: fail closed on bad settings and unsupported columns.
4. DA-07: correct the release script log.
5. HO-01: write and bind the configuration root that the target gives.
6. HO-08: `TryAdd` and the duplicate-code message.

**Wave 2: before the second platform app.**

1. One config snapshot with a last good version for each file: HO-02, HO-05, HO-07.
2. Make the config promise true: NG-03, NG-04, SV-03, SV-04, SV-07, DA-04, DA-05.
3. Data correctness: DA-02, DA-03, DA-06, SV-05, NG-07, SV-06.
4. Refactor `ConfigSectionService` into steps (SV-08) while these changes are made. The refactor and the fixes support each other.
5. Angular component tests (NG-09).

**Wave 3: as the app count and load increase.**

DA-08, DA-09, DA-10, HO-04, HO-06, NG-08, NG-10, NG-11, NG-12, SV-09 to SV-13, DA-11, DA-12, HO-09, HO-10.

Housekeeping: the phase table in `atlas-platform/README.md` still shows C5 to C10 as "Not started". `atlas-platform/INTEGRATION.md` and `Data/Platform/README.md:21-22` have small mistakes (see DA-01).

## 11. Items checked and found correct

- DI lifetimes: no captive dependencies. Singletons use only singletons. Per-request state is scoped. Keyed scoped modules come from the scoped provider.
- Thread safety: `ConcurrentDictionary` in the store, `Lock` in the table registry, frozen `AtlasJson.Options`, stateless `AtlasRuleEngine`.
- No cached config object is changed by a request.
- IDOR on Edit: the server checks the stored record for owner and rules, also in `LogOnly` (test `Ttdv2SectionTests.cs:162`).
- Rule meaning: `MostPermissive` and the B3/B4 defaults agree with spec 04, and `RuleEngineTests` covers them.
- SQL: all identity SQL is parameterized. Dashboard identifiers come only from metadata.
- `AtlasPlatformContext` is scoped and not pooled. This is correct, because pooling would break the shared connection.
- The target has no `EnableRetryOnFailure`, so user transactions are safe. If the target turns retries on, wrap `SaveWithAudit` in `CreateExecutionStrategy().Execute(...)`.
- The 050 copy logic: `IDENTITY_INSERT`, the `NOT EXISTS` guards and `AT TIME ZONE` are correct.
- Zoneless and zone.js: each async path writes a signal, so the view refreshes in both modes.
- The target `CustomRouteReuseStrategy` creates the form again when the parameters change, so an old request cannot overwrite a newer one.
- PascalCase DTOs agree with the target `PropertyNamingPolicy = null`.
- Error details: only `AtlasProblemException` messages go to clients. Other exceptions go to the general 500 of the target.
