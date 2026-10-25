# Module 1: Section Master

Status: Draft 2, 2026-10-25. Complete for review. Design only. No target code is changed.

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

Note: `CPApplicationCode` already has codes that no module uses: `TD, PBSP, AR, CIO, TARU, CAT, IMPBBW, IPEW, CA, CF, BBHILLQTT, CRRFIR, IMGUIGU`. These are probably the planned apps. Confirm with the target team.

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

### SM-03 Control names are in the template (Medium)

The template has special rows for `Vehicle1`, `Vehicle2` and `Vehicle3`. Evidence: `cp-atlas-sectionmaster.html.txt:21-44`. The same lines also render a `pbdev-select` outside the `*ngIf` for each child control, and line 14 has `*ngIf="isLoading"` on the form. These are probably copy errors (the live form works). Confirm.

### SM-04 Dependent values use position in a display label (Medium)

The client gets related values by position after a split on `" - "` (`value.split(' - ')[2]`). The server builds those labels. If a label changes, or a name contains `" - "`, the wrong fields fill. Evidence: `cp-atlas-sectionmaster.ts.txt:251-267`.

### SM-05 Not all control types render, and unknown types fail silently (Medium, Confirm)

- `filepicker` creates a form control but the template has no `filepicker` row. Evidence: `cp-atlas-sectionmaster.ts.txt:135-136`; template.
- The TTD JSON has a grid control (`WeeklyHour`), but the template has no grid row. Evidence: `CPTTDService.cs.txt:153-167`; template.
- A type that is not known shows nothing and gives no error.

The UI copy may be incomplete. Confirm with the live template.

### SM-06 The server selects module services with `if` chains (High for reuse)

`LoadApplicationService`, `LoadSectionService`, `LoadStepService`, `LoadAttachmentService` and `LoadActionService` each have one branch for each code. `LoadActionService` returns `null` for every code except SQMSO. A call to `Execute` for any other code causes a `NullReferenceException`. Evidence: `Service/CPAtlasService.cs.txt:127-185`.

### SM-07 Default values are in the module services (Medium)

Examples: header text `"Tax Time Database Time Sheet"`, `"New Opportunity"`; status `"DRAFT"`; role filter `["MFO"]` for the buyer list. Evidence: `CPTTDService.cs.txt:59-60, 169`; `CPSQMSOService.txt:97-104`; `CPSQMSBService.cs.txt:96-109`.

### SM-08 Role lookup is different in each module (High, security)

- AIN reads only the roles for its own application (`role.CpAtlasApplicationId == nApplicationID`). Evidence: `CPAINService.cs.txt:43-48`.
- SQMSO reads the user's roles for all applications. Evidence: `CPSQMSOService.txt:88-92`.

With SQMSO, a role that has the same name in another app (for example `Write`) also gives access in SQMSO. This is related to H4 in the issues document. Each new module copies one of these two patterns.

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
- Add renderers for `filepicker` and `grid`, or confirm that the live template has them.
- If `Type` is not known, show a visible "Unknown control type" message in non-production builds. Write a console error in production.
- Remove the `Vehicle1/2/3` special rows. Use `OptionsFromChild` and `DivClass` instead.

### 5.6 Server module registry — removes SM-06, SM-10

Replace the `if` chains with a registry. Use .NET keyed services (built in to .NET 8 and later; no third-party package).

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
9. Set the system fields on the server: `CreatedBy`, `CreatedDate`, `ModifiedBy`, `ModifiedDate`, and the `Defaults` values. Use `TimeProvider`, not `DateTime.Now`. The client cannot set these fields.
10. Call the workflow engine (5.10). If the engine result is not a success, stop with an error.
11. Write the audit entry on the server (5.12).
12. Commit. If an error occurs, roll back, write a log, and return the error (5.11).

Hooks: `OnLoad`, `OnBeforeSave`, `OnAfterSave`, `OnExecute`.

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
- `ActorRoles: "User"` sends the roles of the user for this application. If `ActorRoles` is a list, the service sends that list (current behaviour, for example `["MFO"]`). Change SQMS to `"User"` only after a check that each submitter has the role that the engine needs.
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
- The load response has an optional `Version` (base64). The client sends it back in the save input.
- If the save has `Version` and it is different from the database value, return a conflict error. If the save has no `Version` (an old client), do not examine it. This keeps the current clients working.

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
| 4 | Fix the SQMSO role lookup to filter by application (SM-08). | Medium. Users with roles only in another app lose SQMSO access. Check the entitlement data first. |
| 5 | Move the SQMS logic into SQMSO.json and SQMSB.json (`Effects`, `Action`, `Redirects`). Run the regression checks. | Medium |
| 6 | Add `ConfigSectionService`. Use it first for a new pilot app (for example one of the unused codes in section 3). | Low for current apps |
| 7 | Add the server save pipeline (5.9). Start the allow list in log-only mode: log the ignored names, but still save them. After one release with no unexpected names, change to enforce mode. | Medium |
| 8 | Add the `Errors` contract (5.11). Change the client to show errors. | Low |
| 9 | Move the workflow values into JSON (5.10). Keep `ActorRoles` as a list for SQMS until the role check is done. Make the service read `Success`. | Medium |
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
- A save with an extra output name (for example `WorkflowState` or `IsDeleted`) does not change that field.
- A save for a control that is hidden or disabled for the user does not change that field.
- A save that fails shows an error and does not redirect. The server writes no audit entry.
- An engine rejection shows an error. The record and the state do not change.
- Two users save the same record. The second user gets a conflict error.

For a new pilot app with `ConfigSectionService`:

- The app works with only these changes: a JSON file, one registration line, SQL scripts (table, procedures, entitlements) and a dashboard JSON entry.
- A user with a role only in another app cannot see or change the pilot app records.

## 9. Open questions

1. Answered (2026-10-25): one more app uses Step Master; all other new apps use Section Master. Still open: are the unused codes in `CPApplicationCode` those apps?
2. Does the live template render `filepicker` and `grid` controls (SM-05)?
3. Are lines 14 and 29-44 of the template copy errors (SM-03)?
4. Can the target team use .NET keyed services? This needs .NET 8 or later. The copy targets `net9.0` (H11), so yes, if the live projects agree.
5. Do current SQMSO users depend on roles from other apps (SM-08, migration step 4)?
6. Answered (2026-10-25): no, not now. Step Master serves CDT and one more app. Keep it separate. The save pipeline (5.9) and the workflow binding (5.10) are designed so that Step Master can use them later.
7. Does each SQMS submitter have the role `MFO` in `CP_Atlas_UserEntitlement`? The answer decides when SQMS can use `ActorRoles: "User"` (5.10).
8. Do the four tables have other writers (jobs, procedures) that a `rowversion` column can affect? (`rowversion` changes on each update, and `SELECT *` returns one more column.)
