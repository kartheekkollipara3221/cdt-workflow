# Atlas platform: Phase B implementation plan (hand-off)

Written 2026-10-31 by Claude Fable 5.1 for the next session (Claude Opus 5.5). It is the work order for the refactor that `FABLE_REVIEW.md` recommends and the owner approved. Read `FABLE_REVIEW.md` first for the reasons; this file gives the steps.

## 0. Rules for the whole job

1. Read `AGENTS.md`, the top entry of `CODEX_HANDOFF.md`, `atlas-platform/README.md`, `atlas-platform/INTEGRATION.md` and `docs/atlas-platform/FABLE_REVIEW.md` before the first change.
2. **Never read, print or commit anything under `target-implementation-read-only/`.** It is git-ignored. Keep it that way.
3. The target team owns the target code. Write anything the target must do as an integration requirement in `INTEGRATION.md`, never as a change to their code.
4. Contracts, change only additively: public HTTP routes and response shapes, request JSON, JSON config keys, SQL scripts in `sql/release-p1`, the DI methods `AddAtlasPlatform`, `AddAtlasModule<T>`, `StartAtlasPlatformAsync`, `RunAtlasCheckAsync`, and the exported TypeScript names that `INTEGRATION.md` section 2 names (`atlasPlatformRoutes`, `AtlasDashboardNavigation`, `columnDefs`, `sumsRow`, `provideAtlasGridComponents`, `provideAtlasRenderers`, `AtlasRendererBase`, `ATLAS_DASHBOARD_LINK`, `problemText`).
5. One step = one commit. Order inside a step: pin the behavior with the existing tests (add a test only where this plan says so), change the structure, run all gates, commit. Do not start the next step with a red gate.
6. Gates after every step, all must pass (run from the repo root):
   ```
   dotnet build atlas-platform/AtlasPlatform.slnx            (0 warnings; warnings are errors)
   dotnet test  atlas-platform/AtlasPlatform.slnx            (295 pass at the start; the plan says when the count changes)
   dotnet run --project atlas-platform/src/BBH.CP.Atlas.App -- --atlas-check   (exit 0; 0 errors / 0 warnings)
   cd atlas-platform/WebUi && npm run build && npm test -- --watch=false       (77 pass at the start)
   cd atlas-platform/WebUi && npm run check:target-flags
   ```
   The .NET tests need the local SQL Server (see `atlas-platform/local/README.md`).
7. If a "COPY to the target" file changed (anything under `src/*/Platform/`, `src/BBH.CP.Atlas.Service/Application/`, `src/BBH.CP.Atlas.App/Config/TTDV2*.json`, `WebUi/src/app/platform/`, `sql/release-p1/`) or `INTEGRATION.md` changed, rebuild the package after the commit:
   ```
   python atlas-platform/local/make-target-package.py atlas-platform/local/package/atlas-target-package.ps1
   ```
   The script refuses when `atlas-platform/` has uncommitted changes, so commit first, then rebuild, then commit the package. Rebuild once at the end of the whole job is also acceptable; say which in the handoff.
8. Do not push without the owner's OK.
9. Commit message form: `refactor(atlas-platform): step N <short title>` with the body naming the review findings (F01, O3, ...). End with the attribution line from the session.
10. After the last step: update the top entry of `CODEX_HANDOFF.md`, add a row to `docs/atlas-platform/README.md`, and give the AGENTS.md completion summary (Changed / Why / Tests run / Seed verification run / Sample JSON executed / Files updated / Open issues).
11. Writing style for all docs and comments: short plain sentences, as the rest of the repository.

## 1. Owner decisions (2026-10-31)

| Item | Decision |
|---|---|
| Steps | 1 to 14, **step 10 is not done** (the manifest settings `AuditMode`, `AuditTime`, `SystemTime`, `AttachmentTable`, `Service`, `SchemaVersion` stay as they are) |
| Step 9 | Yes: read the config one time at start-up; no live JSON reload |
| Step 6 | Delete `CPTTDV2Service.cs` and `Application/TTDV2/`; `program.cs` registers `AddAtlasModule<ConfigSectionService>("TTDV2")` |

## 2. Baseline at hand-off

Branch `console-code-first-0518`, HEAD `59ffd2f` (docs commit on top of `fb8f3eb`, not pushed). `dotnet build` 0 warnings; `dotnet test` 295 pass; `--atlas-check` 0/0; `ng test` 77 pass. No platform code changed since `fb8f3eb`.

Key paths (all under `atlas-platform/`):
- `src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs` (771 lines; the generic section service)
- `src/BBH.CP.Atlas.Service/Platform/` (Sections, Rules, Security, Registry, Dashboard, Audit, Options, `AtlasLog.cs`, `AtlasJson.cs`, `AtlasPlatformServices.cs`)
- `src/BBH.CP.Atlas.Data/Platform/` (EF shared-type context, table registry, metadata reader, value converter, `Dto/CPPlatformDto.cs`)
- `src/BBH.CP.Atlas.App/Platform/` (`AtlasStartupCheck.cs`, `AtlasConfigLint.cs`, `AtlasPlatformStartup.cs`, DI extensions, problem filter)
- `WebUi/src/app/platform/` (Angular 21; `form/`, `dashboard/`, `*.spec.ts`)
- `tests/BBH.CP.Atlas.Platform.Tests/`
- Stand-in files (`StandIn/` folders, `WebUi/src/app/stand-in/`, `BBH.CP.Atlas.Infra`) are local only and not copied.

## 3. The steps

Each step lists: goal, files, the exact change, tests to pin or add, expected test counts, package and INTEGRATION effects, and the "prove nothing changed" check.

### Step 1. Delete dead code (F09, F10, F06 part, F14 comment, F19)

Goal: subtract what nothing reads. No behavior change.

C#:
- Delete the 9 duplicate `public static readonly EventId` fields: `ConfigSectionService.cs:67-69` (`FieldIgnored`, `SaveFailed`, `AfterSaveFailed`), `Security/AtlasAccessService.cs:34`, `Registry/AtlasConfigStore.cs:41`, `Registry/AtlasModuleResolver.cs:40`, `App/Platform/AtlasPlatformStartup.cs:19-20`, `App/Platform/AtlasProblemExceptionFilter.cs:15`. `AtlasLog.cs` keeps all IDs. Grep first: no reader outside the declaring files (verified at review time).
- Delete `BeginSharedTransactionAsync` (`Data/Platform/AtlasDataExtensions.cs:29-41`) and `FindActiveAsync` (`:98-103`). Change `tests/.../Data/AtlasPlatformContextTests.cs` lines 58, 102, 122, 123, 134, 135, 170, 233, 270 to the sync `FindActive(...)` and `BeginSharedTransaction(...)`. Fix the `<see cref>` in `AtlasPlatformContext.cs:10`.
- Dead code paths behind JSON keys with no reader. **Keep the DTO properties** (JSON keys are contracts): `CPEffect.OptionsFromChild` (`Dto/CPPlatformDto.cs:217`), `CPPermissions.Execute` (`:158`), `CPButtonAction.Set` (`:233`). Add one XML comment to each: "Reserved; not read by the platform in P1." Nothing else to delete for them (there is no code).
- `AtlasRuleContext.States` (`Rules/AtlasRuleEngine.cs:7, 154`): keep the record member and the `Matches` line (the DTO `CPRule.States` is a target contract and the engine should still honor it when a caller passes states); no change. Remove only the comment-free `[]` literal duplication by adding a static `AtlasRuleContext.NoStates` if it reads better; optional.
- `AtlasRuleEngine.ApplyToSection:132-135`: the line `if (result.Enable.Contains(child.Name)) disabled = false;` repeats the decision already made in the ternary on line 132 when `result.Disabled` is true; when `result.Disabled` is false, `Disable` and `Enable` can both name the child only in `FirstMatch` mode. Keep the behavior exact: replace the two statements with one expression `child.Disabled = child.Disabled || (result.Enable.Contains(child.Name) ? false : result.Disabled || result.Disable.Contains(child.Name));` and keep `RuleEngineTests` green. If any `RuleEngineTests` case fails, revert this bullet and leave a note; it is cosmetic.

TypeScript (`WebUi/src/app/platform/`):
- `form/atlas-effects.ts:1-3` header: the server also applies Enable, Disable, Set, Clear and SetFromOption (B35). Fix the comment.
- `form/atlas-form.component.ts:373` `export { problemText }`: delete (no importer; the stand-in imports from `atlas-errors.ts`). Grep `problemText` in `WebUi/src` to confirm.
- `form/atlas-renderer-registry.ts:75-77` `types()`: delete, and delete the spec lines in `platform.spec.ts` that call it (look near line 228). Keep `register()` for step 2/16 later.
- `atlas-history-dialog.ts:14, 132` `event` is built and never rendered: delete the field and its assignment.
- `atlas-platform.scss`: delete `.radio-group` (`:59-65`), `.grid-column-start-end-1-2` and `.p-8` (`:119-126`) only if `grep -rn` in `WebUi/src/app/platform` and `src/BBH.CP.Atlas.App/Config` finds no user. Note: `TTDV2.json` uses `grid-column-start-end-1-2` in `DivClass` (line 123), so **keep that rule**. Delete `.atlas-muted` (`:290-292`) only if the platform files do not use it (the stand-in has its own copy).
- `form/atlas-validators.ts:168, 171`: replace `validator.Min!` / `validator.Max!` with the narrowed values (`hasMin` / `hasMax` are already computed at `:162-163`; store `const min = validator.Min` and use `min !== null && min !== undefined`).

Tests: no new tests. Expected: `dotnet test` 295; `ng test` 77 minus the `types()` cases (count them; expect 75 or 76; record the number).
Package: yes (COPY files). INTEGRATION.md: no.
Prove: all gates green; `grep -rn "FindActiveAsync\|BeginSharedTransactionAsync\|types()" atlas-platform/src atlas-platform/WebUi/src/app/platform` returns nothing.

### Step 2. TypeScript duplication (F13, O15, O16, O17)

Goal: one copy of each idea in the client. No behavior change.

- New `WebUi/src/app/platform/atlas-dates.ts` with `parseLocalDay(text: unknown): Date | null` (the regex `/^(\d{4})-(\d{2})-(\d{2})/` and `new Date(y, m - 1, d)`), plus `isNil(value: unknown): value is null | undefined`. Header comment: `// COPY to the target: WebUi/src/app/platform/.`
- Use it in `dashboard/atlas-dashboard-columns.ts` (`formatDate:16-32`, `cellDay:39-52`; delete the `MONTHS` array at `:13` and format with Angular `formatDate(day, 'dd-MMM-yyyy', 'en-US')`), `form/renderers/atlas-grid-renderer.ts` (`dateHeader:167-174`), `form/atlas-form-model.ts` (`toModelValue` date case `:47-50`). Check the existing spec expectations for the exact text of `formatDate` (`platform.spec.ts`) before you change the formatter; the output text must not change.
- Replace the 11 `!== null && !== undefined` / `=== null || === undefined` chains with `isNil` (`atlas-effects.ts:24-30`, `atlas-validators.ts:150-163`, others found by grep).
- `form/renderers/atlas-renderers.ts:89-96` and `:113-119` identical `options` getters: move one memoized `toSelectOptions` getter into `AtlasRendererBase` (or a small `memoSelectOptions()` helper) and delete the two copies.
- `dashboard/atlas-action-menu.ts:25-42` three identical `@if (show('X'))` blocks and `:72-74` string dispatch: one `ITEMS` table `{ name, label, run: (actions, row) => ... }` and one `@for`. Keep the rendered markup and classes the same (the spec and the target look depend on them).
- `form/atlas-form.component.ts:303-306` and `:321` identical HTTP error handlers: one private `httpFail(error)`.

Tests: no new tests. Expected: `ng test` same count as after step 1; `dotnet test` 295.
Package: yes. INTEGRATION.md: no.
Prove: gates green; `grep -c "new Date(Number" WebUi/src/app/platform -r` = 1.

### Step 3. One decision for navigation and values; delete the client-only account split (F12, F15, F14)

- `dashboard/atlas-dashboard.component.ts`: inject `AtlasDashboardNavigation` (`atlas-navigation.ts:42`) and call `open(code, id, target?)` from `editRow`/`open(row)` and `onButton`. Delete the component's own `apps` load, the `forkJoin` (`:86`), `navigate` (`:170-174`) and the `formTypeOf` import if unused. Keep `rows()` in the service as is.
- `form/atlas-form-model.ts:61-75` `toOutputValue`: switch on `valueKind(control)` (as `toModelValue` does), not on the runtime type. Keep the outputs identical: Date -> `yyyy-MM-dd`, boolean -> `'true'`/`'false'`, list -> joined with `LIST_SEPARATOR`, else `String(value)` or `null`.
- `form/atlas-effects.ts:43-53` `optionData`: delete the `' - '` fallback. Return `option?.Data ?? null`. Reason: the server (`AtlasServerEffects.Values`) uses only `option.Data`; the fallback made the client and the server disagree for options with no `Data`. `PBAccounts` already sets `Data`.

Tests: pin first. Add to `platform.spec.ts` one case for `optionData` with no `Data` returning `null` (replace any existing case that expects the split). Check `platform.component.spec.ts` dashboard tests still pass with the service injected (they may need `AtlasDashboardNavigation` in the TestBed providers; it is `providedIn: 'root'` so usually nothing to add).
Expected: `ng test` count +1 or unchanged; `dotnet test` 295.
Package: yes. INTEGRATION.md: no (the dashboard component is marked "reference").
Prove: gates green; in the local browser, open the TTDV2 dashboard, click a row, click Add New: both open the form.

### Step 4. Rule outcome instead of mutate-then-diff (F01, O4)

Goal: the rule engine says what it hid or disabled; the field list is built once per request.

- `Rules/AtlasRuleEngine.cs`: add `public sealed record AtlasRuleOutcome(IReadOnlySet<string> HiddenByRule, IReadOnlySet<string> DisabledByRule)` (names of controls, case-insensitive sets). Change `IAtlasRuleEngine.ApplyToSection` to return `AtlasRuleOutcome` (a `void` method becoming a method with a return value is source-compatible for callers that ignore it). Inside `ApplyToSection`, when a control or child goes from `Visible` to not visible, add its name to `HiddenByRule`; from enabled to `Disabled`, add to `DisabledByRule`. For a grid control, also add each `Grid.Columns[].field` so grid fields are covered as `AtlasFormFields.MarkHiddenByRules` covers them today.
- `Sections/AtlasFormFields.cs`: add `public static AtlasFormFields From(CPApplicationSection section, AtlasRuleOutcome outcome)` that sets `HiddenByRule`/`DisabledByRule` from the sets. Keep `From(section)` (used by the start-up check). Delete `MarkHiddenByRules` after the callers are gone.
- `ConfigSectionService.cs`:
  - `LoadPage` (`:146-154`): build `fields` after the rules once: `AtlasFormFields fields = AtlasFormFields.From(section, outcome)`; `BindValues`/`LoadOptions`/`OnLoad` need the fields before the rules today (`OnLoad` runs before the rules by design, B8). Keep that order: build `fields` before the rules (as now), apply the rules and get `outcome`, then set `HiddenByRule` on the same `fields` instance from `outcome` (add `AtlasFormFields.ApplyOutcome(outcome)`), and let `ClearRuleHiddenValues` take `fields` instead of rebuilding. Net: `From` is called once in the load.
  - `SavePage` (`:305-308`): `var outcome = Services.Rules.ApplyToSection(...); AtlasFormFields fields = AtlasFormFields.From(section, outcome);`. Delete `beforeRules`.
  - `GetAuditLog` (`:684`) and `RuleHiddenFields` (`:194-209`): replace with `HashSet<string> hidden = HiddenNamesAndColumns(form, outcome)` where the outcome comes from one `ApplyToSection` on a fresh form copy (the form is still needed to apply the rules; keep that one call, delete the two `From` calls and the `MarkHiddenByRules`). The set must still contain both `Name` and `Field` of each hidden field, because `AtlasAuditMask` matches on field names.
- Delete `MarkHiddenByRules`. Grep for other callers (none expected outside the service).

Tests to pin (already exist, run them first and after): `Ttdv2SectionTests`: `An_effect_cannot_show_a_field_that_a_rule_hid`, `The_server_applies_Set_and_Disable_effects_as_the_client_does`, the RV-02 tests (rule-hidden values cleared on load, save outputs, History masked), and `SectionStepTests`. Expected: 295.
Package: yes. INTEGRATION.md: no.
Prove: gates green; `grep -rn "AtlasFormFields.From" atlas-platform/src` count drops from 9 to about 5 (load 1, save 1, history 1, start-up check 2).

### Step 5. Split `SavePage` (F02, O3)

Goal: eight named steps over one state record; no behavior change.

- In `ConfigSectionService.cs` add a private sealed class `SaveState` with: `Manifest`, `Form`, `Table`, `Input`, `Result`, `Errors` (list), `Stored` (nullable record), `IsNew`, `Previous`, `Section`, `Fields`, `Submitted`, `Derived`, `Computed`, `Accepted`, `Record`. Keep the same local names as today so the diff is readable.
- Methods, each 10 to 30 lines, called in this order from `SavePage`: `LoadStored(state)` (steps 1-2 incl. the RV-04 create check), `ApplyRules(state)` (step 3), `ReadAndFilter(state)` (`ReadOutputs` + `AtlasServerEffects.Apply` + the allow list + the `FieldRefused` log), `ValidateFields(state)` (step 6, incl. the grid "empty = 0" rule), `CheckVersion(state)` (step 7), `MapRecord(state)` (step 8 + effects values + defaults), `ComputeAndSystemFields(state)` (Computed + step 9), `RunBeforeSave(state)`, `Persist(state)` (rowversion original value + `SaveWithAudit` + `OnAfterSave` + outputs). `SavePage` becomes about 25 lines: build the state, call the steps, return `Failed(...)` at the first non-empty `Errors` where the code returns today (after step 8 mapping, after `OnBeforeSave`, after `SaveWithAudit`). Keep the exact points where `Failed` is returned; they decide which errors a client sees together.
- `AtlasAllowList.Check` (`Sections/AtlasAllowList.cs`): keep the public class and signature (public, used by `SectionStepTests`), but the service now calls a private `FilterSubmitted(state)` that contains the loop. If you prefer one copy, make `AtlasAllowList.Check` call a shared private implementation; do not duplicate the loop. The lambdas `isServerSet` and `optionError` become private methods `IsServerSet(state, field)` and `OptionError(...)` (the latter already exists).

Tests to pin: all of `Ttdv2SectionTests` and `SectionStepTests` (they cover refusals, options, version, defaults, computed, audit, hooks). Expected: 295.
Package: yes. INTEGRATION.md: no.
Prove: gates green; `SavePage` is under 40 lines; no method in the file is over 60 lines.

### Step 6. Pilot without C# (F03, O9)

Goal: TTDV2 is JSON, SQL and one registration line.

- `Service/Platform/Options/AtlasOptionProviders.cs`: add `YearsOptionProvider(TimeProvider time)` with `Name => "Years"`, reading `request.Source.Parameters`: `Before` (default `3`), `After` (default `1`); values from `year + After` down to `year - Before` as text (same order as `TaxYearsOptionProvider`: next year first). Add `WeekEndingsOptionProvider(TimeProvider time)` with `Name => "WeekEndings"`, parameters `Count` (default `4`), `DayOfWeek` (default `Sunday`): the last such day (today if today is that day) and the next `Count - 1`, as `yyyy-MM-dd` (same list as the app provider today). Parse the parameters with `int.TryParse` / `Enum.TryParse`; an invalid value falls back to the default. Register both in `AtlasPlatformServices.Add`.
- `src/BBH.CP.Atlas.App/Config/TTDV2.json`: `TaxYear` gets `"OptionsSource": { "Provider": "Years", "Parameters": { "Before": "3", "After": "1" } }`; `WeekEnding` gets `"OptionsSource": { "Provider": "WeekEndings", "Parameters": { "Count": "4", "DayOfWeek": "Sunday" } }`. The defaults `{Options[1]}` and `{Options[0]}` stay valid (same order).
- Delete `src/BBH.CP.Atlas.Service/Application/CPTTDV2Service.cs` and the folder `src/BBH.CP.Atlas.Service/Application/TTDV2/`. The `OnLoad` header hook is dead (the client sets the headers from `HeaderFromDate`; `atlas-grid-renderer.ts:126`).
- `src/BBH.CP.Atlas.App/StandIn/StandInHost.cs`: change `AddAtlasModule<CPTTDV2Service>("TTDV2")` to `AddAtlasModule<ConfigSectionService>("TTDV2")`. Grep for `CPTTDV2Service` everywhere (tests, docs, `INTEGRATION.md`, `local/make-target-package.py`, `Config/README.md`, `src/BBH.CP.Atlas.Service/Platform/README.md`).
- `TTDV2.app.json`: `"Service": "TTDV2"` would now give the start-up warning "does not match the registered class ConfigSectionService". Change it to `"Service": null` (the key stays; the owner kept the settings). Confirm `--atlas-check` shows 0 warnings.
- `local/make-target-package.py`: remove the two `COPY` lines for `CPTTDV2Service.cs` and `TTDV2/`.
- `INTEGRATION.md`: section 1 row for `Application/` becomes `ConfigSectionService.cs` only; section 2 `program.cs` row: `AddAtlasModule<ConfigSectionService>("TTDV2")`; section 4.1 row "TTDV2 module code" becomes "No module class. JSON: 19 + ~214 + 92 lines, SQL 040, one registration line. Baseline for the next apps." Add a B76 row in section 6: "Review FABLE F03: the OnLoad header hook duplicated the client HeaderFromDate; the two date providers are platform providers `Years` and `WeekEndings` with `Parameters`."
- `docs/atlas-platform/12-ttd-pilot-build.md` section 3 table row "Service": add one line "Changed 2026-10-31 (FABLE F03): no class."

Tests: `Ttdv2SectionTests` use `SectionHarness`, which registers the TTDV2 module; change the harness to `ConfigSectionService` where it names `CPTTDV2Service`. Any test that asserts the `OnLoad` header text (grep `dddd dd-MMM` and `headerName` in tests) must change to assert the JSON `HeaderFromDate` is present instead, or be deleted if it only tested the dead hook. Add two unit tests (no database) for the new providers: the list for a fixed `TimeProvider` date, and the defaults when `Parameters` is null. Expected: 295 plus 2, minus any deleted header test.
`--atlas-check`: must still be 0 errors, 0 warnings (the start-up check verifies that the providers named in the JSON exist: `AtlasStartupCheck.CheckForm:215-228`).
Package: yes (COPY list changes). INTEGRATION.md: yes.
Prove: gates green; in the browser, a new TTDV2 record shows Tax Year default = this year and Week Ending default = last Sunday, and the grid headers show the week days.

### Step 7. Options pattern for the platform settings (F05, O8)

- New `Service/Platform/Registry/AtlasPlatformOptions.cs`: `public sealed class AtlasPlatformOptions { public string ConfigFolder { get; set; } = "Config"; public int ConfigRefreshSeconds { get; set; } = 2; public AtlasStartupErrorMode? OnStartupError { get; set; } ... }`. Bind from `configuration.GetSection("AppSettings")` in `AddAtlasPlatform`. Because the enum parsing today rejects numbers and accepts any case, bind the raw text (`string? OnStartupError`) and validate with one shared `AtlasEnumText.TryParse<T>(text, out value)` used by both `AtlasConfigStore.Override<T>` and `AtlasPlatformStartup.OnStartupError`; delete the second parser. Module overrides stay as they are in `AtlasConfigStore.ApplyEnvironmentOverrides` (step 9 moves them).
- `AtlasPlatformConfiguration` stays for now (step 9 decides); or fold `Sources` into the options class and delete it here. Either is fine; say which.
- `ConfigRefreshSeconds` goes away in step 9; bind it here anyway so step 9 is a deletion.

Tests to pin: `RegistryTests` for settings (`The_platform_settings_come_from_the_configuration_given_to_AddAtlasPlatform`, HO-03 tests, `A_setting_OnStartupError_that_is_not_valid_is_a_check_error`, `OnStartupError_decides_if_one_bad_app_stops_the_host`). Expected: unchanged count.
Package: yes. INTEGRATION.md: no (same keys, same defaults).
Prove: gates green; `grep -rn '"AppSettings:' atlas-platform/src` shows one place for each key.

### Step 8. Drop the four single-implementation interfaces (F07, O2)

- Delete `IAtlasAccessService` (`Security/AtlasAccessService.cs:17-30`), `IAtlasRuleEngine` (`Rules/AtlasRuleEngine.cs:36-47`), `IAtlasAuditWriter` (`Audit/AtlasAuditWriter.cs:25-32`), `IAtlasDashboardService` (`Dashboard/AtlasDashboardService.cs:18-21`). Register and inject the sealed classes (`AtlasPlatformServices.Add`, `AtlasModuleServices` constructor and properties, `AtlasDashboardService` constructor, `ConfigSectionService`). Move the XML doc comments from the interfaces to the classes.
- Keep `IAtlasConfigStore`, `IAtlasModuleResolver`, `IAtlasUserContext`, `IAtlasUserDirectory`, `IAtlasCurrentUserSource` (faked in tests or implemented at the target) and `IAtlasOptionProvider`, `IAtlasModule`.
- `AtlasModuleServices` is public; its constructor changes type names. No production code constructs it (DI does). `SectionHarness` resolves it from DI. Grep `IAtlasRuleEngine|IAtlasAccessService|IAtlasAuditWriter|IAtlasDashboardService` across `src`, `tests`, docs.

Tests: unchanged count. Package: yes. INTEGRATION.md: add one line to the `AtlasModuleServices` note if it names the interfaces (grep).
Prove: gates green.

### Step 9. Config read once at start-up (F04, F08, O1, O10)

Owner decision: yes. This is the largest step; do it after 1-8 so the diff is small.

Design:
- New `Service/Platform/Registry/AtlasApp.cs`: `public sealed class AtlasApp { AtlasAppManifest Manifest; string? FormText; string? DashboardText; string? DisabledReason; CPAtlasApplicationConfig Form() => AtlasJson.Deserialize<...>(FormText, Manifest.Form); CPAtlasDashboard? Dashboard() => ...; }` (each call returns a fresh copy, as today).
- New `AtlasAppCatalog` (singleton): built one time by `StartAtlasPlatformAsync` (and by `RunAtlasCheckAsync`) from the folder: for each `*.app.json`, parse the manifest, apply the environment overrides (move `ApplyEnvironmentOverrides` and `Override<T>` here or into a small `AtlasManifestReader`), read the form and dashboard text (with the HO-09 path check), and record errors per file. A file that cannot be read gives an `AtlasApp` with `DisabledReason` (or no app when there is no manifest) and a `ConfigErrors` entry, so the start-up check reports it and the resolver answers 404; the host keeps running (HO-02 at start-up).
- `AtlasConfigStore` implements `IAtlasConfigStore` over the catalog: `GetManifests`, `FindManifest`, `GetForm`, `GetDashboard`, `ConfigErrors`, `ConfigFolder`. Delete `_files`, `_lock`, `_snapshot`, `ManifestSnapshot`, `Snapshot()`, `Build()`, `ReadText()` caching, `_refresh`, `_time`, the folder-signature code, and the `TimeProvider` constructor parameter. The interface does not change.
- `AtlasDisabledApps` and `AtlasStartupErrorMode`: move `DisabledReason` onto `AtlasApp`; `AtlasPlatformStartup.DisableApps` writes it there; `AtlasModuleResolver` reads it. Delete `AtlasDisabledApps` and `AtlasPlatformStartupGate` (the start-up runs in the hosted service; keep `StartAtlasPlatformAsync` idempotent with a simple `Interlocked`/`Lazy<Task>` on the catalog). Keep `AtlasStartupErrorMode` and the `OnStartupError` setting.
- `AtlasModuleResolver.Get` hands the module its `AtlasApp`: add `AtlasApp App { get; set; }` to `IAtlasModule`? No: that is a public interface change. Instead keep `IAtlasModule.AppCode` and add to `ConfigSectionService` a lazily resolved `protected AtlasApp App => _app ??= Services.ConfigStore.GetApp(AppCode)` (add `GetApp` to `IAtlasConfigStore` as a default interface member that falls back to `FindManifest` + `GetForm`, so test fakes still compile). Replace the four `Manifest / GetForm / MainTable` triples in `ConfigSectionService` with `App.Manifest`, `App.Form()`, `MainTable()`. Same in `AtlasDashboardService.Load`.
- Settings: delete `ConfigRefreshSeconds` from the options class and from `INTEGRATION.md` section 4 (replace the row with: "Config files are read one time at start-up. After a change to a manifest, form or dashboard file, restart the app pool, as for a table change."). Keep the key accepted-and-ignored (no error if present).

Tests:
- Delete `RegistryTests`: `The_manifests_are_parsed_once_while_the_folder_does_not_change`, `A_changed_manifest_is_seen_after_the_refresh_time`, `A_manifest_file_that_becomes_unreadable_keeps_its_last_good_version`. Keep and adapt `A_manifest_file_that_cannot_be_read_does_not_stop_the_other_apps` (start-up case). Tests that edit a config file and expect the change to be seen must create a new harness/host after the edit (the `SectionHarness.ConfigCopy` pattern already builds a new harness per folder, so most already do); the test `AtlasCheckTests` that relies on a refresh (grep `ConfigRefreshSeconds` in tests) must build a new host instead.
- Add one test: a form file changed after start-up is not seen until a new start-up (pins the new rule).
- Expected: 295 + 2 (step 6) - 3 + 1 = about 295; record the real number.
Package: yes. INTEGRATION.md: yes (section 4 row; B77 row in section 6 with the reason: F04).
Prove: gates green; `--atlas-check` 0/0; the stand-in host starts, serves TTDV2, and a bad manifest file in a copy of `Config` disables only that app (manual check with a second app folder is optional; `AtlasCheckTests` covers it).

### Step 11. Split the start-up check (F18)

- `App/Platform/AtlasStartupCheck.cs`: keep `Run(IServiceProvider)` as the entry point and the `AtlasCheckItem` record. Move each concern into a private static method that returns `IEnumerable<AtlasCheckItem>` and takes only what it needs: `CheckCode`, `CheckSettings`, `CheckRegistration`, `CheckAppRow`, `CheckTables`, `CheckPermissions`, `CheckFields`, `CheckSoftDelete`, `CheckComputed`, `CheckDefaults`, `CheckProviders`, `CheckDashboard`, `CheckRoles`, `CheckControlTypes`, `CheckGridCodes`, `CheckTimes`, `CheckContract`. `Run` loops over the apps and concatenates. Keep every message text exactly (`AtlasCheckTests` asserts them).
- `AtlasConfigLint.CheckForm`: same split (`DuplicateNames`, `EffectTargets`, `EffectCycles`, `RuleTargets`, `Defaults`, `ButtonActions`).
- Optional: replace the reflection walk in `CheckUnknownProperties` with a lint-only deserialization using `JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow }` only if it still reports the JSON path of every unknown member; if it reports only the first, keep the walk. Say which in the commit.

Tests: unchanged. Package: yes. INTEGRATION.md: no.
Prove: gates green; no method in the two files is over 50 lines.

### Step 12. Form component view model and reactive loads (F17, review section 5 row 10, F11-TS B6)

- `form/atlas-form.component.ts`: `fields = computed(() => ...)` giving `{ control, renderer, hasInput, required, error }` per shown child; the template uses it instead of calling `registry.resolve`, `registry.hasInput`, `error(child)`, `isRequired` per field. Split `build()` into `initialFlags(section)` (pure, in `atlas-form-model.ts`), `disableUnsupported()`, `wireEffects()`, `wireComputed()`. Make `form` private or a signal. Replace the hand-made cancel in `load()` (`loadRequest?.unsubscribe()`) with `paramMap.pipe(switchMap(...))`.
- `dashboard/atlas-dashboard.component.ts`: `visibleButtons` becomes `computed`; `paramMap.pipe(switchMap(load))` so a stale load is cancelled.
- Keep the DOM, classes and ids the same (`platform.component.spec.ts` and the target look depend on them).

Tests: unchanged; `platform.component.spec.ts` must stay green without edits other than TestBed providers. Package: yes. INTEGRATION.md: no.
Prove: gates green; browser check of the form: live Weekly Total, required error with aria links, save.

### Step 13. Parity cases for effects and Computed (review section 6.1)

- New `WebUi/src/app/platform/form/atlas-effect-cases.ts` (strict JSON array, same style as `atlas-validator-cases.ts`): 6 cases for `matches` (`HasValue` true/false, `Equals` case-insensitive, `NotEquals`, `In`, no `When`). Read it in `platform.spec.ts` and in a new C# `EffectParityTests` (like `ValidatorParityTests`, which reads the TS file from `TestPaths`).
- Computed: today the TS `computeValue` treats empty as 0 and the C# `AtlasComputedFields` leaves empty out. Same result for `Sum`, different for `Count`, `Min`, `Max`. Decide one rule (recommend: leave empty out on both sides, as the server does), fix the TS, and add 4 shared cases.

Tests: +6 TS cases, +6 C# cases (one Theory), +4 and +4. Package: yes. INTEGRATION.md: no.
Prove: gates green.

### Step 14. Tailwind class list to a safelist line (O18)

- `WebUi/src/app/platform/atlas-tailwind-classes.ts` exists only so the target Tailwind scan generates classes that appear in server JSON. Add to `INTEGRATION.md` section 2 a row for `tailwind.config.js`: `safelist: [{ pattern: /^(grid|grid-cols-\d+|gap-\d+|items-start|p-\d+|bg-white)$/ }]` built from the classes that `TTDV2.json` uses (read them from the JSON; list them exactly). Keep the TS file until the target confirms the safelist works (delete it in a later release); note that in the row. If the local `tailwind.config.js` exists in `WebUi/`, add the same safelist there and remove the import of the TS file from the local build to prove the classes are still generated (check the TTDV2 form layout in the browser).

Tests: unchanged. Package: yes. INTEGRATION.md: yes.

## 4. Test hygiene (do inside the steps that touch the files, or as a last step 15 if the owner agrees)

From `FABLE_REVIEW.md` section 4.1 and 4.3, in priority order:
1. `tests/.../TestPaths.cs:11-12`: find the repo root by `atlas-platform` alone; skip the 3 tests that read `target-implementation-read-only` when the folder is absent (`StandInSmokeTests.cs:24`, `RegistryTests.cs:219, 230`). A fresh clone must run the suite.
2. Delete or trim: `StandInSmokeTests.cs:35`, `:21`; `EndpointTests.cs:137`, `:144` (move two asserts to `RegistryTests`); `AccessServiceTests.cs:131` (keep `MatchesAny` lines); `RegistryTests.cs:274`; `Ttdv2SectionTests.cs:340` (keep line 349), `:452`, `:475` (replace with a user-visible assertion or delete); `AtlasPlatformContextTests.cs:187`, `:265-266`; `UserContextSqlTests.cs:74`; `AtlasCheckTests.cs:382`; Angular `platform.spec.ts:150-162` (after step 16 of the registry, if `register` goes), `:426-438`.
3. Add the two hook tests (`OnLoad`, `OnBeforeSave` adding an error) through a small test module class in `SectionHarness` (highest-value missing coverage).
4. Shared stubs in `TestSupport` (one `EmptyConfigStore`, one `TestHost`, one `FixedTimeProvider`); `SectionHarness.ConfigCopy` as `IDisposable` with `Replace(file, old, new)`; one `InsertGeneralAsync` in the fixture; one script runner with `stopOnError`.
5. `SectionHarness` static `_registry`: load it in the fixture `InitializeAsync`.
6. Single source for the SQL scripts: point `SqlServerDatabaseFixture.cs:33-35` at `sql/release-p1`, delete the `docs/atlas-platform/sql/12-*.sql` copies and `ReleaseScriptTests` (ask the owner first: the docs copies are referenced from the module 12 spec).

## 5. Not in scope (say no if asked)

- Step 10 (manifest settings): the owner said no.
- Anything in `target-implementation-read-only/`.
- Async controller methods, the `' - '` option format at the target, Tailwind config at the target: integration requirements only, written in `INTEGRATION.md`.
- SQL script changes in `sql/release-p1` (the `090` time window and log row are DBA notes in the review, not Phase B work).
- New packages.

## 6. Hand-off at the end

Update the top entry of `CODEX_HANDOFF.md` with: steps done (commit per step), test counts after each step, package rebuilt (commit), INTEGRATION.md rows changed (B76, B77), open items, and the next-session prompt. Add a row 22 to `docs/atlas-platform/README.md`. Give the AGENTS.md completion summary.

## 7. Next-session prompt

"Resume. Read the top entry of CODEX_HANDOFF.md, then docs/atlas-platform/FABLE_IMPLEMENTATION_PLAN.md. Do Phase B step 1. One step per commit, all five gates after each step, no push. Keep the broad goal: seamless integration at the target, platform generic and simple, nothing breaks the existing system."
