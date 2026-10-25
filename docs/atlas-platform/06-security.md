# Module 6: Security and entitlements

Status: Draft 1, 2026-10-25. Design only. No target code is changed.

## 1. Purpose

Each new app must get the same access control with no security code of its own. This module defines one way to identify the user, read the roles, and check access on the server for each app, each record and each operation. The JSON of the app gives the rules. The platform applies them.

Scope: identity, the application gate, roles, access checks on the API, ownership, record visibility. Related items in other modules: the save allow list (module 1, 5.9), `RoleScope` and families (module 1, section 11), `Permissions` and `OwnerFields` (module 1, 10.5), attachments and download (module 8, issues H1 and H2), dashboard output (module 3, issue H5).

## 2. Current behaviour

### 2.1 Identity

- The browser signs in with Windows authentication. The API reads `User.Identity.Name` (`DOMAIN\lanid`).
- `UserInfo.GetCurrentUser()` finds the user in Active Directory on each call. It makes `DisplayName` from `GivenName + " " + Surname`. Evidence: `Atlasapp/Atlasapp/UserAuthentication/UserInfo.cs.txt:17-38`.
- `v1/authorize` also finds the user in Active Directory, but it uses `principal.DisplayName`. Evidence: `Controllers/V1/CPSecurityController.cs.txt:78`.
- The default authentication scheme is JWT bearer with an empty key (issue M17). Evidence: `program.cs.txt:56-67`.

### 2.2 Application gate

- `v1/authorize` gives a user object only if the user is in the AD group `AppSettings:ADGroup` (`IM_TACO`). Otherwise it returns `null`, and the client opens `unauthorized-access`. Evidence: `CPSecurityController.cs.txt:21-92`; `app.component.ts.txt:65-95`; `appsettings.json.txt:22`.
- The client also opens `unauthorized-access` if the user has no app in `v1/Application` (procedure `dbo.Get_Application`). Evidence: `CP_Sharepoint.txt:1502-1534`.

### 2.3 Roles

- Roles are rows in `CP_Atlas_UserEntitlement` → `CP_Atlas_Entitlement` (with `CP_Atlas_ApplicationID`) → `CP_Atlas_Application`.
- `v1/authorize` returns the role names of all apps in one flat list. The client uses this list, for example `mfo` / `ipfg` for the redirect after save. Evidence: `CPSecurityController.cs.txt:45-49`; `cp-atlas-sectionmaster.ts.txt:347-367`.
- Each module service reads the roles again with its own query (module 1, SM-08).
- No role query filters `IsDeleted`.

### 2.4 Access checks on the API

- The controllers have `[Authorize]` only. Any signed-in domain user can call each endpoint. Evidence: `CPAtlasController.cs.txt:28-135`; `CPAtlasExternalController.cs.txt:21`.
- The rules (`CPRule`) only set `Visible` and `Disabled` in the page response. The server does not check them again on save, delete, execute, download or history (issue H4).

## 3. Findings

### SE-01 The API does not check app or record access (High, security)

Any signed-in domain user can call these endpoints for any app and any record ID:

| Endpoint | Effect |
|---|---|
| `GET v1/SectionPage`, `GET v1/StepPage` | Read any record of any app |
| `POST v1/ManagePage` | Create or change a record (H4) |
| `POST v1/RemovePage` | Delete a record (H4) |
| `GET v1/Execute` | Run an app action (for example `MakeBid`) |
| `GET v1/GetAuditLog` | Read the change history of any record |
| `GET v1/Download`, `POST v1/RemoveAttachment` | Read or remove files (H1) |

Example: a user with no TTD role sends `GET v1/SectionPage?Code=TTD&ID=10001`. The API returns the time sheet. Evidence: `CPAtlasController.cs.txt:28-135`.

### SE-02 The AD group gate is only in the client path (High, security)

Only `v1/authorize` checks the AD group. The other endpoints do not check it. A domain user who is not in `IM_TACO` cannot open the UI, but can call the API directly. Also, the group check uses `Contains`: the group `IM_TACO` also agrees with `IM_TACO_OLD` or `XIM_TACOY`. Evidence: `CPSecurityController.cs.txt:56-66`.

### SE-03 The roles are a flat list with no app (High, security)

`v1/authorize` returns the role names of all apps together. A role name in one app (for example `Owner` in TTD) and the same name in another app look the same to the client. Deleted rows also give roles: no query filters `IsDeleted` (`v1/authorize`, `dbo.Get_Application`, the module services). Evidence: `CPSecurityController.cs.txt:45-49`; `CP_Sharepoint.txt:1516-1525`.

### SE-04 Users are matched by display name (High)

- The records keep people as display names: `CreatedBy`, `ModifiedBy`, `TaxPreparerName`, `SellerRMTeam`, `BuyerRMTeam`.
- The code finds users by display name in `CP_Atlas_User` (recipients) and compares display names for ownership. Evidence: `CPSQMSOService.txt:93-95, 335-337`.
- Two sources make the display name differently: `UserInfo` uses `GivenName + " " + Surname`; `v1/authorize` uses the AD `DisplayName`. `CP_Atlas_User.DisplayName` is a third value.
- SQMSO ownership uses `SellerRMTeam.Contains(DisplayName)`, a text search. "Ann Lee" also agrees with "Joann Leeds".

Results: two users with the same name share ownership. A name change in AD removes ownership and notifications. A text search gives false owners.

### SE-05 Each request reads Active Directory and the roles many times (Medium, performance)

`UserInfo.GetCurrentUser()` queries Active Directory on each call. A request can call it more than one time, and each module service reads the roles again. If Active Directory is slow or not available, all requests fail. Evidence: `UserInfo.cs.txt:21-24`; `CPAtlasController.cs.txt` (one call for each endpoint); module 1 SM-08.

### SE-06 The authentication setup does not agree with the identity code (Medium)

The default scheme is JWT bearer with an empty key (M17), but the code reads a Windows identity (`User.Identity as WindowsIdentity`). Confirmed (2026-10-25): the live API is on IIS with Windows authentication, and no caller uses JWT. The JWT setup is not used. Evidence: `program.cs.txt:56-67`; `CPSecurityController.cs.txt:52`.

### SE-07 The record visibility rules are in code for each app (Medium for reuse)

Some apps limit the records or options for a user. Examples: SQMSB filters accounts by the department of the user (H13); the dashboard procedures get `@LanID`. Each app writes its own rule in C# or SQL. The PB account list is the same for all users and is in a cache for 24 hours. Evidence: `CPSQMSBService.cs.txt` (Departments); `CPAtlasExternalService.cs.txt:91-115`; `CPApplicationService.cs.txt:536-560`.

## 4. Reusable design

### 4.1 One user context for each request

Add a scoped service `IAtlasUserContext`. It is made one time for each request:

- `LanId`: from `User.Identity.Name` (the part after `\`). This is the key of the user in all checks.
- `DisplayName`, `Email`, `Department`: from Active Directory, in a memory cache for each `LanId` (for example 30 minutes). The display name is only for display.
- `Groups`: from the Windows token (`WindowsPrincipal.IsInRole`), not from a text search.
- `RolesFor(appCode)`: the active roles of the user for the app and its `RoleScope` (module 1, 11.2). One query for each request, then kept for the request.

This removes SE-05 and the different display names in SE-04.

### 4.2 One gate for all endpoints

Add a fallback authorization policy `AtlasUser`. It applies to each endpoint, also `v1/authorize`:

- The user is authenticated with Windows authentication.
- The user is in one of the AD groups of `AppSettings:ADGroup`. The check is an exact group match (`IsInRole("DOMAIN\\IM_TACO")`). Decision (2026-10-25): the setting has two groups separated by `|`, one for end users and one for support. There are no AD groups for each app. AD membership gives access to the platform only. What a user can see and do in each app comes only from the entitlement tables.
- The user is an active row in `CP_Atlas_User` (`IsDeleted` = 0).

If the gate fails, the API returns 401 or 403. `v1/authorize` keeps its current response (`null`) for the client, so the client continues to open `unauthorized-access`. This removes SE-02.

### 4.3 One access check for each operation

Add `IAtlasAccessService.Check(appCode, recordId, operation)`. The platform calls it in one place before each operation. A module does not call it itself.

| Operation | Endpoints | Rule source |
|---|---|---|
| `View` | `SectionPage`, `StepPage`, `GetAuditLog` | The user has a role in the app (`RoleScope`) and `Permissions.View` (default: any app role) |
| `Create` | `ManagePage` with `ID` = 0 | `Permissions.Create` |
| `Edit` | `ManagePage` with `ID` > 0 | `Permissions.Edit`, `Rules`, `OwnerFields` (module 1, 10.5) |
| `Delete` | `RemovePage` | `Permissions.Delete` |
| `Execute:<action>` | `Execute` | `Permissions.Execute.<action>` |
| `Download` / `RemoveAttachment` | attachments | `View` / `Edit` of the parent record (module 8) |

- The check also confirms that the record belongs to the app, exists and is not deleted.
- A family form uses the cross-form visibility rules (module 1, 11.2).
- A denied check returns 403 with a short message. The server writes a log line with the `LanId`, the app, the record and the operation.

This removes SE-01 and issue H4.

The full `Permissions` block:

```json
"Permissions": {
  "View":    ["Read", "Prepare", "Owner"],
  "Create":  ["Prepare", "Owner"],
  "Edit":    { "Own": ["Prepare"], "All": ["Owner"] },
  "Delete":  ["Owner"],
  "Execute": { "MakeBid": ["MFO"] },
  "History": ["Prepare", "Owner"]
}
```

Each item is optional. If an item is not there, the current behaviour stays (see section 5).

### 4.4 Roles by app

- One database view gives the active roles: [sql/06-active-user-roles-view.sql](sql/06-active-user-roles-view.sql) (`dbo.vw_CP_Atlas_ActiveUserRole`). It filters `IsDeleted` on the user, the user entitlement, the entitlement and the application. All role queries use this view. This removes the `IsDeleted` part of SE-03.
- `v1/authorize` adds an optional field `AppRoles`: `{ "SQMS": ["MFO"], "TTD": ["Prepare"] }`. Keep the flat `Roles` list for the current client. The updated client uses `AppRoles`. This removes the rest of SE-03.
- The server decisions (redirect, buttons, rules) use the roles of the app, not the flat list.

### 4.5 Users by LAN ID

- New apps keep people as LAN IDs, not display names. A person field (for example `TaxPreparerName`) has `Value` = LAN ID and `Label` = display name. The `UsersInRoles` option provider gives these values (module 1, 10.2).
- The system columns of new apps are `CreatedBy` and `ModifiedBy` with the LAN ID. The display name comes from `CP_Atlas_User` when the page loads.
- `OwnerFields` compare LAN IDs, with an exact match.
- Recipients for the workflow engine are found by LAN ID.
- For the existing apps, add nullable LAN ID columns and fill them from `CP_Atlas_User` where the display name is unique (decision 2026-10-25). Keep the display name columns. The rule uses the LAN ID column when it has a value, and the display name when it does not. This removes SE-04 step by step.
- DDL: [sql/06-lanid-columns-ddl.sql](sql/06-lanid-columns-ddl.sql). Columns: `CreatedByLanId` and `ModifiedByLanId` on `SQMS.Opportunity`, `SQMS.Bid`, `TTD.General` and `AIN.AccountInventory`; `SellerRMTeamLanIds` (Opportunity), `BuyerRMTeamLanIds` (Bid), `TaxPreparerLanId` (TTD).
- DML: [sql/06-lanid-backfill-dml.sql](sql/06-lanid-backfill-dml.sql). It fills only unique names, keeps `system` (the engine SQMS job) as `system`, fills a multi-person field only if all names resolve, and lists the names that did not resolve.
- After the change, the save writes both columns: the display name (as today) and the LAN ID.

### 4.6 Record visibility from configuration

Decision (2026-10-25): some existing and new apps hide records by user data (for example department, branch or relationship manager). Each such app has one SQL rule that the target team owns.

```json
"Visibility": { "Procedure": "SQMS.Can_Access_Record" }
```

Template: [sql/06-record-visibility-template.sql](sql/06-record-visibility-template.sql). One rule has two users:

- `<Schema>.fn_VisibleRecords(@LanID)`: an inline table function. It returns the IDs that the user can see. The rule is written here only.
- `<Schema>.Can_Access_Record(@LanID, @ID, @Operation, @Allowed OUTPUT)`: uses the function for one record. The platform calls it in each access check (4.3), after the role check.
- The dashboard procedures of the app join the same function. Thus the list and the record check always agree (module 3).
- A new record (`ID` = 0) has no row yet. The roles decide (`Permissions.Create`).
- An app with no `Visibility` block has no record rule. Then the roles decide.
- The SQMS Bid department filter (H13) moves into this rule for SQMS. This also removes the dictionary lookup error.

This moves the record rules from C# to SQL that the target team controls, and removes SE-07.

### 4.7 Authentication setup

Decision (2026-10-25): the live API is on IIS with Windows authentication, and no caller uses JWT.

- Remove the JWT bearer setup (`AddJwtBearer`, the `Jwt` settings and the Swagger bearer definition).
- Make Windows authentication the default scheme: `AddAuthentication(IISDefaults.AuthenticationScheme)` (IIS in-process hosting). Keep Windows authentication on and anonymous authentication off in IIS for the API.
- In development (Kestrel), use `AddNegotiate()`.

This removes SE-06 and issue M17.

## 5. Backward compatibility

- Do not change the endpoints, routes, request and response shapes. `AppRoles` is a new optional field. The flat `Roles` list stays.
- The access checks start in **log-only mode** (decision 2026-10-25): the server writes each denied check to the log, but it lets the request continue. After one release, review the log, correct the entitlements, then change each app to enforce mode, one app at a time. This finds users who need roles before they lose access.
- The mode is a setting for each app: `AppSettings:Modules:<Code>:AccessMode = LogOnly | Enforce`. The default is `LogOnly`. A change back to `LogOnly` is the rollback, with no deployment.
- A denied request in enforce mode returns 403. This is a new status for unauthorized calls only. Permitted calls do not change.
- The LAN ID columns are additive and nullable. The display name columns stay.
- An app with no `Permissions` block keeps the current behaviour: any app role can view, create, edit and delete. The SQMS and TTD JSON get their blocks in their migration steps.

## 6. Migration steps

| Step | Change | Risk |
|---|---|---|
| 1 | Run the view script (4.4). Change all role queries to the view. | Low. Users with deleted rows lose those roles. Run part 4 of the role scope script first (module 1). |
| 2 | Add `IAtlasUserContext` with the cache (4.1). | Low |
| 3 | Add the gate policy (4.2) in log-only mode, then enforce. | Medium |
| 4 | Add `IAtlasAccessService` (4.3) in log-only mode for all apps. Review the log. | Low |
| 5 | Add `Permissions` to TTD (module 1, 10.5) and SQMS. Change these apps to enforce mode. | Medium |
| 6 | Add `AppRoles` to `v1/authorize`. Change the client to use it. | Low |
| 7 | Run the LAN ID DDL and DML scripts (4.5). Review part 3 of the DML (names that did not resolve) and correct `CP_Atlas_User`. Change ownership and recipients to LAN ID. | Medium |
| 8 | Remove the JWT setup and make Windows authentication the default scheme (4.7). Test in QC that each endpoint still gets the user. | Low |

## 7. Acceptance

- A user with no role in TTD gets 403 for `SectionPage`, `ManagePage`, `RemovePage` and `GetAuditLog` of a TTD record.
- A domain user who is not in the AD group gets 403 for each endpoint, also with a direct API call.
- A user in group `IM_TACO_OLD` is not accepted for `IM_TACO`.
- A deleted entitlement gives no role.
- Each cell of the TTD access table (module 1, 10.5) gives the correct result on the server, also with a changed request.
- Two users with the same display name do not share ownership in a new app.
- One request queries Active Directory at most one time, and reads the roles one time for each app.
- In log-only mode, each denied check writes one log line and the request continues.

## 8. Open questions

1. Answered (2026-10-25): two AD groups, separated by `|` in `AppSettings:ADGroup`: one for end users, one for support. No AD group for each app. AD membership gives access to the platform. The entitlement tables decide what the user can see and do in each app. Support users also need entitlement rows to work in an app.
2. Answered (2026-10-25): yes, IIS with Windows authentication. No caller uses JWT. Remove the JWT setup (4.7).
3. Answered (2026-10-25): yes. New apps keep people as LAN IDs. The existing tables get LAN ID columns (scripts in 4.5).
4. Answered (2026-10-25): yes, some existing and new apps limit records by user data. One SQL rule for each such app (a function and a procedure, 4.6), owned by the target team, is acceptable.
5. Answered (2026-10-25): yes. Log-only mode for one release, then enforce, one app at a time.
6. Who manages the entitlements today (a screen, or SQL by the DBA)? What does `IsAdmin` on `CP_Atlas_Entitlement` mean?
