# Module 3: Dashboard

Status: Draft 2, 2026-10-25. All open questions answered (section 9). Design only. No target code is changed.

## 1. Purpose

Each app opens on a dashboard: tabs, one grid for each tab, buttons above the tabs and a menu on each row. A new app must get its dashboard from JSON and SQL only, with no change to the shared client code or the server code. This module defines that design and corrects the current defects.

Scope: the dashboard JSON, the tab data load, the tab, button and menu rules, the grid columns and the navigation from the dashboard. Related items in other modules: the access check and `Permissions` (module 6, 4.3), the active roles view (module 6, 4.4), the redirect after save (module 1, 5.7), the form type for navigation (module 1, 5.8), the app registry (module 7).

## 2. Current behaviour

### 2.1 Configuration

- One JSON file for all apps: `CPAtlasDashboardConfig.json`, path in `AppSettings:DashboardConfigPath`. Evidence: `Atlasapp/Atlasapp/appsettings.json.txt:32`.
- Each dashboard has `Code`, `Name`, `Tabs` and `Buttons`. Each tab has `ID`, `Code` (the app of the rows), `Label`, `Description`, `Procedure`, `Parameters`, `Groups`, `Sums`, `ColumnTypes`, `Menus` and `Rules`. Evidence: `Data/Data/DTO/CPAtlasDashboardConfig.cs.txt:12-50`.
- The repository has three copies of the file. Two are the same (367 lines). The copy in `Atlasapp/Atlasapp/Config` (399 lines) is newer and has AIN. Evidence: `Config/Config/`, `Atlasapp/Atlasapp/Config/`, `Atlasapp/Atlasapp/Config/Config/`.
- Each app service reads and parses the file in its constructor, on each request, and selects its dashboard with `Single`. Evidence: `Service/Application/CPTTDService.cs.txt:37-38`, `CPSQMSOService.txt:62-66`.

### 2.2 Load

- `GET v1/Dashboard?code=<Code>` calls `LoadDashboard` of the app service. Evidence: `Controllers/V1/CPAtlasController.cs.txt:44-47`.
- `LoadDashboard` runs the procedure of **each** tab, puts all rows in `KeyValues`, and computes the column widths (`KeyLength`). Then it applies the menu, button and tab rules. Evidence: `Service/CPApplicationService.cs.txt:529-585`.
- A parameter with the value `LANID` gets the LAN ID of the user. Only the types `Varchar` and `Int` are supported. Evidence: `CPApplicationService.cs.txt:549-574`.
- The roles come from a query that does not filter `IsDeleted` (module 6, SE-03). Evidence: `CPApplicationService.cs.txt:537-542`.

### 2.3 Rules

- A tab rule and a button rule match by role, state and owner. The first rule that matches sets `Visible` (tab) or `Visible` and `Disabled` (button). Evidence: `CPAtlasDashboardConfig.cs.txt:86-184`.
- A menu has one rule. It matches by role only. Evidence: `CPAtlasDashboardConfig.cs.txt:60-84`.
- The rules change only flags in the response. The client hides or disables the items.

### 2.4 Client

- The client shows the visible tabs, makes one AG Grid for the active tab, and makes the columns from the keys of the first rows. Evidence: `WebUi/.../cp-atlas-dashboard.ts.txt:130-282`.
- The buttons and the row edit open a form by an `if` chain on the app code. Evidence: `cp-atlas-dashboard.ts.txt:109-125, 316-330`.
- Special cells are fixed by column name: `UPDATEDDATE` (date format) and `AccountInformation` (link). Evidence: `cp-atlas-dashboard.ts.txt:220-228`.

## 3. Findings

### DB-01 The data of hidden tabs goes to the browser (High, security)

`LoadDashboard` runs all tab procedures before the tab rules. A hidden tab gets `Visible = false`, but its rows stay in `KeyValues` in the response. Example: an MFO user opens the SQMS dashboard. The tabs `IPG - Opportunities` and `IPG - Bids` are hidden, but the response contains all opportunities and all bids of all users (`SQMS.Get_SQMSO_IPGOpportunity` and `SQMS.Get_SQMSB_IPGBid` have no user parameter). The browser developer tools show the data. Evidence: `CPApplicationService.cs.txt:544-583`; `CP_Sharepoint.txt:2093, 2220-2222`; dashboard JSON, SQMS tabs `IPGOpportunities`, `IPGBids`.

### DB-02 The dashboard checks no app access (High, security)

Any signed-in user can call `v1/Dashboard` for any app code and get the rows of all tabs that have no rule. Example: a user with no CDT role gets all pending and completed trades. `dbo.Get_CDT_Dashboard` has no user filter and does not use its `@AppCode` parameter. This is the dashboard part of module 6, SE-01. Evidence: `CPAtlasController.cs.txt:44-47`; `CP_Sharepoint.txt:1574-1596`.

### DB-03 Each dashboard load runs all tab procedures (Medium, performance)

The SQMS dashboard runs 6 procedures and returns all rows of each, also when the user looks at one tab. The tab change in the client does not call the server. The load time grows with each tab and each row. Evidence: `CPApplicationService.cs.txt:544-581`; `cp-atlas-dashboard.ts.txt:125-128`.

### DB-04 A new app needs client code (Medium for reuse)

- `onClick` opens a form by the button name (`CDT`, `Opportunity`, `Bid`, `TTD`, `AIN`). `editRow` opens a form by the app code. A new app needs a new branch in both. Evidence: `cp-atlas-dashboard.ts.txt:109-125, 316-330`.
- A new app also needs a code in `CPApplicationCode` and a section in the shared JSON file (module 1, section 3).

### DB-05 Menu rule defects (Medium)

- `ApplyMenuRules` does not reset `roleCondition` for each menu. If one menu matches, the next menus also match. Today the menus of each tab have the same roles, so the result is correct by chance. Evidence: `CPAtlasDashboardConfig.cs.txt:63-80`.
- A menu with no `Rule` stops the load with a null reference (`source.Rule!.Visible`). Evidence: `CPAtlasDashboardConfig.cs.txt:77-80`.
- A menu rule hides a menu only in the client. The server does not check `Delete` or `History` (module 6, SE-01).

### DB-06 The configuration is read on each request and changed in memory (Medium)

- Each request reads and parses the full JSON file (module 1, SM-08).
- `LoadDashboard` writes the LAN ID into `param.Value` of the configuration object. The object is new for each request, so no user sees the value of another user today. If the configuration is cached later (6.1), this becomes a defect: the second user gets the LAN ID of the first user. Evidence: `CPApplicationService.cs.txt:555-556`.
- `Single` stops the request with an error if the app has no dashboard section.

### DB-07 Parameter types (Low)

Only `Varchar` and `Int` are supported. A parameter with another type (for example `Date`) is not added, and the procedure fails with "expects parameter". A parameter with no `Type` stops the load with a null reference. Evidence: `CPApplicationService.cs.txt:558-573`.

### DB-08 Client cell defects (Low)

- The `AccountInformation` link and the `UPDATEDDATE` format never apply: the procedures return `Account Information` (with a space) and no `UPDATEDDATE`. If the alias changes, the link builds HTML from the value with no encoding (issue H5). The group row renderer also builds HTML from the value. Evidence: `cp-atlas-dashboard.ts.txt:158-160, 178-180, 220-228`; `CP_Sharepoint.txt:1583`.
- `dateFormatter` reads `params.value`, but the caller gives the value itself. The format gives no date. Evidence: `cp-atlas-dashboard.ts.txt:221-223, 284-286`.
- A `debugger;` statement is in `GenerateColumns`. Evidence: `cp-atlas-dashboard.ts.txt:280`.
- The columns come from the keys of the rows. A tab with no rows shows no columns and no message.

## 4. Reusable design

### 4.1 Configuration

- The platform reads each dashboard one time at start-up and keeps it in memory (read-only). The request uses a copy of the tab parameters. This removes DB-06.
- One file for each app (decision 2026-10-25, question 4), named in the module registration: `dashboardFile: "PBSP.Dashboard.json"`. A new app always has its own file, so a new app never changes a shared file. If the registration names no file, the platform reads the shared file as today. The existing apps can stay in the shared file, or move one at a time.
- An app code must be in one place only. The start-up check stops the API if a code is in its own file and also in the shared file.
- The start-up check (module 5, 6.8) also checks each dashboard: each tab has an `ID`, a `Procedure` that exists, and parameters with a supported type. A defect stops the start-up and the log names the app and the tab.
- If an app has no dashboard, `v1/Dashboard` returns 404, not an error 500.

### 4.2 Load of tab data

- The server applies the tab rules **before** it runs a procedure. It runs no procedure for a hidden tab, and it sends no rows for a hidden tab. This removes DB-01.
- New optional setting for each dashboard: `"Load": "Eager" | "Lazy"`.
- `Eager` (default): the current behaviour, but only for the visible tabs.
- `Lazy`: `v1/Dashboard` returns the tabs with no rows. The client gets the rows of a tab when the user opens it, with a new endpoint `GET v1/DashboardTab?code=<Code>&tabId=<ID>`. This removes DB-03.
- `v1/DashboardTab` applies the same rules. A request for a hidden tab returns 403.

### 4.2.1 Server paging

Decision 2026-10-25 (question 1): today the largest tabs have a few thousand rows; later they will have tens of thousands. Thus the design supports paging on the server for each tab.

- New optional tab setting: `"Paging": "Client" | "Server"`. Default `Client`: the current behaviour (all rows to the grid). A tab with `Server` paging requires `Load: Lazy`.
- With `Server`, the client sends `page`, `size`, `sort`, `dir` and `search` to `v1/DashboardTab`. The server gives the procedure the parameters `@PageNumber`, `@PageSize`, `@SortColumn`, `@SortDir`, `@Search`.
- The procedure returns two result sets: the rows of the page, then one row with `TotalRows` and the `Sums` totals of all rows (not only the page).
- The server accepts a `sort` value only if it is a name in the tab `Columns` list. The procedure uses the value only in a `CASE` list, never in dynamic SQL. Maximum `size`: 500.
- `Groups` are not available with `Server` paging (the group rows need all rows). The start-up check rejects a tab with both.
- The grid uses the AG Grid server-side row model (the client already uses `ag-grid-enterprise`).
- With `Source: Table` (4.7) the server makes the paging part, so a tab can change to `Server` with no SQL change. The procedure template for special tabs also has the paging parameters.
- Change an existing tab to `Server` only when its rows become many. Start with the tab that grows first.
- Before the load, the access check of module 6 runs with the operation `View` for the dashboard app. In log-only mode it writes a log line. In enforce mode it returns 403. This removes DB-02.

### 4.3 Parameters

- Value tokens: `{LanId}`, `{AppCode}`, `{TabCode}`. The current value `LANID` stays as an alias of `{LanId}`.
- Types: `Varchar`, `NVarchar`, `Int`, `Bit`, `Date`, `DateTime`, `Decimal`. The start-up check rejects other types. This removes DB-07.
- The client cannot send parameter values. All values come from the JSON or from the tokens.

### 4.4 Rules

- One rule engine for tabs, buttons and menus (the same match for role, state and owner). The menu rule gets the same logic as the tab rule, with a reset for each item. This removes DB-05.
- Rules can use the role flags of module 6: `"Roles": ["@Admin"]`.
- If an app has a `Permissions` block (module 6, 4.3) and a tab has no `Menus`, the platform makes the menus from it: `Edit` from `Permissions.Edit`, `History` from `Permissions.History`, `Delete` from `Permissions.Delete`. The same for a button with no `Rules`: `Disabled` if the user has no `Permissions.Create`. The JSON of a new app then has no menu and button rules.
- The rules continue to set only `Visible` and `Disabled` for the display. The server check of module 6 decides the access for each operation.

### 4.5 Navigation

- A button gets an optional `Target`: `{ "AppCode": "PBSP", "Id": 0 }`. A tab row opens the form of the tab `Code`.
- The client gets the form type (`SECTION` or `STEPPER`) for each app code from the server (module 1, 5.8; module 7). It opens `sectionmaster` or `stepmaster` with no `if` chain.
- If `Target` or the form type is not there, the client uses the current `if` chain. This removes DB-04.

### 4.6 Columns

- New optional `Columns` list for each tab: `Name`, `Label`, `Type` (`string`, `number`, `date`, `currency`), `Format`, `Hidden`, `Width`, `Link` (`Edit` to open the form of the row). If there is no `Columns` list, the client makes the columns from the rows, as today. `ColumnTypes`, `Groups` and `Sums` stay.
- With a `Columns` list, a tab with no rows still shows its columns and the message "No records".
- All cell and group renderers write text, not HTML (H5). A link is made with DOM methods.
- Remove the `debugger;` statement. Correct `dateFormatter` (DB-08).
- Grid functions (decision 2026-10-25, question 3): Excel export and the date filter come from AG Grid Enterprise. The platform adds no code for them. A column with `Type: date` gets the AG Grid date filter (with "in range"). Excel export uses the grid menu. Limit: with `Paging: Server` (4.2.1), the export contains only the loaded rows. Saved views for each user are not in scope.

### 4.7 Tab data from the table (no procedure)

Decision 2026-10-25 (question 2): a simple tab of a new app needs no procedure. The platform makes the list query from the JSON. A procedure is necessary only for a special tab (for example a join of two tables, a calculation, or the SQMS "Pending Seller Approval" list).

- New tab property `"Source": "Table"`. The tab has no `Procedure`. The table is the registered table of the tab `Code` (module 5, 6.1).
- The query selects only the columns in the tab `Columns` list. The start-up check confirms each name against `sys.columns`, as for the form fields (module 5, 6.8). `Label` is the grid header.
- Optional `Filter` list: `{ "Column": "Status", "Op": "In", "Value": ["Draft", "Submitted"] }`. Operators: `Eq`, `NotEq`, `In`, `IsNull`, `NotNull`, `Gt`, `Lt`. A value can be a token (4.3), for example `{LanId}` for "my records" on a `CreatedBy` LAN ID column.
- The platform always adds `IsDeleted = 0` and, if the app has a `Visibility` block, a join to `<Schema>.fn_VisibleRecords(@LanId)` (module 6, 4.6). The JSON cannot remove these conditions.
- Default order: `OrderBy` of the tab, else `ID DESC`.
- Paging, sort and search (4.2.1) work the same as for a procedure tab. The server makes the paging part.
- The server makes the SQL with parameters only. Table and column names come only from the registration and the checked `Columns` list, in `QUOTENAME` form. No value from the request goes into the SQL text.
- A tab with `Procedure` works as today. A tab must have `Procedure` or `Source: Table`, not both (start-up check).

The new-app SQL template (module 5, 6.9) needs no dashboard procedure. The team adds a procedure only for a special tab, from a short template with the visibility join and the paging parameters (4.2.1).

## 5. Example: dashboard JSON for a new app

```json
{
  "Code": "PBSP",
  "Name": "PB Service Plan",
  "Load": "Lazy",
  "Tabs": [
    {
      "ID": "MyItems",
      "Code": "PBSP",
      "Label": "My Items",
      "Source": "Table",
      "Filter": [ { "Column": "CreatedByLanId", "Op": "Eq", "Value": "{LanId}" } ],
      "Columns": [
        { "Name": "ID", "Hidden": true },
        { "Name": "ReferenceNo", "Label": "Reference", "Link": "Edit" },
        { "Name": "Status" },
        { "Name": "ModifiedDate", "Label": "Modified Date", "Type": "date", "Format": "MM/dd/yyyy" }
      ]
    }
  ],
  "Buttons": [ { "Name": "Add", "Label": "Add New", "Target": { "AppCode": "PBSP", "Id": 0 } } ]
}
```

The menus and the button rule come from the `Permissions` block of the app (4.4).

## 6. Backward compatibility

- `v1/Dashboard` keeps its route, request and response shape. A hidden tab now has no rows (`KeyValues` is empty). The client does not show hidden tabs, so the display does not change.
- `Load` is optional. The default `Eager` keeps one call for all visible tabs.
- `v1/DashboardTab`, `Target`, `Columns`, the tokens and the new types are additive. The existing JSON stays valid.
- The value `LANID` continues to work.
- The access check starts in log-only mode (module 6, section 5).
- The client keeps the `if` chain as the fallback.
- The fixes of DB-05 do not change the result for the current JSON (the menus of each tab have the same roles).

## 7. Migration steps

| Step | Change | Risk |
|---|---|---|
| 1 | Apply the tab rules before the load; no rows for hidden tabs (4.2). | Low |
| 2 | Read the dashboards one time at start-up; copy the parameters for each request; start-up check (4.1). | Low |
| 3 | Use the active roles view (module 6, 4.4). Correct the menu rules (4.4). | Low |
| 4 | Add the access check for `View` in log-only mode (4.2). | Low |
| 5 | Add the tokens and the types (4.3). | Low |
| 6 | Add `Target` and the form type to the client (4.5), with the `if` chain as fallback. | Low |
| 7 | Add `Columns` and the text renderers; correct DB-08 (4.6). | Low |
| 8 | Add `Load: Lazy` and `v1/DashboardTab` (4.2). Use it for SQMS first and compare the load time. | Medium |
| 9 | Add `Source: Table` with `Filter` (4.7). Add a short procedure template for special tabs. | Medium |
| 10 | Add `Paging: Server` (4.2.1). Use it first for the tab that grows first. | Medium |

## 8. Acceptance

- An MFO user opens the SQMS dashboard. The response has no rows for the IPG tabs.
- A user with no CDT role calls `v1/Dashboard?code=CDT` in enforce mode and gets 403. In log-only mode the server writes one log line.
- A new app shows its dashboard with a JSON file only (`Source: Table`), with no procedure and no client or server code change.
- A `Source: Table` tab returns only rows with `IsDeleted = 0` that `fn_VisibleRecords` permits, also if the `Filter` list is empty.
- A `Columns` name that is not a column of the table stops the start-up, and the log names the app and the tab.
- A button with `Target` opens the correct form for an app that is not in the `if` chain.
- With `Load: Lazy`, the dashboard load runs no procedure, and the first tab load runs one procedure.
- A value `<img src=x onerror=alert(1)>` in a cell shows as text.
- A tab with a `Columns` list and no rows shows its columns and "No records".
- A tab with `Paging: Server` and 50,000 rows returns one page of at most `size` rows, the correct `TotalRows`, and the sums of all rows. A `sort` value that is not in `Columns` returns 400.
- The CDT, SQMS, TTD and AIN dashboards show the same tabs, rows, buttons and menus as before for each role.

## 9. Open questions

1. Answered (2026-10-25): today a few thousand rows; later tens of thousands. Paging on the server is necessary: optional for each tab (4.2.1).
2. Answered (2026-10-25): the platform makes a simple list query from the JSON (`Source: Table`, 4.7). A procedure only for special tabs.
3. Answered (2026-10-25): Excel export and the date-range filter come from AG Grid Enterprise, with no platform work (4.6). Saved views for each user: not in scope, to limit the scope.
4. Answered (2026-10-25): one file for each app (4.1). The existing apps can stay in the shared file.
5. Answered (2026-10-25): the copy with AIN (`Atlasapp/Atlasapp/Config`, 399 lines) is deployed. This spec uses that copy. The two other copies (367 lines, no AIN) are old; the target team can remove them, so that nobody changes the wrong file.
