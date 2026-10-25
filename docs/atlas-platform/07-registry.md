# Module 7: App registry

Status: Draft 2, 2026-10-25. All open questions answered (section 9). Design only. No target code is changed.

## 1. Purpose

Today the facts about one app are in many places: an enum, five `if` chains, `appsettings.json`, the dashboard JSON, a database row and the client code. A new app must change each place. This module defines one registry: each app is described one time, the platform registers it at start-up, and all modules (forms, dashboard, rules, security, data, workflow) read the app from the registry.

Scope: the app manifest, the registration, the service resolver, the app code type, the link to `CP_Atlas_Application`, the menu data for the client, and the configuration folder. Related items: the server module registry (module 1, 5.6), the table registration (module 5, 6.1), the dashboard file (module 3, 4.1), the access mode (module 6, section 5), the rule mode (module 4, 4.2), the workflow binding (module 1, 5.10; module 10).

## 2. Current behaviour

### 2.1 Where an app is defined

| Place | What it holds | Evidence |
|---|---|---|
| `CPApplicationCode` enum | The app code (19 values) | `Data/Data/Enumerations/CPApplicationCode.cs.txt:9-30` |
| `CPAtlasService` | Five `if` chains: app, section, step, attachment and action service | `Service/CPAtlasService.cs.txt:127-185` |
| Module service constructor | `Code`, `ParentCode`, the JSON path keys, the dashboard selection | `CPSQMSBService.cs.txt:52-70`; `CPTTDService.cs.txt:31-38` |
| `appsettings.json` | `<Code>ConfigPath` for each app, `DashboardConfigPath` (absolute local paths) | `Atlasapp/Atlasapp/appsettings.json.txt:26-32` |
| Dashboard JSON | Tabs and buttons | module 3 |
| `dbo.CP_Atlas_Application` | `Code`, `Name`, `ParentID`; the roles link to it | `CP_Sharepoint.txt:359-372` |
| Client | Navigation `if` chains, redirect after save | module 1, SM-02; module 3, DB-04 |

### 2.2 Service resolution

- Each request makes a new `CPAtlasService` in the controller. It makes the module service with `new`, with different constructor arguments for each app. Evidence: `CPAtlasController.cs.txt:40, 47`; `CPAtlasService.cs.txt:127-146`.
- An unknown code returns `null`. The caller then fails with a null reference (HTTP 500). Evidence: `CPAtlasService.cs.txt:71, 97, 102, 124, 144-145`.
- The workflow services are optional constructor arguments. `SQMS` (the dashboard code) makes `CPSQMSOService` with no workflow services; `SQMSO` makes it with them. Evidence: `CPAtlasService.cs.txt:132-136`.

### 2.3 The app code on the wire

- `v1/Dashboard`, `v1/Download`, `v1/RemoveAttachment` and `v1/RemovePage` bind the code to the enum. `CPAtlasPage.Code` is the enum. Evidence: `CPAtlasController.cs.txt:45, 74, 94, 124`; `Data/Data/DTO/CPAtlasPage.cs.txt:12`.
- The responses use `System.Text.Json` with no enum converter, so an enum in a response is a number. Evidence: `program.cs.txt:70`.
- The menu comes from `dbo.Get_Application`: the apps where the user has a role, with `Code`, `Name`, `ParentID` and the role names. The client links each app to `dashboard/<Code>`. Evidence: `CP_Sharepoint.txt:1508-1534`; `WebUi/.../app.component.ts.txt:131-155`.

## 3. Findings

### RG-01 Five `if` chains select the services (High for reuse)

A new app needs a change in up to five methods. A missing branch returns `null` and gives HTTP 500 with no message (module 1, SM-06). `LoadActionService` supports only SQMSO. Evidence: `CPAtlasService.cs.txt:127-185`.

### RG-02 The services are not made by dependency injection (Medium)

The services are made with `new`. Each app has its own list of constructor arguments, and a forgotten argument is `null` with no error until it is used. The services cannot get the platform services (rules engine, access check, user context) from the container. Evidence: `CPAtlasService.cs.txt:129-143`.

### RG-03 The app code is an enum (Medium for reuse)

The 13 next apps are already in the enum. App number 18 and later need an enum change and a new build of all projects. A request with a code that is not in the enum fails in model binding (HTTP 400 with a binding message) or in `JsonConvert` (HTTP 500). Evidence: `CPApplicationCode.cs.txt`; `CPAtlasService.cs.txt:95`.

### RG-04 The settings of one app are in many places (Medium)

To see or change one app, a person must look in seven places (2.1). The JSON paths in `appsettings.json` are absolute paths of one developer computer (`C:\Projects\Investment Management\...`), so each environment needs its own file with seven keys. Evidence: `appsettings.json.txt:26-32`.

### RG-05 The parent of an app is in two places (Low)

`CP_Atlas_Application.ParentID` and the `ParentCode` in the service constructor give the same fact. Nothing checks that they agree. Evidence: `CPSQMSBService.cs.txt:52-53`; `CP_Sharepoint.txt:368`.

### RG-06 The menu can show an app that does not work (Low)

`dbo.Get_Application` returns each app where the user has a role, also when the app has no service or no dashboard, and it does not filter `IsDeleted` (module 6, SE-03). The menu item then opens a dashboard that fails. Evidence: `CP_Sharepoint.txt:1508-1534`.

## 4. Reusable design

### 4.1 App manifest

Each app has one manifest file, `<Code>.app.json`, in the configuration folder (4.7). The manifest names the other files and holds the settings of the app for all modules:

```json
{
  "Code": "PBSP",
  "Name": "PB Service Plan",
  "FormType": "SECTION",
  "Parent": null,
  "Form": "PBSP.json",
  "Dashboard": "PBSP.Dashboard.json",
  "Table": "PBSP.General",
  "AuditTable": "PBSP.AuditLog",
  "Service": null,
  "Workflow": null,
  "AccessMode": "LogOnly",
  "RuleMode": "MostPermissive",
  "RuleDefault": "ReadOnly",
  "Visibility": null,
  "Enabled": true
}
```

- `Service`: the key of a custom service class (for example `"TTD"`). `null` means the generic `ConfigSectionService` (module 1, 5.7).
- `Workflow`: the workflow binding (module 1, 5.10; module 10). `null` means no workflow.
- `Parent`: the code of the parent app of a family (module 1, section 11).
- The form JSON keeps the form items (`Section`, `Rules`, `OwnerFields`, `Permissions`, `Effects`). The manifest keeps only the items that are not part of the form. No item is in both files.
- Decision 2026-10-25 (question 2): the behaviour settings are in the manifest (JSON, deployed with the API, with a review). The database keeps only the identity of the app (`Code`, `Name`, `ParentID`) and the roles, as today. No new columns in `CP_Atlas_Application`.
- The settings in the manifest can be changed for each environment in `appsettings.<Environment>.json` with `AppSettings:Modules:<Code>:<Setting>` (for example `AccessMode`, `Enabled`). The environment value wins.

### 4.2 Registration

Decision 2026-10-25 (question 1): each app has one registration line in `Program.cs`. Thus each new app goes through a code review and a release. The platform does not register an app from its manifest only.

```csharp
// Composition root:
services.AddAtlasPlatform(configuration);              // platform services, manifests, start-up check
services.AddAtlasModule<ConfigSectionService>("PBSP");  // a new app with the generic service
services.AddAtlasModule<CPTTDService>("TTD");           // an app with a custom service
```

- `AddAtlasModule<T>(code)` registers the app by its code as a keyed service for each interface that `T` implements (`IApplicationService`, `ISectionService`, `IStepService`, `IAttachmentService`, `IActionService`).
- The line and the manifest must agree. The start-up check stops the API if a manifest has no line, if a line has no manifest, or if the line uses a class that is not the `Service` of the manifest (`null` = `ConfigSectionService`).
- The existing apps (CDT, SQMSO, SQMSB, TTD, AIN, and SQMS for the dashboard) get manifests that name their existing services. Their behaviour does not change.
- `Enabled: false` registers nothing for the app. The app is not in the menu and its endpoints return 404.
- Decision 2026-10-25 (question 3): `Enabled` is used to turn an app on in each environment. One build goes to all environments. Example: `AppSettings:Modules:PBSP:Enabled = false` in `appsettings.Production.json` until the business approves the app; then change the value to `true` (configuration change and restart, no new build).

### 4.3 Resolver

`IAtlasModuleResolver.Get<TService>(appCode)` replaces the five `if` chains:

- The services come from the container (scoped), so the constructors get the platform services and the workflow services from dependency injection. This removes RG-02.
- An unknown code, a disabled app, or an app with no service for the interface returns a problem response: 404 `"App 'XYZ' is not registered"` or 400 `"App 'PBSP' has no step form"`. No `null`. This removes RG-01.
- `CPAtlasService` keeps its public methods and calls the resolver.

### 4.4 App code as text

- The platform uses a string app code (case is ignored). Pattern: `^[A-Z][A-Z0-9]{1,19}$` (the `Code` column is `varchar(20)`).
- The controller parameters change from the enum to `string`. The same query values (`code=TTD`) bind as before. A number value (`code=13`) is mapped to the enum name, for old callers.
- `CPAtlasPage` gets an optional `AppCode` string. The server uses `AppCode`, else `Code`. A new app after the enum list sends `AppCode`. The `Code` property stays.
- The enum stays for the existing code. The response shapes do not change (an enum in a response is still a number). This removes RG-03.

### 4.5 Link to the database

- The start-up check confirms that each enabled manifest has an active row in `CP_Atlas_Application` with the same `Code`, and that `Parent` agrees with `ParentID`. A difference stops the start-up, and the log names the app. This removes RG-05.
- The new-app SQL template (module 5, 6.9) makes the row. The manifest does not make database rows.
- The `Name` in the manifest is for the logs only. The menu continues to use `CP_Atlas_Application.Name`.

### 4.6 Menu data

- `v1/Application` keeps its rows and adds two optional fields from the registry: `FormType` and `HasDashboard`.
- The server removes an app from the result if it is not registered, not enabled, or deleted (module 6, the active roles view). This removes RG-06.
- The client uses `FormType` for the navigation (module 3, 4.5) and shows a menu item only if `HasDashboard` is true. If the fields are not there, the client works as today. Decision 2026-10-25 (question 4): the client and the API are always deployed together, so the client and the API change in the same release. Keep the fallback anyway, for a rollback of the API only.

### 4.7 Configuration folder

- New setting `AppSettings:ConfigFolder`, relative to the content root (default `Config`). All manifests, form JSON and dashboard JSON are in this folder.
- The old `<Code>ConfigPath` and `DashboardConfigPath` keys stay. If a key exists, it overrides the folder for that file (module 1, 5.6).
- The files are read one time at start-up, kept in memory, and read again when a file changes. Each request gets a copy (module 1, 5.6; module 3, 4.1).

### 4.8 Start-up check

One start-up check runs the checks of all modules for each enabled app: the manifest (4.1), the services (4.2), the database row (4.5), the form fields and table columns (module 5, 6.8), the dashboard (module 3, 4.1) and the rules (module 4, 4.7). It writes one report to the log, with each defect and the app code. An error stops the start-up; a warning does not.

## 5. Adding app #17 (target)

| # | Item | Type |
|---|---|---|
| 1 | Run the new-app SQL template (table, audit table, app row, roles) | SQL |
| 2 | Add `<Code>.app.json` | JSON |
| 3 | Add `<Code>.json` (form) | JSON |
| 4 | Add `<Code>.Dashboard.json` | JSON |
| 5 | Grant the roles (module 6, 4.4) | SQL |
| 6 | Add one `AddAtlasModule` line | C# (one line) |

Plus one registration line in `Program.cs` (decision, question 1). No other C# change and no TypeScript change, if the app uses `ConfigSectionService` and the enum already has the code (the 13 next apps).

## 6. Backward compatibility

- All endpoints keep their routes, parameters and response shapes. `AppCode`, `FormType` and `HasDashboard` are additive and optional.
- The string parameters accept the same values as the enum parameters.
- The existing apps get manifests that name their existing services. Their behaviour does not change.
- The `<Code>ConfigPath` keys continue to work.
- The `CPApplicationCode` enum and `IApplicationService.Code` stay. A custom service can still use them.
- An unknown code now returns 404 or 400 in place of 500.

## 7. Migration steps

| Step | Change | Risk |
|---|---|---|
| 1 | Add the resolver with keyed services. Register the existing services with one `AddAtlasModule` line each. Change `CPAtlasService` to the resolver. | Low |
| 2 | Add the manifests for the existing apps, with their existing services. | Low |
| 3 | Add `AppSettings:ConfigFolder`; keep the old path keys. | Low |
| 4 | Change the controller parameters to `string`; add `CPAtlasPage.AppCode`. | Low |
| 5 | Add the start-up check (4.8), with warnings first for one release. | Low |
| 6 | Add `FormType` and `HasDashboard` to `v1/Application`; change the client menu and navigation. | Low |
| 7 | Add the first new app with a manifest and `ConfigSectionService`. | Low |

## 8. Acceptance

- `v1/SectionPage?Code=XYZ` returns 404 with the message "App 'XYZ' is not registered", not 500.
- A new app with a manifest, JSON files, the SQL template and one registration line works with no other C# change and no TypeScript change.
- A manifest with no registration line stops the start-up, and the log names the app.
- A manifest with `Parent` that does not agree with `ParentID` stops the start-up, and the log names the app.
- `Enabled: false` in `appsettings.QC.json` removes the app from the menu in QC only.
- The existing apps give the same responses as before for the same requests.

## 9. Open questions

1. Answered (2026-10-25): one registration line in `Program.cs` for each app (4.2), for a code review and a release step for each new app.
2. Answered (2026-10-25): in the JSON manifest (4.1). The database keeps the identity and the roles only, as today.
3. Answered (2026-10-25): yes. `Enabled` for each environment (4.2), so that one build goes to all environments and the app is turned on in production after approval.
4. Answered (2026-10-25): yes, the client and the API are always deployed together (4.6).
