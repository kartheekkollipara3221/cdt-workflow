# Module 5: Data mapping

Status: Draft 3, 2026-10-25. Questions Q1-Q6 are answered. The target files arrived (context, T4 templates, Data `.csproj`); new findings DM-08 to DM-10 and the scaffold settings (6.2.1). All questions (Q1-Q7) are answered. Design only. No target code is changed.

## 1. Purpose

After the Section Master design (module 1), a new app still needs C# code for its data: an entity class, a `DbSet`, load code and audit code. This module removes that code. The goal: a new Section Master app needs only a database script and JSON. The existing apps keep their entity classes.

Scope: how a Section Master app stores, loads, converts and audits its record data, and how option lists read data. Out of scope: the dashboard procedures (module 3), access rules (module 6), Step Master and CDT (module 2).

## 2. Current behaviour

### 2.1 Data access

- One EF Core context, `CPSharepointContext`, on SQL Server. A second context, `CPPBContext`, reads the PB data warehouse on Oracle (accounts). Evidence: `Atlasapp/Atlasapp/program.cs.txt:25-26`.
- The context is database-first. The entity classes are generated from the tables (scaffold), with custom templates. Evidence: `Data/Data/Data extended/CPSharepointContext.cs.txt`, `DbContext.t4.txt`, `EntityType.t4.txt`; `Data/Data/Models/*.cs.txt`.
- The context maps 37 tables: `SQMS`, `TTD`, `AIN` and 28 `dbo` tables. The `dbo` tables include 19 workflow engine tables and `MockSectionData`. Evidence: `Data/Data/Data extended/CPSharepointContext.cs.txt:24-92`.
- The context is `partial` and calls `OnModelCreatingPartial`. Evidence: `Data/Data/Data extended/CPSharepointContext.cs.txt:10, 1218-1221`.
- Each app has its own table and its own entity class: `TTD.General` → `TTDGeneral`, `SQMS.Opportunity` → `SQMSOpportunity`, `SQMS.Bid` → `SQMSBid`, `AIN.AccountInventory` → `AccountInventory`.
- Stored procedures run through `ExecuteDataReader` / `ExecuteNonQuery` on the context. Evidence: `Data/Data/DatabaseExtension.cs.txt:19-66`.

### 2.2 Mapping between the form and the table

- Load: `BindData<T>` copies each entity property to the controls whose `Field` has the same name. It uses `ToString()`. Evidence: `Service/CPApplicationService.cs.txt:69-81`.
- Save: `MapData<T>` sets each entity property whose name is in `Outputs`. It converts with `Convert.ChangeType`. An empty string becomes `null`. It returns the list of changed values. Evidence: `CPApplicationService.cs.txt:83-107`.
- The mapping is already by name and by reflection. Thus it is half generic. Only the entity class `T` is per app.

### 2.3 Audit

- One audit table for each app or family: `AIN.AuditLog`, `SQMS.AuditLog`, `TTD.AuditLog`, `dbo.CDT_AuditLog`.
- The code finds the audit entity by name: `Code + "AuditLog"` (or `ParentCode + "AuditLog"`), and the `DbSet` by `Code + "AuditLogs"`. It sets `RefId`, `RefType`, `CreatedBy`, `CreatedDate` and `LogMessage` (the changed values as JSON). Evidence: `CPApplicationService.cs.txt:223-245, 587-626, 630-650`.
- This is already a generic pattern by convention.

### 2.4 Column types

Most business columns are `varchar`, also for numbers and dates. Examples: the TTD minute columns and `WeeklyTotal` are `varchar(100)`. `SQMS.Opportunity` has 25 `varchar` columns and only a small number of `datetime`, `int` and `bit` columns. Evidence: `CP_Sharepoint.txt:133-189, 948-991, 1009-1031`.

## 3. The data cost of app #17

| # | Item | Type | After module 1 | After module 5 |
|---|---|---|---|---|
| 1 | Table, audit table, entitlements | SQL | Yes | Yes, from a template (6.9) |
| 2 | Entity class for the table | C# | Yes | No |
| 3 | Scaffold the shared context again, and add a `DbSet` | C# (shared file) | Yes | No |
| 4 | Audit entity and `DbSet` | C# (shared file) | Yes | No |
| 5 | Load and save code | C# | Hooks only | Hooks only |
| 6 | Option lists from data | C# | Provider by name | Provider by name, or a stored procedure in JSON |

## 4. Findings

### DM-01 Each app needs an entity class in the shared context (High for reuse)

The context is generated from the database. A new table needs a new scaffold of the shared context and new entity classes. This changes a shared file for all apps. A scaffold can also change other generated classes if the database has other changes. Evidence: `Data/Data/CodeTemplates/EFCore`; `Data/Data/Models/`.

### DM-02 The load reads the full table (Medium, performance)

`LoadPageData` calls `ToList()` on the table, then finds the record by ID in memory. Evidence: `CPTTDService.cs.txt:149-151`; `CPSQMSOService.txt:242-244`. The time to open one record increases with the table size. (SM-12 for TTD.)

### DM-03 Value conversion depends on the server culture (Medium)

`MapData` uses `Convert.ChangeType` with no culture. `BindData` uses `ToString()`. Dates and decimal numbers thus depend on the culture of the server. Example: a server with `en-GB` culture reads `03/04/2026` as 3 April. A server with `en-US` culture reads it as 4 March. Evidence: `CPApplicationService.cs.txt:69-107`.

### DM-04 A conversion error stops the save with no message (High)

If a value cannot be converted (for example `abc` for an `int` column), `Convert.ChangeType` throws an exception. `SavePage` catches it, rolls back and returns "saved" (H7, SM-16). The user does not know which field is wrong.

### DM-05 Audit errors are hidden (Medium)

`AuditLog` catches all exceptions and writes no log. If the audit entity is not found by name, `Activator.CreateInstance(null)` throws, and the audit entry is lost with no message. Evidence: `CPApplicationService.cs.txt:223-231, 621-624`.

### DM-06 The audit tables have different shapes (Low)

`AIN.AuditLog` and `SQMS.AuditLog` have `RefType`. `TTD.AuditLog` and `dbo.CDT_AuditLog` do not. The CDT audit table is in `dbo`, the others are in the app schema. Evidence: `CP_Sharepoint.txt:190-207, 244-260, 900-912, 992-1008`.

### DM-07 Numbers and dates are stored as text (Low)

Text columns for numbers and dates make sorting, totals and reports harder, and permit bad values. The platform must support the current text columns. New apps can use typed columns.

### DM-08 The generated context is changed by hand after each scaffold (Medium)

The context file in use is not the template output:

- The constructors take `IConfiguration`, and `OnConfiguring` reads the connection with `TPAMAPIAccess`. The template makes an options-only constructor. Evidence: `Data/Data/Data extended/CPSharepointContext.cs.txt:12-22, 95-96`; `Data/Data/Data extended/DbContext.t4.txt:62-65`.
- `EntityType.t4` names the navigation type with the `dbo` rule for all schemas (line 124). For `SQMS.Bid` it makes `OPPortunity`, but the class has `SQMSOpportunity`. Evidence: `Data/Data/Data extended/EntityType.t4.txt:124`; `Data/Data/Models/SQMSBid.cs.txt:44`.
- `AINAccountInventory` is configured two times, with different lengths (1000 and 100). The second block wins. Evidence: `Data/Data/Data extended/CPSharepointContext.cs.txt:100, 242`.

Each new scaffold overwrites these edits. A person must merge them again by hand. An error can break the build or change a mapping with no warning. The platform design (6.2) does not need a new scaffold for a new app, but the existing apps still do.

### DM-09 The Atlas context maps the workflow engine tables (Medium)

The scaffold of all `dbo` tables adds the 19 workflow engine tables (`Workflow*`, `Notification*`, `SlaPolicy`, `IdempotencyRecord`) and `MockSectionData` to the Atlas context. No Atlas code uses these classes. The engine has its own model for these tables. Each engine schema change makes the Atlas model old until the next scaffold, and the Atlas context could write engine tables outside the engine rules. Evidence: `Data/Data/Data extended/CPSharepointContext.cs.txt:54-92`; a search of the target code finds the classes only in `Data/Data/Models`.

### DM-10 Scaffold names and packages (Low)

- The `dbo` rule makes the first 3 letters capital: `WORkflowTask`, `NOTificationOutbox`, `IDEmpotencyRecord`. A `dbo` table name with fewer than 3 letters stops the scaffold (`Substring(0, 3)`). Evidence: `Data/Data/Data extended/DbContext.t4.txt:74-76`. Do not rename the existing classes (backward compatibility). New apps use their own schema, so they get `<Schema><Table>` names.
- The Data project uses the package `Microsoft.AspNetCore.Http` 2.3.9, an old ASP.NET Core 2 package, on .NET 10. Use `<FrameworkReference Include="Microsoft.AspNetCore.App" />` if the project needs `HttpContext`. Evidence: `Data/Data/Data extended/BBH.CP.Atlas.Data.csproj.txt:10`.

## 5. Storage options

The main decision of this module: how a configured app stores its record.

| Option | Description | Advantages | Disadvantages |
|---|---|---|---|
| **A. Typed table per app, generic mapping (recommended)** | Each app has its own table, made by a SQL script (as today). The platform maps the table with no entity class (7.2). | Same as today for DBAs, reports and procedures. Columns can have types. Indexes and foreign keys work. Existing tables work with no change. | Each new app needs a DDL script and a DBA. |
| B. One generic JSON table | All apps write to one table: `AppCode`, `RecordId`, `PayloadJson`. | No DDL for a new app. | Reports and procedures must parse JSON. No column types, no foreign keys, weak indexes. The dashboard procedures must change. |
| C. Typed core + JSON extension | Option A, plus one optional `ExtraJson` column for fields with no column. | Fields can be added with no DDL. | Two places for data. Reports cannot see the extra fields easily. |

Decision (2026-10-25): option A. Each app has its own table and its own audit table. The dashboards, the SQMS procedures and the workflow engine SQMS module already read typed columns. Option B breaks them. Option C can be added later as an additive change if a need appears.

## 6. Reusable design (option A)

### 6.1 Table registration

The module registration names the table. It does not name a class.

```csharp
// Existing app: keep the entity class (no change).
services.AddAtlasModule<CPTTDService>("TTD", configFile: "TTD.json");

// New app: table only.
services.AddAtlasModule<ConfigSectionService>("PBSP", configFile: "PBSP.json",
    table: "PBSP.General", auditTable: "PBSP.AuditLog");
```

### 6.2 Generic entity with no class

Use the EF Core shared-type entity type. EF Core maps a table to `Dictionary<string, object>`, with no C# class for the table. EF Core 10 supports this.

- At start-up, the platform reads the columns of each registered table from the database (`sys.columns`). The database is the source of truth, as in the current database-first process.
- The platform adds one shared-type entity for each table in `OnModelCreatingPartial`. The scaffolded context already calls this partial method. Thus the generated context file does not change.
- Key: `ID` (identity). Concurrency: `RowVersion` with `.IsRowVersion()` (module 1, 5.13).
- Access: `context.Set<Dictionary<string, object>>("PBSP.General")`. The record uses the same context and the same transaction as the audit entry and the other app data.
- Change tracking, `SaveChanges`, transactions and concurrency work as for a normal entity.
- A new table needs a restart of the API, because EF Core builds the model one time. This is the same as today (a new entity class also needs a deployment).

### 6.2.1 Scaffold settings for the existing apps

The scaffold continues for the existing tables only. These settings stop the hand edits (DM-08):

- Move the `IConfiguration` constructors and `OnConfiguring` to a hand-written partial file, `CPSharepointContext.Platform.cs`. In `OnConfiguring`, call `UseSqlServer` only if `!optionsBuilder.IsConfigured` (`AddDbContext` already configures it, `program.cs.txt:25`).
- Scaffold with `--no-onconfiguring`. Then the generated file has no connection code and no parameterless constructor.
- The same partial file holds `OnModelCreatingPartial` with the shared-type entities (6.2) and the start-up check (6.8).
- Correct `EntityType.t4` line 124: use the same name rule as the class (schema prefix for non-`dbo`). Then `SQMSBid.Opportunity` comes out as `SQMSOpportunity` with no hand edit.
- Remove the first `AINAccountInventory` block. Confirm the correct length (`HasMaxLength(100)`, the block that wins today) against the table.
- Keep the scaffold to the existing schemas (`SQMS`, `TTD`, `AIN`) and to all `dbo` tables except the workflow engine tables and `MockSectionData` (decision 2026-10-25, question 7). Never include a new app schema: a new app table must not get a typed class and a shared-type entity together (section 7).
- The EF Core scaffold has no exclude option. Thus the script reads the `dbo` tables from `sys.tables` and gives one `--table dbo.<name>` for each table that is not in the exclude list: `Workflow*`, `Notification*`, `SlaPolicy`, `IdempotencyRecord`, `MockSectionData`, `__EFMigrationsHistory`. A new `dbo` table is then included with no change to the script. The script also gives `--schema SQMS --schema TTD --schema AIN`.
- Run the scaffold with `--force`, then build. The build must give no hand-merge step. Keep the command in a script in the Data project (for example `scaffold.ps1`), so that each person runs the same command.

### 6.3 Field binding

- The `Field` of each control is a column name. `Name` stays the control name.
- The allow list (module 1, 5.9 step 4) is the set of `Field` values of the visible and enabled controls, plus the grid column fields (`SingleRowFields`).
- System columns are set only by the platform: `ID`, `CreatedBy`, `CreatedDate`, `ModifiedBy`, `ModifiedDate`, `IsDeleted`, `RowVersion`, the `Computed` fields, and the family `LinkField`.
- A control with no `Field` is not saved (for example a display-only field).

### 6.4 Value conversion

Convert by the SQL column type, with `CultureInfo.InvariantCulture`. Use one wire format between the client and the server:

| Column type | Wire format | Example |
|---|---|---|
| `int`, `bigint` | Whole number | `90` |
| `decimal`, `money` | Invariant decimal, no group separator | `1250000.50` |
| `date`, `datetime`, `datetime2` | ISO 8601 | `2026-10-25` or `2026-10-25T14:30:00Z` |
| `bit` | `true` / `false` (also accept `Yes` / `No`) | `true` |
| `varchar`, `nvarchar` | Text. Check the column length. | `Smith Trust` |

- If a value cannot be converted, the save stops with a field error (module 1, 5.11). This removes DM-03 and DM-04.
- Text columns that hold numbers (for example the TTD minutes) use the `Validator` (`Integer`, `Min`, `Max`). The value is saved as text, as today.
- `datetime` values are UTC from `TimeProvider` (module 1, 5.9 step 9).

### 6.5 Load by key

- Load one record with its key, in the database: `Find` or `WHERE ID = @id`. Do not load the table (DM-02).
- If the table has `IsDeleted`, a deleted record is not found (404).
- For a child app, load the parent record by `LinkField` in the same way (module 1, section 11).

### 6.6 Audit

- Each new app has its own audit table (decision 2026-10-25). The existing SQMS family keeps its shared `SQMS.AuditLog` with `RefType`. The audit table has this shape: `ID`, `RefID`, `RefType` (nullable), `CreatedBy`, `CreatedDate`, `LogMessage`. This is the current shape of `AIN.AuditLog` and `SQMS.AuditLog`.
- The registration names the audit table. The platform maps it as a shared-type entity (6.2). It does not find it by class name.
- The save pipeline writes the audit entry in the same transaction (module 1, 5.12). `LogMessage` is the list of changed fields with the old and the new value.
- The History window parses `LogMessage` as JSON and shows `Name - Value` for each entry. Evidence: `WebUi/.../Control/history-report-modal.ts.txt:40-42, 83`. Thus keep the keys `Name` and `Value` (the new value) and add `Old`. The current window shows new entries correctly, and the old entries do not change:

```json
[ { "Name": "AccountName", "Old": "Smith Trust", "Value": "Smith Family Trust" },
  { "Name": "Status",      "Old": "DRAFT",       "Value": "SUBMITTED" } ]
```

- An updated History window can show `Old → Value` when `Old` is present.
- If the audit write fails, the save fails. The platform writes an error log. This removes DM-05.
- The current tables stay. `TTD.AuditLog` and `dbo.CDT_AuditLog` have no `RefType`. The platform writes `RefType` only when the column exists. No DDL change is necessary (DM-06).
- Decision (owner, 2026-11-19): the platform follows the target audit shape of 2026-11-18. The record columns are `ReferenceID` and `ReferenceType` (in place of `RefID` and `RefType`), and the index is `IX_AuditLog_ReferenceID`. `040` and `sql/05-new-app-template.sql` make these names. `045` renames the columns of an older `TTDV2.AuditLog`. `--atlas-check` refuses an audit table without `ReferenceID` or `ReferenceType`. The response field `RefID` and the manifest key `AuditRefTypes` do not change (stable names). See `INTEGRATION.md` B85.

### 6.7 Option lists from the database

Add the option provider `StoredProcedure` (module 1, 10.2). Thus a new app can read option data with no C# code:

```json
"OptionsSource": {
  "Provider": "StoredProcedure",
  "Procedure": "PBSP.Get_Options_Desk",
  "Parameters": { "LanID": "{User.LanID}", "AppCode": "{App.Code}" },
  "LabelColumn": "Label",
  "ValueColumn": "Value",
  "DataColumns": ["DeskCode", "Region"]
}
```

- The procedure must be in the schema of the app, or in a list of permitted schemas. The platform does not run a procedure name from the client.
- Parameter values come only from tokens (`{User.LanID}`, `{App.Code}`, `{Record.<Field>}`) or fixed values in the JSON.
- `DataColumns` fills `CPKeyValue.Data` for `SetFromOption` (module 1, 5.2).

### 6.8 Start-up check

At start-up, for each registered app, the platform checks:

- The table and the audit table exist.
- Each control `Field` is a column of the table.
- The system columns exist (`ID` as identity; the others when the JSON uses them).
- `LinkField` and `CopyFields` of a child app are columns of the child and the parent tables.
- Each `OptionsSource` procedure exists.

If a check fails, the API does not start, and the log names the app, the field and the problem. Thus an error in the JSON stops the deployment, not the user.

### 6.9 New-app SQL template

The script [sql/05-new-app-template.sql](sql/05-new-app-template.sql) makes these items for a new app:

- The schema.
- The main table with the system columns (`ID` identity from 10000, `CreatedBy`, `CreatedDate`, `ModifiedBy`, `ModifiedDate`, `IsDeleted`, `RowVersion`).
- The audit table with the standard shape.
- The `CP_Atlas_Application` row (with `ParentID` for a child app).
- The `CP_Atlas_Entitlement` rows (for example `Read`, `Prepare`, `Owner`).

The DBA adds the business columns to the script. The script is additive and safe to run again.

### 6.10 Custom logic

An app with special data logic still uses hooks (`OnLoad`, `OnBeforeSave`, `OnAfterSave`). A hook gets the record as a dictionary. An app can also keep a typed entity class, as the existing apps do.

## 7. Backward compatibility

- SQMSO, SQMSB, TTD, AIN and CDT keep their entity classes, `DbSet`s and audit entities. Their behaviour does not change.
- The shared-type entities are added in `OnModelCreatingPartial`. The generated context file does not change. A new scaffold does not remove them.
- A table cannot be mapped as a typed entity and as a shared-type entity at the same time. Register each table one way only. The start-up check stops a double registration.
- The wire formats (6.4) apply to apps on `ConfigSectionService`. The legacy services keep their current conversion.
- No change to existing tables, audit tables or data.

## 8. Migration steps

| Step | Change | Risk |
|---|---|---|
| 1 | Add `OnModelCreatingPartial` in a separate partial file. Add the shared-type registration and the start-up check. No app uses them yet. | Low |
| 2 | Add the invariant conversion (6.4) and the load by key (6.5) to `ConfigSectionService`. | Low |
| 3 | Add the generic audit (6.6) to the save pipeline. | Low |
| 4 | Add the `StoredProcedure` option provider (6.7). | Low |
| 5 | TTD pilot, part 2 (optional): run TTD on the shared-type entity for `TTD.General`, with the `TTDGeneral` class not registered for that app. Compare with the legacy service (module 1, 10.3). | Medium |
| 6 | Use the SQL template and `ConfigSectionService` for the first new app. Record the steps and the time. | Low |

## 9. Acceptance

- A new app works with a SQL script from the template, a JSON file and one registration line. No entity class, no `DbSet`, no new scaffold.
- The load of one record runs one query by key (check with SQL Profiler or EF logging).
- A date entered as `2026-03-04` is saved as 4 March 2026 on servers with `en-US` and `en-GB` culture.
- A bad value for a typed column gives a field error. The record does not change.
- A save writes exactly one audit entry in the same transaction. If the audit write fails, the record does not change.
- The API does not start if a JSON `Field` is not a column. The log names the app and the field.
- A save cannot change a system column (`CreatedBy`, `IsDeleted`, `RowVersion`, `LinkField`) by a field in the request.

## 10. Open questions

1. Answered (2026-10-25): option A. Each app has its own table and its own audit table.
2. Answered (2026-10-25): the team scaffolds all tables. The target team will change the scaffold to the existing schemas only (`dbo`, `SQMS`, `TTD`, `AIN`), keep `OnModelCreatingPartial` in the templates, and add one partial file for the platform. New apps always use their own schema.
3. Answered (2026-10-25): new apps use typed columns (`int`, `decimal`, `date`). The existing apps keep their text columns.
4. Answered (2026-10-25): development uses a SQL Server login with full DDL, DML, query and transaction access. QC and production use a login with read, insert, update, delete and execute (no DDL). No `VIEW DEFINITION` grant is necessary: SQL Server shows the metadata (`sys.columns`, `sys.procedures`) of the objects on which the login has a permission. Thus the start-up check works. The DBA must give the API login these permissions on each new app schema (the SQL template has a grant step).
5. Answered (2026-10-25): yes, the field name, the old value and the new value. Only the History window of each record reads the audit; no reports. The JSON keeps the current keys `Name` and `Value` and adds `Old` (6.6), so the current History window continues to work.
6. Answered (2026-10-25): the target team runs the script in development. The DBAs run the DDL scripts in QC and production. One schema for each app.
7. Answered (2026-10-25): all `dbo` tables except the 19 workflow engine tables and `MockSectionData` (DM-09, 6.2.1). Other `dbo` tables are important to Atlas and stay in the scaffold. The engine tables stay only in the engine model.
