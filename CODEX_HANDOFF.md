# RESUME HERE - atlas-platform build: C0-C10 DONE, committed and PUSHED (origin/console-code-first-0518 at 96af19e+); WAITING for the user's browser review feedback - 2026-10-25

## Status
- All build phases done (spec docs/atlas-platform/12-ttd-pilot-build.md, section 11). Commits: C5 0ee9027, C6 253f467,
  C7 405d78b, C8 a72ee38, C9 3c4eec4, C10 (this entry). Earlier: C0 4c17e94 .. C4 33994b8.
- Tests: 144 .NET (dotnet test atlas-platform/AtlasPlatform.slnx; SQL tests make throw-away DBs on localhost) + 14 Angular
  (cd atlas-platform/WebUi; npx ng test --watch=false). Build 0 warnings. --atlas-check exit 0.
- Local review DB: CPAtlasPlatformDev (atlas-platform/local/setup-local-db.ps1 [-Recreate]). API http://localhost:5080
  (swagger), UI http://localhost:4200. Steps: atlas-platform/local/README.md. Act-as switch in the UI header.
- Integration guide complete: atlas-platform/INTEGRATION.md (files to copy, target file changes, SQL, settings,
  acceptance with line counts, findings B1-B16 for review).

## Review feedback done
- R1 (B17): Chrome Issues on the form (inputs with no id/name, label for with no target). Renderers pass id ctl_<Name>
  (inputId on pbdev controls, labelForId on ng-select); registry hasInput() leaves 'for' off user/grid labels.
  Checked live: form edit/new and dashboard have 0 fields without id/name and 0 bad label targets.
- Note: record 10003 was soft-deleted during the review (IsDeleted=1, audit Delete row). Reset with setup-local-db.ps1 -Recreate.

## Architecture review (module 13) - done, no code changed
- docs/atlas-platform/13-architecture-review.md + .html: 47 findings (9 High, 24 Medium, 14 Low), IDs NG/HO/SV/DA, fix
  waves in section 10. Wave 1 (before copy to target): NG-01 @pbdev export names, NG-02 /apps route, NG-05 Vitest spec,
  NG-06 styles, SV-01 options check, SV-02 LogOnly delete, HO-03 override parsing, DA-01 column types, DA-07 script log,
  HO-01 config root, HO-08 TryAdd. Waiting for the user to choose which findings to fix.

## What was built (C5-C10)
- C5 Service: Application/ConfigSectionService (load: values, options, KeepSavedValue, defaults tokens, user labels,
  OnLoad, rules, header, Version, UserPermissions, Computed map; save: access, rules, allow list B7, effects, validators,
  version + EF concurrency, conversion, SetFromOption from Data B10, defaults, Computed, system fields UTC/LAN ID, hooks,
  save+audit in one transaction, Errors with HTTP 200; RemovePage soft delete; GetAuditLog). Platform/Options (PBAccounts,
  UsersInRoles), Platform/Audit/AtlasAuditWriter, Platform/Sections (AtlasFormFields, AtlasModuleServices bundle).
  CPTTDV2Service + TTDV2/ providers. Config/TTDV2.json.
- C6 local DB script + seed, stand-in host (Fixed or Negotiate auth, act-as header, CORS, Swagger), stand-in controllers
  with string code, CPAtlasService stand-in with PLATFORM lines, AtlasApplicationMenu (FormType/HasDashboard),
  AtlasAppCode.FromRequest. Fixed QUOTED_IDENTIFIER in 12-ttdv2-app.sql and 05 template (B12).
- C7 Platform/Dashboard: AtlasTabQueryBuilder (Source: Table, Any group, params only), AtlasDashboardService (View check,
  rules before load, user columns, Permissions menus/button). Config/TTDV2.Dashboard.json.
- C8 App/Platform/AtlasStartupCheck + AtlasPlatformStartup (StartAtlasPlatformAsync, RunAtlasCheckAsync); --atlas-check.
- C9 WebUi (Angular 21, zoneless, Vitest): platform/ (form component, renderer registry, effects, validators, form model,
  dashboard with ag-grid-community 33, history dialog, navigation by FormType/Target); stand-in/ (@pbdev stand-in via
  tsconfig path, shell, interceptor). ng-select used for "ngselect" (target already has it).
- C10 sql/release-p1 020-050 (+ parity test), copy-script test on TTD sample data (local/sql/010-local-ttd-sample.sql),
  docs.

## Open points for the user's review (also INTEGRATION.md section 6)
- B3/B4/B7/B9/B10/B14/B15 are design choices made without asking (user said do not wait). Confirm or change.
- Grid column dates follow WeekEnding only on load (TTD did the same); not updated when the user changes WeekEnding.
- Select values are not checked against the options on save (e.g. TaxPreparer can be any LAN ID).
- Script 010 (engine migrations) is made by the target team.

## Restart the local review (servers do not survive the session)
1. DB (only if missing or to reset): powershell -ExecutionPolicy Bypass -File atlas-platform/local/setup-local-db.ps1 -Recreate
2. API: dotnet run --project atlas-platform/src/BBH.CP.Atlas.App --launch-profile StandIn   (http://localhost:5080/swagger)
3. UI: cd atlas-platform/WebUi; npm start   (http://localhost:4200)
Stop the API before dotnet test (the running exe locks the App dll).

## How to work on feedback
- Same rules as before: commit each change; findings and design choices go to atlas-platform/INTEGRATION.md section 6
  (next number B18); keep backward compatibility; DB changes as SQL scripts (copy module script to sql/release-p1 and
  keep ReleaseScriptTests green); update this entry before ending.
- Push only when the user asks.

## Next-session prompt
"Resume atlas-platform. Read the top entry of CODEX_HANDOFF.md. Restart the local review (API + UI). I reviewed TTDV2 in
the browser; here is my feedback: ..."

---

# RESUME HERE - atlas-platform build: C0-C4 DONE and committed; C5 (section service) NOT started in code; user wants C5-C10 done autonomously, commit each phase, then review TTDV2 in the browser - 2026-10-25

## Instruction from the user (still active)
"Complete all remaining steps, committing at each step completion. Do not wait for input. My goal is to review the final
app on the browser before I pass on any review feedback." Do C5..C10 without stopping; record open design choices in
atlas-platform/INTEGRATION.md section 6 (findings B1..) instead of asking. End state: local API + Angular UI running TTDV2.

## Status
- Spec: docs/atlas-platform/12-ttd-pilot-build.md (TTDV2 pilot, all questions answered; section 11 = coding plan,
  11.3 local working copy, 11.4 @pbdev stand-in). Committed.
- Code: atlas-platform/ (AtlasPlatform.slnx; projects BBH.CP.Atlas.Infra (stand-in), .Data, .Service, .App; tests/).
  Build 0 warnings. Tests: 98 pass (dotnet test atlas-platform/AtlasPlatform.slnx; SQL tests make a throw-away DB on localhost).
- Commits: C0 4c17e94, C1 36d34a8, C2 57c8a5c, C3 0cd4792, C4 33994b8.
  - C1 Data/Platform: AtlasPlatformContext (shared-type Dictionary<string,object?>, sentinel fix B1), AtlasColumnMetadataReader
    (sys.columns), AtlasTableRegistry, AtlasValueConverter, AtlasDataExtensions (AddAtlasPlatformData,
    BeginSharedTransactionAsync, NewRecord, FindActiveAsync). local/sql/000-cp-atlas-core-tables.sql (local only).
  - C2 Data/Platform/Dto/CPPlatformDto.cs = ALL partial DTO additions (config Rules/OwnerFields/Permissions/Defaults/
    Computed/SoftDelete/HeaderNameWithId; section AppCode/Version/UserPermissions; control OptionsSource/Effects/
    KeepSavedValue/Audit; validator Integer/Min/Max; CPKeyValue.Data; grid BindMode; button Action/Target; page
    AppCode/Version/Errors; audit Event/CreatedDateUtc/Source; dashboard AppCode; tab Source/Columns/Filter/OrderBy; new
    classes CPPermissions, CPEffect, CPPageError, CPTabColumn, CPTabFilter ...).
    Service/Platform: AtlasJson (tolerant STJ, "required" off = B2), AtlasProblemException, Registry/AtlasAppManifest
    (+ mode enums, AtlasAppCode), AtlasConfigStore, AtlasModuleResolver (IAtlasModule, AtlasModuleRegistration).
    App/Platform: AddAtlasPlatform, AddAtlasModule<T>(code) (keyed scoped IAtlasModule), AtlasProblemExceptionFilter.
    App/Config/TTDV2.app.json.
  - C3 Service/Platform/Security: AtlasRoles (AtlasAppRoles, tokens @Read/@Write/@Admin), AtlasSql, AtlasUserContext,
    AtlasUserDirectory (DisplayName cache, UsersInRoles), AtlasAccessService (View/Create/Edit/Delete/History; LogOnly
    logs Atlas.AccessDenied; Enforce 403; IsOwner by LAN ID; UserPermissions). App/Platform/UserInfoCurrentUserSource.
  - C4 Service/Platform/Rules/AtlasRuleEngine (Evaluate + ApplyToSection; app-level Rules default B5; JSON-disabled
    control stays disabled B3; buttons use Hidden default B4). All registrations: Service/Platform/AtlasPlatformServices.Add.

## C5 plan (worked out, not coded yet) - Service project
- Options/: IAtlasOptionProvider { Name; GetOptions(AtlasOptionRequest) }. PBAccounts (CPAtlasExternalService stand-in;
  Label/Value "number - name"; Data AccountNumber, AccountName, RelationshipManager). UsersInRoles (Value LAN ID, Label
  display name). App providers come from the module class (virtual AppOptionProviders), so program.cs keeps ONE line per app.
- Sections/ConfigSectionService : CPApplicationService, ISectionService, IAtlasModule; ctor(AtlasModuleServices bundle).
  - LoadPage: manifest + form copy; access View/Create; record (FindActive, 404) or new; bind values (ToWire; grid
    SingleRowFields gives Grid.Data with one row); OnLoad hook; options (providers, KeepSavedValue); defaults
    ({User.LanID}, {User.DisplayName}, {Today}, {Options[n]}; new record or empty value); a control of Type "user" gets
    an option with the display name of its LAN ID; rules (role tokens, IsOwner, new record = owner); header
    (HeaderNameWithId); Version (base64 RowVersion); UserPermissions.
  - SavePage: access Create/Edit on the stored record; effective section; allow list = visible and enabled Field
    controls + grid column fields, never the system columns (ID, CreatedBy, CreatedDate, ModifiedBy, ModifiedDate,
    IsDeleted, RowVersion) or Computed fields; an extra, hidden or disabled name = field error "NotAllowed"; grid input
    accepted as one output per field OR as the grid control with a JSON row; validators (Required with server Effects
    Require/Unrequire, MaxLength, Pattern, Integer/Min/Max; the grid control Validator applies to each grid field);
    AtlasValueConverter; Version check (Conflict); map; Computed Sum/Count/Min/Max; system fields in UTC from
    TimeProvider (CreatedBy/ModifiedBy = LAN ID); OnBeforeSave; save + audit in one platform-context transaction;
    OnAfterSave. Errors: ID = 0 + Errors list, HTTP 200. DbUpdateConcurrencyException gives a Conflict error.
  - RemovePage: access Delete; SoftDelete field + Modified*; audit Delete. GetAuditLog: access History; sort descending;
    legacy fields + Event/CreatedDateUtc/Source. AuditLog(page) does nothing when AuditMode = Server.
- Audit/AtlasAuditWriter: LogMessage = [{"Name":"_Event","Value":"Create|Update|Delete"}, {Name, Label, Old, Value} ...];
  CreatedBy = display name; CreatedByLanId if the column exists; CreatedDate per AuditTime (Utc for TTDV2). No entry if
  nothing changed.
- Application/CPTTDV2Service : ConfigSectionService; OnLoad sets the grid headers "dddd dd-MMM-yyyy" from WeekEnding
  (days -6..0). Application/TTDV2/TaxYearsOptionProvider (Year+1 .. Year-3; default Options[1]) and
  WeekEndingsOptionProvider (TimeProvider).
- Finding B6 to record: the legacy LoadWeekEnding loop adds nextSunday+7 three times (duplicate dates). TTDV2 gives the
  last Sunday + the next 3 different Sundays.
- Config/TTDV2.json: from the target TTD.json (sections TaxTimeDatabase1-3 + the WeeklyHour grid at section level);
  Fields = TTDV2 columns (TaxPreparer -> TaxPreparerLanId; ModifiedBy Type "user"); no "Code" key (enum); Rules,
  Permissions, OwnerFields ["CreatedBy","TaxPreparerLanId"], Defaults, Computed WeeklyTotal, SoftDelete,
  HeaderNameWithId; buttons with Visible true (the CPButton default is false).
- Register in AtlasPlatformServices. Tests: load defaults; save refusals (extra name, 1.5, 1:30, -5, 1441); WeeklyTotal
  calculated again; concurrency; audit events; access matrix through the service (SQL tests).

## Remaining phases after C5
- C6: local/setup-local-db.ps1 (create CPAtlasPlatformDev; run 000 + 11 + 06 view + 12-ttdv2-app; seed users incl. the
  current Windows login; sample rows; a local dbo.Get_Application). Demo host: Negotiate auth
  (Microsoft.AspNetCore.Authentication.Negotiate, fallback StandIn:Authentication = Fixed), CORS with credentials for
  http://localhost:4200, Swashbuckle 10.2.3 (as at the target). Stand-in CPAtlasController (v1/Application + FormType/
  HasDashboard, v1/Dashboard, v1/SectionPage, v1/ManagePage, v1/RemovePage, v1/AuditLog, v1/GetAuditLog; string code;
  CPAtlasService resolver hook), v1/authorize, dev "act as" header (stand-in only).
- C7: dashboard service (Source: Table query builder, Any, Type user, menus from Permissions, Add Target) +
  Config/TTDV2.Dashboard.json (spec 12, 3.2).
- C8: start-up check hosted service (loads the table metadata into the registry BEFORE the first request; manifest vs
  registration; app row; fields vs columns; dashboard columns; providers; unknown rule roles = warnings) + --atlas-check.
- C9: WebUi Angular 21 in atlas-platform/WebUi: shell (stand-in); platform form (route form/:code/:id, renderer registry,
  effects, validators, grid row, save/errors/delete/history); platform dashboard (route apps/:code,
  ag-grid-community 33); @pbdev stand-in package + tsconfig path.
- C10: sql/release-p1 numbered copies; INTEGRATION.md and local/README.md complete; full test run; start the API and the
  UI for the user's browser review; update the spec and this handoff.

## Next-session prompt
"Resume atlas-platform. Read the top entry of CODEX_HANDOFF.md. Continue autonomously from C5 through C10, commit after
each phase, do not wait for input; finish with TTDV2 running locally for my browser review."

---


## Current goal
Make CP Atlas (target app, copies in target-implementation-read-only/) a reusable platform so the next 13 apps
(TD, PBSP, AR, CIO, TARU, CAT, IMPBBW, IPEW, CA, CF, BBHILLQTT, CRRFIR, IMGUIGU; all Section Master) integrate with
low-code changes (JSON + SQL + one registration line), backward compatible. Phase 1 = design specs in
docs/atlas-platform/ (done for Section Master apps); no workflow engine change; same repo/branch (console-code-first-0518).
UPDATE 2026-10-25: the user is also the target decision maker. After the design session we CODE the generic
approach; the user integrates it at the target, backward compatible. Code location (decided, option B): separate solution atlas-platform/ in this repo (not in WorkflowEngine.sln),
built against stand-ins for missing target parts; user copies into target. Coding starts after the design session.
Specs are written in ASD-STE100 (~80%). Each spec = .md (source) + .html report generated from it.

## How we work (user preferences)
- Ask the open questions ONE AT A TIME; after each answer update .md, regenerate .html, then ask the next.
- Keep explanations short and plain; the user said a deep detour "is a tad confusing".
- Do not rewrite specs when the user says so; record answers as short "Answered (date): ..." lines.
- Deliver every DB change as a SQL script (dev: team runs; QC/prod: DBAs run DDL). Scripts additive, idempotent.
- Commit/push only when the user asks.

## Regenerate an HTML report from its .md
WARNING: 01-section-master.html is hand-made and differs from its .md. Do NOT regenerate it; patch the HTML text directly.
Use docs/atlas-platform/tools/md2report.py (copy of the session generator). From docs/atlas-platform:
python tools/md2report.py 06-security.md 06-security.html 01-section-master.html "<eyebrow>" "<title>" "<lead>" "<n|label;n|label;...>"
(The 3rd argument is the file to copy the <style> from. Findings must be headed "### XX-nn Title (High|Medium|Low[, area])".
Nested lists are not supported: keep bullets flat.)

## Module status (docs/atlas-platform/README.md is the index)
- Module 1 Section Master: 01-section-master.md/.html - draft 3, ALL questions answered (Q1-Q9, T1-T4, F1-F3). Committed d992d65.
  SQL: sql/01-section-master-rowversion.sql (SQMS schema), sql/01-section-master-role-scope-check.sql (read-only, parts 1-5).
- Module 5 Data mapping: 05-data-mapping.md/.html - draft 3, all Q1-Q7 answered, committed 9d47952. Target files arrived in
  target-implementation-read-only/Data/Data/Data extended/ (context, DbContext.t4, EntityType.t4, Data csproj).
  New: DM-08 context hand-edited after scaffold, DM-09 Atlas context maps 19 engine tables + MockSectionData,
  DM-10 dbo naming/old Http package; 6.2.1 scaffold settings. Q7 answered: scaffold all dbo tables except
  the 19 engine tables + MockSectionData (script builds --table list from sys.tables). No scaffold command/efpt.config.json was given (6.2.1 proposes scaffold.ps1).
  SQL: sql/05-new-app-template.sql.
- Module 6 Security: 06-security.md/.html - draft 2, ALL questions answered, committed c03388d.
  Findings SE-01..SE-07. Answered: Q1 two AD groups (end users | support) in AppSettings:ADGroup, gate only, per-app
  access only from entitlement tables; Q2 IIS + Windows auth, no JWT callers -> remove JWT (M17 updated in issues doc);
  Q3 LAN ID for people (new apps) + LAN ID columns for existing tables; Q4 some apps hide records by user data ->
  per-app SQL rule (fn_VisibleRecords + Can_Access_Record); Q5 log-only one release, then enforce per app
  (AppSettings:Modules:<Code>:AccessMode).
  Q6 team grants/revokes by SQL by hand (admin screen later) -> sql/06-grant-revoke-role-template.sql (soft delete);
  IsAdmin/IsWrite/IsRead = app-level flags per role, platform uses them as @Read/@Write/@Admin in Permissions lists;
  Admin includes Write+Read, Write includes Read.
  SQL (not executed): sql/06-active-user-roles-view.sql, sql/06-lanid-columns-ddl.sql, sql/06-lanid-backfill-dml.sql,
  sql/06-record-visibility-template.sql, sql/06-grant-revoke-role-template.sql.
- Module 3 Dashboard: 03-dashboard.md/.html - draft 2, ALL questions answered, committed 0ece00f. Findings DB-01..DB-08 (DB-01 High: hidden tabs'
  rows still sent to browser; DB-02 High: no app access check, Get_CDT_Dashboard no user filter). Design: rules before
  load, Load Eager|Lazy + v1/DashboardTab, tokens {LanId}, Target on buttons, Columns list, menus from Permissions.
  Q1 answered: server paging per tab (4.2.1, Paging Client|Server). Q2 answered: Source Table list query from JSON (4.7), procs only for special tabs. Q3 answered: export + date filter from AG Grid Enterprise, saved views out of scope.
  Q4 answered: one dashboard file per app (existing may stay shared). Q5 answered: deployed copy = Atlasapp/Atlasapp/Config (with AIN); other 2 copies old.
- Module 4 Rules engine: 04-rules-engine.md/.html - draft 2, ALL questions answered, committed 5751c7e. Findings RE-01..RE-07 (RE-02 High first
  match wins -> AIN Admin+Read gets read-only; RE-03 High no-match keeps JSON = editable; RE-05 display-only).
  Design: IAtlasRuleEngine for all 6 targets, RuleMode FirstMatch|MostPermissive, RuleDefault Keep|ReadOnly|Hidden,
  Relations (~Seller replaces SQMSB CHECKSELLER code), rules before Effects, same rules on save, start-up check,
  v1/RulePreview (@Admin). SQL read-only: sql/04-multi-role-users.sql. Corrected module 1 10.5 note (first match wins).
  Q1 answered: multi-role yes; only TTD + new apps MostPermissive, existing keep FirstMatch. Q2 answered: RuleDefault ReadOnly for new apps. Q3 answered: section owners (4.4.1), by role AND by person (Relation Record Self), per app/section.
  Q4 answered: keep v1/RulePreview.
- Module 7 Registry: 07-registry.md/.html - draft 2, ALL questions answered, committed 685bc2c. Findings RG-01..RG-06 (5 if-chains -> null/500,
  services via new not DI, enum app code, settings in 7 places + absolute paths, parent in 2 places, menu shows broken apps).
  Design: <Code>.app.json manifest, AddAtlasPlatform + AddAtlasModule<T>, IAtlasModuleResolver (keyed DI, 404/400),
  string app code + CPAtlasPage.AppCode, start-up check vs CP_Atlas_Application, v1/Application + FormType/HasDashboard,
  AppSettings:ConfigFolder. Q1 answered: one AddAtlasModule line per app (code review). Q2 answered: settings in JSON manifest; DB keeps identity+roles.
  Q3 answered: Enabled per environment. Q4 answered: client+API always deployed together.
- Module 8 Attachments: 08-attachments.md/.html - draft 2, ALL questions answered, committed cf61ecd. Findings AT-01..AT-07 (H1 download any file,
  H2 upload path traversal, no access check on download/remove, disk copies stay + removed files downloadable,
  only CDT supports files - Section Master client sends empty file list, no limits). Design: IAtlasAttachmentService,
  DB storage GZip, v1/Attachment/{app}/{id} + v1/Download kept but DB lookup by id, Path.GetFileName, memory GZip,
  Accept/MaxSizeMb/MaxFiles, same transaction, audit. Q1 answered: per-app <Schema>.Attachment (template part 3a; manifest AttachmentTable). Q2 answered: 10 MB, pdf docx xlsx csv txt msg png jpg. 
  Q3 answered: no virus scan service. Q4 answered: upload/download/remove only.
- Module 9 Audit/history: 09-audit-history.md/.html - draft 2, ALL questions answered, committed 83411c1. Findings AU-01..AU-05 (client interceptor
  writes audit -> forgeable/missing; Execute GET + RemoveAttachment + engine changes never audited; GetAuditLog no
  access check; no Old/order/labels; local time + display name). Design: 6 server events, LogMessage keeps Name/Value
  + Label/Old + first item _Event, AuditMode Client|Server (v1/AuditLog no-op in Server), GetAuditLog access check
  (History) + sort + optional fields, workflow history via engine IWorkflowInspectionReader merged (Source=Workflow).
  Q1 answered: workflow steps + field changes in one list. Q2 answered: existing tables keep local time, new UTC, API returns CreatedDateUtc. Q3 answered: no retention rule. Q4 answered: no Audit:false fields today (feature optional).
- Module 10 Engine adapter: 10-engine-adapter.md/.html - draft 2, ALL questions answered, committed 3899622. Findings EA-01..EA-05 (H6 engine
  commits inside Atlas tx; H8 Success not read, SLA error swallowed; ~150 lines per service; roles all-apps/fixed MFO,
  recipients by display name .ToList()[0]; new GUID idempotency). Design: IAtlasWorkflowAdapter (DI engine services,
  binding from JSON), instance ID WF-<prefix><ID> unchanged, order: write+SaveChanges -> engine last -> Success?
  commit : rollback+error; dispatch after commit; state check on load (WorkflowSync audit); RequestId idempotency;
  recipients by LAN ID. No engine change. Q1 answered: engine last + check on load + error log, no background job. Q2 answered: outbox retry job runs;
  H9 migrations to be applied (module 11 prerequisite). Q3 answered: refused action always stops save. Q4 answered: next Step Master app uses same engine+adapter (step-level binding).
- Module 11 Config/deployment: 11-config-deployment.md/.html - draft 2, ALL questions answered, NOT COMMITTED. Findings CD-01..CD-06 (env
  values in base appsettings; named persons in Smtp:RoleEmailMap base file; H9 engine migrations missing at target;
  no SQL script order/log; JSON loader must accept comments+trailing commas; fixed log paths). Design: config layers
  (base no hosts/emails; appsettings.<Env>.json; TPAM unchanged), tolerant STJ loader, --atlas-check command in
  pipeline, sql/release-<n>/ numbered scripts, engine idempotent migration script first, dbo.CP_Atlas_ScriptLog,
  switch table, log event names, release plan (see Q4). Q1 answered: today manual server edits; team must move to env files (guide 4.1.1). Q2 answered: DBA runs idempotent engine script first.
  Q3 answered: dbo.CP_Atlas_ScriptLog (sql/11-script-log.sql). Q4 answered: release order P1 TTD pilot -> P2 first new app -> P3 security fixes
  (H1,H2,DB-01; accepted risk) -> P4 migrate SQMS/AIN (+H6/H8, enforce, retire old component) -> P5 later.
  Platform built BESIDE current code: new generic form component + generic section service; old Section Master frozen.
  SQL: sql/11-script-log.sql (release P1).
- Module 12 TTDV2 pilot build: 12-ttd-pilot-build.md/.html - draft 2, ALL Q1-Q6 answered, NOT COMMITTED. P1 = TTDV2, TTD rebuilt as a
  NEW app (own schema TTDV2, typed int/date columns, LAN IDs, RowVersion, UTC); current TTD untouched = reference. Q1 AtlasPlatformContext
  (shared-type only, same connection/tx). Q2 LAN ID (native in TTDV2; TTD LAN ID scripts stay P4). Q3 no TTD switch (replaced by TTDV2).
  Q4 TTD stops at once: copy script part 2d revokes TTD roles, then Enabled=true. Q5 new route form/:code/:id, FormType FORM, dashboard uses
  FormType/Target before if-chains. Q6 dashboard Source: Table for 3 tabs + new Any filter group + column Type "user"; All Report shows 7 days.
  Code layout decision: keep 4 target projects, Platform/ folder in each, CP prefix, BBH.CP.Atlas.* namespaces; stand-ins not copied.
  SQL (not executed): sql/12-ttdv2-app.sql (040), sql/12-ttdv2-copy-from-ttd.sql (050). Template gap: add CreatedByLanId to 05 audit table before P2.
- Modules not started: 2 Step Master (on hold; CDT + one more app). Design session done for the 13 Section Master
  apps. NEXT: write docs/atlas-platform/12-ttd-pilot-build.md = one checklist of every decision TTD needs (vertical
  slice), with comparison tests vs CPTTDService -> becomes the coding plan for atlas-platform/.

## Key facts (do not re-derive)
- Live: .NET 10 + EF Core 10; IIS Windows auth; live SQMS schema is SQMS (target engine already switched; local
  repo engine copy still says SSQMS - do not change local engine unless asked; issue H14 closed for target).
- Pilot = rebuild TTD on the new approach (module 1 section 10). TTD roles: Read view; Prepare create + edit own;
  Owner all incl. delete; own = CreatedBy or TaxPreparerName.
- Families: SQMS (Opportunity parent, Bid child); one future app parent + 2 children, maybe one parent + 1 child.
- Audit JSON keeps Name/Value keys and adds Old (History window parses Name/Value).
- Scaffold: SQMS/TTD/AIN + all dbo tables except the 19 engine tables and MockSectionData (module 5 Q7); new apps use own
  schema + shared-type entities (no scaffold).
- Release plan (module 11 4.7, user decision): P1 TTD pilot -> P2 first new app -> P3 H1/H2/DB-01 fixes (accepted risk,
  do not re-argue) -> P4 migrate SQMSO/SQMSB/AIN (+H6/H8, enforce) -> P5 attachments/adapter/paging/Step Master.
  Build BESIDE current code: new generic form component + ConfigSectionService; old Section Master frozen.

## Next steps
1. Commit module 11 (+ sql/11-script-log.sql, README, handoff) when the user asks.
2. Write docs/atlas-platform/12-ttd-pilot-build.md (+ .html): one checklist of all decisions TTD needs (modules 1, 3-11),
   scope in/out of P1, comparison tests vs CPTTDService, file list for atlas-platform/. Ask open points one at a time.
3. Then plan coding: atlas-platform/ solution layout, stand-ins for missing target parts (Infra, TPAMAPIAccess, @pbdev).

## Uncommitted at save time
docs/atlas-platform/11-config-deployment.md (+ .html), sql/11-script-log.sql, docs/atlas-platform/README.md, CODEX_HANDOFF.md.

## Next-session prompt
"Resume the CP Atlas platform specs. Read the top entry of CODEX_HANDOFF.md. Module 11 is done (commit when I ask).
Next: write the TTD pilot build spec (12-ttd-pilot-build.md) from all module decisions, then plan coding in atlas-platform/.
One question at a time, short."

---
# Atlas platform: module 5 Data mapping draft 2, all questions answered - 2026-10-25

New: docs/atlas-platform/05-data-mapping.md/.html, sql/05-new-app-template.sql (not executed). Findings DM-01..DM-07.
Decisions: Q1 option A (own typed table + own audit table per app; SQMS keeps shared SQMS.AuditLog). Q2 team scaffolds all; target team will limit scaffold to dbo/SQMS/TTD/AIN, keep OnModelCreatingPartial, add one platform partial file; new apps use own schema (shared-type entities). Q3 typed columns for new apps. Q4 dev login full DDL/DML; QC/prod login DML+execute, no DDL; metadata visible via object permissions, no VIEW DEFINITION; template has commented schema GRANT step. Q5 audit JSON keeps Name/Value keys and adds Old (History window parses Name/Value, history-report-modal.ts.txt:40-42); audit read only by per-record History window. Q6 team runs template in dev, DBAs in QC/prod.
Files needed to confirm scaffold details (optional): CPSharepointContext.cs, CodeTemplates/EFCore contents, scaffold command or efpt.config.json, Data .csproj.
Module 5 NOT final: waiting for target files (context, EFCore templates, scaffold command or efpt.config.json, Data .csproj); finalize 6.2 scaffold steps when they arrive. Not committed. Next: module 6 Security (include display-name ownership).

---
# Atlas platform: Section Master draft 3, all questions answered - 2026-10-25

User answered every Section Master question one at a time. Decisions now in docs/atlas-platform/01-section-master.md/.html:
- Q1: the 13 unused CPApplicationCode values are the next 13 apps, all Section Master. Pilot = rebuild TTD (section 10).
- Q2/Q3: live form shows grid + file upload; template oddities were copy errors; vehicle = one dropdown driven by fund.
- Q4: live = .NET 10 + EF Core 10 (H11 closed). Q5: each app has own rules/roles; child apps may use parent roles (RoleScope, section 11).
- Q7: send real user roles (ActorRoles "User"); engine does the role check. Q8: engine SQMS scheduled job writes Status/WorkflowState/ModifiedBy/ModifiedDate with explicit columns; rowversion safe and prevents lost updates.
- Q9: live schema is SQMS; target engine already switched; local repo engine copy still SSQMS (H14 closed for target, do not change local engine unless asked).
- T1 server computes WeeklyTotal (Computed); T2 whole minutes 0-1440; T3 Read view / Prepare create+edit own / Owner all incl. delete (10.5, Permissions, OwnerFields, RuleMode MostPermissive); T4 own = CreatedBy or TaxPreparerName.
- F1 one family with parent + 2 children, maybe one with parent + 1 child, rest single; F2 one level only; F3 SQMS child sees copied parent fields only; other families configurable via View.Mode None/CopiedFields/Fields/Full.
New findings: SM-19 MakeBid StartsWith bug; role queries ignore IsDeleted; ModifiedDate UTC vs local.
SQL (not executed): sql/01-section-master-rowversion.sql (SQMS schema, pre-check), sql/01-section-master-role-scope-check.sql (read-only, parts 1-5).
Issues doc: H14 added and closed for target; High count 14.
Not committed yet at time of writing.
Next: commit/push; then module 5 Data mapping, then module 6 Security (include display-name ownership).

---
# Atlas platform: Section Master draft 2, complete for review - 2026-10-25

User decision: only ONE more app uses Step Master; all other new apps use Section Master. Step Master spec waits. Section Master is the priority.
Completed: added save-path findings SM-13..SM-18 (copied save pipeline, client-trusted save H3/H4, workflow values in code + H8, hidden errors H7, client-written audit, no concurrency) and design 5.9-5.13 (12-step server save pipeline with allow list, workflow binding from JSON, additive Errors contract with HTTP 200 in phase 1, server audit, rowversion token). Migration steps 7-12, extra regression checks, Q1/Q6 answered, Q7/Q8 added. HTML report mirrors the md. New SQL: docs/atlas-platform/sql/01-section-master-rowversion.sql (additive, idempotent; not executed).
Changed files: docs/atlas-platform/01-section-master.md, 01-section-master.html, sql/01-section-master-rowversion.sql, README.md. Not committed.
Remaining: target-team answers Q1 (codes), Q2-Q5, Q7, Q8; then next module (recommend module 5 data mapping, then 6 security).
Next-session prompt: "Section Master spec draft 2 is complete for review. Commit if not done; start module 5 Data mapping."

---
# Atlas platform: design-only specs started, Section Master draft 1 - 2026-10-24 (session 2)

Current goal: Make CP Atlas a reusable platform so ~16 more business apps integrate with low-code changes. User chose Option 3: design specs only in docs/atlas-platform/; no Atlas code, no workflow engine change, same repo and branch.

Completed changes:
- docs/atlas-platform/README.md: goal, rules, spec template, module list (11 modules).
- docs/atlas-platform/01-section-master.md: findings SM-01..SM-12 and reusable design (client Effects, CPKeyValue.Data, CPButton.Action, Redirects, renderer registry, keyed-service module registry, ConfigSectionService, dashboard FormType), backward-compatibility rules, migration steps, regression checks, open questions.
- Key facts: CPApplicationCode already lists 13 unused codes (TD, PBSP, AR, CIO, TARU, CAT, IMPBBW, IPEW, CA, CF, BBHILLQTT, CRRFIR, IMGUIGU), probably the planned apps. SQMSO role lookup is not filtered by application (SM-08); AIN is.
- docs/README.md indexes the specs.

Changed files: docs/atlas-platform/README.md, docs/atlas-platform/01-section-master.md, docs/README.md, CODEX_HANDOFF.md. Not committed.
Build/test status: not run (documentation only).
Remaining tasks: user review of Section Master draft; answers to its open questions; next module (Step Master / CDT, module 2).
Next-session prompt: "Continue Atlas platform specs (docs/atlas-platform/). Section Master draft 1 is done; review open questions, then start module 2 Step Master."

---
# Target analysis: Atlas docs indexed, H9-H12 status added - 2026-10-24 (session 2)

Current goal: Finish the CP Atlas read-only analysis follow-ups.

Completed changes:
- H9: no new SQL is needed. The scripts already exist and match the EF migrations column for column: sql/upgrades/20261014_outbox_retry_scheduling.sql (20261014222530) and sql/upgrades/20261019_01_cdt_v2_schema_ddl.sql (20261018214009). The user reported both ran in TEST on 2026-10-19. The issues doc now has a Status line: apply both, in order, to each other environment.
- H10 and H11: marked "probably resolved" from user-reported evidence (TEST start-up + v1 form load; target build with engine files). Live values still to confirm per environment.
- H12: still open. The copy confirms the config sends ReportType and AIN.Get_AccountInventory has no parameters. Ask for the live proc (sp_helptext).
- M17, M24, L18 Confirm items: not changed; they need the deployed appsettings, the live AIN table and the business owner.
- docs/README.md: indexed TARGET_ATLAS_APPLICATION_FLOW.html and TARGET_ATLAS_ISSUES_BY_SEVERITY.html.
- Not published as artifacts: the issues doc lists exploitable defects (path traversal, XSS) in the target team's internal application. Publish only if the user asks.

Changed files: docs/TARGET_ATLAS_ISSUES_BY_SEVERITY.html, docs/README.md, CODEX_HANDOFF.md. Committed locally with docs/TARGET_ATLAS_APPLICATION_FLOW.html and docs/target-api-workflow-integration-evidence.html (README links to it). Not pushed.
Build/test status: not run (documentation only). Seed verification: not run. Sample JSON: not executed.
Remaining tasks: target-team answers for H10/H11 (other environments), H12, M17, M24; business answers for M6 and L18.
Next-session prompt: "Continue CP Atlas follow-ups. H9 scripts exist in sql/upgrades; open Confirm items are H12, M17, M24, plus per-environment checks for H10/H11."

---
# Target analysis: CP Atlas holistic flow and issues documents - 2026-10-24

Current goal: Read-only analysis of `target-implementation-read-only/` (CP Atlas). The user wants the WHOLE application explained: framework, security, modules, UI, data and deployment. The workflow engine is one integrated part, not the only subject. Write the documents in ASD-STE100 at about 80% compliance.

Completed changes:
- `docs/TARGET_ATLAS_APPLICATION_FLOW.html`, 12 sections: what CP Atlas is; architecture; metadata framework (module JSON, control types, CPRule rules engine, MapData/BindData, dashboard JSON); security model; request flows; workflow engine integration (division of work, registration, Workflow JSON block, engine calls, background jobs); CDT; SQMS Opportunity/Bid; TTD; AIN; data stores; configuration/deployment.
- `docs/TARGET_ATLAS_ISSUES_BY_SEVERITY.html`: 13 High, 26 Medium, 18 Low, 3 Info, plus a copy-error section. Each item has an area tag. "Confirm" marks items that depend on files we do not have.
- Main High items:
  - H1 Download path traversal.
  - H2 upload file-name traversal.
  - H3 MapData mass assignment.
  - H4 no server-side access check on save/delete. CDT sends step-config roles as the engine actor roles.
  - H5 dashboard XSS.
  - H6 engine/Atlas transaction split.
  - H7 hidden save errors.
  - H8 engine result not checked.
  - H9 the target DB is missing migrations 20261014222530 and 20261018214009 (no NotificationOutbox.NextAttemptAtUtc).
  - H10 no DefaultConnection (Confirm).
  - H11 Atlas net9.0 versus engine net10.0 (Confirm).
  - H12 AIN dashboard sends @ReportType to a proc with no parameters (Confirm).
  - H13 SQMSB Departments dictionary lookup crash.

Facts established:
- Modules: CDT (PortfolioTrade), SQMSO (SqmsOpportunity) and SQMSB (SqmsBid) use the engine. TTD and AIN do not.
- The engine instance ID is "WF-" + BusinessObjectId: CDT-{id}, SQMS-OPP-{id} or SQMS-BID-{id}.
- CDT page state comes from CDT_General.WorkflowState. Only SQMS reads IWorkflowContextReader for the buttons.
- No hosted background worker exists in the engine or in Atlas, so run-sla and process-notification-outbox need a scheduled job.
- Engine SmtpNotificationSender no longer reads Smtp:RoleEmailMap. It reads only Smtp:RecipientOverride, so the RoleEmailMap keys in Atlas appsettings are unused.
- TradingDeskRework allows only TradingTradeExecuted in the engine seed. The CDT UI still offers "No" there (issue M6).
- BDRM "No" -> BdrmTradeFulfilled is by design (F01; see the memory cdt-bdrm-no-maps-to-fulfilled). This supersedes the 2026-10-19 note below that called it a mismatch.
- Copy-error rule: the .txt files are imperfect copies. Code that cannot compile (OppportunityId, CPBBContext vs CPPBContext, garbled UI files) is copy noise, not a defect.
- Atlasapp/Atlasapp/Config/*.json is the newer config copy.
- WebUi/WebUi/Source is the newer UI copy.

Changed files: docs/TARGET_ATLAS_APPLICATION_FLOW.html, docs/TARGET_ATLAS_ISSUES_BY_SEVERITY.html, CODEX_HANDOFF.md. No source code was changed. No target files were changed.
Build/test status: not run, because this was documentation only. Seed verification: not run. Sample JSON: not executed.
Remaining tasks:
- If the user asks, add both docs to docs/README.md and commit.
- The user may want the docs published as shareable pages.
- Resolve the "Confirm" items with the target team.
- Supply DDL scripts for the two missing migrations (H9).
Known risks:
- The issues are taken from copies; the live target code may differ.
- Some agent-reported items were spot-checked, not fully re-traced.
- The status line was not configured: no PS1 or PowerShell profile exists, so the user needs to say what it should show.
Next-session prompt: "Continue CP Atlas target analysis. Flow and issues docs are in docs/TARGET_ATLAS_*.html (ASD-STE100 ~80%). Ask whether to index in docs/README.md, commit, or publish; follow up on Confirm items and H9 migration scripts."

---
# Manual integration: Target publication reported complete, action mapping check next - 2026-10-19

User says "all done" after target commit/push checklist. Record target commit/publication as user-reported complete; target commit SHA/remote result not supplied or independently verified. Do not perform local/target Git writes or remote actions. Next gated read-only check: inspect target Config/CDT.json Workflow.Notifications.BdrmTradeNotExecutedProperly.ActionKey. Snapshot incorrectly maps this entry to BdrmTradeFulfilled; ask user to show live entry before changing it. Desired correct action key is BdrmTradeNotExecutedProperly, preserving Trading Desk Rework branch. V2 remains inactive absent later activation confirmation. Do not claim full integration acceptance.

Changed: CODEX_HANDOFF.md only, to preserve progress. Tests/seed/sample JSON: not run locally. Prior user-reported evidence: target final build, v1 form load, schema/membership/cleanup and v2 SQL checks passed. Remaining: live action mapping and adapter/date/desk/error checks, controlled test activation, complete workflow/SLA/SMTP/SQMS acceptance. Known risks: target publication not independently verified; live config may differ from snapshot. SQL-only delivery and user-managed DI remain in force. Next-session prompt: "Continue manual TEST target integration. User reports target commit/push complete. Inspect live CDT.json BdrmTradeNotExecutedProperly notification ActionKey before test activation."

---
# Manual integration: Final build successful, target commit checklist requested - 2026-10-19

User confirms target build successful after CPCDTService edit and asks for actual target files to commit plus a valid message while user commits/pushes at target. No Codex target Git/remote action requested or performed. Expected scope: 13 bundled runtime C# files at actual target locations (dispatcher specifically WorkflowEngine.Infrastructure/WorkflowNotification/WorkflowActionNotificationDispatcher.cs), InfrastructureServiceCollectionExtensions.cs, target service file containing CPCDTService, and project/central dependency change for PKCS 10.0.13. Expected 15 C# files plus dependency file(s), subject to actual target git diff. Include reviewed SQL deployment scripts/docs when saved in target repository; direct database execution alone creates no Git change. Keep activation/reactivation scripts as deployment assets without claiming executed. Program.cs only if actually changed with intentional final diff; no duplicate option registrations. Do not include source EF migrations/snapshots/seed initializers as SQL-only target deployment changes.

Evidence user-reported: final build success, existing v1 form loads, schema scripts, inactive v2 creation, five test recipient additions, bulk placeholder deactivation and SQL verification PASS. V2 activation and full target workflow/SLA/SMTP/SQMS regression remain pending. Commit message must distinguish these limits. Actual target Git status not available; ask for git status --short and git diff --stat if exact staging verification needed. User's commit/push completion not yet confirmed.

Changed: CODEX_HANDOFF.md only, to preserve progress. Tests/seed/sample JSON: not run locally. Remaining: user target publication, config/adapter checks and test activation/full acceptance. Known risks: target filenames/layout may differ from source manifest; staged scope not inspected. Next-session prompt: "Continue TEST target integration after target commit checklist. Confirm user commit/push if needed; v2 activation/full workflow acceptance remain pending."

---
# Manual integration: Adapter edit reported complete, no remaining bundled C# files - 2026-10-19

Latest confirmation: after asking which CPCDTService changes were needed, user explicitly confirmed the version-2-only selectedCcUsers removal block is complete. Next gated step is dotnet build -warnaserror in target solution directory; wait for new build result before configuration/activation guidance. Earlier successful build preceded this adapter edit.

User says "am all done" after version-2-only selectedCcUsers removal instructions and asks if additional C# files need integration. Treat adapter edit as user-reported complete, final build/runtime not confirmed. Manifest contains 13 runtime C# files, all already user-confirmed in restarted pass. Additional manual C# changes were InfrastructureServiceCollectionExtensions.cs option registrations and CPCDTService.Execute recipient filtering; PKCS reference is in project/central dependency file. No additional bundled C# files remain. Do not copy source EF migrations, model snapshots or seed initializers to target as deployment steps.

Next: final target warning-as-error build after adapter edit, then target CDT configuration/live adapter checks (ResubmitForChecker, snapshot BdrmTradeNotExecutedProperly effective-action mismatch, current date/desk forwarding and SLA validation failures), controlled test activation and real target acceptance. Do not claim full target integration verified solely from completed file transfer or SQL PASS. V2 remains inactive absent a later explicit confirmation.

Changed: CODEX_HANDOFF.md only, to preserve progress. Tests/seed/sample JSON: not run locally. User-reported evidence: prior target build, v1 form loads, schema/membership/placeholder cleanup and v2 SQL PASS. Known risks: latest C# edit not rebuilt, shared config and full runtime paths unverified. SQL-only delivery and user-managed DI remain in force. Next-session prompt: "Continue TEST integration after all bundled C# files and manual DI/CDT adapter edits. Final build and configuration/acceptance checks remain; v2 not yet activated."

---
# Manual integration: V2 SQL checks passed, CDT recipient adapter next - 2026-10-19

User reports packaged v2 verification PASS. Next gated target-only edit: in CPCDTService.Execute (snapshot Service/Service/Application/CPCDTServices.txt near lines 689-692), after GetWorkflowNotification and before WorkflowNotificationRequestMapper.Map, for effective notify.ActionKey CheckerReturnToMaker, TradingReturnToMaker or BdrmTradeFulfilled read existing instance context via WorkflowContextReader.Read(WorkflowContextReadRequest(workflowInstanceId, User.LanID, step.Workflow.Roles)). Only when context.WorkflowContext.Workflow.WorkflowDefinitionVersion == 2 remove selectedCcUsers from notify.Request.RecipientInputs. Keep v1 inputs unchanged and preserve mapper/hash ordering. Snapshot CPApplicationService.GetWorkflowNotification creates a fresh request populated according to shared CDT.json; removing global config keys would affect v1. Existing contract exposes WorkflowDefinitionVersion via context. Do not activate v2 until adapter readiness; wait for user edit confirmation then build/check next adapter concerns (effective-action mapping, latest date/desk, validation error propagation).

Changed: CODEX_HANDOFF.md only, to preserve progress. Tests/seed/sample JSON: not run locally; target SQL PASS is user-reported. Known risks: live target adapter may differ from snapshot; shared config BdrmTradeNotExecutedProperly maps to BdrmTradeFulfilled in snapshot and needs separate review before full acceptance; target SLA exception swallowing and saved-date/desk flow also pending. SQL-only database delivery/user-managed DI remain in force. Next-session prompt: "Continue manual TEST integration. V2 SQL checks passed. Version-2-only selectedCcUsers removal in CPCDTService.Execute instructions provided; await edit confirmation before advancing."

---
# Manual integration: Bulk cleanup succeeded, v2 verification next - 2026-10-19

User reports bulk placeholder deactivation "worked". Do not assume zero-row follow-up inventory was independently confirmed; user did not supply output. Next gated step: execute complete sql/upgrades/20261019_04_portfolio_trade_v2_verify.sql against same TEST database and paste final result/errors. Review script before instructions; v2 must stay inactive until metadata and adapter/runtime gates are ready. All five test group additions previously confirmed. No activation instruction or production deployment authorization.

Changed: CODEX_HANDOFF.md only, to preserve progress. Tests/seed/sample JSON: not run locally; SQL cleanup user-reported. Remaining: verification result, target adapter changes/full v1/SQMS/v2 runtime acceptance, controlled test activation when ready. Known risks: recipient/route correctness not yet verified; source and target test runtime evidence remain separate. SQL-only database deployment and user-managed DI remain in force. Next-session prompt: "Continue manual TEST integration. Bulk placeholder cleanup succeeded; packaged v2 verification script instructions provided. Await results before activation or further steps."

---
# Manual integration: Bulk placeholder deactivation requested - 2026-10-19

User rejects one-address-at-a-time cleanup and asks to adapt inventory SQL to remove all placeholders together. Latest instruction supersedes sequential deactivation plan. Provide one transactional UPDATE joining definition/version/routes/rules, scoped to PortfolioTrade version 2, active StaticAddress rules and exact seven original placeholder addresses. Set IsActive=0 (preserve rows/history), OUTPUT affected address/ID/status, leave v1 and user-added addresses untouched. All five real test group additions were user-confirmed before this cleanup. First individual deactivation was instructed but not confirmed; bulk filter is safe for already inactive rows. Wait for result, then rerun placeholder inventory and packaged verification. V2 remains inactive; no activation instruction yet.

Changed: CODEX_HANDOFF.md only, to record user's bulk-cleanup preference and next SQL. Tests/seed/sample JSON: not run locally; no Codex database execution. Remaining: bulk update result, verification, target adapter/full test acceptance and activation gate. Known risks: user-edited target data not independently checked; cleanup targets exact packaged placeholder spellings. SQL-only delivery and user-managed DI remain in force. Next-session prompt: "Continue manual TEST integration. Bulk v2 placeholder deactivation SQL provided at user's request; await result before verification."

---
# Manual integration: Placeholder inventory received, first ICG placeholder removal next - 2026-10-19

User pasted read-only v2 placeholder inventory, with expected ICG/RA, Risk and three desk example.test addresses across routes (25 rows apparent; pasted text has OCR/truncation, so do not treat exact spelling/count as independently verified). No copied rework placeholder route shown. Next gated step: use complete working-copy membership template with VersionNumber=2, Group=ICG_RA, Address=icg-ra-1@example.test, Action=DEACTIVATE. Wait for result before next placeholder. Preserve user-added test recipients and all v1 data. Remaining original placeholders: icg-ra-2@example.test, risk-team-1@example.test, risk-team-2@example.test, taxable-trader-1@example.test, tax-exempt-trader-1@example.test, equity-trader-1@example.test. V2 remains inactive.

Changed: CODEX_HANDOFF.md only, to preserve progress. Tests/seed/sample JSON: not run locally. Remaining: placeholder deactivation one step at a time, recipient verification including copied rework audience, target adapter and full acceptance before activation. Known risks: copied result text is imperfect; use exact addresses from packaged template/creation script and report errors. SQL-only delivery and user-managed DI remain in force. Next-session prompt: "Continue manual TEST integration. Placeholder inventory received. DEACTIVATE icg-ra-1@example.test in v2 ICG_RA requested; await result before next address."

---
# Manual integration: Five groups populated, placeholder inventory next - 2026-10-19

User confirms Equity desk addition complete. At least one recipient addition now user-confirmed for ICG_RA, RISK, DESK_TAXABLE, DESK_TAX_EXEMPT and DESK_EQUITY. Actual addresses/result rows not supplied. Next gated step: read-only SQL list of DISTINCT active static addresses ending @example.test, route keys and assigned role keys for PortfolioTrade version 2 (join WorkflowDefinition, WorkflowDefinitionVersion, NotificationRoute and NotificationRecipientRule). Ask user to paste result before scoped placeholder deactivation; include copied rework routes in inventory. Do not remove or modify recipients in this inventory step. V2 remains inactive.

Changed: CODEX_HANDOFF.md only, to preserve progress. Tests/seed/sample JSON: not run locally. Remaining: placeholder inventory/deactivation, copied rework audience review, SQL verification, target adapter and full acceptance before activation. Known risks: placeholder recipients still active; membership completion is user-reported. SQL-only delivery and user-managed DI remain in force. Next-session prompt: "Continue manual TEST integration. All five group additions confirmed. Read-only active v2 example.test recipient inventory requested; await results before cleanup."

---
# Manual integration: Tax Exempt recipient added, Equity desk next - 2026-10-19

User confirms Tax Exempt desk recipient step complete. ICG/RA, Risk and Taxable additions previously confirmed. Next gated step: working-copy membership template with VersionNumber=2, Group=DESK_EQUITY, Address=one intended Equity desk test recipient, Action=ADD; execute full script and wait for confirmation. Applies to assignment, due-today and overdue routes for TradingDeskEquity. V2 remains inactive. Remaining: additional intended members as needed, placeholder deactivation/rework recipient review, SQL verification, target adapter and full acceptance before activation. Actual addresses/result rows not supplied; progress is user-reported.

Changed: CODEX_HANDOFF.md only, to preserve progress. Tests/seed/sample JSON: not run locally. Known risks: placeholders remain active; one ADD only adds one person to one group. SQL-only delivery and user-managed DI remain in force. Next-session prompt: "Continue manual TEST integration. ICG, Risk, Taxable and Tax Exempt recipients added. Equity ADD instructions provided; await confirmation before placeholder cleanup."

---
# Manual integration: Taxable recipient added, Tax Exempt desk next - 2026-10-19

User confirms Taxable desk recipient step complete. ICG/RA and Risk additions previously confirmed. Next gated step: working-copy membership template with VersionNumber=2, Group=DESK_TAX_EXEMPT, Address=one intended Tax Exempt desk test recipient, Action=ADD; execute full script and wait for confirmation. Applies to assignment, due-today and overdue routes for TradingDeskTaxExempt. V2 remains inactive. Remaining: Equity recipient, further intended members as needed, placeholder deactivation/rework recipient review, SQL verification, target adapter and full acceptance before activation. Actual recipient addresses/result rows not supplied; progress is user-reported.

Changed: CODEX_HANDOFF.md only, to preserve progress. Tests/seed/sample JSON: not run locally. Known risks: placeholders remain active; one ADD only adds one person to one group. SQL-only delivery and user-managed DI remain in force. Next-session prompt: "Continue manual TEST integration. ICG, Risk and Taxable recipients added. Tax Exempt ADD instructions provided; await confirmation before Equity."

---
# Manual integration: Risk recipient added, Taxable desk next - 2026-10-19

User confirms Risk recipient added after ICG/RA addition. Next gated step: working-copy membership template with VersionNumber=2, Group=DESK_TAXABLE, Address=one intended Taxable desk test recipient, Action=ADD; execute full script and wait for confirmation. This applies to assignment, due-today and overdue routes for TradingDeskTaxable. V2 remains inactive. Remaining: Tax Exempt and Equity desk recipients, additional intended members as needed, placeholder deactivation/rework recipient review, SQL verification, target adapter and full acceptance before activation. Addresses/result rows not supplied; additions are user-reported.

Changed: CODEX_HANDOFF.md only, to preserve progress. Tests/seed/sample JSON: not run locally. Known risks: placeholders remain active; a single ADD only adds one person to one group. SQL-only delivery and user-managed DI remain in force. Next-session prompt: "Continue manual TEST integration. ICG and Risk recipients added. Taxable desk ADD instructions provided; await confirmation before Tax Exempt desk."

---
# Manual integration: ICG recipient added, Risk recipient next - 2026-10-19

User confirms "ICG recipient added" in TEST database via membership template. Address/result rows not supplied; completion is user-reported. Next gated step: in working copy of sql/upgrades/20261019_03_recipient_group_membership_dml_template.sql set VersionNumber=2, Group=RISK, Address=one intended test Risk recipient, Action=ADD; execute full script, then wait for confirmation. Preserve original template. Remaining memberships: three desk groups, any additional intended members, then remove all active placeholder recipients and review copied rework recipients before verification. V2 stays inactive; target adapter/full acceptance and activation remain pending.

Changed: CODEX_HANDOFF.md only, to preserve progress. Tests/seed/sample JSON: not run locally; no independent target SQL/runtime verification. Known risks: one ADD run changes only one person in one group; placeholders are still active. SQL-only delivery and user-managed DI remain in force. Next-session prompt: "Continue manual TEST integration. ICG recipient added; Risk ADD instructions provided. Wait for result before desk groups."

---
# Manual integration: Inactive v2 confirmed, first recipient addition next - 2026-10-19

User confirms inactive-v2 creation script complete in TEST database. Next gated step: edit a working COPY of sql/upgrades/20261019_03_recipient_group_membership_dml_template.sql for VersionNumber=2, Group=ICG_RA, Address=one approved test address supplied by user, Action=ADD; execute full copy and report result. Do not run unchanged example address or activate v2. Template changes ONE person in ONE group per run and applies across that group's routes; it does not replace all placeholders automatically. Preserve original template. Wait for result before additional recipients/groups. Next groups: RISK, DESK_TAXABLE, DESK_TAX_EXEMPT, DESK_EQUITY; add intended memberships first, then deactivate all original example.test placeholders (including copied rework metadata address review), verify and complete target adapter/acceptance before activation.

Changed: CODEX_HANDOFF.md only, to preserve progress. Tests/seed/sample JSON: not run locally. Runtime/build/schema/v1 form-open steps are user-confirmed; no independent target runtime proof. Known risks: accidental unchanged template execution or active placeholders; no approved test addresses supplied yet. SQL-only delivery and user-managed DI remain in force. Next-session prompt: "Continue manual TEST integration. Inactive v2 created. First recipient step: use membership template to ADD an approved test address to v2 ICG_RA. Wait for result before advancing."

---
# Manual integration: V1 form loads, inactive v2 creation next - 2026-10-19

User confirms "v1 form loads" against the TEST database after both schema scripts. This is user-reported form-open smoke success, not action/SMTP/SQMS acceptance. All runtime files, dependency, DI options and target build previously confirmed; pasted DI wiring reviewed. Next gated step: execute ONLY sql/upgrades/20261019_02_portfolio_trade_v2_create_inactive_dml.sql from extracted bundle against the same test database. Script creates inactive v2, copies unchanged metadata from that database's v1, leaves v1 activation unchanged, and inserts placeholder recipient addresses to replace with reviewed membership script next. If version 2 already exists, script stops without changes; inspect rather than delete/recreate. Wait for execution result before membership instructions. Do not activate v2 yet.

Changed: CODEX_HANDOFF.md only, to preserve progress. Tests/seed/sample JSON: not run locally; target smoke is user-reported. Remaining: inactive v2 result, approved test recipient membership, SQL verification, target adapter/full acceptance and activation gate. Known risks: copied target customizations and placeholder addresses need review before activation. SQL-only deployment and user-managed DI remain in force. Next-session prompt: "Continue manual TEST target integration. V1 form loads; inactive-v2 creation script instructions provided. Wait for my SQL result before recipient membership step."

---
# Manual integration: Both schema scripts confirmed, v1 smoke check next - 2026-10-19

Latest clarification: user confirms this is a TEST database. This supersedes the unspecified-environment note below. V1 form-load smoke remains pending; this clarification is not confirmation of runtime success. Continue the one-step-at-a-time test integration.

User confirms SQL 2 (20261019_01_cdt_v2_schema_ddl.sql) complete, after SQL 1 successful. Schema progress is user-reported. Per packaged INTEGRATION.md, next gate precedes inactive-v2 creation: updated consumers/code readiness and v1 smoke test. Guide user to start updated target API in integration/test environment with scheduled SLA/outbox jobs paused, then open an existing v1 CDT form and check workflow context loads without DI/missing-column errors. Do not authorize production deployment or activation. Wait for result before next script 20261019_02_portfolio_trade_v2_create_inactive_dml.sql. Read-only open/context is initial smoke only, not full v1 action/SQMS/SMTP regression.

Changed: CODEX_HANDOFF.md only, to preserve progress. Tests/seed/sample JSON: not run locally; target runtime smoke pending. Remaining: updated host readiness/v1 smoke, inactive v2 creation, reviewed recipient membership, verification, target adapter and complete runtime acceptance, separately authorized activation. Known risks: all actual consumers must use updated code before controlled processing; environment unspecified. SQL-only delivery and user-managed DI remain in force. Next-session prompt: "Continue manual integration. Both schema scripts confirmed. Updated target API/v1 form-open smoke requested in integration/test environment; wait for result before inactive-v2 script."

---
# Manual integration: SQL 1 confirmed successful, SQL 2 next - 2026-10-19

User reports first script 20261014_outbox_retry_scheduling.sql completed successfully. Next gated instruction: execute ONLY sql/upgrades/20261019_01_cdt_v2_schema_ddl.sql from extracted bundle in same intended target database, keeping cutover pause in place. This is additive SLA policy/schedule schema preparation; does not activate v2. Wait for completion/errors before metadata scripts or application startup. SQL execution is user-reported, not independently inspected; server/database/environment identity and backup confirmation were not supplied. No Codex SQL execution.

Changed: CODEX_HANDOFF.md only, to preserve SQL progress. Tests/seed/sample JSON: not run locally. Remaining: SQL 2 result, adapter/readiness and acceptance, inactive v2 creation/membership/verification and separately authorized activation. Runtime files, dependency, DI options and build previously user-confirmed. Known risks: target schema/customizations may differ; retain existing data and SQL-only delivery. Next-session prompt: "Continue manual target integration. SQL 1 successful; SQL 2 20261019_01_cdt_v2_schema_ddl.sql instructions provided. Await my result before advancing."

---
# Manual integration: First SQL script instructions provided - 2026-10-19

User says they are connected to the database and can execute scripts directly. Provide manual SSMS instructions for ONLY sql/upgrades/20261014_outbox_retry_scheduling.sql from extracted bundle, then wait for completion/errors before second script. Reviewed script adds nullable dbo.NotificationOutbox.NextAttemptAtUtc and validates/creates IX_NotificationOutbox_RetryEligibility; it uses a transaction, preserves existing rows, and does not seed or alter EF history. It expects dbo.NotificationOutbox; do not blindly substitute schema if it fails. Instruct backup readiness and pause workflow writers/SLA/outbox consumers for cutover before execution, consistent with existing integration plan. Environment/backup status not explicitly supplied; do not claim confirmed or executed. User remains SQL operator; no Codex SQL execution or production deployment.

Changed: CODEX_HANDOFF.md only, to record the next guided step. Tests/seed/sample JSON: not run locally. Remaining: first SQL result, second schema script, inactive v2 metadata/membership/verification, target adapter/acceptance and authorized activation. All 13 runtime files, dependency, DI options and target build previously user-confirmed. Known risks: live schema may differ; test/production environment unspecified. Next-session prompt: "Continue manual integration. First SQL script 20261014_outbox_retry_scheduling.sql instructions provided; await my result before second script."

---
# Manual integration: Console-only migrations confirmed, database identification next - 2026-10-19

User confirms migrations are not called by target solution; they are triggered from console. Treat this as user-reported startup-path confirmation, not an independent scan of deployed binaries. Keep console init-db/migrations/automatic seeding out of target deployment. All 13 runtime files, dependency, option registrations and target build previously user-confirmed; pasted DI registration reviewed.

Next gated step: user identifies intended SQL Server/database via read-only SELECT @@SERVERNAME, DB_NAME(), confirms test/rehearsal versus production, and confirms backup/recovery readiness before schema writes. Preserve one-step-at-a-time flow. First eventual write script is sql/upgrades/20261014_outbox_retry_scheduling.sql, followed by 20261019_01_cdt_v2_schema_ddl.sql. Do not start updated consumers against old schema; writes and SLA/outbox consumers must be paused for controlled cutover. No SQL execution has been instructed yet at this checkpoint.

Changed: CODEX_HANDOFF.md only, to preserve progress. Tests/seed/sample JSON: not run locally. Remaining: database identification/readiness, SQL scripts one at a time, target adapter and runtime acceptance. Known risks: environment and database identity not yet confirmed; production release authorization is separate. Next-session prompt: "Continue manual target integration. Console-only migration path confirmed; database identity/environment and backup readiness requested before first SQL script."

---
# Manual integration: Target build reported successful, startup check next - 2026-10-19

User reported "build sucess" after the requested target warning-as-error build. Build output was not supplied; no independent execution or runtime verification. Runtime files, dependency and pasted DI registrations are complete at the current guided stage. Next gated step: read-only search of target startup/hosted/deployment code for Database.Migrate/MigrateAsync, EnsureCreated/EnsureCreatedAsync, and WorkflowDatabaseInitializer/IWorkflowDatabaseInitializer calls (Initialize performs migration and seed in source). Service registrations/definitions alone are not execution. Ask user to show actual calls, if any; do not instruct blanket deletion or change composition behavior without inspecting call sites. SQL-only target delivery remains mandatory; no init-db or EF migrations. Do not start target API before this check and schema preparation.

Changed: CODEX_HANDOFF.md only, to preserve progress. Tests, seed verification and sample JSON: not run locally; target build is user-reported. Remaining: startup check, controlled SQL deployment environment/backup confirmation, SQL scripts one by one, adapter changes and runtime acceptance. Known risks: startup/deployment auto-migration and automatic seed paths not yet verified. Next-session prompt: "Continue manual target integration. Target build reported successful. Check startup/deployment for migration or seed execution before SQL steps, waiting for my result."

---
# Manual integration: Pasted DI wiring reviewed, target build next - 2026-10-19

User pasted current InfrastructureServiceCollectionExtensions.cs. Both option singletons are registered via FromConfiguration(configuration). RunSlaService and ProcessNotificationOutboxService use AddScoped<T>(); dispatcher uses AddScoped<IWorkflowActionNotificationDispatcher, WorkflowActionNotificationDispatcher>(). Interface forwarding factories resolve the registered concrete services, not old constructors. No manual constructor factory correction is needed in this pasted file. Expected DI selection uses the new resolvable option-aware constructors, assuming merged classes match bundle. This is source review only, not runtime DI proof. User-confirmed dispatcher namespace is WorkflowEngine.Infrastructure.WorkflowNotification and interface namespace WorkflowEngine.Application.WorkflowNotification.

Next gated step: user runs dotnet build -warnaserror from target solution directory (or specifies the solution filename if multiple exist), then reports result/errors. Do not start API or apply SQL yet. Keep existing Program.cs AddWorkflowInfrastructure call; avoid duplicate option registrations. SQL-only database deployment and user-managed DI remain in force.

Changed: CODEX_HANDOFF.md only, to record review and next step. Tests, seed verification and sample JSON: not run; no local implementation changed. Remaining: target build, startup migration/seed check, SQL deployment, adapter and acceptance. Known risks: build and runtime DI unverified; registration of initializer/seed services alone does not execute them, startup callers still need checking. Next-session prompt: "Continue manual integration. Pasted DI registrations reviewed; target build with warnings as errors requested. Wait for build result before advancing."

---
# Manual integration: DI options confirmed, service wiring check next - 2026-10-19

Latest user confirmed both option singleton registrations added inside target InfrastructureServiceCollectionExtensions.cs / AddWorkflowInfrastructure. All 13 runtime files and PKCS dependency were previously confirmed. Keep Program.cs calling AddWorkflowInfrastructure(configuration); avoid duplicate option registrations there. Next gated step: user shows existing registrations (complete factory blocks if present) for RunSlaService, ProcessNotificationOutboxService and WorkflowActionNotificationDispatcher from target's registration file, so constructor/options wiring can be checked before build. Old manual factories may select compatibility constructors and ignore configured options. Dispatcher remains in target WorkflowEngine.Infrastructure\WorkflowNotification\WorkflowActionNotificationDispatcher.cs; preserve actual target namespace/interface.

Changed: CODEX_HANDOFF.md only, to record progress and next check. Tests, seed verification and sample JSON: not run; target completion is user-reported, no local implementation changed. Remaining: registration/factory check, target restore/build, SQL-only deployment, adapter and acceptance steps, each awaiting user completion. Known risks: factory code not yet inspected; target edits unverified. Next-session prompt: "Continue manual target integration. DI options are confirmed in AddWorkflowInfrastructure. Check the three service registrations/factories next and wait for my input before advancing."

---
# Manual integration: Target-specific DI guidance provided - 2026-10-19

User-confirmed target dispatcher location: WorkflowEngine.Infrastructure\WorkflowNotification\WorkflowActionNotificationDispatcher.cs. The bundle's workflow-api path is the source reference; target keeps its existing infrastructure location. Do not infer its namespace from the folder; preserve the actual target namespace and interface. This clarification does not confirm DI option registration completion; that step remains pending.

User requested guidance using target-implementation-read-only. Inspected Atlasapp/Atlasapp/program.cs.txt: line 34 calls builder.Services.AddWorkflowInfrastructure(configuration), line 35 AddSqmsWorkflowIntegration(configuration). Current instruction: in target's actual Program.cs add Notifications/Sla using directives and the two builder.Services.AddSingleton option registrations immediately before AddWorkflowInfrastructure(configuration), if those options are not already registered inside that extension. The snapshot contains the call but not the extension implementation; do not claim its factories were inspected. Next inspect manual factories/constructor selection after user confirms this step. No target source edits by Codex. DI options completion not yet confirmed.

Changed: CODEX_HANDOFF.md only, to preserve exact target guidance. Tests/seed/sample JSON: not run; guidance and read-only inspection only. Remaining: user DI confirmation, factory review, build, SQL, adapter and acceptance. Known risks: snapshot may differ from live target; extension implementation absent. Next-session prompt: "Continue manual integration. All runtime files and dependency confirmed. Target Program.cs DI options instructions provided; wait for my confirmation."

---
# Manual integration: Dependency confirmed, DI option registration next - 2026-10-19

User confirmed the PKCS dependency step complete after all 13 runtime files. Next gated instruction for the user to apply: in target's existing workflow service registration file add singleton OutboxProcessingOptions.FromConfiguration(configuration) and SlaProcessingOptions.FromConfiguration(configuration), using target's IConfiguration variable and appropriate target namespaces. Keep existing registrations and service lifetimes; avoid duplicates. DI remains user-managed; no target edits by Codex. Wait for user completion before checking manual factories/constructor selection and restore/build. SQL-only database delivery remains mandatory.

Changed: CODEX_HANDOFF.md only, to preserve progress. Why: record dependency completion and next manual step. Tests, seed verification and sample JSON: not run because no local implementation changed. Remaining: DI registrations and constructor/factory readiness, restore/build, SQL deployment, adapter and acceptance steps. Known risks: user-reported edits unverified; old factories can ignore new options. Next-session prompt: "Continue manual integration. All 13 runtime files and PKCS dependency confirmed; DI option registration instructions provided. Wait for my confirmation before advancing."

---
# Manual integration: All 13 runtime files confirmed, dependency step next - 2026-10-19

Restarted pass: user confirmed File 13 (WorkflowActionNotificationDispatcher.cs) complete. All 13 runtime files are user-confirmed, not independently inspected or built. Next gated step: carry the existing release's System.Security.Cryptography.Pkcs 10.0.13 explicit reference into the target infrastructure project (or its existing central package-version mechanism). Do not replace target project files or change target framework. Wait for user completion before advancing. DI remains user-managed; SQL-only database delivery remains mandatory.

Changed: CODEX_HANDOFF.md only, to preserve progress. Why: advance the guided integration after File 13 confirmation. Tests, seed verification and sample JSON: not run because no local implementation changed. Remaining: dependency confirmation, user-managed DI readiness, restore/build and SQL/adapter/acceptance steps, each waiting for user completion. Known risks: target edits unverified, central package management may differ, old manual factories can ignore new options. Next-session prompt: "Continue manual target integration. All 13 runtime files confirmed; PKCS dependency step provided. Wait for my completion before advancing."

---
# Manual integration: Files 1-12 confirmed, File 13 next - 2026-10-19

Restarted pass: user confirmed File 12 (ProcessNotificationOutboxService.cs) complete. Next: merge only workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionNotificationDispatcher.cs from the extracted bundle into target's equivalent immediate notification dispatcher. Include OutboxProcessingOptions overload (retain three-parameter constructor), deferred retry result, retry eligibility during selection/claiming, clearing NextAttemptAtUtc after success, and complete ReleaseClaimAfterFailure logic with retry scheduling and saturated retry count. Preserve target namespaces/interfaces and custom behavior. Wait for user confirmation before subsequent integration steps. Completion is user-reported, not independently verified. Changed: CODEX_HANDOFF.md only, to preserve progress. Tests, seed verification and sample JSON: not run because no local implementation changed. Remaining: File 13 and subsequent gated dependency/build, SQL, adapter and acceptance steps. Known risk: target immediate dispatcher differs from standalone API reference. SQL-only database delivery and user-managed DI remain in force. Next-session prompt: "Continue restarted manual integration. Files 1-12 confirmed; File 13 instructions provided. Wait for my confirmation before advancing."

---
# Manual integration: Files 1-11 confirmed, File 12 next - 2026-10-19

Restarted pass: user confirmed File 11 (RunSlaService.cs) complete. Next: merge only src/WorkflowEngine.Infrastructure/Notifications/ProcessNotificationOutboxService.cs from the extracted bundle. Compare complete file: OutboxProcessingOptions constructor overload (retain three-parameter constructor), batch limits, retry eligibility during selection and claiming, failure retry scheduling and success timestamp clearing, fair bounded selection ordering, and bounded warning queries. Preserve target customizations; if none, copy complete file with namespace/import adjustments. Wait for user confirmation before File 13. Completion is user-reported, not independently verified. Changed: CODEX_HANDOFF.md only, to preserve progress. Tests, seed verification and sample JSON: not run because no local implementation changed. Remaining: Files 12-13 and subsequent gated integration steps. Known risk: incomplete retry/selection merge. SQL-only database delivery and user-managed DI remain in force. Next-session prompt: "Continue restarted manual integration. Files 1-11 confirmed; File 12 instructions provided. Wait for my confirmation before File 13."

---
# Manual integration: Files 1-10 confirmed, File 11 next - 2026-10-19

Restarted pass: user confirmed File 10 (WorkflowExecutionService.cs) complete. Next: merge only src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs from the extracted bundle. Compare complete file: SlaProcessingOptions constructor overload (retain two-parameter constructor), bounded scanning and fresh batch contexts, per-schedule failure isolation, reminder recipient snapshots/timing, caller connection ownership and tracked-schedule handling. If no target customizations, copy complete file with namespace/import adjustments; otherwise preserve target customizations while merging complete behavior. Wait for user confirmation before File 12. Completion is user-reported, not independently verified. Changed: CODEX_HANDOFF.md only, to preserve progress. Tests, seed verification and sample JSON: not run because no local implementation changed. Remaining: Files 11-13 and subsequent gated integration steps. Known risk: substantial merge and target context customizations. SQL-only database delivery and user-managed DI remain in force. Next-session prompt: "Continue restarted manual integration. Files 1-10 confirmed; File 11 instructions provided. Wait for my confirmation before File 12."

---
# Manual integration: Files 1-9 confirmed, File 10 next - 2026-10-19

Restarted pass: user confirmed File 9 (WorkflowSlaSynchronizationService.cs) complete. Next: merge only src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs from the extracted bundle. Add NotificationRecipientResolver as fourth argument in the compatibility constructor's WorkflowSlaSynchronizationService construction; add the ValidateSlaReminderAudience call after successful SLA-input validation; copy the complete private ValidateSlaReminderAudience method. Preserve target authentication, transactions, idempotency and status handling. Wait for user confirmation before File 11. Completion is user-reported, not independently verified. Changed: CODEX_HANDOFF.md only, to preserve progress. Tests, seed verification and sample JSON: not run because no local implementation changed. Remaining: Files 10-13 and subsequent gated integration steps. Known risk: incomplete method merge or target customizations. SQL-only database delivery and user-managed DI remain in force. Next-session prompt: "Continue restarted manual integration. Files 1-9 confirmed; File 10 instructions provided. Wait for my confirmation before File 11."

---
# Manual integration: Files 1-8 confirmed, File 9 next - 2026-10-19

Restarted pass: user confirmed File 8 (SlaScheduleCalculator.cs) complete. Next: merge only src/WorkflowEngine.Infrastructure/Sla/WorkflowSlaSynchronizationService.cs from the extracted bundle. Include Notifications import, recipient-resolver constructor and compatibility overload, reminder audience resolution/validation and preserved snapshots when rescheduling, new schedule fields, calculation and policy-validation changes. Preserve target transaction/persistence customizations. Wait for user confirmation before File 10. Completion is user-reported, not independently verified. Changed: CODEX_HANDOFF.md only, to preserve progress. Tests, seed verification and sample JSON: not run because no local implementation changed. Remaining: Files 9-13 and subsequent gated integration steps. Known risk: incomplete method merges or target customizations. SQL-only database delivery and user-managed DI remain in force. Next-session prompt: "Continue restarted manual integration. Files 1-8 confirmed; File 9 instructions provided. Wait for my confirmation before File 10."

---
# Manual integration: Files 1-7 confirmed, File 8 next - 2026-10-19

Restarted pass: user confirmed File 7 (SlaReminderAudienceResolver.cs) complete. Next: merge only src/WorkflowEngine.Infrastructure/Sla/SlaScheduleCalculator.cs from the extracted bundle. Copy the complete eight-parameter CalculateApplicationDate overload ending in DateTimeOffset now, bool skipMissedDueReminder, and complete CalculateNextLocalDay method. Both appear before CalculateNextRecurringOccurrence. Preserve the existing CalculateApplicationDate overload and all target customizations; avoid duplicate methods. Wait for user confirmation before File 9. Completion is user-reported, not independently verified. Changed: CODEX_HANDOFF.md only, to preserve progress. Tests, seed verification and sample JSON: not run because no local implementation changed. Remaining: Files 8-13 and subsequent gated integration steps. Known risk: prior-pass methods may already exist. SQL-only database delivery and user-managed DI remain in force. Next-session prompt: "Continue restarted manual integration. Files 1-7 confirmed; File 8 instructions provided. Wait for my confirmation before File 9."

---
# Manual integration: Files 1-6 confirmed, File 7 next - 2026-10-19

Restarted pass: user confirmed File 6 (WorkflowDbContext.cs) complete. Next: add/compare only src/WorkflowEngine.Infrastructure/Sla/SlaReminderAudienceResolver.cs from the extracted bundle into target infrastructure's Sla folder. Copy complete contents, adjusting namespaces/imports only as needed; preserve target customizations if it already exists. Wait for user confirmation before File 8. Completion is user-reported, not independently verified. Changed: CODEX_HANDOFF.md only, to preserve progress. Tests, seed verification and sample JSON: not run because no local implementation changed. Remaining: Files 7-13 and subsequent gated integration steps. Known risk: prior-pass file may already exist. SQL-only database delivery and user-managed DI remain in force. Next-session prompt: "Continue restarted manual integration. Files 1-6 confirmed; File 7 instructions provided. Wait for my confirmation before File 8."

---
# Manual integration: Files 1-5 confirmed, File 6 next - 2026-10-19

Restarted pass: user confirmed File 5 (WorkflowTaskSlaSchedule.cs) complete. Next: merge only WorkflowDbContext.cs from src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs in the extracted bundle. Add time column mappings for SlaPolicy.DueReminderLocalTime and WorkflowTaskSlaSchedule.DueReminderLocalTime, and the NotificationOutbox retry eligibility index on Status, NextAttemptAtUtc, CreatedAtUtc, Id. Preserve all target mappings; avoid duplicates. Wait for user confirmation before File 7. Completion is user-reported, not independently verified. Changed: CODEX_HANDOFF.md only, to preserve progress. Tests, seed verification and sample JSON: not run because no local implementation changed. Remaining: Files 6-13 and subsequent gated integration steps. Known risk: target DbContext customizations must be preserved. SQL-only database delivery and user-managed DI remain in force. Next-session prompt: "Continue restarted manual integration. Files 1-5 confirmed; File 6 instructions provided. Wait for my confirmation before File 7."

---
# Manual integration: Files 1-4 confirmed, File 5 next - 2026-10-19

Restarted pass: user confirmed File 4 (SlaPolicy.cs) complete. Next: update only WorkflowTaskSlaSchedule.cs from src/WorkflowEngine.Infrastructure/Persistence/Entities/WorkflowTaskSlaSchedule.cs in the extracted bundle. Add TimeOnly? DueReminderLocalTime, string? DueRecipientsJson, string? OverdueRecipientsJson after OverdueNotificationRouteKey and before Status; preserve existing properties and avoid duplicates. Wait for user confirmation before File 6. Completion is user-reported, not independently verified. Changed: CODEX_HANDOFF.md only, to preserve progress. Tests, seed verification and sample JSON: not run because no local implementation changed. Remaining: Files 5-13 and subsequent gated integration steps. Known risk: prior-pass additions may already exist. SQL-only database delivery and user-managed DI remain in force. Next-session prompt: "Continue restarted manual integration. Files 1-4 confirmed; File 5 instructions provided. Wait for my confirmation before File 6."

---
# Manual integration: Files 1-3 confirmed, File 4 next - 2026-10-19

Restarted pass: user confirmed File 3 (NotificationOutbox.cs) complete. Next: update only SlaPolicy.cs from src/WorkflowEngine.Infrastructure/Persistence/Entities/SlaPolicy.cs in the extracted bundle. Add TimeOnly? DueReminderLocalTime, bool SkipMissedDueReminder, bool OverdueStartsNextLocalDay after OverdueNotificationRouteKey and before IsActive; preserve existing properties and avoid duplicates. Wait for user confirmation before File 5. Completion is user-reported, not independently verified. Changed: CODEX_HANDOFF.md only, to preserve progress. Tests, seed verification and sample JSON: not run because no local implementation changed. Remaining: Files 4-13 and subsequent gated integration steps. Known risk: prior-pass additions may already exist. SQL-only database delivery and user-managed DI remain in force. Next-session prompt: "Continue restarted manual integration. Files 1-3 confirmed; File 4 instructions provided. Wait for my confirmation before File 5."

---
# Manual integration: Files 1-2 confirmed, File 3 next - 2026-10-19

Restarted pass: user confirmed File 2 (SlaProcessingOptions.cs) complete after File 1. Next: merge the nullable NextAttemptAtUtc property into target NotificationOutbox.cs, using src/WorkflowEngine.Infrastructure/Persistence/Entities/NotificationOutbox.cs from the extracted bundle. Wait for user confirmation before File 4. Target completion is user-reported, not independently verified. Changed: CODEX_HANDOFF.md only, to preserve progress. Tests, seed verification and sample JSON: not run because no local implementation changed. Remaining: Files 3-13 and subsequent gated integration steps. Known risk: property may already exist from prior pass; do not duplicate. SQL-only database delivery and user-managed DI remain in force. Next-session prompt: "Continue restarted manual target integration. Files 1-2 confirmed; File 3 instructions provided. Wait for my confirmation before File 4."

---
# Manual integration: File 1 confirmed, File 2 next - 2026-10-19

Restarted pass: user confirmed File 1 (OutboxProcessingOptions.cs) complete. Next: copy/compare only src/WorkflowEngine.Infrastructure/Sla/SlaProcessingOptions.cs into target's infrastructure Sla folder. Wait for user confirmation before File 3. Target completion is user-reported; no local code change, build/test, seed verification or sample execution. Changed file: CODEX_HANDOFF.md, to preserve progress. SQL-only deployment and user-managed DI remain in force. Remaining: Files 2-13 and subsequent gated integration steps. Known risk: prior-pass files may already exist; compare before replacing. Next-session prompt: "Continue the restarted manual target integration. File 1 is confirmed complete; File 2 instructions were provided. Wait for my completion confirmation before advancing."

---
# Manual integration restarted at File 1 - 2026-10-19

Latest user instruction: "start for step 1 file by file". Restart the guided sequence at File 1 of 13, OutboxProcessingOptions.cs, from the extracted bundle path src/WorkflowEngine.Infrastructure/Notifications/OutboxProcessingOptions.cs. User copies/compares this one file at target and confirms completion before File 2. Earlier Files 1-12 completion reports remain historical; do not advance based on those reports in this restarted pass. Preserve existing target customizations; DI remains user-managed and database delivery SQL-only.

Changed: this checkpoint only, to record the requested restart. Tests/seed/sample execution: not run because no implementation changed. Remaining: wait for File 1 confirmation, then guide File 2, SlaProcessingOptions.cs. Known risk: files may already exist from the earlier pass, so compare before replacing. Next-session prompt: "Continue the restarted manual target integration, one file at a time, from the latest user-confirmed file. File 1 instructions were provided; await confirmation."

---
# Manual target integration recovered from chat - 2026-10-19

Current goal: guide the user through integrating the already unbundled files at their target, one step at a time. User explicitly required waiting for their completion approval at each step. Do not require a local target checkout or attempt autonomous target edits.

Authoritative recovered chat: C:\Users\Dolly Gutta\.codex\sessions\2026\10\19\rollout-2026-10-19T19-42-43-01a1568b-c428-7563-8756-99030e1551d8.jsonl. User confirmed Files 1-12 complete; the final assistant instruction was File 12, ProcessNotificationOutboxService.cs, followed by the user's "complete". Completion is user-reported, not independently inspected or tested.

Next step: File 13 of 13, WorkflowActionNotificationDispatcher.cs. Bundle path: workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionNotificationDispatcher.cs. Merge retry behavior into target's equivalent immediate dispatcher, preserving target namespaces/interfaces and custom behavior. Keep the three-argument constructor, add the OutboxProcessingOptions overload, honor NextAttemptAtUtc during selection/claiming, schedule retries on failure, clear retry timestamp on success, and retain retry-count saturation. Then WAIT for user confirmation before the next integration step. SQL-only deployment and user-managed DI remain in force.

Completed changes: chat recovery and this corrected checkpoint only. Files updated: CODEX_HANDOFF.md. Build/tests, seed verification and sample JSON were not run; no implementation changed locally. Remaining: user's File 13 confirmation, then dependency/build and SQL/adapter/acceptance steps from the existing integration plan, each gated by user completion. Known risks: target edits unverified; immediate dispatcher may use different namespaces or custom logic. Next-session prompt: "Resume manual target integration from the recovered chat checkpoint. Files 1-12 were confirmed complete; continue File 13 and wait for my confirmation at each step."

The earlier resume check below was superseded by direct chat recovery; its request for a live local target path does not apply to this manual workflow.

---
# Superseded target integration resume check - 2026-10-19

Current goal: resume the interrupted target integration step, starting with the live source-to-target map. Read PROJECT.md, the integration PLAN, and the extracted package instructions/manifest/DI checklist. Local HEAD remains a0b6424. The restore script SHA-256 still matches 742ba25834988955bd50be9b4993304528ad1308194ac86eb853b7ae31508c15.

Live target location is not yet confirmed. C:\projects\cdt-workflow-target-integration is an August export without Git metadata; C:\projects\cp-atlas\backend contains Atlas.Backend.sln and workflow sources but inspected project files target net9.0, whereas the user confirmed the real target is .NET 10. Do not assume either is current or upgrade them as part of resumption.

Completed changes: context recovery and this checkpoint only. Changed files: CODEX_HANDOFF.md. Build/tests, seed verification and sample JSON were not run because no implementation changed. No SQL, DI, target code, commit, push or deployment performed. Existing pending files preserved.

Remaining: obtain the current .NET 10 target checkout path, then produce the source-to-target map using the existing PLAN. Preserve SQL-only target deployment and user-owned DI. Known risk: merging into a stale export or reconstructed backend. Next-session prompt: "Resume the target integration mapping from this checkpoint using the current .NET 10 target checkout at <path>."

---
# Resume target integration - 2026-10-18

User paused until tomorrow. Current goal: integrate the selective workflow changes into target. Target was current through e80569825953be259912215d3083437fabaec2d3; implementation release is 55728f6a44e57be5221090c262e7ce26f16b4cf5. User confirmed target is already .NET 10. All target schema and metadata changes MUST use SQL scripts only, never EF migrations, EnsureCreated, init-db, or automatic seeding. EF runtime entities and mappings still need merging. User will integrate required DI registrations separately.

Published handoff: bundle-output/target-integration-e805698-to-55728f6/restore-target-integration.ps1, committed and pushed as a0b64241f1de76267c0b0a2b49994178cbc7479f on origin/console-code-first-0518. Remote SHA verified. Only this PowerShell file was committed; other pending files were left untouched.

Bundle is selective, NOT the full repository: 13 runtime C# files, seven SQL scripts, JSON contract HTML, merge diffs, manifest, DI/dependency checklist, verification record and handoff. Total 30 extracted files. No EF migrations, seed initializers, console/demo code, binaries, credentials, or target snapshot files. Target shared files such as WorkflowDbContext must be merged, not blindly replaced.

Final extraction: C:\projects\cdt-workflow\artifacts\target-integration-bundle\extracted-final. Read INTEGRATION.md, MANIFEST.json and DI-AND-DEPENDENCIES.md there first. User's word "decompile" means extract the Base64 bundle and compare files, NOT assembly decompilation. All 21 original code/SQL/contract files matched local bytes by SHA-256; all 30 extracted files matched staging with no missing/extra/mismatched files. Restore script refuses an existing destination and performs ZIP/file hash checks. Script SHA-256: 742ba25834988955bd50be9b4993304528ad1308194ac86eb853b7ae31508c15.

Build proof: disposable e805698 baseline at artifacts/target-integration-bundle/baseline-build, overlaid with extracted runtime files plus the packaged DI/package review patch. Core and API solution builds passed with warnings as errors, zero warnings/errors. No runtime tests, seed verification, sample execution, target SQL execution or target deployment occurred during packaging. Detailed build logs and comparison JSON are in artifacts/target-integration-bundle. ILSpy was installed under artifacts/target-handoff-tools before clarification but was not used or added as an application dependency.

Integration plan: .planning/quick/261018-target-integration-e805698-to-55728f6/PLAN.md. First reconcile the live target checkout against the stale read-only snapshot and produce the source-to-target mapping. Preserve target authentication, business-save/workflow transaction ownership, idempotency and status projection. Update both background and immediate mail dispatch. DI checklist covers singleton OutboxProcessingOptions/SlaProcessingOptions, manual factory constructor selection, and PKCS 10.0.13 dependency pin. Do not replace target's composition root.

SQL sequence: 20261014_outbox_retry_scheduling.sql; 20261019_01_cdt_v2_schema_ddl.sql; deploy updated consumers while v1 remains active; 20261019_02 inactive v2 creation; reviewed completed 20261019_03 recipient membership template; 20261019_04 verification; after target acceptance/change control, 20261019_05 activation. 20261019_06 reactivates v1 for NEW starts only. Open v2 instances still need v2-capable code. No forced workflow closure/reset. Prior two recipient/route findings remain accepted by user under target change control.

Remaining acceptance: old v1 and shared SQMS regression; user's full draft/Checker return/resubmit/Trading return/new date and desk/approval/completion path through REAL target entry points, with at least one email per SLA-enabled task occurrence. Prior local reference had 9 action plus 8 SLA emails; target behavior is not yet proven. Verify recipients, revised date/desk, repeated-scan deduplication, no reminders after completion, retry isolation and target transaction rollback. The earlier smtp4dev capture was left running; check current status before reuse, do not assume it is still live.

Next-session prompt: "Resume target integration from CODEX_HANDOFF.md. Use the published selective bundle a0b6424 and plan .planning/quick/261018-target-integration-e805698-to-55728f6/PLAN.md. Target is .NET 10; SQL scripts only for database changes; I will handle DI integration separately. Start by mapping packaged files to the live target checkout and preserve target customizations."

This checkpoint is local only. No new commit/push requested for it. Other pending reports/snapshot files remain outside the integration scope.

---
# CDT v2 implementation and contract published - 2026-10-18

User requested adding the target JSON contract to the pending local commit and pushing with a detailed message. Amended local 362404d to 55728f6a44e57be5221090c262e7ce26f16b4cf5. The only content difference from 362404d is docs/CDT_V2_TARGET_JSON_CONTRACT.html. Pushed to origin/console-code-first-0518 and verified the remote SHA matches.

Commit message documents implementation, migration/release, contract examples, original and subsequent verification, the 17-email happy-path run, and owner-accepted C1/C2 risks. No production deployment. Unrelated local reports, target snapshot, docs/README change and handoff history remain uncommitted. No engine changes or repeated runtime tests in this publication step; staged whitespace and commit scope verified.

Next: share the published contract with target team and follow its release guide. Earlier references to 362404d describe the identical implementation before the document was added. smtp4dev was left running for user inspection.

---
# CDT v2 target JSON contract delivered - 2026-10-18

Created the single self-contained HTML docs/CDT_V2_TARGET_JSON_CONTRACT.html. Approximate 80% ASD-STE100 style target, not certified/measured. Existing public JSON shape and /v1 endpoints remain; v2 is workflow metadata. Target snapshot uses in-process services, so document separates standalone DTOs from target-owned form JSON and provides field mappings.

Includes 12 full requests (start/draft, complete return/resubmit flow, changed desk/date, completion, SLA-only), field types, action/recipient matrix, template fields, response/error/retry behavior, SLA timing and release acceptance. Actual action key is ResubmitForChecker. Remove selectedCcUsers entirely on both return routes and BdrmTradeFulfilled, including empty/null keys. Reminder recipients are not request inputs.

Verification: all 12 JSON requests deserialized into actual public DTOs; action keys, desk roles and allowed recipient inputs checked read-only against v2 local test metadata. HTML structure, 10 unique IDs, 10 internal links and 13 JSON blocks validated; embedded requests match checked fixtures. Logs and generator in artifacts/cdt-v2-contract. No endpoint execution, browser rendering or production verification for this document. Engine/target source, SQL and plan unchanged. No commit/push.

smtp4dev remains running at http://localhost:5000 for the earlier 17-email happy-path demonstration. Next: share the HTML with target team; verify their deployed adapter and run target acceptance. C1/C2 remain accepted under target controls.

---
# CDT v2 happy-path SMTP run completed - 2026-10-18

Completed the requested real-code flow: SaveDraft, submit, Checker return, resubmit, Checker approval, Trading Desk return, resubmit with new desk/date, Checker approval, Trading approval, BDRM approval. Actual start/execution/SLA/outbox/SMTP services and SQL; no target HTTP/UI invocation. Injected clock: 15-20 January 2030 New York.

PASS: 10 actions, Completed workflow, 6 SLA tasks, 8 SLA reminders, 9 action emails. All 17 outbox rows Sent and captured SMTP subjects/ordered To/Cc matched. Taxable/Jan15 changed to Equity/Jan18. Duplicate scans queued nothing; completion stopped reminders. Seed verification passed. No source change.

Trade CDT-HAPPY-20261018_191130; workflow WF-CDT-HAPPY-20261018_191130; database burrito_cdt_happy_20261018_191130. Report docs/CDT_V2_HAPPY_PATH_RESULT.html. Driver/evidence artifacts/cdt-v2-happy-path. First runtime attempt stopped on a driver state-name casing assertion, then corrected and fully rerun using fresh database/capture. First-attempt log retained.

smtp4dev LEFT RUNNING for user inspection: http://localhost:5000, SMTP localhost:3025, PID 1112. Capture database artifacts/cdt-v2-happy-path/smtp4dev/smtp4dev-final.db. Browser launch was blocked by tool policy; user has direct link. No external relay, production change, commit or push. No full regression suite for this scenario.

Next: inspect emails with the user. Keep smtp4dev running. Accepted C1/C2 risks remain accepted. Preserve unrelated work.

---
# Commit 362404d owner disposition - 2026-10-18

The user accepted both reproduced findings under target production change controls: C1 route disable/reactivation and C2 removal of all required group members. Neither requires a code fix under this decision. Updated 362404d-REVIEW.md to show no remaining blockers from this review; retained original evidence and severity. Target controls are owner-confirmed, not independently audited.

No source change, new test run, commit, push or deployment. Earlier changes-requested statements below are superseded by this disposition. Next: continue only with the user's requested work; do not reopen C1/C2 without new evidence or changed constraints.

---
# Commit 362404d review - 2026-10-18

Completed source review and SQL-backed checks for local commit 362404d. Report: .planning/quick/261018-cdt-portfolio-trade-v2-notification-routes/362404d-REVIEW.md. Changes requested: C1 High, inactive explicit reminder route saves null audience and sends broad task recipients after reactivation; C2 Medium, group template permits removing all Risk members and activation still passes. Both reproduced with real engine/scripts.

Verification: 39 committed CdtV2 tests passed. Review probe in artifacts/review-362404d/probe and results in probe.log; dedicated local database burrito_review_362404d remains available. No SMTP sends/production operations. Full core/API/SMTP/fresh-clone gates were not rerun. Source/tests/SQL/API/PROJECT match reviewed commit; no fixes or commit/push. Existing unrelated work preserved.

Next: read 362404d-REVIEW.md and address C1/C2 only after user authorization. Earlier plan reviews and HTML are historical context, not current implementation results.

---
# Final PortfolioTrade v2 plan review - 2026-10-18

Reviewed the revised final plan. Prior R1-R5 are addressed at design level for the stated supported paths. Added 261018-cdt-FINAL-REVIEW.md beside the plan. Three Medium corrections remain: scope placeholder checks to active rules; provide a production-address configuration path for the unchanged rework route; define the clear-error handling for normal HTTP actions (the current SLA exception catch is actionless-only). No new Critical or High identified. Hard deletion of all route rules remains a documented risk.

Source inspection only. No engine tests, seed verification or sample execution. No plan/source/schema/database change and no commit/push. Earlier REVIEW.md and HTML describe the earlier plan, not this revision. Implementation remains pending user instruction. Next: resolve F1-F3 and use the final-review guardrails during implementation.

---
# PortfolioTrade v2 review HTML - 2026-10-18

Created docs/CDT_V2_PLAN_REVIEW_EXPLAINED.html at the user's request. Approximate 80% ASD-STE100 style target, not a measured compliance score. Covers three High and two Medium findings, scenarios, impact, plan corrections, acceptance tests, group maintenance, release sequence and source evidence. All findings are source-review conclusions, not executed v2 test results.

Verification: balanced HTML, 10 unique IDs, 20 valid local links, git diff --check passed. Browser rendering was not tested. No engine tests, seed verification or sample execution for this documentation task. No source, plan, SQL or database changes. No commit or push requested. Changed files: the new HTML and this local handoff.

Next: review the HTML and revise the v2 plan before implementation. Preserve the prior audit files and target snapshot.

---
# PortfolioTrade v2 plan review - 2026-10-18

Reviewed .planning/quick/261018-cdt-portfolio-trade-v2-notification-routes/261018-cdt-PLAN.md against PROJECT.md and current source. Verdict: revise before implementation. Report: 261018-cdt-REVIEW.md beside the plan. High risks: activating placeholder recipients, unsafe old-binary rollback with v2 instances, and empty explicit SLA routes falling back to a broader task audience. Medium gaps: live membership versus snapshot contract and late-scan behavior across dates. Also clarify group identity/SortOrder maintenance.

No plan, source, seed, schema or database changes. No tests, seed verification or sample execution; source-only review. Production state and source images were not verified. H01 remains published as d881c15. Next: revise the plan and resolve its decisions before implementing v2. Preserve unrelated files.

---
# CDT PortfolioTrade v2 notification routes (plan stage)

Updated: 2026-10-18. Goal: publish PortfolioTrade v2 with the new CDT recipients and SLA timing (docs/CDT_requirements/*.jpeg); keep v1 for the 4 running production workflows.
Plan: .planning/quick/261018-cdt-portfolio-trade-v2-notification-routes/261018-cdt-PLAN.md. All recipient and SLA decisions are recorded in it.
Status: IMPLEMENTED locally (2026-10-18), not committed, not deployed. Engine E1/E2/E3, migration AddCdtV2SlaReminderControls, v2 seed + seed/verification refactor, init-db --portfolio-trade-version, scripts sql/upgrades/20261019_01..06, docs/CDT_V2_RELEASE.md, PROJECT.md section 13 addendum, OPEN-ISSUES entry.
Verified: both solutions build warning-free (0/0); final full core suite 566 passed, 5 skipped (SMTP), 0 failed; API suite 70/70; new CdtV2* tests 39/39 included; scripts tested end to end on DOLLY scratch DBs; section 21 console gates passed on cdtv2_gates. Not run: SMTP tests (sender code unchanged), fresh-clone gate (tests HEAD only; changes are uncommitted).
Scratch DBs on DOLLY to drop when done: cdtv2_ddl_manual, cdtv2_ddl_ef, cdtv2_scripts, cdtv2_gates.
Engine work: E1 SLA-route recipient rules, E2 separate Due Today time + skip missed due reminder, E3 first overdue next local day. Deliverables include DDL/DML scripts in sql/upgrades (the user applies them at the target; never run init-db against production).
Reroute report (new desk or new date after a return to the Maker): not reproduced in the engine (temporary SQL-backed test passed on both return paths; test deleted). A permanent regression test is in the plan. The user will run a full test after implementation.
Next session: read the plan and PROJECT.md, then wait for the user's approval before implementing.

---

# H01 fix completed - 2026-10-18

User authorized: "fix this high". H01 bounded outbox starvation is fixed. Bounded selection uses eligibility time, RetryCount, creation time and ID. Zero-delay bounded failures store the current time; unlimited defaults remain unchanged. No schema, package, public contract or workflow changes.

Verification: red SQL run 4 expected failures/2 controls passed; focused 33 passed; final core 527, API 70, local SMTP 5 passed. Both warning-as-error builds passed with zero warnings/errors. Seed verification, all ten sample JSON files, branch/demo and fresh-clone gates passed. Independent reviewer found no material findings. Capture server PID 24932 was stopped. Evidence: artifacts/h01.

Files: processor, Phase5OutboxTests, OUTBOX_RETRY_ROLLOUT, OPEN-ISSUES, STATE, quick task 261018-fur PLAN/SUMMARY/REVIEW, and this local handoff. Existing audit reports, docs/README edit and target snapshot were preserved. Commit d881c15 contains the eight scoped fix files. User authorized publication; pushed to origin/console-code-first-0518 on 2026-10-18 and verified remote SHA d881c15424bd3e2207965ecff6f4d2dee30331bd. No production deployment.

Remaining: deploy updated binaries to all bounded workers through the normal release process. No new migration or active workflow closure needed. Clock/overload/SQL-sort and existing at-least-once limits are documented in the task SUMMARY. Other findings and archive decisions remain deferred.

Next-session prompt: Read PROJECT.md and .planning/quick/261018-fur-fix-h01-bounded-outbox-retry-starvation/261018-fur-SUMMARY.md. H01 is fixed and verified; preserve unrelated work. Do not push/deploy without authorization.

---

# H01 HTML explanation ? 2026-10-18

Completed the requested standalone HTML explanation at docs/H01_OUTBOX_BATCH_BLOCKING_EXPLAINED.html. Writing target: approximately 80% ASD-STE100-style English; no measured compliance claim. Includes the failure sequence, visual batch example, timing, High severity rationale, source/model evidence limits, proposed correction and confirming SQL test.

Verification: HTML tags, unique IDs, local links and all 608 protected file hashes passed. Browser rendering was not tested. Earlier audit tests were not rerun. No source, SQL, package, seed, sample, SMTP or production changes. Updated files: this handoff and the new HTML document.

Remaining: H01 is unfixed. SQL-backed confirmation and a compatible selection policy remain open. Resume from the HTML explanation and .planning/audits/20261018-core-critical-high/REVIEW.md. Preserve earlier work and deferred archive decisions.

---

# Fresh core-engine Critical/High audit checkpoint

Updated: 2026-10-18. Baseline: f47c99cbf2b2497514dc79e79581d4b4fd6c3167.

Current goal: completed a fresh read-only core workflow engine audit after C06/C13/C14 maintenance. Report: [.planning/audits/20261018-core-critical-high/REVIEW.md](.planning/audits/20261018-core-critical-high/REVIEW.md).

Completed: traced current execution/start, replay/concurrency, persistence, SLA and notification paths; challenged candidates against contracts and settled owner decisions. One new conditional High finding (H01): permanently failing oldest messages can repeatedly occupy a bounded outbox batch and indefinitely block newer healthy mail. No Critical defect substantiated. The caller-owned transaction candidate was excluded because the caller must roll back unsuccessful results. Prior Medium hash omission was not relabeled as a new High defect.

Evidence: 35/35 database-free unit tests passed; five source-derived selection model cases passed expected assertions. Two model cases produced 2,400 failed attempts and zero attempts of the healthy tail across 24 polls; three controls delivered it. The model extracts current selection code and compiles current retry options, but does not execute SQL-backed ProcessAsync. No SQL, seed, samples, SMTP or production operations ran. No full regression/fresh-clone claim.

Changed files: this handoff and the new audit directory (PLAN, REVIEW, probe and evidence). No engine/test/config/package/SQL/sample edits. Earlier report edits and target snapshot remain preserved. No commit/push/deployment.

Remaining tasks: SQL-backed confirmation of H01 in an isolated test database, identify deployed batching/retry/poll settings, then agree a compatible fairness policy before implementing a fix. No implementation requested or authorized by the audit. Legacy unlimited defaults do not have this particular bounded-batch starvation mechanism. Archival remains deferred.

Known risks: the finding is conditional on a full batch of persistent failures and eligibility at each poll; no production incident is claimed. Other engine/integration findings and deployment gaps are not resolved by this audit.

Next-session prompt: Read PROJECT.md and .planning/audits/20261018-core-critical-high/REVIEW.md. Review H01 and its evidence limits with the owner. Preserve the existing uncommitted reports and target snapshot. Do not implement fairness, alter retry ordering/contracts, run SQL-backed tests, change production data or send email without a new scoped instruction.

---

# C14 SLA batching checkpoint

Updated: 2026-10-17. User authorized bounded SLA batch processing and deferring archival to the end of the issue list. Archive implementation, retention and purge remain undecided.
Goal: bound candidate/EF tracker growth while preserving reminder semantics and public contracts.
Task: .planning/quick/261017-vqr-implement-c14-bounded-sla-batches-and-de/261017-vqr-PLAN.md.
Implemented: SlaProcessingOptions default100 with optional Sla:BatchSize; keyset pages ordered by reminder time/ID and run-start upper schedule ID; fresh context per batch; existing per-schedule transactions/failure isolation; aggregate existing results. Batch contexts borrow live connection without ownership and preserve runtime command timeout/interceptors. Unchanged pretracked caller schedules refresh values/rowversion; caller edits remain untouched.
Files: RunSlaService, SlaProcessingOptions, Infrastructure DI, Phase5SlaTests, SlaProcessingOptionsTests, docs/SLA_BATCH_PROCESSING.md and docs/OPEN-ISSUES.md, task artifacts. No schema/seed/package change; unrelated prior files preserved.
Tests: full core519/519 passed, then final focused39/39 passed after the last caller-edit guard/test; API70/70 and both warning-as-error builds passed with zero warnings/errors. Counts overlap; task SUMMARY records exact order. Seed verification, all ten samples, return branches/demo and fresh-clone checks passed. Initial red/intermediate runs exposed one-context scanning, stale caller schedules, owned/runtime connection issues and undetected edits; all corrected and tested. Final independent review reports no material issues. Evidence artifacts/c14. SMTP not rerun because sending code is unchanged.
Commit: f47c99c contains the scoped implementation, tests and documentation with detailed validation notes. Pushed at the user's request to origin/console-code-first-0518 on 2026-10-17; remote verified as f47c99cbf2b2497514dc79e79581d4b4fd6c3167. Local handoff and unrelated reports remain uncommitted.
Remaining: deployment when requested. No production deployment performed. No active workflow closure required.
Limits: legacy result lists and run duration still scale with due work. Not a production performance benchmark; measured indexes/target whole-table queries remain C14 follow-up. Archival deferred.
Next session: C14 SLA batching is implemented and verified; read task SUMMARY for publication/deployment. Preserve unrelated reports and target snapshot. Archive decisions remain deferred.

---

# C13 implementation and verification

Updated: 2026-10-14. User approved the C13 proposal with "proceed".
Goal: bound notification outbox processing and add durable retry scheduling while preserving existing integrations.
Inspected: PROJECT.md, consolidated C13 finding, outbox processor/entity/DI, result contract and API action dispatcher.
Proposal: optional batch limit (recommended 100), optional exponential retry delay (1 minute doubling to 60 minutes), nullable NextAttemptAtUtc and supporting index with additive upgrade SQL. Preserve legacy defaults; enable controls explicitly. Both delivery paths must honor retry eligibility in selection and atomic claims. Keep unlimited retries, existing recipient snapshots, row ownership and public contracts. SMTP remains at least once.
Active workflows do not need closure. Coordinate all notification processors, allow in-flight sends to finish, apply additive SQL, deploy compatible consumers and enable settings. Do not reset workflows or run production init-db. Old consumers ignore retry delays; avoid mixed consumers once enabled. Rollback can retain additive schema but loses retry controls with old binaries.
Completed changes: optional validated OutboxProcessingOptions; bounded lightweight backlog query and warning list; durable delay and eligibility checks in both consumers; preserved three-argument constructors; nullable timestamp/index EF migration and rerunnable manual SQL; regression tests and docs/OUTBOX_RETRY_ROLLOUT.md. GSD task .planning/quick/261014-pj7-fix-c13-bounded-outbox-batches-and-durab.
Build/test status: initial red tests reproduced batch/retry defects (2) and API delay bypass (1). Final core 508/508, API 70/70 and local SMTP 5/5 passed (583 total). Both warning-as-error builds passed with zero warnings/errors. EF/manual upgrades preserve active workflows and mail; manual SQL rerun passed. Seed verification, all ten samples, branch/demo and fresh-clone checks passed. Independent read-only reviewer found no material issues. Evidence artifacts/c13. Local smtp4dev PID 17356 stopped after tests.
Commit: 7350a4a contains the C13 implementation, tests, migration, rollout guide and detailed validation message. Published to origin/console-code-first-0518 on 2026-10-14 at the user's request. Remote and local HEAD verified as 7350a4ad04580a8ce4f2d2a3ac493c04e2fe4306. Unrelated report changes and earlier handoff notes remain uncommitted.
Remaining: deployment coordination as a separate operational step. No production actions performed. New controls require explicit configuration after schema/consumer deployment.
Known risks: uncertain SMTP acceptance can cause duplicate delivery; terminal failure/dead-letter behavior is outside this proposal.
Next-session prompt: C13 is implemented, verified and published as 7350a4a. Read task SUMMARY and docs/OUTBOX_RETRY_ROLLOUT.md for deployment coordination. Preserve unrelated work. Do not close active workflows or modify production data without deployment instructions.

---

# Current package security fix handoff

Updated: 2026-10-14. User authorized fixing the listed vulnerable package and running tests.

Goal: resolve System.Security.Cryptography.Pkcs 10.0.0 to patched 10.0.13 across
both solutions and the acceptance tool, preserving workflow behavior.

Changed: explicit package reference in
`src/WorkflowEngine.Infrastructure/WorkflowEngine.Infrastructure.csproj`.
Both MailKit/MimeKit and EF Core/SqlClient dependency paths meet there.
Plan: `.planning/quick/261014-p51-patch-vulnerable-pkcs-dependency-and-ver/261014-p51-PLAN.md`.

Verification complete: forced no-cache restores and both warning-as-error builds
passed. Both solutions and acceptance-tool vulnerability scans are clear; all seven
dependent assets files resolve 10.0.13. Core 491/491, API 68/68 and SMTP 5/5 passed
(564 total). Both old fresh-build failures now pass. Seed, all ten samples and
branch/demo checks passed. First SMTP run had one capture-server readiness timeout;
after HTTP 200 readiness verification, the full rerun passed without code changes.
The task-owned smtp4dev PID 27552 is stopped. Logs: artifacts/pkcs-security.
Task PLAN/SUMMARY, .planning/STATE.md and docs/OPEN-ISSUES.md record completion.
Commit df270b2 published to origin/console-code-first-0518 on 2026-10-14 at
the user's request, preserving the same commit and its detailed validation message.
No production data or deployment changes. Unrelated uncommitted work preserved.

Next session: read the package task PLAN/SUMMARY and PROJECT.md. The package fix
is published and ready for target deployment coordination. Preserve unrelated uncommitted review
files and the target snapshot. C06 is already published; its earlier handoff follows.

---

# Preserved C06 fix handoff

Updated: 2026-10-14. User approved implementation of the C06-only design.

## Current goal

Prevent one invalid SLA schedule from aborting reminders for later healthy
workflows. Active workflows do not need to be closed. No production data repair,
schema migration, package upgrade, email delivery or deployment is part of this task.

## Completed changes and files

- `src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs`: private schedule-data
  error boundary, rollback/tracker cleanup, identifying warnings and continuation.
  Calendar exception handling is limited to calendar calculations; unexpected,
  cancellation and infrastructure errors still propagate.
- `tests/WorkflowEngine.Tests/Phase5SlaTests.cs`: SQL-backed poison-row, staged
  outbox rollback, repeated-scan, recovery and fatal-error regression cases.
- `.planning/quick/261014-orf-fix-c06-sla-schedule-failure-isolation/`: approved
  plan and completion evidence. Existing unrelated reports remain unchanged.

## Build/test status

Initial red run: 8 expected C06 failures and 3 passing propagation tests.
First green run: 26 focused SLA tests passed. Final core: 489 passed, 2 failed;
API: 68 passed. All 12 C06 cases passed, including recovery after repair.
Both solutions build with --no-restore, zero warnings/errors. Seed verification,
all ten samples, branch and demo tests passed on dedicated suite databases.
The two existing fresh-clone failures are NU1902/NU1903 advisories for
System.Security.Cryptography.Pkcs 10.0.0. No dependency or warning suppression
changes. Read-only code review found no material issues. Code commit: ad47084.
Published to origin/console-code-first-0518 on 2026-10-14 with full release notes
in commit c0983b4. Live remote and local HEAD both verified as
c0983b44426b3297945cc464e6ebde78ef5f2887. No deployment performed.
See the quick-task SUMMARY for commands, evidence and rollout steps.

## Risks and next-session prompt

Read this checkpoint, the quick-task PLAN/SUMMARY and PROJECT.md. Local C06
implementation is complete with the existing clean-build limitation. Deployment requires the target owner to
identify and update the actual SLA runner's engine build and retain/report
`RunSlaResult.Warnings`. Failed schedules remain available for retry/repair;
the fix does not correct their metadata. All other review issues remain paused.
Preserve all pre-existing uncommitted documentation and target snapshot files.

---

# Preserved consolidated-review handoff

Updated: 2026-10-14.

## Current goal

Completed the user-requested consolidation of both workflow-engine reviews into **one final HTML file**, with a scenario-based runtime explanation for every retained issue. The user corrected the initial request: do not create one file per defect.

## Completed work and changed files

- Created [WORKFLOW_ENGINE_FINAL_CONSOLIDATED_REVIEW.html](docs/WORKFLOW_ENGINE_FINAL_CONSOLIDATED_REVIEW.html).
- Merged overlaps into C01–C24: 21 defects/runtime risks and 3 deployment evidence gaps. Preserved C25 as a development/test-only seed issue, outside the production target count.
- Added starting conditions, ordered runtime sequences, expected results, user/operator effects, triggers, source evidence, proposed confirming tests and compatibility limits for all 25 items.
- Mapped all 59 primary source IDs (A01–A29, P1–P6, IA01–IA24) and 38 inherited dispositions. Refuted/accepted/policy topics remain visible in O1–O6.
- Updated this handoff. Both original reports remain unchanged. No per-defect files were created.

## Verification status

Report checks passed: 25 scenarios, complete ID mapping, 384 valid cited source ranges, balanced HTML, unique anchors, resolved internal links and JavaScript syntax. Both input reports retained their SHA-256 hashes. All 748 protected files matched the prior audit baseline.

No application build/test, database query/change, migration, seed verification, sample JSON, scheduler, load test or email ran. Browser rendering was not tested. This task was documentation consolidation, not implementation or production validation.

## Remaining tasks and known risks

The consolidated report is complete. Deployment evidence and the proposed runtime tests remain open. Do not treat code-supported scenarios as reproduced production failures. Do not infer a missing production scheduler/dispatcher implementation from the incomplete snapshot. Preserve all nine settled owner decisions, existing public contracts, keys, DI methods, snapshots, defaults and manual-SQL production initialization.

## Next-session prompt

Read AGENTS.md, PROJECT.md and docs/WORKFLOW_ENGINE_FINAL_CONSOLIDATED_REVIEW.html. Use C01–C25 as the consolidated issue IDs and the source-ID map for traceability. Review missing deployed-build/runner/schema evidence and the critical C01/C02 scenarios first. Obtain a new scoped instruction before fixes or runtime/database/email tests. Keep both original reviews and all existing uncommitted work unchanged.

---

## Preserved previous handoff

# Current audit handoff

Updated: 2026-10-14. Task: independent read-only code-evidence audit.

## Current goal and completion

Completed the independent engine pass, the Atlas in-process integration trace, and the later comparison with all five prior reports. The report is [WORKFLOW_ENGINE_CODEX_INDEPENDENT_AUDIT.html](docs/WORKFLOW_ENGINE_CODEX_INDEPENDENT_AUDIT.html).

The report contains 24 findings (2 Critical, 6 High, 14 Medium, 2 Low), 73 requested prior-ID dispositions, call-by-call roles/results/retry/concurrency/email evidence, compatible remediation limits, and proposed confirming tests. Conditional findings are not reproduced production defects. New concerns include writable workflow projections, SQMS schema mismatch, bid summaries after commit, and additional field/hash mapping evidence.

## Changed files

- Created docs/WORKFLOW_ENGINE_CODEX_INDEPENDENT_AUDIT.html.
- Updated this handoff. The earlier handoff text is preserved below.
- Did not modify source, configuration, SQL, seed data, packages, databases, prior reports or docs/README.md. Existing uncommitted files remain in place.

## Build and test status

No build, application test, HTTP call, SQL query, seed verification, migration, sample JSON, scheduler, load test or email ran. Browser rendering was not tested. The read-only scope permitted only the report and this handoff.

Report checks passed: HTML tag balance, unique anchors, internal links, cited path/line ranges, all 73 requested IDs, and no external rendering dependencies. All 748 protected files matched hashes captured before document generation. Git status showed only the new report in addition to the pre-existing modified/untracked items. These document checks do not validate runtime behavior.

## Main conclusions and known risks

- Atlas supplies configured CDT roles without a SAVE entitlement gate. SQMS starts assert MFO; existing SQMS saves have incomplete application scoping.
- MapData accepts writable WorkflowState and other protected business fields. CDT SaveDraft can skip engine execution and retain the submitted state projection.
- Atlas ignores engine failed results. Engine and business commits are separate in the supplied code. Dispatch is called before the Atlas commit.
- The target dispatcher/mapper, context setup, deployed binary identity, effective configuration and scheduled jobs are missing from the snapshot. The net9/net10 and DefaultConnection mismatch is a verification gap, not proof of production drift.
- Conditional engine issues include SLA poison-row aborts, SLA/task and send/cancel races, and SQMS classification with an incomplete valid bid basis.
- RequiresReason is a UI hint under PROJECT.md, not an unenforced requirement. Late bids can be classified while their parent remains ClosedToBidding. DueToday uses the seeded Due today: prefix.

## Remaining tasks

Obtain complete deployed-build/config/metadata/runner evidence. Under a separately authorized isolated runtime-test scope, test Critical control paths, rejected results, both commit boundaries, dispatch failure, job faults, replay and stale forms. Only then approve narrow compatible fixes or an explicit versioned behavior change. Do not automatically implement findings.

All nine owner exclusions remain settled, including the intentional BDRM No-to-BdrmTradeFulfilled mapping, upstream authentication, unique business IDs, Draft/SaveDraft handling, one task, reminder-only timers, caller-selected conditional paths, excluded historical migrations and protected SLA edits. Role authorization remains in scope. Production metadata remains manual-SQL driven; do not run WorkflowSeedService/init-db.

## Next-session prompt

Read AGENTS.md, PROJECT.md and docs/WORKFLOW_ENGINE_CODEX_INDEPENDENT_AUDIT.html. Review the supplied source findings and evidence gaps with the owner. Preserve source/config/SQL/packages/databases and all existing uncommitted work. Start with deployed artifact identity and the proposed IA01–IA04 test cases. Do not apply fixes, run database-changing tests or send email without a new authorized scope. Preserve public contracts, DI methods, keys, snapshots and defaults; identify any required versioned behavior change before implementation.

---

## Preserved earlier handoff

# Current handoff

Updated: 2026-10-14. Branch: `console-code-first-0518`.

## Latest completed task: target integration evidence

Goal: determine how Atlas integrates the workflow engine from the supplied
`target-implementation-read-only` snapshot. Produce an ASD-STE100-style HTML
report. Do not change application code, configuration, packages, SQL, or seeds.

Completed: [integration evidence](docs/target-api-workflow-integration-evidence.html)
traces the browser, Atlas routes, three adapters, engine calls, state copies,
notifications, roles, and transactions. It includes 37 line-numbered evidence
items and separates target source from the local engine reference.

Main result: Atlas uses direct in-process engine services. Its form-save path
does not use the standalone workflow-api HTTP contract. Target helper/context
implementations and deployed engine build identity are missing. The supplied
CDT BDRM negative-action mapping points to BdrmTradeFulfilled. Failure handling,
transaction coordination, role selection, and retry behavior need target tests.
These are source findings, not reproduced production defects.

Changed files: the HTML report, docs/README.md, and this handoff only.
Verification: HTML structure, 215 links, source excerpts, duplicate frontend
copies, and SHA-256 checks passed for all 499 protected snapshot/source files.
Browser evidence links open their excerpts. Target build/tests, seed verification,
sample execution, SQL queries, and email delivery were not run: the supplied
text export is not a complete target solution or runtime.

External review evidence and generator:
`C:/projects/cdt-workflow-archive/20261014-target-integration-review`.
The supplied target folder was already untracked; leave it untouched.

Remaining task: obtain the target engine build identity, notification helpers,
context/connection setup, deployed action configuration, and scheduled-job host.
Then run controlled end-to-end target tests under a separately authorized scope.
Do not infer missing production behavior from missing snapshot files.

Next-session prompt: Read PROJECT.md and the target integration HTML report.
Review evidence gaps with the target owner. Preserve the source and all paused
decisions. Do not apply the findings as fixes without explicit authorization.
Production schema/seed changes remain SQL-script driven. Leave init-db code
unchanged. Package upgrades remain deferred. Earlier cleanup details follow.

## Earlier cleanup goal

Complete the user-approved repository cleanup. Keep `src/` and `workflow-api/`
in place and preserve all supported behavior. No defect fixes are authorized.

## Completed changes

- Moved 527 historical/scratch files across 22 paths to the external archive.
- Moved all four registered worktrees with Git; retained branches and untracked runtime data.
- Kept source, tests, SQL, scripts, samples, executable scenario packs, current
  design specifications, acceptance tooling, and restore entrypoints.
- Consolidated current findings in [docs/OPEN-ISSUES.md](docs/OPEN-ISSUES.md).
- Replaced the growing handoff history with this checkpoint. Original content
  and all pre-existing uncommitted documents are preserved in the snapshot.

## Archive and recovery

Archive: `C:/projects/cdt-workflow-archive/20261014-cleanup`.

- `snapshot/`: pre-cleanup files, including the original handoff and review links.
- `archived/` and `manifest.csv`: moved files and their SHA-256 hashes.
- `repository.bundle`: verified Git history; patches and status capture local changes.
- `worktrees/`: relocated Git worktrees, still registered with this repository.
- `verification/`: command logs, TRX results, and cleanup checks.

The archive is local and does not travel with a Git clone. Restore selected
paths from `snapshot/` after reviewing current edits. Do not overwrite later
work. To reverse a worktree move, use `git worktree move` with the original
path recorded in `worktrees-before.txt`. The snapshot excludes rebuildable
bin/obj/IDE output, `.git`, `.tools`, and separately relocated worktrees.

## Changed files

Historical paths removed from the working tree; README, docs index, archive
references, .gitignore, GSD state, and this handoff updated. See the cleanup
[plan](.planning/quick/261014-er0-clean-repository-and-archive-historical-/PLAN.md)
and summary in that same directory. Application source and build settings are unchanged.

## Build/test status

Both solutions restore/build from existing restore state with zero warnings/errors.
Core: 477 passed, 2 failed (fresh-clone builds detect NU1902/NU1903 for the existing
System.Security.Cryptography.Pkcs 10.0.0 dependency). API: 68 passed.
SMTP: 5 passed. Total: 550 passed, 2 failed. Seed verification, all ten samples,
demo coverage, and both restore-package extractions passed. All 527 moved files
passed SHA-256 checks; 561 protected files match the original snapshot exactly.
Local documentation file links and Git diff whitespace checks passed.
No audit suppression or package changes applied. See the cleanup summary;
do not claim a fully passing clean-build gate.

## Remaining tasks and risks

Review the package-advisory build failure separately. All paused engine, SQMS,
and CDT topics remain paused; see [open issues](docs/OPEN-ISSUES.md).
The user authorized publication of this cleanup to `origin/console-code-first-0518`,
accepting the local archive and existing test limitations, provided source remains
untouched. All 561 protected files were rechecked byte-for-byte before publication.
Do not resume defect implementation automatically.

## Next-session prompt

Read PROJECT.md, AGENTS.MD, this handoff, the cleanup SUMMARY.md, and
docs/OPEN-ISSUES.md. Check Git status. Review the cleanup diff and its external
archive. Fetch and verify local/remote commit equality. Resolve the clean-build dependency advisory separately within an
authorized scope. Preserve all paused decisions and public compatibility.
