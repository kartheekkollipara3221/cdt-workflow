# Module 16: Architecture review 2 of the atlas-platform code

Status: Review 2 (2026-10-28) of commit `493a25e` (after review 1 Waves 1-3, the target-alignment rework R0-R8 and the real-library dry run). Target region: development only; nothing goes live before integration, testing and the fixes are complete. The performance backlog (PF1-PF9) stays deferred by user decision: this review maps scale gaps to it and does not reopen it. No code is changed by this review. Fixed after the review: AR-01 (B73) and the guard part of AR-02 (B74), 2026-10-28; the row rule of AR-02 is an open question (section 10).

## 1. Purpose and method

The user asked three questions:

1. How does the new design make the remaining apps (about 16) easier to build than the target implementation?
2. When the code goes into the target, can the new design scale?
3. Can the new design break the existing system (TTD, CDT, SQMS, SQMSO, SQMSB, AIN)?

Method:

- Read the platform code (`atlas-platform/src`, `WebUi/src/app/platform`, `sql/release-p1`) and the target copy (`target-implementation-read-only/`).
- Compare each item with published guidance: Microsoft ASP.NET Core performance best practices, ASP.NET Core in-process hosting on IIS, EF Core performance and property-bag entity types, SQL Server index design; angular.dev (signals, style guide, lazy loading, dynamic components, accessibility).
- Count the work for one app in the target design and in the new design.
- Check the results of review 1 (module 13) and of the dry run. A finding of review 1 that is fixed is not repeated.

ID prefix: `AR` (review 2). Out of scope: authentication (IIS Windows authentication at the target) and the workflow engine.

## 2. Verdict

| Question | Answer | Condition |
|---|---|---|
| Easier for the remaining apps? | **Yes.** An app is SQL from the template, three JSON files and one registration line. No TypeScript, no enum value, no `if` branch. | Only for apps whose features are in the platform (AR-09). |
| Can it scale? | **Yes for the expected load, not yet proven by numbers.** The design has no step that grows per request with the number of apps. Two config items read whole tables (AR-05). The platform shares the thread pool of the target host (AR-04). | AR-05 fixed by config. PF9 numbers during integration testing. |
| Can it break the existing system? | **Not through data or contracts.** All target changes are additive, and TTDV2 has its own schema. **Yes through the shared host,** in two places: a start-up error stops all apps (AR-01), and the menu step changes and can fail the shared menu (AR-02). | AR-01 and AR-02 fixed before the copy to the target. |

The design is sound. Two High findings, both small and inside platform code or settings. No finding needs a change to a target table or to a public contract.

| Severity | Count | Meaning |
|---|---|---|
| High | 2 | Can stop or change an existing app at the target. Fix before the copy to the target. |
| Medium | 6 | Limits scale or the "config only" promise for the next apps. Fix before app #2, or decide. |
| Low | 3 | Watch items. |

## 3. What is better than the target implementation

| Area | Target today (TTD as the example) | New design (TTDV2) | Why it matters |
|---|---|---|---|
| Server code for each app | Enum value in `CPApplicationCode`; 13 `if` branches in `CPAtlasService` (`CPAtlasService.cs.txt:129-180`); `CPTTDService` 323 lines; EF entity and DTO | One `AddAtlasModule<T>("CODE")` line; optional hook class (`CPTTDV2Service.cs`, 31 lines) and option providers (38 lines) | App #17 does not change shared code, so it cannot break app #3. |
| Client code for each app | App and field branches in the section master: `code == 'SQMSO'` (3), `'SQMSB'` (3), `'AIN'`, `Name === 'Account'`, `'WeekEnding'`, `'ClientRelationshipLeavingBBH'`, `'FundConsideredSecondarySale'` ... (`cp-atlas-sectionmaster.ts.txt`) | No TypeScript. Show, hide, enable, set and computed values are JSON `Effects`, `Rules` and `Computed` | The shared component does not grow with each app. |
| Trust | Rules run in the browser only | The server runs the rules again on save, applies the effects, checks the allow list and the option lists, and sets system and computed columns itself | A direct API call cannot save a hidden field, a value that is not an option, or another owner. |
| Data | Text columns, display names for people, last write wins | Typed columns, LAN IDs, `rowversion` (save conflict detected), audit row in the same transaction | Reports and joins work; no lost updates; history is complete. |
| Mistakes | Found by users in production | `--atlas-check` before the deployment: names, tokens, patterns, tables, column types, registration (exit code 0/1) | A bad config does not reach users. |
| Switch off one app | Code change and deployment | `AppSettings:Modules:<CODE>:Enabled = false` | Rollback in minutes. |
| Look and controls | - | Same as the target: pbdev controls, section master layout, ag-grid with the target cell editors, toastr, modal (rework R1-R6; dry run against the real pbdev-core source: 0 errors, 66 tests) | Users see one product. |

Effort for one new app:

| Step | Target design | New design |
|---|---|---|
| Table | Hand-written DDL, EF entity, DbSet, DTO | Template `05-new-app-template.sql` (DDL + app row + roles) |
| Form | JSON + section master branches + service class | JSON (`<CODE>.json`) |
| Dashboard | JSON + dashboard component branches + procedure | JSON (`<CODE>.Dashboard.json`), `Source: Table` (no procedure) |
| Wiring | Enum + `if` chains in service and controller | Manifest (`<CODE>.app.json`) + one registration line |
| Test | Manual | `--atlas-check` + the platform tests |

## 4. Findings: risk to the existing system

### AR-01 A platform start-up error stops the whole host, also the current apps (High, host)

**Evidence.** `AtlasPlatformStartup.cs:67-72`: with no setting, `OnStartupError` is `Stop`. In `Stop` mode any start-up check error (one bad TTDV2 field, SQL not available when the app pool starts) throws, and the host does not start. The platform runs in the target process (`program.cs`, INTEGRATION.md section 2).

**Guidance.** ASP.NET Core in-process hosting on IIS: one app in one app pool; a start-up exception gives 500.30 for all requests, and IIS Rapid-Fail Protection can stop the pool after repeated failures. Fault isolation (bulkhead): a fault in one part must not stop the other parts.

**Impact.** In the shared development region, a TTDV2 config mistake stops TTD, CDT and SQMS for the target team. In production the same occurs for all users.

**Fix.** Set `AppSettings:Atlas:OnStartupError = DisableModule` in the target `appsettings.json` (setting only, B54). Recommended also: make `DisableModule` the default outside Development (the code is not at the target yet, so no current caller changes). `--atlas-check` still returns 1 for the release check. Effort S.

### AR-02 The menu step removes rows that it does not know, and an error in it fails the shared menu (High, service)

**Evidence.** `AtlasApplicationMenu.cs:18-31`: a row is kept only when its code is a platform app or a `CPApplicationCode` enum name; each other row is removed. The call in `GetApplication` (`StandIn/CPAtlasService.cs:28-30`, copied to the target) has no try/catch, so an exception in `Enrich` (a manifest lookup, a resolver error) fails `v1/Application` for each user.

**Guidance.** Backward compatibility (AGENTS.md): existing response JSON must stay the same for existing callers. Fault isolation.

**Impact.** A target `CP_Atlas_Application` row with a code that is not in the enum (for example a family row or a new app of the target team) disappears from the menu with no error. A platform defect removes the whole menu, also for users of the current apps only.

**Note (2026-10-28).** The row rule is the approved design of spec 07, 4.6: it hides the rows of codes that the API cannot open (target defect RG-06: such an item opens a dashboard that fails). The guard part is fixed (B74). The row rule stays until the user decides (section 10).

**Fix.** Remove only rows whose code has a manifest and is not available (disabled, not registered, start-up errors). Keep every other row unchanged. Wrap the call: on an exception, log `Atlas.ConfigError` and return the rows of the procedure unchanged. Add a test with a row code that is not in the enum. At the target, list the codes of `CP_Atlas_Application` to confirm. Effort S.

### AR-03 The seam into the target is about 8 hand edits (Medium, integration)

**Evidence.** INTEGRATION.md section 2: `partial` on 22 DTO classes, `program.cs` (3 lines), `CPAtlasService.cs` (PLATFORM lines in 7 methods + 2 constructor parameters), `CPAtlasController.cs`, `app.routes.ts`, `cp-atlas-dashboard.ts`, `angular.json`, `app.config.ts`, the menu component.

**Guidance.** Backward compatibility rule: run regression coverage that proves the existing apps still work.

**Impact.** Most of the integration risk is in these edits, not in the platform files. A missed PLATFORM line gives a 404 for TTDV2 only (safe). An error in an existing `if` chain breaks a current app.

**Fix.** One integration change (one branch at the target) with a checklist from INTEGRATION.md section 2. Before and after the change, run a smoke test of each current app (menu, dashboard, load, save) in the development region. Keep `Modules:TTDV2:Enabled = false` until the smoke test passes. Effort S.

## 5. Findings: scale

### AR-04 The platform shares the thread pool and the database of the target, and its I/O is synchronous (Medium, host; PF1, PF9 deferred)

**Evidence.** All platform database calls are synchronous (`AtlasSql.cs:25` `ExecuteReader()`, `FindActive`, `SaveChanges`), the same as the target `CPApplicationService`. `AtlasPlatformContext` uses the connection of `CPSharepointContext` (`AtlasDataExtensions.cs:19-20`).

**Guidance.** Microsoft "ASP.NET Core best practices": avoid blocking calls; a blocked thread cannot serve other requests (thread pool starvation).

**Impact.** At the expected load (2-5 requests a second, questionnaire) there is no problem, and the current apps work the same way. With 16 apps in one pool, heavy platform use also slows the current apps. There are no usage numbers yet.

**Fix.** No code change now (PF1 deferred). During integration testing, measure: PF9 (slow-request log event) is the lowest-risk way to get the numbers. Decide PF1 with these numbers. Effort: none now.

### AR-05 Two TTDV2 dashboard tabs read the whole table (Medium, config and SQL; PF3, PF8)

**Evidence.** `TTDV2.Dashboard.json:33-38`: tab `TaxTimeAllReport` has no `Filter` and no `MaxRows`. Tab `CurrentTaxTimesheet` (`:11-18`) filters `CreatedBy OR TaxPreparerLanId OR ModifiedBy`; `040-ttdv2-app.sql:94-99` indexes `CreatedBy` and `TaxPreparerLanId` only.

**Guidance.** Microsoft "ASP.NET Core best practices": return large collections in pages. SQL Server index design: an `OR` over different columns can use an index union only when each column has an index; with one column not indexed, the optimizer scans.

**Impact.** Each dashboard open scans `TTDV2.General` once for each of these tabs, and sends all rows of the "All Report" tab to the browser. The cost grows with the data, and each app made from the same pattern gets it.

**Fix.** Config and SQL only, no platform code:

- Give `TaxTimeAllReport` a `MaxRows` (and, if the business agrees, a tax-year filter).
- Add `IX_General_ModifiedBy ... WHERE [IsDeleted] = 0` in `040`, or remove `ModifiedBy` from the `Any` list.
- Optional (PF8): `--atlas-check` warns for a tab with no `{LanId}` filter and no `MaxRows`, and for a filter column with no index.

Effort S.

### AR-06 One EF Core model holds all platform tables (Low, data)

**Evidence.** `AtlasPlatformContext.cs:29, 75`: one model for all registered tables, keyed on the table signature. Tables load once at start-up (B31), so the model is built once for each process.

**Guidance.** EF Core: property-bag (shared-type) entity types and a custom `IModelCacheKeyFactory` are supported. A compiled model is not possible for a model made at run time, so the build cost is paid at the first use.

**Impact.** Start-up time grows a little with each app. The start-up check already builds the model, so a bad table is found before requests (B25). No effect per request.

**Fix.** None now. Measure the start-up time at the target with TTDV2 and record it as the baseline (PF5). Effort: none.

### AR-07 The form JSON is parsed again on each call (Low, service; PF6)

**Evidence.** `AtlasConfigStore.cs:142` `GetForm` returns a new copy on each call (by design: the request changes it, review 1 section 3).

**Impact.** Small CPU cost per request; it does not grow with the number of apps (only with the size of one form).

**Fix.** PF6 (parsed graph cache + clone) when the PF9 numbers show a need. Effort: none now.

## 6. Findings: the "config only" promise for the remaining apps

### AR-08 The platform is a framework that a team must own (Medium, maintainability)

**Evidence.** Platform code: about 4,300 lines of C# (`src/**/Platform`) and 2,100 lines of TypeScript (`WebUi/src/app/platform`, without specs). 273 .NET and 66 Angular tests. The JSON contract has a lint (`AtlasConfigLint`, B36) but no JSON Schema and no author guide.

**Guidance.** Single owner for shared code; a published contract for metadata authors.

**Impact.** Without an owner, each new app request becomes a platform change by a different developer, and the shared code drifts back to app branches.

**Fix.** Agree with the target team who owns `Platform/` folders. Before app #2: one author guide (manifest, form, dashboard, rules, effects, tokens) and a JSON Schema file for editor help (SV-07, optional in review 1). Effort M.

### AR-09 Features of the remaining apps that the platform does not have yet (Medium, scope)

**Evidence.** Not in P1: attachments (module 8), workflow adapter (module 10), multi-row grids (`Grid.Data`, N5), stepper forms, dashboard row actions of the target `ActionMenuComponent` (N4). The current CDT and SQMS apps use some of these.

**Impact.** An app that needs one of these features cannot go config-only, and a developer may add an app branch to the platform to get it.

**Fix.** Before P2: a feature table of the 16 apps (which features each one uses) to order P2-P5 and to find the apps that can go config-only now. A missing feature is added once, as metadata, never as an app branch. Effort S (inventory).

### AR-10 pbdev-select and the exact pbdev-core version are not proven (Medium, integration; N1, N2)

**Evidence.** The dry run built against the real pbdev-core source, except `pbdev-select` (source not supplied; stand-in). Target `package.json` says `^6.0.0`; the source read is `6.0.1-0`.

**Fix.** Option 2 at the target (the user runs `ng build` with the copied `platform/`): it closes both. Effort S.

### AR-11 The TTD change-over script copies and revokes in one run (Low, SQL; development region)

**Evidence.** `050-ttdv2-copy-from-ttd.sql:7, 18`: part 2 copies the data and revokes all TTD roles in one transaction.

**Impact.** None in the development region. At go-live, the business cannot copy the data and keep TTD open in parallel for a trial period.

**Fix.** Decide at go-live. Option: split the revoke into its own script (`060`), so the copy can run first and TTD stops only on the chosen day. Effort S.

## 7. Check against the published guidance

| Guidance | Platform | Result |
|---|---|---|
| ASP.NET Core: start-up work in an `IHostedService`; options from the given configuration; `TryAdd` in libraries; `ProblemDetailsFactory` | `AtlasPlatformStartupService` (B53), `AtlasPlatformConfiguration` (B27), `TryAddSingleton(TimeProvider.System)` (B28), problem filter (B55) | Follows |
| ASP.NET Core: high-performance logging `[LoggerMessage]` | `AtlasLog.cs` (B56) | Follows |
| ASP.NET Core: avoid blocking calls | Synchronous, as the target | Gap, deferred (AR-04, PF1) |
| EF Core: no-tracking for read-only queries | `FindActive(..., tracking: false)` (B58) | Follows |
| EF Core: optimistic concurrency with a row version | `rowversion`, `VersionRequired` (B39) | Follows |
| EF Core / SQL: parameters, no SQL text from users | Identifiers from `sys.columns`, typed parameters | Follows |
| SQL Server: sargable predicates, filtered indexes | `[IsDeleted] = 0` (B59) | Follows; one `OR` gap (AR-05) |
| angular.dev: standalone components, `inject()`, signals, `computed`, `OnPush` | Form, dashboard, history dialog (B64) | Follows |
| angular.dev: lazy loading with `loadComponent` | `atlas-platform.routes.ts` | Follows |
| angular.dev: dynamic components through a registry and DI | `provideAtlasRenderers`, `provideAtlasGridComponents` (B33, B69) | Follows |
| angular.dev: accessibility (labels, ARIA, keyboard) | B63 | Follows |
| angular.dev style guide: no logic in templates | Some per-field template calls (NG-11 remainder) | Low gap |

## 8. Recommended order

1. Fix AR-01 and AR-02 in the platform (small, with tests). Add the AR-05 config and index items.
2. One-time integration in the development region (option 2): copy, the hand edits of AR-03, the smoke test of the current apps, then TTDV2 on. This also closes AR-10.
3. Integration walk-through B3, B4, B7, B9, B10, B14, B15 (one question at a time).
4. Review with the target team: this report, module 13, INTEGRATION.md. Agree AR-08 (owner) there.
5. Performance backlog with the numbers from integration testing (PF9 first), then the engine defects.
6. Before P2: the feature table (AR-09). At go-live: the AR-11 decision.

## 9. References

- Microsoft Learn, "ASP.NET Core best practices": learn.microsoft.com/aspnet/core/fundamentals/best-practices
- Microsoft Learn, "In-process hosting with IIS and ASP.NET Core": learn.microsoft.com/aspnet/core/host-and-deploy/iis/in-process-hosting
- Microsoft Learn, "Health checks in ASP.NET Core": learn.microsoft.com/aspnet/core/host-and-deploy/health-checks
- Microsoft Learn, "EF Core performance": learn.microsoft.com/ef/core/performance/
- Microsoft Learn, "Shadow and indexer properties (property-bag entity types)": learn.microsoft.com/ef/core/modeling/shadow-properties
- Microsoft Learn, "Handling concurrency conflicts": learn.microsoft.com/ef/core/saving/concurrency
- Microsoft Learn, "SQL Server index architecture and design guide": learn.microsoft.com/sql/relational-databases/sql-server-index-design-guide
- angular.dev: style guide, signals, lazy loading, programmatically rendering components, accessibility
- Module 13 (review 1), INTEGRATION.md, PBDEV.md, module 15 (performance questionnaire)

## 10. Fix status and open question

| Item | Status |
|---|---|
| AR-01 | Fixed (B73): `DisableModule` is the default; any start-up failure keeps the host running; a bad setting value is a check error. |
| AR-02 guard | Fixed (B74): an error in the menu step gives the rows unchanged and is logged. |
| AR-02 row rule | Open. Option A: keep spec 07, 4.6 (hide rows that the API cannot open, fixes RG-06; changes the current menu when such rows exist). Option B: keep unknown rows unchanged (the current menu exactly as today; RG-06 stays). |
