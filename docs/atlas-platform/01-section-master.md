# Module 1: Section Master

Status: Draft 3, 2026-10-25. All open questions are answered (Q1-Q9, T1-T4, F1-F3). Ready for the target team. Design only. No target code is changed.

App mix (business owner, 2026-10-25): one more new app uses Step Master. All other new apps use Section Master. Thus this module is the platform for most new apps.

## 1. Purpose

Section Master shows a single-page form for one application record. It is the form for SQMSO (Opportunity), SQMSB (Bid), TTD (Tax Time Database) and AIN (Account Inventory). CDT uses Step Master (module 2) instead.

Section Master is the main path for new business apps. All new apps except one use Section Master. If Section Master is generic, each new app becomes mostly a JSON file and SQL scripts.

## 2. Current behaviour

### 2.1 Flow

1. The dashboard opens `sectionmaster/:code/:id`. `id` = 0 means a new record.
   Evidence: `WebUi/WebUi/Source/App/app.routes.ts.txt:30-31`; `.../dashboard/cp-atlas-dashboard/cp-atlas-dashboard.ts.txt:109-125, 316-330`.
2. The component calls `GET v1/SectionPage?Code=&ID=`.
   Evidence: `.../cp-atlas-sectionmaster/cp-atlas-sectionmaster.ts.txt:69-86`; `cp-atlas-service.ts.txt:76-82`.
3. The controller calls `CPAtlasService.LoadSectionPage`. This selects a module service with an `if` chain on the app code.
   Evidence: `Atlasapp/Atlasapp/Controllers/V1/CPAtlasController.cs.txt:64-70`; `Service/CPAtlasService.cs.txt:86-90, 164-176`.
4. The module service reads its JSON file from disk (`AppSettings:<Code>ConfigPath`). If `ID` > 0, it binds the database row into the controls (`BindData`). Then it adds default data, options, status, header name and rules.
   Evidence: `Service/Application/CPTTDService.cs.txt:43, 46-63, 146-171`; `CPSQMSOService.txt:68-111`; `CPSQMSBService.cs.txt:70-113`; `CPAINService.cs.txt:34-66`.
5. The component builds an Angular reactive form from `Controls` → `ChildControls`, with validators from `Validator`.
   Evidence: `cp-atlas-sectionmaster.ts.txt:97-149`.
6. The template renders each child control by `Type`: `select`, `ngselect`, `textarea`, `text`, `currency`, `percent`, `datetimepicker`, `option`.
   Evidence: `cp-atlas-sectionmaster.html.txt:15-126`.
7. Buttons come from the JSON `Buttons` list. `Submit` saves through `POST v1/ManagePage`. `Close` goes back. `Bid` calls `GET v1/Execute?action=MakeBid`. On SQMSO/SQMSB, any other button sets `Status` to the button name and saves.
   Evidence: `cp-atlas-sectionmaster.ts.txt:298-380`; `cp-atlas-service.ts.txt:93-120`.

### 2.2 What is already generic

- The control list, labels, types, validators, options, CSS classes and buttons come from JSON.
- The server has a rules engine (`CPRule`: roles, states, owner → show/hide/enable/disable controls and buttons).
  Evidence: `Data/Data/DTO/CPAtlasApplicationConfig.cs.txt` (class `CPRule`, `ApplyRules`).
- `MapData` / `BindData` map form values to and from entity properties by name.

## 3. The cost to add app #17 today

To add one new single-page app, a developer must change these items:

| # | Item | Where | Type of change |
|---|------|-------|----------------|
| 1 | Add the code to `CPApplicationCode` | `Data/Data/Enumerations/CPApplicationCode.cs.txt` | C# |
| 2 | Add `<Code>ConfigPath` | `appsettings.json` | Config |
| 3 | Write a new module service class (`LoadPage`, `SavePage`, `LoadDefaultData`, `LoadPageData`) | `Service/Application/` | C# |
| 4 | Add a branch in `LoadApplicationService` and `LoadSectionService` (and `LoadActionService` if it has actions) | `Service/CPAtlasService.cs.txt:127-185` | C# |
| 5 | Add an entity, a `DbSet` and a table | `Data/`, SQL | C# + SQL |
| 6 | Add a branch in dashboard `onClick` and `editRow` | `cp-atlas-dashboard.ts.txt:109-125, 316-330` | TypeScript |
| 7 | Add branches in `onClick`, `onRedirectPage` and `onSelectValueChange` if the app has buttons, redirects or dependent fields | `cp-atlas-sectionmaster.ts.txt:244-367` | TypeScript |
| 8 | Write the JSON file | `Config/<Code>.json` | Config |
| 9 | Add the entitlements | SQL | SQL |

Only items 8 and 9 are low-code. Items 1 to 7 are code changes in shared files. Each change can break the other apps.

`CPApplicationCode` already has 13 codes that no module uses: `TD, PBSP, AR, CIO, TARU, CAT, IMPBBW, IPEW, CA, CF, BBHILLQTT, CRRFIR, IMGUIGU`. The business owner confirmed (2026-10-25) that these are the next 13 apps, and that all 13 use Section Master.

## 4. Findings

Each finding has an ID (SM-nn). "Confirm" means the copy is not sufficient to be sure.

### SM-01 Field-specific logic is in the shared component (High for reuse)

The component has SQMS field names in code:

- `onSelectValueChange`: `Account` splits the value on `" - "` into `AccountNumber`, `AccountName`, `RelationshipManager`, `BBHAffiliatedAccount`. `OpportunityId` splits into five fields. `BidForFullCommitment`, `BidAskValue` and `FundConsideredSecondarySale` set or clear other fields. Evidence: `cp-atlas-sectionmaster.ts.txt:244-289`.
- `ngAfterViewInit`: after a 5-second `setTimeout`, it shows or hides `RationaleForSecondarySale`, `RecentDateOfValuation` and `ValuationAsRecentDate`, and adds or removes their `required` validators. It uses `document.getElementById`. Evidence: `cp-atlas-sectionmaster.ts.txt:173-227`.
- `initForm`: `FundConsideredSecondarySale` is a special case. Evidence: `cp-atlas-sectionmaster.ts.txt:120-127`.

Result: a new app with dependent fields needs TypeScript changes in the shared component. The 5-second delay also means the form is incorrect for 5 seconds. A user can save in that time without the conditional validators.

### SM-02 App codes and role names are in the shared component (High for reuse)

- `onClick`: only `SQMSO` and `SQMSB` set `Status` from the button name. Evidence: `cp-atlas-sectionmaster.ts.txt:306-309`.
- `onRedirectPage`: the target dashboard tab depends on `SQMSO`/`SQMSB` and the roles `mfo`/`ipfg`. All other apps go to `/dashboard/{code}/MyOpportunities`, which is a SQMS tab name. Evidence: `cp-atlas-sectionmaster.ts.txt:347-367`.
- `onAddBid`: the target is always `SQMSB`. Evidence: `cp-atlas-sectionmaster.ts.txt:369-380`.

### SM-03 The template has special rows for the vehicle control (Low)

The template has special rows for the vehicle control by name. The vehicle options are kept in a component variable (`vehicles`), not on the control. Evidence: `cp-atlas-sectionmaster.html.txt:21-44`; `cp-atlas-sectionmaster.ts.txt:56, 126, 284`.

Confirmed (2026-10-25): the live form shows one vehicle dropdown. Its options change when the user selects a fund. The fund-to-vehicle map is in the JSON (`FundConsideredSecondarySale.ChildOptions`). The JSON has two vehicle controls (`Vehicle`, `Vehicle2`) for the same field `Vehicle`, in two sections. Evidence: `SQMSO.json.txt:334-392, 556, 761, 925`.

Also confirmed: line 14 (`*ngIf="isLoading"` on the form) and the `pbdev-select` outside the `*ngIf` are copy errors. The live form shows correctly.

Note: the fund list and the fund-to-vehicle map are in the JSON two times (`SQMSO.json.txt:334-392` and `706-761`, control `FundConsideredSecondarySale2`). A new fund needs two JSON edits. Use one `OptionsSource` for both (10.2).

### SM-04 Dependent values use position in a display label (Medium)

The client gets related values by position after a split on `" - "` (`value.split(' - ')[2]`). The server builds those labels. If a label changes, or a name contains `" - "`, the wrong fields fill. Evidence: `cp-atlas-sectionmaster.ts.txt:251-267`.

### SM-05 Unknown control types fail silently (Low)

- A type that is not known shows nothing and gives no error. A typing error in the JSON `Type` hides the control, and the user does not know about it.
- Confirmed (2026-10-25): the live form shows the `grid` and `filepicker` controls. Our template copy does not have these rows, because the copy is not complete. This part is not a defect.

### SM-06 The server selects module services with `if` chains (High for reuse)

`LoadApplicationService`, `LoadSectionService`, `LoadStepService`, `LoadAttachmentService` and `LoadActionService` each have one branch for each code. `LoadActionService` returns `null` for every code except SQMSO. A call to `Execute` for any other code causes a `NullReferenceException`. Evidence: `Service/CPAtlasService.cs.txt:127-185`.

### SM-07 Default values are in the module services (Medium)

Examples: header text `"Tax Time Database Time Sheet"`, `"New Opportunity"`; status `"DRAFT"`; role filter `["MFO"]` for the buyer list. Evidence: `CPTTDService.cs.txt:59-60, 169`; `CPSQMSOService.txt:97-104`; `CPSQMSBService.cs.txt:96-109`.

### SM-08 Role lookup is different in each module (High, security)

- AIN reads only the roles for its own application (`role.CpAtlasApplicationId == nApplicationID`). Evidence: `CPAINService.cs.txt:43-48`.
- SQMSO reads the user's roles for all applications. Evidence: `CPSQMSOService.txt:88-92`.

With SQMSO, a role that has the same name in another app (for example `Write`) also gives access in SQMSO. This is related to H4 in the issues document. Each new module copies one of these two patterns.

Also, no role query filters `IsDeleted` on `CP_Atlas_User`, `CP_Atlas_UserEntitlement` or `CP_Atlas_Entitlement`. A deleted entitlement still gives the role. Evidence: `CPSQMSOService.txt:88-92`; `CPAINService.cs.txt:44-48`; `CPApplicationService.cs.txt:538-542`.

Decision (2026-10-25): each app has its own rules and roles. An app in a parent-child family (for example SQMS: SQMSO and SQMSB) can also use the roles of its parent. No app uses the roles of an unrelated app. See section 11.

### SM-09 Rules cannot depend on field values (High for reuse)

`CPRule` has conditions on roles, states and owner only. It runs on the server when the page loads. There is no rule type for "show field B when field A = X" or "set field B when field A changes". That is the reason for SM-01. Also, each module service calls `ApplyRules` and `ApplyButtonRules` itself (AIN, SQMSO, SQMSB), and TTD does not call them. Each new module must remember to call them. Evidence: `CPAINService.cs.txt:62-63`; `CPSQMSOService.txt:142-148`; `CPSQMSBService.cs.txt:196-197`.

### SM-10 Each request reads the JSON file from disk (Low)

Each module service constructor reads and parses its JSON file. Each app also needs its own `AppSettings:<Code>ConfigPath` key. Evidence: `CPTTDService.cs.txt:43` and the other constructors; `appsettings.json.txt:26-32`.

### SM-11 The UI reads `StepClass`, but the section DTO does not have it (Low, Confirm)

`result.StepClass` is read at `cp-atlas-sectionmaster.ts.txt:92, 153`. `CPApplicationSection` has no `StepClass` property. Thus `formClass` is always empty for sections.

### SM-12 TTD loads the full table to find one row (Low, performance)

`TTDGenerals.ToList().Where(x => x.Id.ToString() == ...)`. Evidence: `CPTTDService.cs.txt:149-151`. Use `Find(id)` or `Single(x => x.Id == id)`.

### SM-13 Each module has its own copy of the save pipeline (High for reuse)

`SavePage` has the same structure in each module: map the outputs, set `CreatedBy`/`ModifiedBy` and `DateTime.Now`, set the status in code (`"SUBMITTED"`), start or execute the workflow, commit, catch all exceptions. Each new app copies this method. Evidence: `CPSQMSOService.txt:178-237`; `CPSQMSBService.cs.txt:222-279`; `CPTTDService.cs.txt:65-121`; `CPAINService.cs.txt:91-131`.

### SM-14 The save trusts the client (High, security)

- `MapData` sets each entity property whose name is in `Outputs`. It does not use the controls of the page. Evidence: `Service/CPApplicationService.cs.txt:83-107`. (Issue H3.)
- The save does not apply the rules again. A control that is hidden or disabled for the user is still saved. `ManagePage` does not check the role, state or owner. (Issue H4.)
- The save does not examine the `Validator` rules (`Required`, `MaxLength`, `Pattern`). Only the client examines them.

### SM-15 The workflow calls are in code for each module (High for reuse)

- `Start` has the business object prefix (`"SQMS-OPP-"`, `"SQMS-BID-"`), the initial action (`"SubmitOpportunity"`, `"SubmitBid"`) and the actor roles (`["MFO"]`) in code. The actor roles do not come from the user. Evidence: `CPSQMSOService.txt:302-323`; `CPSQMSBService.cs.txt:386-407`.
- `Execute` builds the notification recipients in code from fields (`CreatedBy`, `SellerRMTeam`). Evidence: `CPSQMSOService.txt:325-380`.
- The service reads only `NewState`. It does not read `Success` or `ResultCode`. (Issue H8.)
- Good: the action selection is already generic. `GetWorkflowAction` uses the `Workflow.Actions` conditions in the JSON. Evidence: `CPApplicationService.cs.txt:285`; `SQMSO.json.txt:36-61`.

### SM-16 Save errors are hidden from the user (High)

`SavePage` catches all exceptions, rolls back and returns the result object with no error (issue H7). The client always shows "Record saved successfully". Evidence: `cp-atlas-sectionmaster.ts.txt:337-339`.

### SM-17 The client writes the audit log (Medium, security)

An HTTP interceptor sends `POST AuditLog` after each successful `POST`, with the response body. Evidence: `WebUi/.../Interceptors/postrequestinterceptor.ts.txt:15-27`. Thus:

- A save that rolled back (SM-16) still gets an audit entry.
- A user can skip the audit entry, or send a false one.
- Each app has its own audit table (`AIN.AuditLog`, `SQMS.AuditLog`, `TTD.AuditLog`, `dbo.CDT_AuditLog`).

### SM-18 There is no concurrency check (Medium)

Two users open the same record. The second save overwrites the first save with no warning. The tables `SQMS.Opportunity`, `SQMS.Bid`, `TTD.General` and `AIN.AccountInventory` have no `rowversion` column. Evidence: `CP_Sharepoint.txt:133, 918, 948, 1009`.

### SM-19 The child form is made in code, and it can select the wrong parent (High)

`MakeBid` makes a new bid from an opportunity with code in `CPSQMSOService`:

- It finds the opportunity option with `x.Value.StartsWith(page.ID.ToString())`. Opportunity 12 also agrees with 120, 121 and 1234. The bid can link to the wrong opportunity.
- It splits the option on `" $#$ "`. The client splits the same field on `" - "` (SM-04). Two different separators exist for one value.
- It copies five parent fields into the bid by position.
- The client gets the result and keeps it in a shared service, then opens `sectionmaster/SQMSB/0` (`onAddBid`). If the user reloads the page, the parent data is lost.

Evidence: `CPSQMSOService.txt:558-592`; `cp-atlas-sectionmaster.ts.txt:81-84, 369-380`.

## 5. Reusable design

The design adds optional JSON properties and a server registry. When a new property is not in the JSON, the current behaviour stays.

### 5.1 Value-based client rules (`Effects`) — removes SM-01, SM-04, SM-09

Add an optional `Effects` list to `CPApplicationControl`. The component runs the effects when the form is built and each time the source value changes. It does not use a timer or `document.getElementById`.

```json
{
  "Name": "ClientRelationshipLeavingBBH",
  "Type": "option",
  "Effects": [
    { "When": { "Equals": "No" },
      "Show": ["RationaleForSecondarySale"],
      "Require": ["RationaleForSecondarySale"] },
    { "When": { "NotEquals": "No" },
      "Hide": ["RationaleForSecondarySale"],
      "Unrequire": ["RationaleForSecondarySale"] }
  ]
}
```

```json
{
  "Name": "Account",
  "Type": "ngselect",
  "Effects": [
    { "When": { "HasValue": true },
      "SetFromOption": {
        "AccountNumber": "AccountNumber",
        "AccountName": "AccountName",
        "RelationshipManager": "RelationshipManager" } }
  ]
}
```

```json
{
  "Name": "BidAskValue",
  "Effects": [
    { "When": { "Equals": "Yes, Fair Value" }, "Set": { "BidExpressedAsFairValue": "100 %" } },
    { "When": { "NotEquals": "Yes, Fair Value" }, "Clear": ["BidExpressedAsFairValue"] }
  ]
}
```

```json
{
  "Name": "FundConsideredSecondarySale",
  "Effects": [ { "When": { "HasValue": true }, "OptionsFromChild": "Vehicle1" } ]
}
```

Rules for `Effects`:

- Conditions: `Equals`, `NotEquals`, `In`, `HasValue`. Do not add an expression language.
- Actions: `Show`, `Hide`, `Require`, `Unrequire`, `Enable`, `Disable`, `Set`, `Clear`, `SetFromOption`, `OptionsFromChild`.
- A hidden control has no validators and is not sent in the save. (Today a hidden control is not in the form group.)
- The server must check the same `Require` rules on save. A client rule is only for the user interface.

### 5.2 Structured option data — removes SM-04

Add an optional `Data` dictionary to `CPKeyValue`:

```json
{ "Label": "12345 - Smith Trust - J. Doe", "Value": "12345 - Smith Trust - J. Doe",
  "Data": { "AccountNumber": "12345", "AccountName": "Smith Trust", "RelationshipManager": "J. Doe" } }
```

`SetFromOption` reads from `Data`. If `Data` is not there, the client uses the current `" - "` split. This keeps the current options working.

### 5.3 Button actions from JSON — removes SM-02

Add an optional `Action` object to `CPButton`:

```json
{ "Name": "Accepted", "Label": "Accept",
  "Action": { "Type": "Save", "Set": { "Status": "Accepted" } } }
{ "Name": "Bid", "Label": "Make a bid",
  "Action": { "Type": "Execute", "Execute": "MakeBid", "OpenCode": "SQMSB" } }
{ "Name": "Close", "Label": "Close", "Action": { "Type": "Close" } }
```

`Type` values: `Save`, `Close`, `Execute`, `Navigate`. If a button has no `Action`, the component uses the current logic by button name (`Submit`, `Close`, `Bid`, SQMS status buttons).

### 5.4 Redirect after save from JSON — removes SM-02

Add an optional `Redirects` list to `CPApplicationSection`:

```json
"Redirects": [
  { "Roles": ["MFO"], "Url": "/dashboard/{code}/MyBids" },
  { "Roles": ["IPFG"], "Url": "/dashboard/{code}/IPFGBids" },
  { "Url": "/dashboard/{code}" }
]
```

The first match by role is used. If there is no `Redirects` list, the component uses the current SQMS logic. For a new app with no list, the default is `/dashboard/{code}`, not `MyOpportunities`. This default applies only to codes that are not SQMSO, SQMSB, TTD or AIN, so the current apps do not change.

### 5.5 Control renderer registry — removes SM-03, SM-05

- Replace the long `*ngIf` chain with one map from `Type` to a renderer component. Each current type keeps its current markup and `div_{Name}` ID.
- Move the live `filepicker` and `grid` rows into the registry with no change to their behaviour. (The live form has them; Q2.)
- If `Type` is not known, show a visible "Unknown control type" message in non-production builds. Write a console error in production.
- Remove the vehicle special rows. `OptionsFromChild` puts the child options on the target control itself, so the normal `select` row shows them. This keeps one vehicle dropdown whose options change with the fund.

### 5.6 Server module registry — removes SM-06, SM-10

Replace the `if` chains with a registry. Use .NET keyed services (built in; the live projects use .NET 10). No third-party package is necessary.

```csharp
// Composition root, one line for each app:
services.AddAtlasModule<CPSQMSOService>("SQMSO", configFile: "SQMSO.json");
services.AddAtlasModule<CPTTDService>("TTD", configFile: "TTD.json");
// A new app with no custom logic:
services.AddAtlasModule<ConfigSectionService>("PBSP", configFile: "PBSP.json");
```

- `AddAtlasModule` registers the service by code for each interface that it implements (`IApplicationService`, `ISectionService`, `IActionService`, `IStepService`, `IAttachmentService`).
- `CPAtlasService` resolves by code. If the code has no service for that interface, return 404 or 400 with a clear message. Do not return `null` (SM-06).
- Load JSON files from one folder setting (`AppSettings:ConfigFolder`). Cache them and reload when the file changes. Return a deep copy for each request, because the services change the section object.
- Keep the old `<Code>ConfigPath` keys. If a key exists, it overrides the folder path for that code.
- Keep `CPApplicationCode` for the current codes. The registry uses the string code, so a new app does not need an enum change. (A separate spec, module 7, covers this.)

### 5.7 Generic section service — removes most of items 3–4 in section 3

`ConfigSectionService` is the default service for a simple app. It does these steps from JSON:

- Header: `HeaderName` and an optional `HeaderNameWithId` template, for example `"{Name} - {ID}"`.
- Defaults: an optional `Defaults` map, for example `{ "Status": "DRAFT", "CreatedBy": "{User.DisplayName}" }`.
- Roles: always read only the roles for the current application (fixes SM-08). Then apply `CPRule` to controls and buttons.
- Options from data: an optional `OptionsSource` on a control (for example a stored procedure name with parameters). The Data module spec (module 5) defines how this works.
- Save and load: use `MapData` / `BindData` with the entity or table from the module registration. The Data module spec (module 5) defines this.

An app that needs special logic still writes its own service class. It inherits from `ConfigSectionService` and overrides only the hooks that it needs (`OnLoad`, `OnBeforeSave`, `OnAfterSave`, `OnExecute`).

### 5.8 Dashboard navigation — removes item 6 in section 3

Add an optional `FormType` (`SECTION` or `STEPPER`) to each app in the dashboard JSON. `onClick` and `editRow` use it to open `sectionmaster` or `stepmaster`. If it is not there, use the current `if` chain. The app JSON already has `Type` (`CPApplicationConfigType`), so the server can also supply it.

### 5.9 Server save pipeline — removes SM-13, SM-14, SM-16

`ConfigSectionService` has one save pipeline. A module service changes it only through hooks.

1. Load the configuration and the record.
2. Read the user roles for this application only. Find the owner and the state.
3. Apply `CPRule` and the server part of `Effects`. The result is the effective section for this user.
4. Make an allow list: the controls that are visible and enabled in the effective section. Ignore all other `Outputs`. Write a warning log for each ignored name. This fixes H3 and the field part of H4.
5. If the button or the save is not permitted for this user and state, stop with an error. This fixes the access part of H4.
6. Examine the `Validator` rules and the `Require` effects on the server.
7. Examine the version token (5.13).
8. Map the allowed values to the entity.
9. Set the system fields on the server: `CreatedBy`, `CreatedDate`, `ModifiedBy`, `ModifiedDate`, and the `Defaults` values. Use UTC from `TimeProvider`, not `DateTime.Now`. The workflow engine also writes `ModifiedDate` in UTC (5.13). The client cannot set these fields.
10. Call the workflow engine (5.10). If the engine result is not a success, stop with an error.
11. Write the audit entry on the server (5.12).
12. Commit. If an error occurs, roll back, write a log, and return the error (5.11).

Hooks: `OnLoad`, `OnBeforeSave`, `OnAfterSave`, `OnExecute`.

`OnAfterSave` runs after the commit. It must not throw. If it throws, the platform logs `Atlas.AfterSaveFailed` (41012) and returns the saved ID, because the record is already saved (review SV-06). Work that must succeed with the save belongs in `OnBeforeSave`.

Note: issue H6 (the engine and Atlas use separate transactions) also applies to step 10. The engine adapter spec (module 10) covers H6.

### 5.10 Workflow binding from JSON — removes SM-15

Add optional properties to the `Workflow` block:

```json
"Workflow": {
  "Key": "SqmsOpportunity",
  "BusinessObjectPrefix": "SQMS-OPP-",
  "InitialAction": "SubmitOpportunity",
  "ActorRoles": "User",
  "Recipients": {
    "maker":        { "FromUserField": "CreatedBy" },
    "sellerRmTeam": { "FromUserField": "SellerRMTeam", "Multiple": true }
  },
  "Actions": { "...": "unchanged" }
}
```

- `BusinessObjectPrefix` and `InitialAction` replace the values in code. Keep the current values, so that the engine instance IDs do not change.
- `ActorRoles: "User"` sends the real roles of the user (the roles in the `RoleScope`, 11.2). This is the default for new apps. The workflow engine does the role check. Atlas does not replace the user roles with a fixed role.
- Decision (2026-10-25): SQMS also changes to `"User"`. A list (for example `["MFO"]`) is the legacy behaviour only. Keep it until the migration step for SQMS, then remove it.
- Before SQMS changes, run part 5 of [sql/01-section-master-role-scope-check.sql](sql/01-section-master-role-scope-check.sql) with `@RequiredRole = 'MFO'`. It lists the family users without that role. Give the role to the users who must submit. After the change, the engine refuses a submit from a user without the role. Atlas shows the engine error (5.11).
- `Recipients` maps a field to a recipient key. The service finds each user by display name in `CP_Atlas_User`.
- The service must read `Success` and `ResultCode` from each engine result.
- `Actions` does not change. `GetWorkflowAction` already uses it.

### 5.11 Error contract — removes SM-16

- Add an optional `Errors` list to the `ManagePage` response: `[{ "Field": "AccountName", "Code": "Required", "Message": "..." }]`. `Field` is empty for a page error.
- On error, the response has `ID` = 0 and `Errors` is not empty. The HTTP status stays 200 in phase 1, so the current client does not fail.
- The updated client shows the errors and does not redirect. It shows "saved" only when `Errors` is empty.
- Phase 2 (with approval): return HTTP 400 for validation errors and 409 for a version conflict.

### 5.12 Server audit — removes SM-17

- The save pipeline writes the audit entry in the same transaction. It uses the change list from `MapData` (`lstDifference`), which the service already makes.
- Keep the `POST AuditLog` endpoint for old clients. When the server audit is active for a module, the endpoint ignores calls for that module. This prevents two entries.
- One shared audit table for all apps is part of module 9. Until then, use the current table of each app.

### 5.13 Version token — removes SM-18

- Add a `rowversion` column to each Section Master table. The SQL script is [sql/01-section-master-rowversion.sql](sql/01-section-master-rowversion.sql).
- In EF Core 10, map the column with `.IsRowVersion()` (or `[Timestamp]`). EF Core then adds the version to the `UPDATE` and gives `DbUpdateConcurrencyException` on a conflict. The pipeline changes this exception to a conflict error (5.11).
- The load response has an optional `Version` (base64). The client sends it back in the save input.
- If the save has `Version` and it is different from the database value, return a conflict error. If the save has no `Version` (an old client), do not examine it. This keeps the current clients working.

Other writer (confirmed 2026-10-25): the workflow engine SQMS module writes to the SQMS tables. The scheduled job (`run-sqms-scheduled`) closes opportunities and classifies bids. After each successful scheduled action, it runs:

```sql
UPDATE [SSQMS].[Opportunity]   -- or [SSQMS].[Bid]. Note: the live schema is SQMS (open question 9, issue H14).
SET [Status] = @state, [WorkflowState] = @state, [ModifiedBy] = @modifiedBy, [ModifiedDate] = @modifiedAtUtc
WHERE [ID] = @id AND ([IsDeleted] IS NULL OR [IsDeleted] = 0);
```

Evidence: `src/WorkflowEngine.Sqms/Persistence/SqlSqmsDataStore.cs:199-257`; `src/WorkflowEngine.Sqms/SqmsWorkflowService.cs:495-536` (only in `TargetProjectionMode.Scheduled`). The engine reads the tables with explicit column lists (`SqlSqmsDataStore.cs:11-22`).

Effect of the `rowversion` column on the engine:

- No break. The engine uses explicit column lists for `SELECT` and `UPDATE`. A new column does not change them. No engine change is necessary.
- The engine `UPDATE` changes the row version. This is the correct result. Example: a user opens opportunity 12 in state `OPEN TO BIDDING`. The scheduled job closes it (`CLOSED TO BIDDING`). Then the user saves the old form. Today the save can write the old `Status` back over the state from the job (lost update). With the version token, the user gets a conflict error and must reload. This is a strong reason for 5.13.
- The interactive Atlas save does not call this projection. Atlas writes `WorkflowState` itself. Thus one Atlas save does not conflict with itself.

Time values: the engine writes `ModifiedDate` in UTC. Atlas writes `DateTime.Now` (local time). The same column has two time zones. The save pipeline (5.9, step 9) must write UTC from `TimeProvider`, the same as the engine.

## 6. Backward compatibility

- Do not change the route `sectionmaster/:code/:id`.
- Do not change the endpoints `v1/SectionPage`, `v1/ManagePage`, `v1/Execute`, or their request and response shapes. All new DTO properties are optional and nullable.
- Do not change control names, `div_{Name}` and `btn_{Name}` element IDs, or the `#$#` multi-value separator.
- The current JSON for SQMSO, SQMSB, TTD and AIN must give the same result with no edits. Each new property has a fallback to the current code path.
- Remove a legacy code path (for example the SQMS branch in `onSelectValueChange`) only after these conditions are true:
  1. The JSON for that module has the equivalent `Effects` / `Action` / `Redirects`.
  2. The regression checks in section 8 pass for that module.
  3. The target team approves the removal.
- Do not change the `ManagePage` response fields. `Errors` and `Version` are new optional fields. The HTTP status stays 200 in phase 1.
- Do not change the engine instance IDs (`BusinessObjectPrefix` + ID) or the action keys.
- Keep the `POST AuditLog` endpoint.
- The only database change is an additive `rowversion` column (5.13). It does not change existing data.

## 7. Migration steps

| Step | Change | Risk |
|------|--------|------|
| 1 | Add the optional DTO properties (`Effects`, `CPKeyValue.Data`, `CPButton.Action`, `Redirects`, `FormType`). No behaviour change. | Low |
| 2 | Add the client `Effects` engine and the renderer registry. Keep the legacy branches. | Medium |
| 3 | Add the server registry and config cache. Keep the old config path keys. Change the `null` returns to clear errors. | Medium |
| 4 | Change the role lookup to `RoleScope` (own app + parent) and filter `IsDeleted` (SM-08, 11.2). Run `sql/01-section-master-role-scope-check.sql` first, and give roles to the users that it lists. | Medium. Users with roles only in an unrelated app lose access. |
| 5 | Move the SQMS logic into SQMSO.json and SQMSB.json (`Effects`, `Action`, `Redirects`). Run the regression checks. | Medium |
| 6 | Add `ConfigSectionService`. Rebuild TTD on it as the pilot (section 10). Keep the legacy TTD service as the rollback. | Low for the other apps |
| 7 | Add the server save pipeline (5.9). Start the allow list in log-only mode: log the ignored names, but still save them. After one release with no unexpected names, change to enforce mode. | Medium |
| 8 | Add the `Errors` contract (5.11). Change the client to show errors. | Low |
| 9 | Move the workflow values into JSON (5.10). Make the service read `Success`. Run part 5 of the role scope script, give the missing roles, then change SQMS to `ActorRoles: "User"`. | Medium |
| 10 | Move the audit to the server (5.12), one module at a time. | Low |
| 11 | Run the `rowversion` script, then add the version token (5.13). | Low |
| 12 | With approval, remove the legacy branches for each migrated module. | Medium |

## 8. Regression and acceptance checks

For each current module (SQMSO, SQMSB, TTD, AIN), new record and existing record:

- The same controls show, with the same labels, values, options, disabled state and required markers.
- The same buttons show and do the same actions.
- The save sends the same `Outputs` names and values.
- After save, the redirect goes to the same dashboard tab for MFO, IPFG and other users.
- SQMS dependent fields: `Account`, `OpportunityId`, `BidForFullCommitment`, `BidAskValue`, `FundConsideredSecondarySale`, `ClientRelationshipLeavingBBH`, `IsValuationForPrivateFundVehicle` give the same results. They now apply at once, not after 5 seconds.
- SQMS status buttons still start and execute the workflow engine actions as before, with the same engine instance IDs.
- The engine gets the real roles of the user. A user without the role that the engine needs gets the engine error, and the record does not change.
- A save with an extra output name (for example `WorkflowState` or `IsDeleted`) does not change that field.
- A save for a control that is hidden or disabled for the user does not change that field.
- A save that fails shows an error and does not redirect. The server writes no audit entry.
- An engine rejection shows an error. The record and the state do not change.
- Two users save the same record. The second user gets a conflict error.

For a new pilot app with `ConfigSectionService`:

- The app works with only these changes: a JSON file, one registration line, SQL scripts (table, procedures, entitlements) and a dashboard JSON entry.
- A user with a role only in another app cannot see or change the pilot app records.

## 9. Open questions

1. Answered (2026-10-25): the 13 unused codes in `CPApplicationCode` are the next 13 apps. All 13 use Section Master. The one more Step Master app is not in this list. The pilot is TTD (section 10).
2. Answered (2026-10-25): yes. The live form shows the grid and the file upload field. The template copy is not complete. SM-05 is now Low.
3. Answered (2026-10-25): yes, they are copy errors. The vehicle is one dropdown whose options change with the selected fund. SM-03 is now Low.
4. Answered (2026-10-25): the live projects use .NET 10 and EF Core 10. Keyed services are available. No team rule against them is known. The copy (`net9.0`) is old; H11 is closed.
5. Answered (2026-10-25): each app has its own rules and roles. A child app can also use the roles of its parent. No app uses the roles of an unrelated app. Run the read-only role scope script before the change. See section 11.
6. Answered (2026-10-25): no, not now. Step Master serves CDT and one more app. Keep it separate. The save pipeline (5.9) and the workflow binding (5.10) are designed so that Step Master can use them later.
7. Answered (2026-10-25): yes, send the real roles. The workflow engine does the role check. Run part 5 of the role scope script before the SQMS change (5.10).
8. Answered (2026-10-25): the workflow engine SQMS scheduled job writes `Status`, `WorkflowState`, `ModifiedBy` and `ModifiedDate`. It uses explicit column lists, so the `rowversion` column does not break it. Its updates change the row version, which correctly stops a save of an old form (5.13). Run the read-only pre-check in the script for other writers.
9. Answered (2026-10-25): the live database uses the schema `SQMS`. The `rowversion` script uses `SQMS`. Note: the workflow engine SQMS module uses `SSQMS` in its SQL (`SqlSqmsDataStore.cs`). On the live database, the scheduled SQMS job cannot find its tables. The user confirmed that the target engine copy is changed to `SQMS`. Only the local repository copy still has `SSQMS` (issue H14, closed for the target).

## 10. Pilot: TTD on the new approach

The business owner selected TTD (Tax Time Database) as the pilot (2026-10-25). The target team designs and develops TTD again with `ConfigSectionService`. TTD is a good pilot:

- It is an existing app, so the old and new results can be compared one to one.
- It has no workflow. The pilot tests the form platform without engine risk.
- It has the difficult parts that the next 13 apps need: a grid, option lists from data, calculated option lists, defaults, a soft delete and an external account list.

### 10.1 Current TTD behaviour and the new approach

| TTD behaviour today | Evidence | New approach | Custom code? |
|---|---|---|---|
| `Account` options from the PB account service, label `"number - name"` | `CPTTDService.cs.txt:174-200` | `OptionsSource: { "Provider": "PBAccounts" }`, with `Data` (5.2) | No |
| `Account` fills `AccountNumber` and `AccountName` by a `" - "` split | `cp-atlas-sectionmaster.ts.txt:244-257` | `Effects` + `SetFromOption` (5.1, 5.2) | No |
| `TaxYear` options are calculated. Default = second item. A saved value that is not in the list is added. | `CPTTDService.cs.txt:202-214` | `OptionsSource: { "Provider": "TaxYears" }`, `Defaults: { "TaxYear": "{Options[1]}" }`, `KeepSavedValue: true` | Provider only |
| `TaxPreparerName` options = users with role `Prepare` or `Owner` in TTD. Default = current user. | `CPTTDService.cs.txt:216-234` | `OptionsSource: { "Provider": "UsersInRoles", "Roles": ["Prepare", "Owner"] }`, `Defaults: { "TaxPreparerName": "{User.DisplayName}" }` | No |
| `WeekEnding` options = last Sunday and the next Sundays. Default = first item. Uses `DateTime.Today`. | `CPTTDService.cs.txt:236-250, 273+` | `OptionsSource: { "Provider": "WeekEndings" }`. The provider uses `TimeProvider`. | Provider only |
| `WeeklyHour` grid: one row. Column headers show the dates of the selected week. | `CPTTDService.cs.txt:252-269`; `TTD.json.txt:139+` | Grid renderer (5.5). `Grid.BindMode: "SingleRowFields"` (10.2). Date headers in the `OnLoad` hook. | `OnLoad` hook |
| Save: the grid row is a JSON string; the service copies its values into `Outputs` | `CPTTDService.cs.txt:74-91` | `SingleRowFields` does this in the pipeline (10.2) | No |
| The client calculates `WeeklyTotal` and sends it. The server saves the value that it gets. | `CPTTDService.cs.txt:74-91` | `Computed: { "WeeklyTotal": "Sum(MondayMinute, TuesdayMinute, WednesdayMinute, ThursdayMinute, FridayMinute, SaturdayMinute, SundayMinute)" }`. The server calculates it on each save (10.2). | No |
| Header `"Tax Time Database Time Sheet"` and `" - {ID}"` | `CPTTDService.cs.txt:59-60, 169` | `HeaderName`, `HeaderNameWithId: "{Name} - {ID}"` | No |
| Delete sets `IsDeleted`, `ModifiedBy`, `ModifiedDate` | `CPTTDService.cs.txt:125-143` | `SoftDelete: { "Field": "IsDeleted" }` in the module registration | No |
| No rules and no role check on load or save | `TTD.json.txt` (no `Rules`) | App roles only (SM-08). Add `Rules` for `Read` / `Prepare` / `Owner`. | No |
| Errors are hidden (H7) | `CPTTDService.cs.txt:118-121` | Save pipeline and `Errors` (5.9, 5.11) | No |

Result: TTD needs one module class with one hook (`OnLoad` for the grid headers), and two option providers (`TaxYears`, `WeekEndings`). Everything else is JSON.

### 10.2 Platform parts that the pilot adds

- **Option providers.** `OptionsSource.Provider` is the name of a registered `IOptionProvider`. The platform supplies `PBAccounts`, `UsersInRoles` and `StoredProcedure`. An app can register more providers, for example `TaxYears`. The next apps use providers by name in JSON.
- **Default tokens.** `{User.DisplayName}`, `{User.LanID}`, `{Today}`, `{Options[n]}`. Defaults apply only to a new record or an empty value.
- **`KeepSavedValue`.** If the saved value is not in the options, add it at the top of the list. This keeps old records correct.
- **Grid `SingleRowFields`.** The grid has one row. Each column `field` is an entity field. Load puts the fields into the row. Save puts the row cells into `Outputs`. The allow list (5.9 step 4) includes the column fields.
- **`Computed` fields.** The server calculates the field on each save, after the allow list (5.9, step 8). It ignores a value from the client for this field. Functions: `Sum`, `Count`, `Min`, `Max`. Do not add an expression language. Empty values count as 0. The client can still show a calculated total while the user types, but the server value is the saved value.
- **`SoftDelete`.** `RemovePage` sets the field to true and sets `ModifiedBy` and `ModifiedDate`.
- **Module switch.** `AppSettings:Modules:TTD:Service = Legacy | Config`. The default is `Legacy`. The switch gives a rollback with no deployment.

### 10.3 Pilot acceptance

- Do not change the TTD route, endpoints, table, column names or control names.
- For a set of existing TTD records, the load response of the new service is the same as the legacy response. Compare the JSON (controls, values, options, headers). Differences are permitted only where this spec fixes a defect (for example SM-12).
- A save with the new service gives the same row in `TTD.General` as the legacy service, except `RowVersion` and the date values.
- A save with an extra output name, or with a bad minute value, is refused with an error.
- A save with a wrong `WeeklyTotal` from the client saves the correct sum of the seven day columns.
- Each day value is a whole number of minutes from 0 to 1440. An empty value is 0. A save with `1.5`, `1:30`, `-5` or `1441` is refused with a field error. Use the JSON validator: `"Validator": { "Integer": true, "Min": 0, "Max": 1440 }` (new optional `CPValidator` properties).
- A user with no TTD role cannot load or save a TTD record.
- The module switch changes TTD back to `Legacy` with no data change.
- The TTD module code is one class with the `OnLoad` hook and two option providers. Record the number of lines. This is the baseline for the next 13 apps.

### 10.4 TTD-specific questions

- T1. Answered (2026-10-25): yes. The server calculates `WeeklyTotal` on each save (`Computed`, 10.2).
- T2. Answered (2026-10-25): the values are whole numbers of minutes. The server checks 0 to 1440 for each day. The columns stay `varchar(100)`; no database change.
- T3. Answered (2026-10-25): `Read` can view; `Prepare` can create and edit own records; `Owner` can do everything, including delete. See 10.5.
- T4. Answered (2026-10-25): yes. Both fields count. A record is "own" when `CreatedBy` or `TaxPreparerName` is the current user.

### 10.5 TTD access (decided 2026-10-25)

| Action | `Read` | `Prepare` | `Owner` |
|---|---|---|---|
| Open and view a time sheet | Yes | Yes | Yes |
| Create a time sheet | No | Yes | Yes |
| Edit a time sheet | No | Own records only | All records |
| Delete a time sheet | No | No | Yes |
| Listed as Tax Preparer | No | Yes | Yes |

"Own record" (confirmed 2026-10-25): `CreatedBy` or `TaxPreparerName` is the current user. Both fields count. Add an optional `OwnerFields` list to the app JSON. The platform sets `IsOwner` for `CPRule` from these fields. This replaces the owner logic in code for each module (for example SQMSO `CreatedBy` / `SellerRMTeam`, SM-07).

```json
"OwnerFields": ["CreatedBy", "TaxPreparerName"],
"Rules": [
  { "Roles": ["Read"], "Disabled": true },
  { "Roles": ["Prepare"], "IsOwner": false, "Disabled": true },
  { "Roles": ["Prepare"], "IsOwner": true, "Disabled": false },
  { "Roles": ["Owner"], "Disabled": false }
],
"Buttons": [
  { "Name": "Submit", "Label": "Save",
    "Rules": [ { "Roles": ["Read"], "Visible": false },
               { "Roles": ["Prepare"], "IsOwner": false, "Visible": false } ] },
  { "Name": "Close", "Label": "Close" }
],
"Permissions": {
  "Create": ["Prepare", "Owner"],
  "Delete": ["Owner"]
}
```

- `Permissions` is a new optional block. The server checks it on `ManagePage` (new record) and `RemovePage`. The dashboard JSON uses the same block to show or hide the add and delete menus (module 3).
- The server applies `Rules` again on save (5.9, steps 3 to 5). A `Prepare` user who edits another user's record gets an error, also with a changed request.
- A user with more than one role gets the most permissive result. Example: `Read` + `Prepare` can edit own records. Note (corrected 2026-10-25, module 4, RE-02): today `ApplyRules` stops at the first matching rule, so the first matching rule wins. Keep that behaviour (`FirstMatch`) for the current JSON. Add an optional `RuleMode: "MostPermissive"` to the app JSON for TTD and the new apps.
- A user with none of the three roles cannot open TTD (SM-08).
- Acceptance: test each cell of the table above with one user for each role, for an own record and for a record of another user.

## 11. Parent and child apps

Decision (2026-10-25): each app has its own rules and roles. Some apps are a parent-child family. Example: SQMS is the family. SQMSO (Opportunity) is the parent form, and SQMSB (Bid) is the child form. A bid belongs to one opportunity. Of the next 13 apps, one is a family with one parent and two children, and possibly one more has one parent and one child. The others are single apps (F1).

### 11.1 What exists now

| Part | Current implementation | Evidence |
|---|---|---|
| Family in data | `CP_Atlas_Application.ParentID` | `CP_Sharepoint.txt:359-370` |
| Family in code | Each service sets `ParentCode = SQMS` in its constructor | `CPSQMSOService.txt:51`; `CPSQMSBService.cs.txt:53` |
| Roles | Dashboard: parent roles. SQMSO page: all roles (SM-08). Buyer list: parent roles. User list: own + parent roles. | `CPApplicationService.cs.txt:536-542`; `CPSQMSOService.txt:88-92, 268-290`; `CPSQMSBService.cs.txt:95-102` |
| Link in data | `SQMS.Bid.OpportunityId` with a foreign key to `SQMS.Opportunity` | `CP_Sharepoint.txt:920, 1358` |
| Child creation | `MakeBid` in code (SM-19) | `CPSQMSOService.txt:558-592` |
| Dashboard | One `SQMS` dashboard with tabs for `SQMSO` and `SQMSB` | `CPAtlasDashboardConfig.json.txt:57-190` |
| Audit | One table for the family (`SQMS.AuditLog`), with `RefType` = child code | `CPApplicationService.cs.txt:595, 635-639` |
| Roll-up | Bid totals on the opportunity, in code (issue M18) | `CPSQMSBService.cs.txt:273, 355-384` |
| Workflow | Each form has its own workflow (`SqmsOpportunity`, `SqmsBid`) | SM-15 |

### 11.2 Design

**Family from data.** The registry reads the parent from `CP_Atlas_Application.ParentID`. A module does not set `ParentCode` in code. A new family needs only rows in `CP_Atlas_Application`.

**Role scope.** Add an optional `RoleScope` to the app JSON:

- `"Self"`: roles of this app only.
- `"SelfAndParent"`: roles of this app and of its parent. This is the default when the app has a parent.
- The platform never uses the roles of an unrelated app.
- The role query ignores rows where `IsDeleted` = 1 on the user, the user entitlement or the entitlement.

**Child link.** Add an optional `Parent` block to the child JSON:

```json
"Parent": {
  "Code": "SQMSO",
  "LinkField": "OpportunityId",
  "CopyFields": {
    "OpportunityFund": "FundConsideredSecondarySale",
    "OpportunityVehicle": "Vehicle",
    "CommitmentAmountForSale": "CommitmentAmountForSale",
    "EstimatedFairValue": "EstimatedFairValue",
    "RemainingCapitalCommitment": "RemainingCapitalCommitment"
  },
  "CreateWhen": { "ParentStates": ["OPEN TO BIDDING"] }
}
```

- The field names in these examples show the shape only. Confirm them against `SQMS.Opportunity` and `SQMS.Bid`.
- `LinkField` is the child field that holds the parent ID.
- `CopyFields` maps a child field to a parent field. The server copies the values from the parent record by ID, not from an option label.
- `CreateWhen` is optional. The server refuses a new child if the parent state is not in the list.

**Create a child.** Add the button action type `CreateChild`:

```json
{ "Name": "Bid", "Label": "Make a bid", "Action": { "Type": "CreateChild", "ChildCode": "SQMSB" } }
```

- The client opens `sectionmaster/SQMSB/0?parentId={ID}`. The `parentId` query value is new and optional.
- The server loads the new child page with the parent ID. It checks that the parent exists, that the user can read the parent, and `CreateWhen`. Then it sets `LinkField` and the `CopyFields`.
- On save, the server checks `LinkField` again. A user cannot change the parent of a child by an edit of the request (5.9, step 4).
- A page reload keeps the parent, because the parent ID is in the URL.
- Keep `Execute?action=MakeBid` and the current `onAddBid` path for old clients.

This removes SM-19.

**Roll-ups.** Add an optional `Rollups` list to the child JSON, for example the bid count and totals on the opportunity. The save pipeline calculates the roll-up in the same transaction as the child save, from all child rows that are not deleted. This also corrects issue M18. A complex roll-up can use the `OnAfterSave` hook.

```json
"Rollups": [
  { "ParentField": "BidsReceivedCount", "Function": "Count" },
  { "ParentField": "TotalBidAmount", "Function": "Sum", "ChildField": "CommitmentAmountRequested" }
]
```

**Cross-form visibility (decided 2026-10-25).** Each family decides what a user of one form can see of the other forms. All cross-form views are read-only. An edit always needs a role in the form of that record.

In the child JSON, `Parent.View` sets what a child user sees of the parent:

```json
"Parent": {
  "Code": "SQMSO",
  "LinkField": "OpportunityId",
  "View": { "Mode": "CopiedFields" }
}
```

In the parent JSON, `Children` sets what a parent user sees of the linked children:

```json
"Children": [
  { "Code": "SQMSB", "View": { "Mode": "Fields", "Fields": ["ID", "BuyerRMTeam", "CommitmentAmountRequested", "Status"] } }
]
```

| `View.Mode` | Child user sees of the parent | Parent user sees of the children |
|---|---|---|
| `None` | Nothing | Nothing |
| `CopiedFields` (default for `Parent.View`) | Only the `CopyFields` values, on the child form | Not used |
| `Fields` | The listed parent fields, in a read-only panel on the child form | A read-only grid of the linked children with the listed fields |
| `Full` | A read-only link to the full parent record | A read-only grid with a link to each full child record |

- SQMS uses `Parent.View.Mode = "CopiedFields"`. A bid user with no SQMSO or SQMS role sees the copied opportunity fields on the bid, and cannot open the opportunity. This is the current SQMS behaviour (F3).
- `Full` opens the other form in view mode. The server gives view access only for a record that is linked to a record that the user can open. It does not give access to other records of that form.
- The server applies the view settings. It sends only the permitted fields. The client cannot ask for more.
- The default for `Children` is `None`. Thus a parent form shows no children until the JSON says so.

**Audit and dashboard.** Keep the family audit table with `RefType` (module 9). Keep the family dashboard with one tab for each form (module 3).

### 11.3 Backward compatibility

- SQMSO and SQMSB keep their codes, tables, link field, routes and workflow keys.
- `RoleScope` is optional. For SQMSO the change from "all roles" to `"SelfAndParent"` can remove access. Before the change, run the read-only script [sql/01-section-master-role-scope-check.sql](sql/01-section-master-role-scope-check.sql) for `SQMSO` and `SQMSB`. Give the needed SQMS roles to the users in the list first. Then make the change.
- The `IsDeleted` filter can also remove access. The same script lists the deleted rows that give roles today.
- `Parent`, `CreateChild` and `Rollups` are optional. Without them, the current code paths stay.

### 11.4 Acceptance

- A user with a role only in an unrelated app cannot open SQMSO or SQMSB.
- A user with a role in SQMS (the parent) can open SQMSO and SQMSB, as permitted by the rules of each form.
- A deleted entitlement gives no role.
- "Make a bid" on opportunity 12 links the bid to opportunity 12, also when opportunity 120 exists.
- A page reload on a new bid keeps the opportunity data.
- An edit of `OpportunityId` in the save request does not change the parent of the bid.
- A save or delete of a bid updates the opportunity roll-up values in the same transaction.
- A bid user with no SQMSO or SQMS role sees the copied opportunity fields on the bid, and gets an access error when the user opens the opportunity.
- For a family with `View.Mode = "Fields"`, the response contains only the listed fields of the other form.
- For a family with `View.Mode = "Full"`, a user can open a linked record in view mode, but cannot open an unlinked record of that form.

### 11.5 Family questions

- F1. Answered (2026-10-25): most of the next 13 apps are single apps with no children. One app is a family with one parent and two children. Possibly one more app is a family with one parent and one child. The design supports more than one child for each parent: each child has its own `Parent` block, its own `CreateChild` button on the parent, its own `Rollups` and its own dashboard tab.
- F2. Answered by F1 (2026-10-25): no known app needs three levels. Keep one level (parent and children). A third level needs a new spec change.
- F3. Answered (2026-10-25): for SQMS, a bid user sees the copied parent fields and cannot open the opportunity without a parent-form role. Other families can choose differently: full parent and linked children, or only selected fields. See "Cross-form visibility" in 11.2.
