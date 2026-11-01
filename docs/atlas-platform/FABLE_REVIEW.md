# Atlas platform: independent code review and simplification plan (Phase A)

Date: 2026-10-31. Branch `console-code-first-0518`, HEAD `fb8f3eb`. Reviewer: Claude Fable 5.1.
Scope: `atlas-platform/` only (C#, Angular, tests, SQL). The workflow engine is out of scope. `target-implementation-read-only/` was not opened.
No code was changed. Baseline measured at review start: `dotnet build` 0 warnings; `dotnet test` 295 passed; `ng test` 77 passed.

Standards used: Uncle Bob ub-mode (one decision in one place, subtract before you add, validate at the boundary, pure business logic, behavior tests, CRAP < 10, no duplication at >= 0.8, delete dead code); the Angular style guide and Angular 21 practice; Microsoft .NET guidance (C# conventions, Framework Design Guidelines, ASP.NET Core and EF Core practice); and the goal "16 more apps by JSON".

All complexity (CC) and duplication figures are estimates made by reading the code. The crapper, dryer and mutator tools are not installed.

---

## 1. Verdict (one page)

**Is this the simplest design that meets the goal?** No, but it is the right design. The architecture is sound and I would keep it: a JSON manifest per app, a form JSON and a dashboard JSON, SQL metadata read into EF Core shared-type entities (no C# class per table), one generic section service, one rule engine, one access check, one start-up check, and a thin Angular form and dashboard that render the JSON. That is the correct shape for "16 apps by JSON". The pure parts are good: `AtlasValueConverter`, `AtlasTabQueryBuilder`, `AtlasFieldValidator`, `AtlasRuleEngine.Evaluate`, `AtlasComputedFields`, `AtlasDefaultTokens`, the TS validators and effects. They have one job, no I/O, and real tests.

**Where it is not simple.** The code carries the marks of three review waves. Each finding was fixed with an addition, rarely with a subtraction. The result is about 5,400 lines of production C# and 2,500 of TypeScript for a platform whose core idea needs roughly 70% of that. The specific weight:

1. **Rules are applied by mutating the DTO, then diffing two snapshots** of the field list to find what a rule hid. `AtlasFormFields.From` runs 9 times across the code and 2 to 3 times per request. The rule engine knows what it hid; it should say so.
2. **The save is one 180-line method** (`SavePage`, CC about 35). The B45 extraction moved parts into static classes with 8 parameters and 3 callbacks (`AtlasAllowList.Check`), which moved complexity instead of removing it.
3. **The config store is a live-reload cache** (263 lines: `ConcurrentDictionary`, `Lock`, `Volatile`, folder signature, 2-second refresh, "last good version") for a folder of a few small files, while the table registry needs a restart anyway. One rule ("config changes need a restart") would delete about 130 lines and a setting.
4. **Knobs with one real value or no reader.** `AuditMode.Client` (0 uses), `AuditTime` next to `SystemTime`, `AttachmentTable`, `Service`, `SchemaVersion`, `CPEffect.OptionsFromChild` (0 uses in C# and TS), `CPPermissions.Execute` (0), `CPButtonAction.Set` (0), `CPOptionsSource.Parameters` (declared, never read), `AtlasRuleContext.States` (always empty), 9 duplicate `EventId` fields, 2 unused async overloads.
5. **Eleven interfaces, four with one implementation and no test fake** (`IAtlasAccessService`, `IAtlasRuleEngine`, `IAtlasAuditWriter`, `IAtlasDashboardService`).
6. **The pilot still needs C#.** `CPTTDV2Service` has a hook that duplicates the client `HeaderFromDate` (the client overwrites the header the server computed), and two option providers that are generic date lists. With `OptionsSource.Parameters` (already in the DTO, never read) they become platform providers and TTDV2 needs zero C#.
7. **Front end:** good overall (strict, zero `any`, OnPush, signals, pure model functions). Excess is local: four copies of a date parser, a mutable renderer registry whose write API only tests call, dashboard navigation decided in two places, a 373-line form component.

**What I would build if I started today.** The same shape, with these differences: configuration loaded once at start-up into an immutable `AtlasApp` (manifest + form + dashboard + table) so no service re-resolves manifest, form and table on every call; a rule engine that returns an outcome (hidden, disabled, required sets) instead of mutating and diffing; a save split into 8 named steps over one `SaveState` record; one time setting per app; one `AtlasOptions` class bound with the options pattern instead of string keys in four places; no interface for a class with one implementation; parameterized option providers so the pilot has no C#; and about 170 fewer lines of TypeScript. The current code is a few days of careful refactoring away from that, not a rewrite. Nothing in the plan below changes a public HTTP route, a response shape, a JSON key that a target file uses, a SQL script, or a DI method signature.

**Honest summary.** The design is right. The code is about 25-30% heavier than it needs to be, mostly from review-driven additions. The highest-value simplifications are also the lowest-risk ones (steps 1-6 in section 8). The one change that needs an owner decision is the config store (step 9), because it removes live reload of JSON files.

---

## 2. Findings ranked by impact

Format: file:line; standard broken; example; simpler alternative; compatibility risk (none / additive / breaking + the contract).

### F01. Rules applied by mutate-then-diff; field list rebuilt 2-3 times per request
- `atlas-platform/src/BBH.CP.Atlas.Service/Application/ConfigSectionService.cs:305-308` (save), `:173-191` (`ClearRuleHiddenValues`), `:194-209` (`RuleHiddenFields`); `Service/Platform/Sections/AtlasFormFields.cs:140-148` (`MarkHiddenByRules`); `Service/Platform/Rules/AtlasRuleEngine.cs:104-149` (`ApplyToSection` mutates `control.Visible/Disabled`).
- Standard: one decision in one place; business logic in pure functions.
- Example (save):
  ```csharp
  AtlasFormFields beforeRules = AtlasFormFields.From(section);
  Services.Rules.ApplyToSection(section, form.Rules, RuleContext(...), manifest.RuleMode, manifest.RuleDefault);
  AtlasFormFields fields = AtlasFormFields.From(section);
  fields.MarkHiddenByRules(beforeRules);
  ```
  `GetAuditLog` repeats the same four lines on a fresh form copy only to learn which names were hidden. `AtlasFormFields.From` is called 9 times in the code.
- Simpler: `ApplyToSection` returns `AtlasRuleOutcome(IReadOnlySet<string> Hidden, Disabled)`; `AtlasFormFields.From(section, outcome)` sets `HiddenByRule`/`DisabledByRule` once. `RuleHiddenFields` becomes `outcome.Hidden`. Deletes `MarkHiddenByRules`, the before/after pairs and `RuleHiddenFields` (about 45 lines). The rule engine stays pure.
- Risk: none (internal).

### F02. `SavePage` is one 180-line method, CC about 35
- `ConfigSectionService.cs:271-452`.
- Standard: CRAP < 10 (CRAP >= CC even at full coverage, so CC must be under 10); small functions.
- Example: numbered comments `// 1-2.`, `// 3.`, `// 4.`, `// 6.`, `// 7.`, `// 8.`, `// 9.`, `// 11-12.` inside one method, plus a local function `Current` and four lambdas passed to `AtlasAllowList.Check`.
- Simpler: a `SaveState` record (manifest, form, table, fields, stored, previous, submitted, accepted, errors, derived, computed) and eight private methods of 10-25 lines each: `LoadStored`, `ApplyRules`, `ReadAndFilter`, `Validate`, `CheckVersion`, `MapRecord`, `SetSystemFields`, `Persist`. `AtlasAllowList.Check` (8 parameters, 3 delegates, `AtlasAllowList.cs:16-24`) becomes `FilterSubmitted(SaveState)` with direct access to the state; the `isServerSet` lambda becomes a method `IsServerSet(field)`. Same behavior, pinned by the existing `Ttdv2SectionTests` and `SectionStepTests`.
- Risk: none. `AtlasAllowList` is public but only `ConfigSectionService` and `SectionStepTests` call it; keep it as a thin forwarder if the owner wants zero public-surface change.

### F03. The pilot needs C# that the platform could own
- `src/BBH.CP.Atlas.Service/Application/CPTTDV2Service.cs:18-29`; `Application/TTDV2/TTDV2OptionProviders.cs`; `Data/Platform/Dto/CPPlatformDto.cs:199` (`CPOptionsSource.Parameters`, declared, never read: grep finds only the declaration).
- Standard: goal 4 (JSON-only apps); delete dead code.
- Example: `OnLoad` sets `headerName` for 7 columns from `WeekEnding`. The client renderer (`WebUi/src/app/platform/form/renderers/atlas-grid-renderer.ts:126`, `def.headerName = this.headerName(column)`) overwrites it from `HeaderFromDate` in `TTDV2.json`. The server hook is dead for the platform client. `TaxYearsOptionProvider` and `WeekEndingsOptionProvider` are "N years around now" and "N week-end days from now".
- Simpler: delete the hook; add two platform providers `Years` (`Parameters: { "Before": "3", "After": "1" }`) and `WeekEndings` (`Parameters: { "Count": "4", "DayOfWeek": "Sunday" }`) in `AtlasOptionProviders.cs`; `TTDV2.json` names them with parameters; `program.cs` registers `AddAtlasModule<ConfigSectionService>("TTDV2")`. The pilot then proves the claim "a new app is JSON, SQL and one registration line".
- Risk: additive for the platform (new providers, `Parameters` starts to be read). Deleting `CPTTDV2Service.cs` and `TTDV2/` removes two files from the copy list and one line in INTEGRATION.md section 1 and 4.1 (the "one class with one hook" acceptance row). Changes the target package. Alternative with no package change: keep the files, make them one-liners that return the platform providers.

### F04. Config store: live reload for a folder that needs a restart anyway
- `src/BBH.CP.Atlas.Service/Platform/Registry/AtlasConfigStore.cs:47-49, 75-164, 201-210`; setting `AppSettings:ConfigRefreshSeconds` (INTEGRATION.md section 4).
- Standard: subtract before you add; one decision in one place.
- Example: `ConcurrentDictionary<string,(DateTime,string)> _files`, `Lock _lock`, `Volatile.Read/Write`, `ManifestSnapshot` record with a folder signature of `path:ticks:length`, "last good version" per file, and a `TimeProvider` only for the refresh clock. INTEGRATION.md B31 already states that a new table "needs a restart"; B29 says manifests are re-read every 2 seconds. So a live edit is half-honored: a new field in the form JSON is live, a new column is not.
- Simpler: read all manifests, forms and dashboards once in `StartAtlasPlatformAsync` (the hosted service already runs before requests) into an immutable `AtlasAppCatalog`; `GetForm`/`GetDashboard` return a deep copy (`AtlasJson.Deserialize` of the cached text, as now). A file that cannot be read disables its app at start-up (same outcome as HO-02 at start-up; the "last good version while running" case disappears). One rule for operators: "config change = restart", which is already true for tables. Removes about 130 lines, the `ConfigRefreshSeconds` setting, and 2-3 tests (`The_manifests_are_parsed_once...`, `A_changed_manifest_is_seen_after_the_refresh_time`, `..._becomes_unreadable_keeps_its_last_good_version`).
- Risk: **behavior change** (live JSON reload goes away; `ConfigRefreshSeconds` becomes a no-op). The `IAtlasConfigStore` interface does not change. Needs the owner's decision; INTEGRATION.md section 4 changes one row.

### F05. Settings read by string key in four places; no options pattern
- `AtlasConfigStore.cs:57-59` (`AppSettings:ConfigFolder`, `AppSettings:ConfigRefreshSeconds`), `:215-240` (`AppSettings:Modules:{code}:*`, with hand-rolled enum parsing in `Override<T>`), `App/Platform/AtlasPlatformStartup.cs:26, 81-90` (`AppSettings:Atlas:OnStartupError`, with a second hand-rolled enum parse), `Registry/AtlasPlatformConfiguration.cs` (a wrapper whose only job is to remember which `IConfiguration` was given).
- Standard: Microsoft options pattern; one decision in one place (two enum parsers that both reject numbers, both case-insensitive, written twice).
- Simpler: `AtlasPlatformOptions { ConfigFolder, OnStartupError, Dictionary<string, AtlasModuleOverrides> Modules }` bound from `configuration.GetSection("AppSettings")` in `AddAtlasPlatform`, with `ValidateDataAnnotations().ValidateOnStart()`; invalid enum text becomes one validation error reported by the start-up check. `AtlasPlatformConfiguration` is replaced by the bound options plus the `Sources` string. About 60 lines removed; the HO-03 fail-closed rule stays (a validation error disables the app).
- Risk: additive (same keys, same defaults). `AtlasPlatformConfiguration` is public but only the platform reads it.

### F06. Manifest and DTO knobs with one real value or no reader
- `Registry/AtlasAppManifest.cs:7` `AtlasAuditMode { Client, Server }`: `Client` has 0 uses; three places test `== Server`.
- `AtlasAppManifest.cs:58, 64` `AuditTime` and `SystemTime`: two time settings for one app, plus a start-up warning (`AtlasStartupCheck.cs:141-143`) that they disagree.
- `AtlasAppManifest.cs:39` `AttachmentTable`: loaded at start-up (`AtlasPlatformStartup.cs:166`), never read again (no attachments in P1).
- `AtlasAppManifest.cs:52` `Service`: produces only a warning when the class name does not contain the text (`AtlasStartupCheck.cs:96-97`); the registration line is the real binding.
- `AtlasAppManifest.cs:83` `SchemaVersion`: read by the lint only.
- `Dto/CPPlatformDto.cs:217` `CPEffect.OptionsFromChild`, `:158` `CPPermissions.Execute`, `:233` `CPButtonAction.Set`, `:199` `CPOptionsSource.Parameters`: 0 readers in C# and TS (`Parameters` becomes live with F03).
- `Rules/AtlasRuleEngine.cs:7` `AtlasRuleContext.States`: both callers pass `[]` (`ConfigSectionService.cs:718`, `AtlasDashboardService.cs:44`); `Matches` still intersects it.
- Standard: delete dead code; model the domain so illegal states cannot be built (two time settings can disagree; `AuditMode: Client` on a platform app silently writes no audit).
- Simpler: one `Time` setting (`Utc` default, `Local`) that drives record times, wire format and audit time; delete `AuditMode` (platform apps always audit on the server; the legacy client call stays a no-op); delete `AttachmentTable`, `Service`, `SchemaVersion`, `OptionsFromChild`, `Execute`, `CPButtonAction.Set`, `States` until a feature needs them. About 80 lines plus 4 start-up checks and their tests.
- Risk: **JSON keys are contracts by the AGENTS.md rule.** `TTDV2.app.json` uses `AuditMode` and `AuditTime`; keeping them as accepted-and-ignored keys (loader is tolerant, B2) is additive. Deleting the DTO properties is a contract change with no caller today (TTDV2 is not yet at the target). Owner decides: keep the JSON keys and delete the code behind them (additive), or delete both (breaking on paper, harmless in practice). Documented in INTEGRATION.md section 4 either way.

### F07. Interfaces with one implementation and no fake
- `Security/AtlasAccessService.cs:17`, `Rules/AtlasRuleEngine.cs:36`, `Audit/AtlasAuditWriter.cs:25`, `Dashboard/AtlasDashboardService.cs:18`. Tests fake only `IAtlasUserContext`, `IAtlasConfigStore`, `IAtlasModuleResolver`, `IAtlasCurrentUserSource`.
- Standard: Framework Design Guidelines (prefer classes; add an interface when there is a second implementation or a test need).
- Simpler: register and inject the sealed classes. `AtlasModuleServices` keeps the same property names with class types. About 50 lines.
- Risk: source change on the public `AtlasModuleServices` constructor (no production caller constructs it; `SectionHarness` does through DI). A module class that declared a field of type `IAtlasRuleEngine` would need a one-word change; none exists.

### F08. Every service method re-resolves manifest, form and table
- `ConfigSectionService.cs:128-130, 274-276, 644-646, 677-679`: `Manifest; Services.ConfigStore.GetForm(AppCode); MainTable(manifest)` four times. `AtlasDashboardService.Load:35-40` does the same.
- Standard: parse outside data once; one decision in one place.
- Simpler: with F04, the resolver hands the module an `AtlasApp` (manifest, table, form text, dashboard text) once; `Form` and `Dashboard` are properties that deserialize a fresh copy. The four triples become one property read.
- Risk: none.

### F09. Duplicate log event declarations
- `AtlasLog.cs` holds all events; the same IDs are declared again as `public static readonly EventId` in `ConfigSectionService.cs:67-69`, `AtlasAccessService.cs:34`, `AtlasConfigStore.cs:41`, `AtlasModuleResolver.cs:40`, `AtlasPlatformStartup.cs:19-20`, `AtlasProblemExceptionFilter.cs:15`.
- Standard: one decision in one place. The INTEGRATION.md contract is the event name and number, not a C# field.
- Simpler: delete the 9 fields (grep shows no reader outside their own files). 12 lines.
- Risk: none in practice (public fields, no caller). Strictly a public-surface removal; mark as such.

### F10. Unused async overloads next to the sync path
- `Data/Platform/AtlasDataExtensions.cs:29-41` `BeginSharedTransactionAsync`, `:98-103` `FindActiveAsync`: used only by `AtlasPlatformContextTests`. Production is synchronous end to end (`SaveWithAudit`, `FindActive`, `AtlasSql.Query`), because the target controller methods are synchronous (review DA-10, deferred).
- Standard: ASP.NET Core "async all the way"; delete dead code.
- Honest position: sync I/O on the shared IIS pool is a real scalability cost, but fixing it means async controller methods at the target, which is an integration requirement for the target team, not a platform change. Until then the async overloads are dead weight. Delete them (and switch the tests to the sync overloads), or keep them as the future path and say so in one comment. I recommend delete; 30 lines.
- Risk: none (public, no production caller).

### F11. `AtlasModuleServices` hands modules 13 services, including raw `IConfiguration` and both `DbContext`s
- `Sections/AtlasModuleServices.cs:21-48`; the comment at `:17-19` asks modules not to write through the contexts.
- Standard: model the domain so illegal states cannot be built; a rule in a comment is not a constraint.
- Simpler: the hook contexts (`AtlasLoadContext`, `AtlasSaveContext`) already carry manifest, form, fields and record. A module needs `Time`, `User`, `Directory` and a read-only query helper. `CPTTDV2Service` uses only `Services.Time`.
- Risk: removing properties is a source-breaking change for module classes; none use them today. Defer until app #2 shows what a module needs; do not add more.

### F12. Dashboard and form each decide "which route opens a row"
- `WebUi/src/app/platform/dashboard/atlas-dashboard.component.ts:170-174` (`navigate`, with its own `apps` load and `forkJoin`) vs `atlas-navigation.ts:47-58` (`AtlasDashboardNavigation.open`, the exported contract that INTEGRATION.md section 2 tells the target to call).
- Standard: one decision in one place.
- Simpler: the platform dashboard injects `AtlasDashboardNavigation`; drop `apps`, `forkJoin`, `navigate`. 15 lines.
- Risk: none (the component is marked "reference" in INTEGRATION.md; the service is the contract).

### F13. Four copies of the local-day parser in TypeScript
- `dashboard/atlas-dashboard-columns.ts:21-31` (`formatDate`), `:41-51` (`cellDay`), `form/renderers/atlas-grid-renderer.ts:168-172` (`dateHeader`), `form/atlas-form-model.ts:48-49`. Each has `/^(\d{4})-(\d{2})-(\d{2})/` and `new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]))`.
- Standard: no structural duplication at >= 0.8.
- Simpler: one `parseLocalDay(text): Date | null` in `atlas-dates.ts`; `formatDate` uses Angular `formatDate(day, 'dd-MMM-yyyy', 'en-US')` and the `MONTHS` array (`:13`) goes. 20 lines.
- Risk: none.

### F14. Domain names inside the generic client effects engine
- `form/atlas-effects.ts:51-52`:
  ```ts
  const parts = String(value).split(' - ');
  return parts.length >= 2 ? { AccountNumber: parts[0].trim(), AccountName: ... } : null;
  ```
- Standard: keep the engine generic (AGENTS.md: no app behavior hardcoded in the reusable engine).
- Example of the divergence: the server (`AtlasServerEffects.Values:71-80`) uses only `option.Data`. For an option with no `Data` the client fills the two fields and the server ignores the client value (the targets are "derived", `ConfigSectionService.cs:315-320`) and writes nothing. The two sides disagree.
- Simpler: delete the fallback; every provider puts `Data` on its options (`PBAccounts` already does).
- Risk: none for platform apps (the server never honored the fallback). The header comment of `atlas-effects.ts:2-3` ("The server applies Show/Hide/Require/Unrequire again") is stale: the server also applies Enable/Disable/Set/Clear/SetFromOption since B35. Fix the comment.

### F15. Two decision rules for one value mapping (TS)
- `form/atlas-form-model.ts:45-58` `toModelValue` switches on `valueKind(control)`; `:61-75` `toOutputValue` switches on the runtime type (`instanceof Date`, `typeof boolean`, `Array.isArray`).
- Simpler: both switch on `valueKind(control)`. Risk: none.

### F16. Mutable renderer registry whose write API only tests call
- `form/atlas-renderer-registry.ts:50-59` `register`, `:75-77` `types()`; `ownMessages.delete(key)` in `register` is a hidden decision (an app renderer can never say it draws its own messages).
- Simpler: three frozen `Record`s plus `provideAtlasRenderers` as a multi-provider; keep `provideAtlasRenderers`, `AtlasRendererBase`, `resolve`, `hasInput`, `drawsMessages`. 25 lines.
- Risk: `register()` removal is a public-surface change with no known caller (INTEGRATION.md names `provideAtlasRenderers`, not `register`).

### F17. Logic in the form template, re-evaluated per field per change detection
- `form/atlas-form.component.html:40-51`: `registry.resolve(child.Type)`, `registry.hasInput(child.Type)` twice, `error(child)` twice, `isRequired(child.Name)` twice. `dashboard/atlas-dashboard.component.ts:102` `visibleButtons()` is a method; the form uses `computed` for the same idea.
- Simpler: `fields = computed(() => ...)` view model `{control, renderer, hasInput, required, error}`. Risk: none.

### F18. Start-up check is two long procedures
- `App/Platform/AtlasStartupCheck.cs:30-155` (`Run`, CC about 28), `:157-228` (`CheckForm`, CC about 24); `AtlasConfigLint.CheckForm:46-147` (CC about 20).
- Simpler: one small class per concern, each `IEnumerable<AtlasCheckItem> Check(AtlasApp app)` and pure where possible (`PermissionsCheck`, `FieldsCheck`, `ComputedCheck`, `SoftDeleteCheck`, `ProvidersCheck`, `DashboardCheck`, `RolesCheck`, `ContractCheck`). `Run` becomes a loop over the list. Also `CheckUnknownProperties` (reflection walk, `AtlasConfigLint.cs:162-221`) can shrink to a lint-only deserialization with `JsonUnmappedMemberHandling.Disallow` if the owner accepts "first unknown member per file" instead of all; otherwise keep it.
- Risk: none (same messages; `AtlasCheckTests` pin the texts).

### F19. Minor
- `AtlasRuleEngine.ApplyToSection:132-135`: `disabled` is computed with `Enable` already considered, then `if (result.Enable.Contains(child.Name)) disabled = false;` repeats the decision. 2 lines.
- `AtlasDashboardService.Token:191-197` resolves `{AppCode}` and `{TabCode}` that no JSON uses. Keep or delete; 2 lines.
- `AtlasAuditWriter.ToDto:89-130` parses the message JSON to find `_Event`, then `GetAuditLog` calls `ToText` to strip it again. Store the event in its own column when the audit table has one (it does: `Event` is in `CPAuditLog`), or keep one parse. 15 lines.
- `form/atlas-validators.ts:168, 171` non-null assertions where `hasMin`/`hasMax` already narrow.
- `form/atlas-form.component.ts:373` `export { problemText }` re-export with no external importer; `atlas-renderer-registry.ts:75` `types()`; `atlas-history-dialog.ts:14,132` `event` built and never rendered; `atlas-platform.scss:59-65, 119-126, 290-292` rules with no platform user.

Things I checked and found good (keep as they are): `AtlasValueConverter` (pure, culture-safe, typed compare), `AtlasTabQueryBuilder` (names from metadata only, every value a parameter, stable order), `AtlasFieldValidator` plus `atlas-validator-cases.ts` (one case list run on both sides), `AtlasRuleEngine.Evaluate`, `AtlasAppCode.ToLegacy/FromRequest` (the seam needs exactly this), `AtlasProblemExceptionFilter` (uses the host `ProblemDetailsFactory`), `AtlasPlatformContext` (shared-type entities, sentinel fix, mappable filter), the Angular form model functions, the a11y directives, the dialog `switchMap` cancel.

---

## 3. Over-engineering: delete or merge

| # | Item | Action | Lines (est.) | Risk |
|---|---|---|---|---|
| O1 | `AtlasConfigStore` refresh cache (`_files`, `_lock`, snapshot, signature, last-good) and `ConfigRefreshSeconds` | load once at start-up (F04) | -130 | behavior change, owner decision |
| O2 | `IAtlasAccessService`, `IAtlasRuleEngine`, `IAtlasAuditWriter`, `IAtlasDashboardService` | delete, inject classes (F07) | -50 | source change on `AtlasModuleServices` ctor, no caller |
| O3 | `AtlasAllowList` 8-parameter static with 3 delegates | private method on a `SaveState` (F02) | -20 | none |
| O4 | `AtlasFormFields.MarkHiddenByRules`, `RuleHiddenFields`, before/after snapshots | rule outcome (F01) | -45 | none |
| O5 | `AuditMode`, `AuditTime` (merge into `SystemTime`), `AttachmentTable`, `Service`, `SchemaVersion`, `OptionsFromChild`, `Permissions.Execute`, `CPButtonAction.Set`, `AtlasRuleContext.States`; their 4 start-up checks | delete (F06) | -80 | JSON key contract: keep keys as ignored, or delete; owner decides |
| O6 | 9 duplicate `EventId` fields | delete (F09) | -12 | public field, no caller |
| O7 | `BeginSharedTransactionAsync`, `FindActiveAsync` | delete (F10) | -30 | none |
| O8 | `AtlasPlatformConfiguration` + two hand enum parsers + string keys | one options class (F05) | -60 | additive |
| O9 | `CPTTDV2Service.OnLoad` and two app providers | platform providers with `Parameters` (F03) | -45 (app) +40 (platform) | package change |
| O10 | `AtlasDisabledApps` as its own singleton + `AtlasPlatformStartupGate` | fold into the catalog of F04 (`AtlasApp.DisabledReason`) | -35 | none |
| O11 | `AtlasAuditWriter.ToDto` JSON parse + `ToText` strip | one parse or an `Event` column read | -15 | none |
| O12 | TS: `AtlasRendererRegistry` mutable API, `types()` | frozen tables (F16) | -25 | undocumented surface |
| O13 | TS: dashboard `apps`/`forkJoin`/`navigate` | `AtlasDashboardNavigation` (F12) | -15 | none |
| O14 | TS: 4 date parsers, `MONTHS` | `parseLocalDay` (F13) | -20 | none |
| O15 | TS: two identical `options` memo getters (`atlas-renderers.ts:89-96, 113-119`) | one in `AtlasRendererBase` | -10 | none |
| O16 | TS: `atlas-action-menu.ts:25-42` three identical `@if` blocks + string dispatch `:72-74` | one table + `@for` | -15 | none |
| O17 | TS: dead exports and SCSS rules (F19), `isNil` helper for 11 null-or-undefined chains | delete / helper | -35 | none |
| O18 | TS: `ATLAS_TAILWIND_CLASSES` side channel (`atlas-tailwind-classes.ts`) | Tailwind `safelist` in the target config, as an INTEGRATION.md line | -16 | INTEGRATION.md change |

Total: about 600 lines of C# and 170 lines of TypeScript (roughly 11% and 7%). Kept on purpose: `AtlasEffectTarget` (one implementation, but it is what keeps `applyEffects` pure), `ATLAS_DASHBOARD_LINK` and `ATLAS_GRID_COMPONENTS` (contracts with a real target need), `AtlasFieldA11yDirective`/`AtlasControlIdDirective` (cheap workarounds for pbdev controls), `AtlasModuleServices` as a parameter object (F11 is deferred), the `IAtlasOptionProvider` interface (4 implementations), `IAtlasConfigStore`/`IAtlasModuleResolver`/`IAtlasUserContext` (faked in tests).

---

## 4. Test quality

(.NET: 295 tests from 182 `[Fact]`/`[Theory]` methods in 21 files; about 4,070 lines, of which about 420 are support code. Angular: 77 runtime tests from 56 `it(` blocks in 2 files, 962 lines.)

**Verdict.** The suite is mostly good. About 270 of the 295 .NET cases assert an observable outcome: a row in the database, a status code, an error code, a report line, generated SQL text. The highest-value files are `Ttdv2SectionTests` (the save pipeline through the real service and a real database), `AtlasCheckTests` (the lint that will catch the JSON mistakes of 16 apps) and `CopyScriptTests` (the DBA contract). 110 cases need SQL Server; that is the right place for them, the save pipeline cannot be judged with mocks. Fixtures do not hide the behavior under test. The Angular specs mostly assert through the DOM and `HttpTestingController`. Ratio of test to production code is about 0.75:1 in C# and 0.4:1 in TS, proportionate for a platform.

### 4.1 Tests that check implementation details, re-state the code, or would pass against empty functions
Delete or trim these (about 20 cases):
- `tests/.../StandInSmokeTests.cs:35` `A_section_service_builds_on_the_stand_in_base_class`: asserts `"Smoke"`, a value returned by the test's own stub (`:45-53`). Passes with every production method empty. Delete.
- `StandInSmokeTests.cs:21` `Current_TTD_json_reads_into_the_stand_in_DTOs`: `Assert.NotNull(config.Section)`; `RegistryTests.cs:211` covers the same file with the platform loader. Delete.
- `Api/EndpointTests.cs:137` `Swagger_lists_the_endpoints`: tests Swashbuckle, needs Kestrel and SQL Server. Delete.
- `EndpointTests.cs:144` `A_number_code_of_an_old_caller_maps_to_the_enum_name`: a pure unit test inside the SQL Server collection; `RegistryTests.cs:184-185` already asserts `ToLegacy("13")`. Move the two asserts to `RegistryTests`.
- `Security/AccessServiceTests.cs:131` `Flags_follow_the_flag_order`: re-states the constants of `TestSupport.cs:24-26`. Keep only the `MatchesAny` lines (`:141-142`).
- `Registry/RegistryTests.cs:305` `The_manifests_are_parsed_once_while_the_folder_does_not_change`: `Assert.Same(first, store.FindManifest("DEMO"))` tests reference identity (cache internals). Delete (it disappears with step 9 anyway).
- `RegistryTests.cs:277` `The_TimeProvider_of_the_host_is_kept` and `:274` `Assert.Same(given, ...Configuration)`: DI wiring re-statements. Keep the first only if the owner wants the HO-08 rule pinned; drop line 274.
- `Sections/Ttdv2SectionTests.cs:340` `LogOnly_writes_AccessDenied_and_continues`: `Assert.NotNull(section)` is a no-exception assert and the insert at `:343` is dead setup (`_ = id;`). Keep line 349 only.
- `Ttdv2SectionTests.cs:475` `Load_and_history_do_not_track_the_record`: `Assert.Empty(ChangeTracker.Entries())` is an EF detail; passes if `LoadPage` returns nothing. Replace with the user-visible effect (a save after a load in the same scope gives no false `Conflict`) or delete.
- `Ttdv2SectionTests.cs:452` (in `A_failed_save_leaves_no_changed_entity_in_the_change_tracker`): same EF detail; lines 453-454 carry the behavior. Drop 452.
- `Data/AtlasPlatformContextTests.cs:187` `Two_registries_with_different_tables_get_different_models`: inspects `Model.FindEntityType`; `:271` already asserts the behavior (`Records(...)` throws). Delete. Also drop `:265-266` (`IsMappable` helper check inside a behavior test).
- `Security/UserContextSqlTests.cs:74` `Assert.Equal(3, cache.Count)`: cache internals, and nothing asserts "one query" despite the test name. Drop the line, or count commands with a `DbCommandInterceptor`.
- `Api/AtlasCheckTests.cs:382`: a second `StartAtlasPlatformAsync()` call with no assertion after it. Assert the registry is the same instance or drop the line.
- Angular `platform.spec.ts:150-162` tests `register()`, which production never calls; `:228-235` asserts a map lookup that the component spec (`:120-136`) already proves by rendering; `:426-438` snapshots `width 100` and `cellRenderer toBe(AtlasActionMenu)`; `:331-364` and `platform.component.spec.ts:288-297` assert CSS class names (look parity, brittle); `platform.component.spec.ts:218-229` returns `expect(true).toBe(true)` when Zone is absent, a green no-op in the local runner (by design, but it counts as a passing empty test locally).
- Multi-scenario bodies with several reasons to fail (split, otherwise good): `RuleEngineTests.cs:43` (conditional asserts), `:91`, `Ttdv2SectionTests.cs:43` (17 asserts), `:194` (three scenarios), `TabQueryBuilderTests.cs:62` (TOP, order and a throw in one test).

### 4.2 Behavior with no test, ranked by risk
1. **The hooks the 16 apps will override.** `OnBeforeSave` adding to `saveContext.Errors` (`ConfigSectionService.cs:105, 419-422`) and `OnLoad` (`:100`) have no test; only `OnAfterSave` is tested. Highest risk; add two tests through a test module.
2. **Start-up checks that matter in QC and production** with no test: `"has no active row in dbo.CP_Atlas_Application."`, `"AccessMode is LogOnly in {Environment}"`, `"has a registration line but no {code}.app.json"`, the code pattern error, the `Parent` vs `ParentID` error, the `Service` warning.
3. **Dashboard runtime**: tab `Rules` hiding a tab, menu `Rules`, button `Rules`, `MaxRows` reached at run time (only the SQL text is tested), a tab naming an app with no manifest. `AtlasDashboardService.cs:45-85`.
4. **Save default path**: a required field with a `Defaults` entry and no client value (`ConfigSectionService.cs:341-342`); `NewInput` always sends `TaxPreparer`, so the branch never runs.
5. **History of a soft-deleted record** (`:675-680`): `FindActive` returns null and `Demand(History, null, refID)` runs with no record. Untested security edge; decide and pin.
6. **Remaining start-up messages**: `OwnerFields` not a column, `SoftDelete` not a column or not bit, three `Computed` variants, `Defaults` names a non-field, option provider not registered, tab with no ID, both-or-none Procedure/Source, and the table-level errors (`AtlasStartupCheck.cs:179-227, 284-297, 338-361`).
7. **Angular**: Require/Unrequire/Enable/Disable and Show/Hide effects through the component (only the pure `Recorder` tests exist); the RV-L21 "touch only shown fields" rule; dashboard `load()` error alert, `onButton` with `Target`, `confirmRemove` with server `Errors`; `AtlasFieldA11yDirective` `aria-invalid` removal; grid renderer non-SingleRowFields note and unknown `cellRenderer` removal.
8. **SQL**: `050` THROW paths 55004, 55005, 55007 (the concurrency guard), `040` 55011, the `090` one-hour window, the `030` NULL branches.

### 4.3 Test infrastructure and SQL
- `TestPaths.cs:11-12` finds the repo root only when `target-implementation-read-only` exists. The folder is git-ignored, so a fresh clone fails all 295 tests, though only 3 tests read it (`StandInSmokeTests.cs:24`, `RegistryTests.cs:219, 230`). Find the root by `atlas-platform` alone and skip those 3 when the folder is absent. Highest-value test fix.
- Duplicated setup: three raw inserts into `TTDV2.General` (`Ttdv2DashboardTests.cs:135-142`, `Ttdv2SectionTests.cs:707-717`, `AtlasPlatformContextTests.cs:282-291`); two identical `IAtlasConfigStore` stubs (`UserContextSqlTests.cs:88-94`, `ApplicationMenuTests.cs:45-56`); two `IHostEnvironment` stubs (`RegistryTests.cs:402-408`, `SectionHarness.cs:126-133`); three `TimeProvider` fakes; the copy-config, edit, run, delete pattern with `try/finally` eight times (`Ttdv2SectionTests.cs:264-282, 410-424, 495-512, 535-551, 557-580, 586-633, 639-662`, `Ttdv2DashboardTests.cs:99-125`). A disposable `SectionHarness.ConfigCopy` with `Replace(file, old, new)` removes about 40 lines; shared stubs in `TestSupport` remove about 45. `SqlServerDatabaseFixture.cs:54-68` and `:73-97` duplicate the read, replace, split loop (-20 lines).
- `SectionHarness.cs:44-45, 80-96`: a static `_registry` shared across the collection, while `AtlasCheckTests.cs:337` adds and drops a column on `TTDV2.General`. Latent ordering hazard; load the registry in the fixture's `InitializeAsync`.
- Two copies of every release script (`docs/atlas-platform/sql/12-*.sql` and `sql/release-p1/0*.sql`) policed by `ReleaseScriptTests`. Keep `sql/release-p1` as the single source, point the fixture at it, delete the docs copies and the test. The DBA folder does not change.
- SQL scripts `020`, `030`, `040`, `050`, `090` are correct and idempotent; they are a contract and stay. Two additive notes for the DBA review, not for Phase B: `090:37-39` restores TTD roles only inside a one-hour window before `@CopyAt` (a longer `050` run leaves roles revoked; `ue.IsDeleted = 1 AND ue.ModifiedBy = @CopyRunBy` would be enough), and `090:57-58` writes a log row on every run while its header says a second run changes nothing. `040` steps 2a and 2b (upgrade of an earlier dev shape) can go once no shared database has that shape; the DBA confirms.

---

## 5. The 10 most complex functions (estimated CC; CRAP >= CC at any coverage, so each needs a split)

| # | Function | Est. CC | Split |
|---|---|---|---|
| 1 | `ConfigSectionService.SavePage` (`ConfigSectionService.cs:271-452`) | ~35 | F02: `SaveState` + 8 steps of 10-25 lines; the lambdas become methods |
| 2 | `AtlasStartupCheck.Run` (`AtlasStartupCheck.cs:30-155`) | ~28 | F18: a list of `Check(AtlasApp)` functions; `Run` loops |
| 3 | `AtlasStartupCheck.CheckForm` (`:157-228`) | ~24 | `PermissionsCheck`, `FieldsCheck`, `SoftDeleteCheck`, `ComputedCheck`, `ProvidersCheck` |
| 4 | `AtlasConfigLint.CheckForm` (`AtlasConfigLint.cs:46-147`) | ~20 | `DuplicateNames`, `EffectTargets`, `EffectCycles`, `RuleTargets`, `DefaultTokens`, `ButtonActions` |
| 5 | `AtlasValueConverter.FromWire` (`AtlasValueConverter.cs:49-85`) | ~18 | pure and fully tested, so the lowest real risk in this list; a `Dictionary<string, Func<...>>` of converters would make it data; optional |
| 6 | `AtlasTabQueryBuilder.Build` + `Condition` (`AtlasTabQueryBuilder.cs:25-82, 84-109`) | ~14 + ~10 | `SelectList`, `WhereList`, `TopClause`; `Condition` as a small `switch` expression per operator |
| 7 | `AtlasRuleEngine.ApplyToSection` (`AtlasRuleEngine.cs:104-149`) | ~14 | F01: return an outcome; `ApplyToControl(control, result)` and `ApplyToChild(child, result)` |
| 8 | `AtlasDashboardService.Load` (`AtlasDashboardService.cs:33-89`) | ~14 | `VisibleTab(tab)`, `RowsOf(tab)`, `ButtonsOf(dashboard)` |
| 9 | `AtlasConfigStore.Snapshot` + `Build` (`AtlasConfigStore.cs:75-164`) | ~12 + ~12 | disappears with F04; else `ReadManifest(path)` returning a result record |
| 10 | TS `AtlasFormComponent.build` (`atlas-form.component.ts:84-130`) | ~12 | `initialFlags(section)` (pure, into `atlas-form-model.ts`), `disableUnsupported`, `wireEffects`, `wireComputed` |

Runners-up: TS `columnDefs` inner `columnDef` (`atlas-dashboard-columns.ts:83-119`, ~13; split `filterFor(type)`, `editLinkRenderer`, `cellText`), TS `errorText` (`atlas-validators.ts:195-225`, ~12, a flat switch; a `Record<code, (e) => string>` makes it data), `AtlasPlatformStartup.StartAtlasPlatformAsync` + `DisableApps` (~10 each), `AtlasAuditWriter.ToDto` (~9).

---

## 6. Duplication

### 6.1 C# and TypeScript (by design: the client validates for the user, the server for truth)
| Concern | TypeScript | C# | Note |
|---|---|---|---|
| Validators and texts | `atlas-validators.ts` | `Sections/AtlasFieldValidator.cs` | Pinned by `atlas-validator-cases.ts` + `ValidatorParityTests` (19 cases). Good. Keep. |
| Effect conditions and actions | `atlas-effects.ts` `matches`, `applyEffects` | `Sections/AtlasServerEffects.cs` `Matches`, `Apply`, `Values` | Same logic; TS header comment is stale (F14); TS `optionData` fallback has no server twin (F14). No parity cases like the validators have. Add 6 shared cases in the same style. |
| Computed fields | `atlas-form-model.ts` `computeValue` | `Sections/AtlasComputedFields.cs` | TS treats empty as 0, C# leaves empty out; for `Sum` the same, for `Count`/`Min`/`Max` not. Add parity cases or drop `Count/Min/Max` until used. |
| Visible-and-enabled-only outputs | `isShown`, `shownInvalid`, `buildOutputs` | `AtlasFormFields` + allow list in `ConfigSectionService` | Same rule, two implementations; acceptable. |
| Wire formats | `toModelValue`/`toOutputValue` (`yyyy-MM-dd`, `true/false`, `#$#`) | `AtlasValueConverter` (also accepts `yes/no/1/0`) | Server is more lenient; fine. |
| Date header | client `dateHeader` (`HeaderFromDate`) | `CPTTDV2Service.OnLoad` | Dead on the server (F03). |
| Control type list | `AtlasRendererRegistry` | `AtlasStartupCheck.ClientTypes:380-383` | Two lists that must agree by hand. Generate one from the other is not possible across languages; a shared JSON list read by both tests is the cheap fix. |
| Grid cell editor names | JSON `cellRenderer` | `provideAtlasGridComponents` map | Fine (target components). |

### 6.2 Inside C#
- `ConfigSectionService` four `Manifest / GetForm / MainTable` triples (F08).
- Two enum-from-text parsers that reject numbers: `AtlasConfigStore.Override<T>:243-259` and `AtlasPlatformStartup.OnStartupError:81-90` (F05).
- `AtlasDataExtensions` sync/async pairs (`BeginSharedTransaction`/`Async`, `FindActive`/`Async`) at ~0.9 similarity (F10).
- `AtlasUserDirectory.DisplayName:92-107` and `DisplayNames:110-145` build the same cache key and entry options twice; `DisplayName` could call `DisplayNames([lanId])`.
- `CPTTDV2Service` header dates and `TTDV2.json` `HeaderFromDate` (F03).

### 6.3 Inside TypeScript (from the independent front-end read, verified by spot-check)
- `atlas-renderers.ts:89-96` vs `:113-119`: 1.0.
- `atlas-action-menu.ts:25-30` vs `:31-36` vs `:37-42`: 1.0.
- `atlas-dashboard-columns.ts:16-32` vs `:39-52`: ~0.85.
- `atlas-grid-renderer.ts:167-174` vs `atlas-form-model.ts:47-50`: ~0.8.
- `atlas-dashboard.component.ts:107-110,170-174` vs `atlas-navigation.ts:47-58`: ~0.8.
- `atlas-dashboard.component.ts:150-168` `confirmRemove` vs `atlas-form.component.ts:310-323` `remove`: ~0.8.
- `atlas-form.component.ts:303-306` vs `:321` error handlers: ~0.9.

---

## 7. Simplicity for the next 16 apps

**Can a new app be added with JSON alone today?** Almost. A TTDV2-shaped app needs: SQL (`040`-style script), three JSON files, role grants, one `AddAtlasModule<T>` line, and a C# class whenever it needs an option list that is not `PBAccounts` or `UsersInRoles`. That last item is the gap: date and year lists are the most common options, and they live in app C#. F03 closes it for the pilot.

**Where the platform forces C#:** a new option source (F03 covers dates/years; a `SqlOptionsProvider` with a parameterized query name in `Parameters` would cover most of the rest), a hook (`OnBeforeSave` for cross-field rules that `Effects`/`Validator` cannot express: no `Compare` validator exists), a new Computed function (the table has Sum/Count/Min/Max), a new control type or grid cell component (TypeScript, expected), a Tailwind class not in `ATLAS_TAILWIND_CLASSES` (TypeScript, avoidable with a safelist, O18), a dashboard tab that runs a procedure (`RV-N1`, not in P1).

**Abstractions with one caller:** the four interfaces of F07; `AtlasAllowList` (one caller); `AtlasPlatformConfiguration` (one reader); `AtlasDisabledApps` + `AtlasPlatformStartupGate` (one writer each); TS `AtlasRendererRegistry.register` (tests only); TS `AtlasNoticeService` (18 lines over `ToastrService`).

**Settings an integrator must understand today** (INTEGRATION.md section 4 plus the manifest): `ConfigFolder`, `ConfigRefreshSeconds`, `OnStartupError`, per app `Enabled`, `AccessMode`, `AuditMode`, `AuditTime`, `SystemTime`, `RuleMode`, `RuleDefault`, `RequireVersion`, `SchemaVersion`, `AuditRefTypes`, `FormType`, `Service`, `Parent`. After F04-F06: `ConfigFolder`, `OnStartupError`, `Enabled`, `AccessMode`, `Time`, `RuleMode`, `RuleDefault`, `RequireVersion`, `AuditRefTypes`, `FormType`, `Parent`. Eleven instead of sixteen.

---

## 8. Refactor plan: small, independently verifiable steps, ordered by value over risk

Each step: pin behavior with the existing tests (add one only where section 4 names a gap), change the structure, run all five gates (`dotnet build`, `dotnet test` 295, `--atlas-check` 0/0, `ng build` + `ng test` 77, `check:target-flags`), one commit. "Package" = a "COPY to the target" file changes, so `make-target-package.py` must run. "INTEGRATION" = a hand edit in INTEGRATION.md.

| Step | Change | Findings | Package | INTEGRATION | Risk |
|---|---|---|---|---|---|
| 1 | Delete dead code: 9 `EventId` fields, 2 async overloads (tests to sync), `OptionsFromChild`/`Execute`/`CPButtonAction.Set`/`States` code paths (keep JSON keys as ignored if the owner prefers), TS dead exports and SCSS rules, stale `atlas-effects.ts` header | F09, F10, F06 (part), F14 (comment), F19 | yes | no | none |
| 2 | TS duplication: `parseLocalDay`, one `options` getter, action-menu table, `isNil`, one `httpFail` | F13, O15, O16, O17 | yes | no | none |
| 3 | TS: dashboard uses `AtlasDashboardNavigation`; `toOutputValue` by `valueKind`; delete the `' - '` fallback in `optionData` | F12, F15, F14 | yes | no | none (server never honored the fallback) |
| 4 | Rule outcome: `ApplyToSection` returns hidden/disabled sets; `AtlasFormFields.From(section, outcome)`; delete `MarkHiddenByRules`, `RuleHiddenFields`, the before/after pairs | F01 | yes | no | none; pinned by `Ttdv2SectionTests` (RV-02, SV-03, SV-04 tests) |
| 5 | Split `SavePage` into `SaveState` + 8 steps; fold `AtlasAllowList.Check` into a step | F02, O3 | yes | no | none; pinned by `Ttdv2SectionTests` + `SectionStepTests` |
| 6 | Pilot without C#: platform `Years` and `WeekEndings` providers reading `Parameters`; delete `CPTTDV2Service.OnLoad`; `CPTTDV2Service` and `TTDV2OptionProviders` become one-line forwarders (or are deleted: owner choice) | F03 | yes | yes (section 1 copy list and 4.1 row if deleted) | additive; `TTDV2.json` gains two `Parameters` blocks |
| 7 | Options pattern: `AtlasPlatformOptions` bound from `AppSettings`; one enum validation; delete `AtlasPlatformConfiguration` | F05 | yes | no (same keys) | additive |
| 8 | Drop the four single-implementation interfaces | F07, O2 | yes | no | source change on `AtlasModuleServices` ctor; no caller |
| 9 | **Owner decision.** Load config once at start-up (`AtlasAppCatalog`); delete the refresh cache, `ConfigRefreshSeconds`, `AtlasDisabledApps`, the gate; `AtlasApp` carries manifest, table, form and dashboard; services stop re-resolving | F04, F08, O10 | yes | yes (section 4 rows for `ConfigRefreshSeconds` and the restart rule) | **behavior change**: no live JSON reload |
| 10 | **Owner decision.** One `Time` setting; delete `AuditMode`, `AuditTime`, `AttachmentTable`, `Service`, `SchemaVersion` (JSON keys kept as ignored, or deleted) | F06 | yes | yes (section 4 and `TTDV2.app.json`) | JSON key contract |
| 11 | Split the start-up check into per-concern checks; optional `JsonUnmappedMemberHandling` lint | F18 | yes | no | none; `AtlasCheckTests` pin the texts |
| 12 | TS: form view model `fields = computed(...)`; `build()` split; `paramMap.pipe(switchMap)` loads | F17, section 5 row 10 | yes | no | none |
| 13 | Parity cases for effects and Computed (6 + 4 shared cases, same style as validators) | section 6.1 | yes | no | none; adds tests only |
| 14 | `ATLAS_TAILWIND_CLASSES` to a Tailwind safelist line in INTEGRATION.md | O18 | yes | yes | none |

Steps 1-5 are pure structure, no contract touched, about one day. Steps 6-8 are additive and about one day. Steps 9 and 10 need a yes or no from the owner before I touch them. Steps 11-14 are polish.

Integration requirements for the target team that fall out of this review (not platform changes): async controller methods if the sync I/O becomes a measured problem (F10); a Tailwind `safelist` entry if O18 is taken; nothing else.

---

## 9. Owner decisions (2026-10-31)

| Decision | Answer |
|---|---|
| Steps for Phase B | 1 to 14, without step 10 |
| Step 9: read the config one time at start-up, no live JSON reload | Yes |
| Step 10: one Time setting, delete AuditMode/AttachmentTable/Service/SchemaVersion | No. The settings stay as they are. |
| Step 6: the pilot C# files | Delete them. program.cs registers `AddAtlasModule<ConfigSectionService>("TTDV2")`. |

Phase B runs one step per commit with the five gates after each step. No push without the owner's OK.
