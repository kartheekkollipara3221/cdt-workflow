# Module 25: ClientInflows (P2) implementation design

Status: Draft 1, 2026-11-20. Design only. No code and no SQL script exists yet. The user must approve this design
before the build starts (section 14).

## 1. Purpose

ClientInflows (app code `CFPINFLOW`) is release P2: the second platform app, and the first one with attachments, rich
text and a workflow. This document tells how to build it. Most of the work is generic platform code that the next apps
use again. The app itself is JSON and SQL.

Inputs:

- Requirements: [ONBOARDING_ANSWERS.md](../ClientInflows/ONBOARDING_ANSWERS.md), decisions D1 to D34.
- Gaps and answers: [GAP_REPORT.md](../ClientInflows/GAP_REPORT.md), items A1 to A17, B1 to B12, C1 to C8, answers
  Q1 to Q9.
- Module designs: [08-attachments.md](08-attachments.md) (section 10), [10-engine-adapter.md](10-engine-adapter.md),
  [07-registry.md](07-registry.md) (section 5), [12-ttd-pilot-build.md](12-ttd-pilot-build.md) (the model).

Rules that stay in force:

- No `CFPINFLOW`, `CFP`, `AIN` or `Inflow` name in C# or TypeScript. Tests and JSON can have them.
- The workflow engine code (`src/`) does not change (Q1, Q2).
- TTDV2 and the current target apps do not change. Each new JSON key and each new request field is optional.
- Database changes are SQL scripts only: additive, idempotent, with a `CP_Atlas_ScriptLog` row.
- The time comes from `TimeProvider`. No `DateTime.UtcNow`.

## 2. Decisions that shape the design

| Source | Decision |
|---|---|
| Q1 | Build the engine adapter (module 10) for section forms. The engine code does not change. |
| Q2 | Two emails at the first submit: the adapter runs two engine actions. The second action keeps the state. |
| Q3 | The Investment Type lists are in the platform: a map in the form JSON, the addresses in the app settings. |
| Q4 | Keep `CFP.InflowAttachment`. `Type` holds the control name, so each file shows in its own section. |
| Q5 | `Description` and `Comment` change to `nvarchar(max)`. |
| Q6 | Server HTML clean-up with the NuGet package `HtmlSanitizer`. |
| Q7 | New severity "Info" in `--atlas-check`. It is not counted. |
| Q8 | E3 uses the RM and RMCC of the first submit. New column `CFP.Inflow.RMCC`. |
| Q9 | Export = the Excel export that is built into the grid at the target. |

## 3. Overview

```text
Browser (Angular, platform/)                    Server (platform)                         Other
---------------------------                     -----------------                         -----
form: richtext, filepicker,     v1/ManagePage   ConfigSectionService.SavePage
      state rules, Execute  ------------------>   1 validate (rules by state, MustBeTrue)
      button                    + files           2 clean HTML (HtmlSanitizer)
                                                  3 write record, audit, attachments
                                                  4 engine adapter: Start / Execute  ---->  workflow engine
                                                  5 write state + status, commit             (own tables, outbox)
                                                  6 follow-up engine action, dispatch ---->  SMTP
download link  ---------------> v1/Attachment/{appCode}/{id}  (new platform controller)
account list   <--------------- Table option provider  <------------------------------------ AIN.AccountInventory
```

New parts, all generic:

| Part | Section |
|---|---|
| SQL scripts for release P2 | 4 |
| App files: manifest, form, dashboard | 5 |
| Rules by state, `MustBeTrue`, button `Execute`, `Action.Set` | 6 |
| Table option provider (account lookup) | 7 |
| Attachments (module 8) | 8 |
| `richtext` control | 9 |
| Engine adapter, recipients, template data | 10 |
| `--atlas-check`: Info severity and the name report | 11 |
| Export, History text | 12 |

## 4. SQL scripts

Folder: `atlas-platform/sql/release-p2/`, with a `README.md` as in `release-p1`. Each script follows the model
`release-p1/040-ttdv2-app.sql`: `@RunBy`, one transaction, each object made only if it is not there, one
`CP_Atlas_ScriptLog` row. Run in number order.

| Script | Database | Content |
|---|---|---|
| `100-cfp-inflow-columns.sql` | CP Atlas | Changes to `CFP.Inflow` (4.1) |
| `110-cfp-inflow-attachment-columns.sql` | CP Atlas | Changes to `CFP.InflowAttachment` (4.2) |
| `120-cfp-audit-log.sql` | CP Atlas | New table `CFP.AuditLog`, the same shape as `TTDV2.AuditLog` (`ReferenceID`, `ReferenceType`, `CreatedBy`, `CreatedByLanId`, `CreatedDate`, `LogMessage`) and the index on `ReferenceID`, `ReferenceType` |
| `130-cfpinflow-app.sql` | CP Atlas | `CP_Atlas_Application` row (`CFPINFLOW`, "Client Flows Portal - Inflow", no parent); roles `Read` (IsRead) and `Write` (IsWrite, IsRead) |
| `140-ain-account-inventory-rmcc.sql` | CP Atlas | `ALTER TABLE AIN.AccountInventory ADD RMCC varchar(1000) NULL` (D32). The table belongs to the AIN app; the target team runs or copies this script |
| `150-cfpinflow-workflow.sql` | Engine tables | Workflow metadata (4.3) |

Role grants use the template `docs/atlas-platform/sql/06-grant-revoke-role-template.sql`. No grant is in a release
script.

### 4.1 `CFP.Inflow`

The table exists at the target (D2). The script changes it in place. No column is removed or renamed.

| Change | Detail |
|---|---|
| `Description`, `Comment` | `varchar(max)` to `nvarchar(max)`, only if the type is `varchar` (Q5). The data stays. |
| `RowVersion` | `rowversion NOT NULL`. The platform version check finds it by type. |
| `WorkflowState` | `varchar(100) NULL`. Engine state key: `Submitted`, `FundsReceived`. |
| `Status` | `varchar(100) NULL`. Label for the dashboard: "Submitted", "Funds Received". |
| `FundsReceived` | `bit NULL`. The check box "Funds have been received". |
| `CashSubmittedDate` | `datetime NULL`, UTC. "Funds confirmed date". |
| `RMCC` | `varchar(1000) NULL`. Display names, separated by `;` (Q8). |
| `DestructionDate` | Computed: `AS DATEADD(year, 7, [CreatedDate])`. Read only. |
| Index | `IX_Inflow_WorkflowState` on `WorkflowState`, filtered `IsDeleted = 0`, for the "Inflow Pipeline" tab. |

`IsDeleted` stays `bit NULL`; the platform reads NULL as "not deleted". The person columns (`CreatedBy`, `ModifiedBy`,
`FlowSubmittedBy`, `CashSubmittedBy`) hold LAN IDs, and the dates are UTC. The target team must use the same values
when it moves the old SharePoint records (integration note).

### 4.2 `CFP.InflowAttachment`

| Change | Detail |
|---|---|
| `ContentType` | `varchar(200) NULL` |
| `SizeBytes` | `bigint NULL` (size before compression) |
| `CreatedByLanId`, `ModifiedByLanId` | `nvarchar(50) NULL`. `CreatedBy` and `ModifiedBy` keep the display name for the History window (module 8, 4.2). |
| `FileName` | `varchar(1000)` to `nvarchar(1000)`, only if the type is `varchar`. Approved (section 14.1). |
| Index | `IX_InflowAttachment_InflowId` on `InflowId` |

`Type` (`varchar(500)`) holds the control name of the `filepicker` field. `FileContent` holds the GZip-compressed
file, as in module 8.

### 4.3 Engine workflow metadata

One script for the engine tables, in the form of the current engine metadata scripts (`sql/sqms/`). It is additive:
all keys are new.

- Workflow key `ClientInflow`, version 1.
- States: `Draft` (initial), `Submitted`, `FundsReceived` (end).
- Engine role: `Write`.
- No task and no SLA policy (D20).

| Action key | From | To | Role | Route | Email |
|---|---|---|---|---|---|
| `Submit` | `Draft` | `Submitted` | `Write` | `ClientInflow.ActionRequired` | E1 |
| `NotifyInformational` | `Submitted` | `Submitted` | `Write` | `ClientInflow.SubmittedInformational` | E2 |
| `ConfirmFunds` | `Submitted` | `FundsReceived` | `Write` | `ClientInflow.FundsReceived` | E3 |

| Route | To | Cc |
|---|---|---|
| E1 | Static: ICG list | none |
| E2 | Request input `investmentLists` (required); request input `relationshipManager` (not required) | none |
| E3 | Request input `investmentLists` (required); request input `relationshipManager` (not required); static: CS Team list | Request input `rmcc` (not required); static: IMRG list; static: Star FAS IFI Cash Team list |

- The subject and body text come from `email1.jpeg` to `email3.jpeg` (D19). Tokens: `{{accountName}}`,
  `{{accountNumber}}`, `{{amount}}`, `{{inflowDate}}`, `{{recordLink}}`.
- The static addresses in the script are placeholders (`icg-inflow@example.com`, ...). The target team sets the real
  lists before the run. The four fixed lists stay in the engine metadata; only the Investment Type lists are in the app
  settings (Q3).
- `NotifyInformational` keeps the state. A test must prove that the engine accepts it (Q2). This test is the first
  build step (section 13).

### 4.4 Local database

`atlas-platform/local/sql/` gets two scripts, and `setup-local-db.ps1` runs them:

- `020-local-cfp-ain-tables.sql`: `CFP.Inflow`, `CFP.InflowAttachment` and `AIN.AccountInventory` in the exact target
  shape (from the target copy), so the release scripts are tested against the real start point. Sample accounts: clean
  names, one RM with no user, one RM name with two users, one RMCC with two names.
- `900-local-seed.sql` (extended): users A035313 (`Write`) and A023456 (`Read`), and placeholder addresses.

The local database also gets the engine tables. `setup-local-db.ps1` applies them with the tooling that the engine has
today. The emails go to smtp4dev.

## 5. App files

Folder `atlas-platform/src/BBH.CP.Atlas.App/Config/`. Registration: one line,
`AddAtlasModule<ConfigSectionService>("CFPINFLOW")`.

### 5.1 `CFPINFLOW.app.json`

```json
{
  "Code": "CFPINFLOW",
  "Name": "Client Flows Portal - Inflow",
  "FormType": "FORM",
  "Parent": null,
  "Form": "CFPINFLOW.json",
  "Dashboard": "CFPINFLOW.Dashboard.json",
  "Table": "CFP.Inflow",
  "AuditTable": "CFP.AuditLog",
  "AttachmentTable": "CFP.InflowAttachment",
  "AttachmentColumns": { "RecordId": "InflowId", "ControlName": "Type" },
  "LookupTables": [ "AIN.AccountInventory" ],
  "AccessMode": "Enforce",
  "AuditMode": "Server",
  "AuditTime": "Utc",
  "RuleMode": "MostPermissive",
  "RuleDefault": "ReadOnly",
  "Enabled": true
}
```

New optional manifest keys: `AttachmentColumns` (8.1) and `LookupTables` (7). `AccessMode` is `Enforce`, because the
app is new and has no old users.

### 5.2 `CFPINFLOW.json` (form)

Top level:

```json
{
  "Name": "Client Flows Portal - Inflow",
  "Type": "SECTION",
  "Permissions": {
    "View": [ "@Read" ],
    "Create": [ "Write" ],
    "Edit": { "All": [ "Write" ] },
    "Delete": [ "Write" ],
    "History": [ "@Read" ],
    "Execute": { "SubmitPart1": [ "Write" ], "SubmitPart2": [ "Write" ] }
  },
  "Defaults": { "FlowSubmittedBy": "{User.LanID}" },
  "SoftDelete": { "Field": "IsDeleted" },
  "HeaderNameWithId": "{Name} - {ID}",
  "Workflow": { "...": "section 10.2" },
  "Section": { "Controls": [ "..." ], "Buttons": [ "..." ] }
}
```

Controls:

| # | Name | Type | Notes |
|---|---|---|---|
| 1 | `Account` | `ngselect` | `OptionsSource` = Table provider (7). `SetFromOption` fills `AccountNumber`, `AccountName`, `StarNumber`, `RelationshipManager`, `Custodian`, `RMCC`. Required. |
| 2 to 5 | `AccountName`, `StarNumber`, `RelationshipManager`, `Custodian` | `text` | `Disabled: true`. Set by the server from the option. |
| 6 | `InvestmentType` | `select` | Options: Fixed Income, Equity, Fixed Income & Equity. Required. |
| 7 | `Amount` | `currency` | `Validator: { Required: true, Min: 0.01 }` |
| 8 | `InflowDate` | `date` | Required |
| 9 | `Description` | `richtext` | Not required |
| 10 | `FlowAttachments` | `filepicker` | `Multiple: true`, required, label "Please attach email/signed instruction from the client." |
| 11 | `FlowSubmittedBy` | `user` | `Disabled: true` |
| 12 | `FlowConfirm` | `checkbox` | `Validator: { MustBeTrue: true }` |
| 13 | `FundsReceived` | `checkbox` | `Validator: { MustBeTrue: true }` |
| 14 | `Comment` | `richtext` | Not required |
| 15 | `CashAttachments` | `filepicker` | `Multiple: true`, required, label "Please attach proof of custodian receipt." |
| 16 | `CashSubmittedBy` | `user` | `Disabled: true`. Set by the button (6.4). |
| 17 | `CashConfirm` | `checkbox` | `Validator: { MustBeTrue: true }` |

`RMCC` has a column and no control: it is a data-only target of the effect (7.3). The control names
`FlowAttachments` and `CashAttachments` become the `Type` values; they are stable identifiers from the first save.

Rules (intent; the exact JSON is fixed against the `AtlasRuleEngine` tests in build step 3):

| Part | State `Draft` (new) | State `Submitted` | State `FundsReceived` |
|---|---|---|---|
| Part 1 sections | `Write`: enabled | read-only | read-only |
| Part 2 sections | hidden | `Write`: enabled | read-only |
| `Read` role | read-only | read-only | read-only |

`RuleDefault: ReadOnly` gives the read-only cells. One rule for each enabled cell names the role and the state, for
example `{ "Roles": [ "Write" ], "States": [ "Draft" ], "Disabled": false }`. Part 2 has
`{ "States": [ "Draft" ], "Visible": false }`.

Buttons:

```json
"Buttons": [
  { "Name": "SubmitPart1", "Label": "Submit",
    "Action": { "Type": "Execute" },
    "Rules": [ { "Roles": [ "Write" ], "States": [ "Draft" ] } ] },
  { "Name": "SubmitPart2", "Label": "Submit",
    "Action": { "Type": "Execute", "Set": { "CashSubmittedBy": "{User.LanID}", "CashSubmittedDate": "{Now}" } },
    "Rules": [ { "Roles": [ "Write" ], "States": [ "Submitted" ] } ] },
  { "Name": "Close", "Label": "Close", "Action": { "Type": "Close" } }
]
```

No Save button (D15: no draft save). No Delete button on the form: delete is in the row menu (D24).

### 5.3 `CFPINFLOW.Dashboard.json`

- Tab `Inflows`: `Source: Table`, no filter, `OrderBy: [ "CreatedDate DESC" ]`.
- Tab `InflowPipeline`: filter `{ "Column": "WorkflowState", "Op": "Eq", "Value": "Submitted" }`, the same order.
  A record has "Funds have been received = No" exactly when its state is `Submitted`.
- Columns on the two tabs (D23): `ID` (`Link: Edit`), `AccountNumber`, `AccountName`, `InvestmentType`, `Amount`
  (`currency`), `InflowDate` (`date`), `Status`, `FlowSubmittedBy` (`user`), `CreatedDate` (`date`),
  `CashSubmittedBy` (`user`), `CashSubmittedDate` (`date`), `DestructionDate` (`date`).
- Button `Add` with `Target: { "AppCode": "CFPINFLOW", "Id": 0 }`. The permissions hide it for a `Read` user.

## 6. Form behaviour (generic)

### 6.1 State in the rules (B4)

- `ConfigSectionService.RuleContext` reads the value of the `Workflow.StateField` column of the record and gives it to
  the rule engine as the state list. Today the list is always empty.
- A new record has the state `Workflow.InitialState` (`Draft`).
- The state comes from the database row on load and on save. A state value from the client is never read.
- A form with no `Workflow` binding has an empty state list, as today. TTDV2 does not change.

### 6.2 Validation and state

The field validator and the allow list use the effective form of the user and the state. A hidden or disabled field is
not validated and its value is not accepted. Thus the part 2 fields cannot stop the first submit, and the part 1
fields cannot change at the second submit. Build step 3 has tests for the two cases.

### 6.3 `MustBeTrue` (B5)

New optional validator key. The value must be `true`. It runs on the server (`AtlasFieldValidator`) and on the client
(`atlas-validators.ts`), with the shared cases file. Message: "Select <label> before you submit."

### 6.4 Button `Execute` and `Action.Set` (B6, B7)

- New optional request field `Button` (the button name) on `v1/ManagePage`. With no `Button`, the save is a plain
  save, as today.
- The server finds the button in the form JSON. It checks `Permissions.Execute[<button>]` and the button rules
  (roles and state) for the user. A refusal is HTTP 403 with the page error contract.
- `Action.Set` gives field values for this save: field name to token. Tokens: `{User.LanID}` and `{Now}` (UTC, from
  `TimeProvider`), with the token code of `AtlasDefaultTokens`. A field in `Set` is server-set: a client value for it
  is not accepted.
- The client sends the button name, never an engine action key. The binding selects the engine actions (10.2).
- The client disables the buttons while a save runs.

## 7. Table option provider: the account lookup (B3)

A generic provider with the name `Table`, in `Service/Platform/Options/`. It reads the options from a table that the
form JSON names.

```json
"OptionsSource": {
  "Provider": "Table",
  "Parameters": {
    "Table": "AIN.AccountInventory",
    "Value": "AccountNumber",
    "Label": "{AccountNumber} - {AccountName}",
    "Data": "AccountName,StarAccountNumber,RelationshipManager,Custodian,RMCC",
    "OrderBy": "AccountNumber",
    "UserNameColumns": "RelationshipManager,RMCC"
  }
}
```

### 7.1 Rules

- The table must be in `LookupTables` of the manifest. The platform reads its column metadata at start-up, with the
  same reader as the app tables. A lookup table is read-only: the platform never writes to it.
- The start-up check refuses a table, a column or a label token that is not in the metadata. The names go into the
  SQL text only after this check, in brackets. All values are parameters.
- If the table has an `IsDeleted` column, the provider reads only the rows that are not deleted (D29).
- The response has only the `Value`, the label and the `Data` columns. No other column leaves the server.
- The provider sends all rows with the form load, as the TTDV2 account list does today. The option list of a table is
  cached for a short time (60 seconds) in memory, so many form loads do not read the table each time.
- `KeepSavedValue: true` on the control keeps a saved account in the list after the AIN row is deleted.

### 7.2 Use in other apps

Another app uses the same provider with its own `Data` columns. It adds `AIN.AccountInventory` to its `LookupTables`.
No code is necessary.

### 7.3 Data-only effect target

`SetFromOption` can name a field that has a column and no control (here `RMCC`). The server effect sets the column
from the option data. The client does not show it and does not send it. The start-up check accepts a target that is a
control or a column of the app table, and refuses other names.

## 8. Attachments (B1, B11)

The design is module 8 with its section 10 decisions. This section gives only the parts that P2 adds or fixes.

### 8.1 Column map

`AttachmentColumns` in the manifest maps the module 8 names to the real column names. Each key is optional; the
default is the module 8 name.

| Module 8 name | `CFP.InflowAttachment` |
|---|---|
| `RecordId` | `InflowId` |
| `ControlName` | `Type` |
| all others | same name |

The start-up check refuses an attachment table that does not have each necessary column with a fitting type.

### 8.2 Server

- `IAtlasAttachmentService` in `Service/Platform/Attachments/`: list, add, remove, open.
- `SavePage(input, files)` reads the files. A file is accepted only when its control is a `filepicker` that is
  visible and enabled in the effective form (the same check as the allow list).
- Limits: 10 MB for each file, 25 MB for the new files of one save, the default type list, the file signature check
  (module 8, 4.3). The file name is `Path.GetFileName`, 255 characters at most.
- New optional request field `RemovedAttachmentIds`. The server removes (soft delete) only the IDs that belong to this
  record and to an enabled `filepicker`.
- `Required` on a `filepicker`: after the save, the control has one file at least (files that stay + new files).
- All of this is in the transaction of the record. Each added and each removed file gets one audit entry.
- Load: the files of the record, grouped by control name. For each file: ID, name, size, created by, created date
  and the download URL.
- New controller `App/Platform/AtlasAttachmentController.cs`: `GET v1/Attachment/{appCode}/{id}`. It checks `View` on
  the parent record, then streams the decompressed file with `Content-Disposition: attachment` and
  `X-Content-Type-Options: nosniff`. An unknown ID, a deleted file or a deleted record gives 404.

### 8.3 Client

- New renderer `filepicker` in `form/renderers/`, registered in `atlas-renderer-registry.ts`. It uses the target file
  control of `@pbdev/pbdev-core` if the stand-in has it; the look follows the CDT step page.
- The form sends the files as form parts `<ControlName>#$#<FileName>`, the format that the target uses today.
- Remove: the question dialog, then the file shows as "removed when you save".
- The client checks the size limits before it sends. The server checks again.
- A disabled `filepicker` shows the list with download links only.

## 9. Rich text (B2, B9)

### 9.1 Client

- New renderer `richtext`. It loads CKEditor 5 with a dynamic `import()`, so the editor code is only loaded for a form
  that has a `richtext` control.
- Packages: `ckeditor5` and `@ckeditor/ckeditor5-angular`, the latest versions at build time that support Angular 21,
  with fixed version numbers. `licenseKey: 'GPL'`.
- Plugins: paragraph, headings, bold, italic, underline, strikethrough, lists, link, table, block quote, undo, paste
  from Office. No picture plugin and no upload adapter (D12, D31). A pasted picture is dropped.
- A disabled control shows the editor in read-only mode with no toolbar.

### 9.2 Server

- On save, each `richtext` value goes through `HtmlSanitizer` before validation and before the write. The check is by
  control type, so each app gets it.
- Allowed tags: `p`, `br`, `strong`, `b`, `em`, `i`, `u`, `s`, `h2`, `h3`, `h4`, `ul`, `ol`, `li`, `blockquote`, `a`,
  `figure`, `table`, `thead`, `tbody`, `tr`, `th`, `td`. Allowed attributes: `href` on `a`; `colspan` and `rowspan` on
  cells; `class` on `figure`. Allowed link schemes: `http`, `https`, `mailto`. No `style`, no `img`, no event
  attributes.
- The list is one constant in the platform. It is not in the app JSON, so an app cannot make it weaker.
- A value that is empty after the clean-up is saved as NULL.
- Largest size: 200,000 characters after the clean-up; more gives a field error.
- The audit entry holds the old and the new value as plain text (tags removed, entities decoded). The History window
  shows plain text.
- The dashboard shows a `richtext` column as plain text, with the same converter. (P2 has no such column.)
- Rich text is not in the emails of P2.

## 10. Engine adapter (C1 to C8)

The design is module 10. This section gives the parts for a section form and the answers Q2, Q3 and Q8.

### 10.1 Parts

- `IAtlasWorkflowAdapter` in `Service/Platform/Workflow/`: `Start`, `Execute`, `ReadContext`, `DispatchNotifications`.
  SLA synchronization and the history read are not in P2 (D20, D27).
- The adapter uses the engine services from dependency injection, as the target services do
  (`IStartWorkflowService`, `IWorkflowExecutionService`, the dispatcher, the context reader, the registered
  normalizer and hasher).
- `BBH.CP.Atlas.Service.csproj` gets a reference to the engine application project. The target Service project has
  this reference already. An app with no `Workflow` binding makes no engine call.
- `SourceSystem` = `BBH.CP.Atlas.Platform`. Instance ID = `WF-` + `BusinessObjectPrefix` + record ID.

### 10.2 Binding

Optional properties on the `Workflow` object of the form (partial class of the target `CPWorkflow`; `Key` and
`Actions` are target properties and stay).

```json
"Workflow": {
  "Key": "ClientInflow",
  "Actions": {},
  "BusinessObjectPrefix": "CFP-INFLOW-",
  "ActorRoles": "User",
  "StateField": "WorkflowState",
  "StatusField": "Status",
  "InitialState": "Draft",
  "StatusLabels": { "Draft": "Draft", "Submitted": "Submitted", "FundsReceived": "Funds Received" },
  "Steps": {
    "SubmitPart1": [
      { "Start": "Submit" },
      { "Execute": "NotifyInformational", "Recipients": [ "investmentLists", "relationshipManager" ] }
    ],
    "SubmitPart2": [
      { "Execute": "ConfirmFunds", "Recipients": [ "investmentLists", "relationshipManager", "rmcc" ] }
    ]
  },
  "Recipients": {
    "investmentLists": {
      "ListByField": "InvestmentType",
      "Map": {
        "Fixed Income": [ "FixedIncome" ],
        "Equity": [ "Equity" ],
        "Fixed Income & Equity": [ "FixedIncome", "Equity" ]
      }
    },
    "relationshipManager": { "FromNameField": "RelationshipManager", "Optional": true },
    "rmcc": { "FromNameField": "RMCC", "Optional": true }
  },
  "TemplateData": {
    "accountName": { "Field": "AccountName" },
    "accountNumber": { "Field": "AccountNumber" },
    "amount": { "Field": "Amount", "Format": "$#,##0.00" },
    "inflowDate": { "Field": "InflowDate", "Format": "MM/dd/yyyy" },
    "recordLink": { "RecordLink": true }
  }
}
```

- `Steps`: button name to a list of engine steps. The first step can change the state. Each later step must keep it.
- `Recipients` of a step: the recipient keys that the adapter sends with this step. The engine refuses a key that
  the route does not declare, so each step names its own keys.
- The start-up check refuses: a `Steps` key that is not a button; a recipient key with no definition; a field name
  that is not a column; a `Map` value that is not an option of the field; a list key with no address in the settings.

### 10.3 Save order

For a save with a `Button` that has steps:

1. Validate (rules, access, fields, files). No write.
2. Begin the transaction. Write the record, the audit entries and the attachments. `SaveChanges`, no commit.
3. Run the first step in the engine (`Start` with the initial action, or `Execute`).
4. Engine `Success` is false: roll back. Return the engine code and message as a page error. Nothing changed.
5. Engine `Success` is true: write `WorkflowState` (the engine state key) and `Status` (the label of
   `StatusLabels`). Commit.
6. After the commit, run each later step in order. Then dispatch the notifications of all steps.

Failures after the commit do not undo the save:

- A later step fails (E2): the record stays saved. The adapter writes an error log with the instance ID and the
  action key. The response says "Saved. One notification was not sent." No automatic second try in P2.
- A dispatch fails: warning log; the engine outbox sends the email again (module 10, 4.5).
- The commit of step 5 fails after the engine committed: error log, and the state check on the next load corrects the
  record (10.4).

Double submit: new optional request field `RequestId` (a GUID from the client for each user action). The
idempotency key of step n is `RequestId` + `:` + n. With no `RequestId`, the server makes a GUID.

### 10.4 Load

When a record with a binding loads, the adapter reads the engine context. If the engine state is not the
`WorkflowState` of the record, the platform writes the engine state and the status label to the record, writes one
audit entry (`WorkflowSync`) and a warning log. The engine is the source of truth for the state. A record with no
engine instance (new) is not checked.

### 10.5 Actor roles

`ActorRoles: "User"`: the adapter sends the names of the active roles of the user in the app, from the active roles
view. The engine role `Write` has the same name as the app role.

### 10.6 Recipients

| Source | Meaning |
|---|---|
| `ListByField` + `Map` | The value of the field selects list keys. Each key has one or more addresses in `AppSettings:Modules:<Code>:Workflow:Lists:<Key>` (separated by `;`). |
| `FromNameField` | The field holds display names, separated by `;`. Each name is found in `CP_Atlas_User`. |
| `FromUserField` | The field holds a LAN ID (module 10, 4.6). Designed, not used in P2. |

Display name rules (D33, D34), in one service `IAtlasUserDirectory`:

- The compare ignores case, and spaces at the start, at the end and doubled.
- Exactly one active user (`IsDeleted` is not 1) with an email: use the email.
- No user, a user with no email, or two or more users: leave the name out. Write the warning log event
  `Atlas.RecipientSkipped` with the app, the record ID, the recipient key and the name.
- `Optional: true`: the step continues with no address for this key. `Optional: false`: an empty result is a field
  error and the save stops (before the first engine call).
- The adapter removes duplicate addresses.

The recipients are found before step 3 of 10.3, from the values of the saved record. Thus E3 uses the
`RelationshipManager` and `RMCC` of the first submit (Q8).

### 10.7 Template data

- `Field` + optional `Format`: the value of the record as text. `Format` is a .NET format text, used with the
  `en-US` culture. A date field is formatted as stored (no time zone change for a date with no time).
- `RecordLink`: `AppSettings:Atlas:WebBaseUrl` + `/form/<Code>/<ID>`. The start-up check gives an error when a binding
  uses `RecordLink` and the setting is empty.
- The adapter sends the same template data with each step.

### 10.8 Accepted limits

- A deleted record in state `Submitted` keeps its engine instance. No email goes out (no SLA). Accepted (gap N3).
- E1 and E2 are two engine commits. A failure between them sends E1 and not E2. Accepted with Q2.

## 11. `--atlas-check` (B8)

- New severity `Info` in `AtlasStartupCheck`. An Info line is printed and logged. It is not counted: the summary stays
  `0 error(s), 0 warning(s)` and the exit code does not change. Info lines are never a reason to disable an app.
- Generic data report: for each Table provider with `UserNameColumns`, the check reads the not-deleted rows and
  lists each row where a name (split by `;`) does not match exactly one active user with an email. Format:
  `Info: AIN.AccountInventory AccountNumber 12345: RelationshipManager "John Smith": no user`.
- The report prints 200 lines at most for each table, then the count of the rest.
- The report runs only with the `--atlas-check` switch, not at each start-up of the host.

## 12. Export and History

- Export (Q9): the target registers `AllEnterpriseModule`, so each grid has the built-in menu with "Export" and
  "Excel Export". The platform dashboard columns give the displayed text to the export (dates, currency, user names).
  No platform button and no new package. The local stand-in has `ag-grid-community` only: the Excel export is tested
  at the target. **See section 14, item 5.**
- History (D27): the audit writer of today records each field. New: the entries for files (8.2), plain text for
  `richtext` (9.2), and `WorkflowSync` (10.4). No workflow history on the form.

## 13. Build order

Each step ends with the platform gates and one commit. No push.

| Step | Content | Proof |
|---|---|---|
| 0 | Engine test: an action that keeps the state runs and makes its outbox row. Test only, no engine change. | If it fails: stop and ask (Q2 falls back to its option B or C). |
| 1 | `release-p2` scripts 100 to 140, local tables and seed, manifest, form with the controls of today, dashboard. | The app shows in the menu; a plain record saves; SQL tests for each script, run two times. |
| 2 | Table provider, `LookupTables`, data-only effect target, `Info` severity, name report. | Account list fills 5 fields + `RMCC`; `--atlas-check` prints the Info lines and `0 error(s), 0 warning(s)`. |
| 3 | State in the rules, `MustBeTrue`, request field `Button`, `Permissions.Execute`, `Action.Set`. | Part 2 hidden in `Draft`; refusals tested; TTDV2 tests pass with no change. |
| 4 | Attachments: service, save, removal on save, limits, audit, controller, `filepicker` renderer. | Module 8 section 7 acceptance for a platform app. |
| 5 | `richtext`: packages, renderer, `HtmlSanitizer`, plain text in History. | Script and event attributes are removed (API test with raw HTML). |
| 6 | Engine adapter: script 150, binding, steps, recipients, template data, state check, `RequestId`. | Module 10 section 8 acceptance; scenarios 1 and 2 with smtp4dev. |
| 7 | `INTEGRATION.md`, `CODE_TRACE.md`, `Config/README.md`, target package, onboarding template update. | Scenarios 1 to 6 of the requirements; all gates; package rebuilt. |

### 13.1 Tests

- .NET (xUnit, SQL Server): each script is idempotent; provider SQL is safe for bad names; rule context by state;
  `MustBeTrue`; `Set` tokens; a client value for a server-set field is refused; attachment limits, signature, control
  check, removal of a file of another record refused; download access (403, 404); HTML clean-up cases; name match
  cases (case, spaces, none, two users, no email); list map; template formats; save order with a refused engine
  action (nothing changed); follow-up step failure (record saved, error log); state sync on load; double submit with
  one `RequestId`.
- Angular (Vitest): `filepicker` and `richtext` renderers; `MustBeTrue` with the shared cases; `Execute` button sends
  `Button` and `RequestId`; state rules hide part 2.
- Regression: all TTDV2 tests pass with no change to their expected results.
- `CodeCommentRulesTests` stays green: no D, Q, B or module number in a code comment.

## 14. Items that need your approval

These are the stop-and-ask items of this design. The answers Q1 to Q9 already cover the engine adapter, the
`nvarchar` change of `Description` and `Comment`, `HtmlSanitizer`, CKEditor and the `RMCC` columns.

| # | Item | Recommendation |
|---|---|---|
| 1 | `v1/ManagePage` gets three optional request fields: `Button`, `RequestId`, `RemovedAttachmentIds`. New route `GET v1/Attachment/{appCode}/{id}` (decided in module 8). All additive. | Approve |
| 2 | `CFP.InflowAttachment.FileName` from `varchar(1000)` to `nvarchar(1000)`, so a file name keeps its characters. The table has no platform data yet. | Approve |
| 3 | Target files that change (integration steps, the user does them): `program.cs` (one `AddAtlasModule` line), `package.json` (the two CKEditor packages replace the six 41.4.2 packages, after the target team checks its own use of them), the Service `.csproj` (`HtmlSanitizer`), `appsettings.json` (list addresses, `WebBaseUrl`), `tailwind.config.js` only if the form uses a new class. | Approve |
| 4 | New log events 41025 to 41029: `Atlas.RecipientSkipped`, `Atlas.WorkflowStepFailed`, `Atlas.WorkflowSync`, `Atlas.NotificationDispatchFailed`, `Atlas.AttachmentRefused`. New identifiers, none changed. | Approve |
| 5 | Export: the built-in grid menu, no new button. Tell me if you also want a visible "Export" button on the platform dashboard (generic, one dashboard JSON key). | Built-in menu only |
| 6 | The four fixed lists (ICG, CS Team, IMRG, Star FAS IFI Cash Team) stay as static addresses in the engine script. A change of such a list is an SQL change. The other choice is to put them in the app settings too, with the same list mechanism. | Keep in the engine script |

### 14.1 Answers

| # | Answer | Date |
|---|---|---|
| 1 | Approved: the three optional request fields and the new download route. | 2026-11-20 |
| 2 | Approved: `CFP.InflowAttachment.FileName` changes to `nvarchar(1000)`. | 2026-11-20 |
| 3 | Approved: the target file changes (`program.cs`, `package.json`, the Service `.csproj`, `appsettings.json`, and `tailwind.config.js` only if necessary). | 2026-11-20 |
| 4 | Approved: the log events 41025 to 41029. | 2026-11-20 |

## 15. Target integration steps (for `INTEGRATION.md`)

1. Prerequisites: the engine migrations are current and the engine scheduled job with the outbox retry runs
   (module 10, 4.5; module 11).
2. Run `sql/release-p2/` 100 to 150 in number order. Set the real list addresses in `150` first. The AIN team gets
   `140`.
3. Copy the platform folders as for P1, plus `Config/CFPINFLOW.app.json`, `CFPINFLOW.json`,
   `CFPINFLOW.Dashboard.json`.
4. Do the target file changes of section 14, item 3.
5. Settings in each environment: `AppSettings:Modules:CFPINFLOW:Workflow:Lists:FixedIncome`,
   `...:Lists:Equity`, `AppSettings:Atlas:WebBaseUrl`.
6. Grant the roles `Read` and `Write` with the grant template.
7. Run `--atlas-check`. Read the Info lines: each line is an RM or RMCC name that gets no email. Correct the data in
   AIN or in `CP_Atlas_User`.
8. Restart the app pool.
9. Old data: the target team moves the SharePoint records with LAN IDs in the person columns, UTC dates,
   `WorkflowState` and `Status` filled. A moved record in state `Submitted` has no engine instance; the second submit
   of such a record needs a decision of the target team before the move (start an instance in the move script, or
   move only closed records).

## 16. Backward compatibility

- No engine code change. No change to an existing engine key; `ClientInflow` and its keys are new.
- TTDV2: no JSON change, no behaviour change. The new code paths run only for a form that has a `Workflow` binding,
  a `filepicker`, a `richtext` or a `Button` in the request.
- TTD, CDT, SQMSO, SQMSB, AIN: no change. The platform does not write to `AIN.AccountInventory`.
- `v1/...`: additive only (section 14, item 1).
- `CFP.Inflow` and `CFP.InflowAttachment`: no column removed or renamed. `CFP.Outflow` and `CFP.OutflowAttachment`
  are not touched.
- Rollback: set `AppSettings:Modules:CFPINFLOW:Enabled = false` and restart. The tables and the engine metadata stay.

## 17. Open points for the build (no decision necessary now)

- The email text is typed from the screenshots in build step 6; the user checks the three emails in smtp4dev.
- The CKEditor and `HtmlSanitizer` version numbers are fixed in build step 5 and written to `INTEGRATION.md`.
- The way the local setup makes the engine tables is fixed in build step 0.
