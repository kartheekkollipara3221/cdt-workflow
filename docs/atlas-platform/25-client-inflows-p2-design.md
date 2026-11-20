# Module 25: ClientInflows (P2) implementation design

Status: Draft 3, 2026-11-20. Draft 2 was approved by the user (14.1). Draft 3 has the corrections of the design
review ([26-p2-design-review.md](26-p2-design-review.md), findings DR-01 to DR-21, answers in its section 8).
Section 18 lists what changed. Design only. No code and no SQL script exists yet. The build starts with step 0 of
section 13 when the user says so.

## 1. Purpose

ClientInflows (app code `CFPINFLOW`) is release P2: the second platform app, and the first one with attachments, rich
text and a workflow. This document tells how to build it. Most of the work is generic platform code that the next apps
use again. The app itself is JSON and SQL.

Inputs:

- Requirements: [ONBOARDING_ANSWERS.md](../ClientInflows/ONBOARDING_ANSWERS.md), decisions D1 to D34.
- Gaps and answers: [GAP_REPORT.md](../ClientInflows/GAP_REPORT.md), items A1 to A17, B1 to B12, C1 to C8, answers
  Q1 to Q9.
- Review: [26-p2-design-review.md](26-p2-design-review.md), findings DR-01 to DR-21 and the answers.
- Module designs: [08-attachments.md](08-attachments.md) (section 10), [10-engine-adapter.md](10-engine-adapter.md),
  [07-registry.md](07-registry.md) (section 5), [12-ttd-pilot-build.md](12-ttd-pilot-build.md) (the model).

Rules that stay in force:

- No `CFPINFLOW`, `CFP`, `AIN` or `Inflow` name in C# or TypeScript. Tests and JSON can have them.
- The workflow engine code (`src/`) does not change (Q1, Q2).
- The current target apps do not change. Each new JSON key and each new request field is optional. Section 16 gives
  the two small changes for TTDV2 users who cannot edit.
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

### 2.1 Platform rules from the review

These rules are generic. They are for each platform app, not only for ClientInflows.

| Rule | Source | Section |
|---|---|---|
| **One submit button.** A section form with a workflow has one submit button, not one button for each part. The server selects the workflow step from the state of the record. Each save of such a form runs the step for the current state; no save goes past the workflow. This rule is also in `AGENTS.md`. | DR-02 | 6.4, 10.2 |
| **Saved values stay.** An effect of a control sets values only when the user can change that control. A lookup table is read when the user selects a value, not again at each later save or load. | DR-01 | 6.5 |
| **Saved option only.** When the user cannot change a dropdown, the server sends only the saved option, not the full list. | DR-18 | 7.1 |
| **No "Add New" for a read-only user.** The dashboard hides the `Add` button for a user who has no `Create` permission. The platform code does this today. | User, 2026-11-20 | 5.3 |

## 3. Overview

```text
Browser (Angular, platform/)                    Server (platform)                         Other
---------------------------                     -----------------                         -----
form: richtext, filepicker,     v1/ManagePage   ConfigSectionService.SavePage
      state rules, one      ------------------>   1 validate (rules by state, MustBeTrue)
      Submit button             + files           2 clean HTML (HtmlSanitizer)
                                                  3 write record, audit, attachments
                                                  4 engine adapter: Start / Execute  ---->  workflow engine
                                                  5 write state + status, commit             (own tables, outbox)
                                                  6 follow-up engine action, dispatch ---->  SMTP
file download (HttpClient) ---> v1/Attachment/{appCode}/{id}  (new platform controller)
account list   <--------------- Table option provider  <------------------------------------ AIN.AccountInventory
```

New parts, all generic:

| Part | Section |
|---|---|
| SQL scripts for release P2 | 4 |
| App files: manifest, form, dashboard | 5 |
| Rules by state, `MustBeTrue`, one submit button, effects rule | 6 |
| Table option provider (account lookup) | 7 |
| Attachments (module 8) | 8 |
| `richtext` control | 9 |
| Engine adapter, steps by state, recipients, template data | 10 |
| `--atlas-check`: Info severity and the name report | 11 |
| Export, currency format, History text | 12 |

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

- No new index (DR-14). The dashboard query uses `ISNULL([IsDeleted], 0) = 0`, so it cannot use a filtered index, and
  the table gets about 500 rows each year.
- `IsDeleted` stays `bit NULL`; the platform reads NULL as "not deleted".
- The account control writes the column `AccountNumber`. The column `Account` of the table is not used by the
  platform app (DR-07). This is an integration note for the target team.
- The person columns (`CreatedBy`, `ModifiedBy`, `FlowSubmittedBy`, `CashSubmittedBy`) hold LAN IDs, and the dates are
  UTC. The target team must use the same values when it moves the old SharePoint records (integration note).

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
| E1 | `icg` (required) | none |
| E2 | `investmentLists` (required); `relationshipManager` (not required) | none |
| E3 | `investmentLists` (required); `relationshipManager` (not required); `csTeam` (required) | `rmcc` (not required); `imrg` (required); `starFasIfiCash` (required) |

Each recipient rule is a request input: the adapter sends the addresses with the action (10.6). The script has no
email address.

- The subject and body text come from `email1.jpeg` to `email3.jpeg` (D19). Tokens: `{{accountName}}`,
  `{{accountNumber}}`, `{{amount}}`, `{{inflowDate}}`, `{{recordLink}}`.
- All six lists (ICG, CS Team, IMRG, Star FAS IFI Cash Team, Fixed Income, Equity) are in the app settings of each
  environment (Q3 and section 14.1, answer 6). The local settings have placeholder addresses
  (`icg-inflow@example.com`, ...).
- `NotifyInformational` keeps the state. A test must prove that the engine accepts it (Q2). This test is the first
  build step (section 13).

### 4.4 Local database

`atlas-platform/local/sql/` gets two scripts, and `setup-local-db.ps1` runs them:

- `020-local-cfp-ain-tables.sql`: `CFP.Inflow`, `CFP.InflowAttachment` and `AIN.AccountInventory` in the exact target
  shape (from the target copy), so the release scripts are tested against the real start point. Sample accounts: clean
  names, one RM with no user, one RM name with two users, one RMCC with two names, one account with an empty name.
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

Top level. The `Workflow` object is in the `Section`, because the target class `CPApplicationSection` holds it
(DR-03).

```json
{
  "Name": "Client Flows Portal - Inflow",
  "Type": "SECTION",
  "Permissions": {
    "View": [ "@Read" ],
    "Create": [ "Write" ],
    "Edit": { "All": [ "Write" ] },
    "Delete": [ "Write" ],
    "History": [ "@Read" ]
  },
  "Defaults": { "FlowSubmittedBy": "{User.LanID}" },
  "SoftDelete": { "Field": "IsDeleted" },
  "HeaderNameWithId": "{Name} - {ID}",
  "Section": {
    "Workflow": { "...": "section 10.2" },
    "Controls": [ "..." ],
    "Buttons": [ "..." ]
  }
}
```

Controls:

| # | Name | Type | Notes |
|---|---|---|---|
| 1 | `AccountNumber` | `ngselect` | Label "Account". `OptionsSource` = Table provider (7). The value is the account number, and the control writes the column `AccountNumber` (DR-07). `SetFromOption` fills `AccountName`, `StarNumber`, `RelationshipManager`, `Custodian` and `RMCC`. Required. |
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
| 16 | `CashSubmittedBy` | `user` | `Disabled: true`. Set by the step of state `Submitted` (6.4). |
| 17 | `CashConfirm` | `checkbox` | `Validator: { MustBeTrue: true }` |

`RMCC` has a column and no control: it is a data-only target of the effect (7.3). The control names
`FlowAttachments` and `CashAttachments` become the `Type` values; they are stable identifiers from the first save.

Rules (intent; the exact JSON is fixed against the `AtlasRuleEngine` tests in build step 3). Part 1 is the inflow
part (controls 1 to 12). Part 2 is the cash receipt part (controls 13 to 17).

| Part | State `Draft` (new) | State `Submitted` | State `FundsReceived` |
|---|---|---|---|
| Part 1 | `Write`: enabled | read-only | read-only |
| Part 2 | hidden | `Write`: enabled | read-only |
| `Read` role | read-only | read-only | read-only |
| Submit button | `Write`: shown | `Write`: shown | hidden |

`RuleDefault: ReadOnly` gives the read-only cells. One rule for each enabled cell names the role and the state, for
example `{ "Roles": [ "Write" ], "States": [ "Draft" ], "Disabled": false }`. Part 2 has
`{ "States": [ "Draft" ], "Visible": false }`.

Buttons. The form has one submit button (rule of 2.1). Each button has `Visible` and `Disabled`, because the default
of `Visible` in the target class is `false` (DR-03).

```json
"Buttons": [
  { "Name": "Submit", "Label": "Submit", "Visible": true, "Disabled": false,
    "Action": { "Type": "Save" },
    "Rules": [ { "Roles": [ "Write" ] } ] },
  { "Name": "Close", "Label": "Close", "Visible": true, "Disabled": false,
    "Action": { "Type": "Close" } }
]
```

- The Submit button does the first submit in state `Draft` and the second submit in state `Submitted`. The server
  selects the step (6.4). In state `FundsReceived`, the server hides the button.
- No draft save (D15): each click on Submit is a workflow step.
- No Delete button on the form: delete is in the row menu (D24).

### 5.3 `CFPINFLOW.Dashboard.json`

- Tab `Inflows`: `Source: Table`, no filter, `OrderBy: [ "CreatedDate DESC" ]`.
- Tab `InflowPipeline`: filter `{ "Column": "WorkflowState", "Op": "Eq", "Value": "Submitted" }`, the same order.
  A record has "Funds have been received = No" exactly when its state is `Submitted`.
- Columns on the two tabs (D23): `ID` (`Link: Edit`), `AccountNumber`, `AccountName`, `InvestmentType`, `Amount`
  (`currency`, formatted as in 12), `InflowDate` (`date`), `Status`, `FlowSubmittedBy` (`user`), `CreatedDate`
  (`date`), `CashSubmittedBy` (`user`), `CashSubmittedDate` (`date`), `DestructionDate` (`date`).
- Row menu (D24, DR-04). Each tab has `Menus`, so a `Read` user also gets "Edit" (the form opens read-only). With no
  `Menus`, the platform shows "Edit" only to a user with the `Edit` permission. The exact JSON is fixed against the
  dashboard tests in build step 1.

```json
"Menus": [
  { "Name": "Edit", "Label": "Edit" },
  { "Name": "History", "Label": "History" },
  { "Name": "Delete", "Label": "Delete", "Visible": false, "Rule": { "Roles": [ "Write" ] } }
]
```

- Button `Add` with `"Visible": true, "Disabled": false` and `Target: { "AppCode": "CFPINFLOW", "Id": 0 }`. The
  button has no rule, so the dashboard service uses `Permissions.Create`: a user with no `Create` permission (the
  `Read` role) does not get the button (rule of 2.1). The server also refuses a new record from such a user. Build
  step 1 has a test for the two checks.

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

### 6.4 One submit button (B6, B7, DR-02)

The rule: a section form with a workflow has one submit button. The server selects the workflow step from the state
of the record.

- A form is a "form with steps" when its `Workflow` binding has `Steps` (10.2). `Steps` has one entry for each state
  in which a submit is possible.
- A form with steps has exactly one button with `Action.Type` = `Save`. This is the submit button. The start-up check
  gives an error for no such button or for two or more.
- Each save of a form with steps is a submit. The server reads the state of the record from the database (a new
  record has `InitialState`) and takes the entry of `Steps` for this state. The request has no state, no engine
  action key and no button name.
- When `Steps` has no entry for the state, the server refuses the save (code `NotAllowed`). On load, the server then
  hides the submit button. Thus the button rules need only the roles.
- Optional `Label` in a step: the text of the submit button in this state. With no `Label`, the button keeps its
  text. ClientInflows does not use it (the text is "Submit" in the two states).
- Optional `Set` in a step: field values for this save, name to token. A name is a control name or a column name of
  the app table (DR-08); the start-up check refuses other names. Tokens: `{User.LanID}` and `{Now}` (UTC, from
  `TimeProvider`), with the token code of `AtlasDefaultTokens`. A field in `Set` is server-set: a client value for it
  is not accepted.
- Access: the save needs `Create` (new record) or `Edit` (existing record), as each save today. The engine checks the
  role of the action again.
- A submit needs a `RequestId` (10.3).
- The client disables the buttons while a save runs.
- A form with no `Steps` saves as today. TTDV2 does not change.

Not built: the request field `Button` and `Permissions.Execute` of draft 2. With one submit button they are not
necessary. `Permissions.Execute`, the button action type `Execute` and `CPButtonAction.Set` stay reserved, as in
release P1.

### 6.5 Effects only from a control that the user can change (DR-01)

Today the server runs the effects of each control at each save, with the stored value when the client sends none.
The client runs the effects each time the form opens. For a form with two submits, this reads the lookup table again
at the second submit and changes the values of part 1.

The rule, the same on the server (`AtlasServerEffects`) and on the client (`atlas-effects.ts`):

- An effect (`Set`, `Clear`, `SetFromOption`) sets values only when its source control is visible and enabled for the
  user and the state (the effective form).
- When the source control is disabled or hidden, the effect sets nothing. The saved values stay in the record and
  show on the form.

Result for ClientInflows:

- First submit: the account control is enabled. The server reads the AIN row and saves `AccountName`, `StarNumber`,
  `RelationshipManager`, `Custodian` and `RMCC`.
- After the first submit: the account control is read-only. AIN is not read again. A later change in AIN does not
  change the record, the form or the recipients of E3 (Q8, D16).

TTDV2: no change for a user who can edit, because the source control is then enabled. A user who cannot edit sees
the saved values. Build step 2 runs all TTDV2 tests. If a TTDV2 effect has a source control that is disabled for a
user who can save, stop and ask.

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
- When the user can change the control, the provider sends all rows with the form load, as the TTDV2 account list
  does today. The option list of a table is cached for a short time (60 seconds) in memory, so many form loads do not
  read the table each time.
- When the user cannot change the control (disabled or read-only in the effective form), the server sends only the
  saved option: the value and its label, with no `Data` (DR-18). This is a rule for each dropdown with an options
  source, not only for the Table provider. A `Read` user and each user after the first submit thus do not get the RM,
  the custodian and the RMCC of all accounts.
- `KeepSavedValue: true` on the control keeps a saved account in the list after the AIN row is deleted.

### 7.2 Use in other apps

Another app uses the same provider with its own `Data` columns. It adds `AIN.AccountInventory` to its `LookupTables`.
No code is necessary.

### 7.3 Data-only effect target

`SetFromOption` can name a field that has a column and no control (here `RMCC`). The server effect sets the column
from the option data. The client does not show it and does not send it. The start-up check accepts a target that is a
control or a column of the app table, and refuses other names. The rule of 6.5 also applies to a data-only target.

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
- `AtlasSqlTypes` gets the type `varbinary`, so the platform can read and write `FileContent` (DR-06). This is
  generic.
- `SavePage(input, files)` reads the files. A file is accepted only when its control is a `filepicker` that is
  visible and enabled in the effective form. A `filepicker` control has no `Field` and no column, so the check reads
  the state of the control itself, not the field allow list (DR-20). The start-up check does not report "no column"
  for a `filepicker`.
- Limits: 10 MB for each file, 25 MB for the new files of one save, the default type list, the file signature check
  (module 8, 4.3). The file name is `Path.GetFileName`, 255 characters at most.
- New optional request field `RemovedAttachmentIds`. The server removes (soft delete) only the IDs that belong to this
  record and to an enabled `filepicker`.
- `Required` on a `filepicker`: after the save, the control has one file at least (files that stay + new files).
- All of this is in the transaction of the record. Each added file gets one audit entry with the event
  `AttachmentAdd`, and each removed file one with `AttachmentRemove`. These are the event names of module 9 (DR-15).
- Load: the files of the record, grouped by control name. For each file: ID, name, size, created by and created
  date. No download URL (DR-05). The list query does not select `FileContent`; only the download reads it (DR-06).
- New controller `App/Platform/AtlasAttachmentController.cs`: `GET v1/Attachment/{appCode}/{id}`. It checks `View` on
  the parent record, then streams the decompressed file with `Content-Disposition: attachment` and
  `X-Content-Type-Options: nosniff`. The server sets the content type from the file extension, not from the value
  that the client sent with the upload (DR-17). An unknown ID, a deleted file or a deleted record gives 404.

### 8.3 Client

- New renderer `filepicker` in `form/renderers/`, registered in `atlas-renderer-registry.ts`. It uses the target file
  control of `@pbdev/pbdev-core`; the look follows the CDT step page. The stand-in has no file picker today: build
  step 4 makes the stand-in file picker from the real target source (DR-20).
- The form sends the files as form parts `<ControlName>#$#<FileName>`, the format that the target uses today.
- Remove: the question dialog, then the file shows as "removed when you save".
- The client checks the size limits before it sends. The server checks again.
- Download (DR-05): the client gets the file with `HttpClient` as a blob and then gives the blob to the browser. A
  plain browser link is not used: the target API uses JWT bearer authentication and has a different address than the
  client, and only an `HttpClient` call goes through the target interceptors. Build step 4 checks how the CDT
  download works at the target and follows it.
- A disabled `filepicker` shows the list with the download action only.

## 9. Rich text (B2, B9)

### 9.1 Client

- New renderer `richtext`. It loads CKEditor 5 with a dynamic `import()`, so the editor code is only loaded for a form
  that has a `richtext` control.
- Packages: `ckeditor5` and `@ckeditor/ckeditor5-angular`, the latest versions at build time that support Angular 21,
  with fixed version numbers. `licenseKey: 'GPL'`.
- Plugins: paragraph, headings, bold, italic, underline, strikethrough, lists, link, table, block quote, undo, paste
  from Office. No picture plugin and no upload adapter (D12, D31). A pasted picture is dropped.
- A disabled control shows the editor in read-only mode with no toolbar.
- The risk is tested first (DR-11): build step 0 adds the two packages to the stand-in, shows one editor, and runs
  `ng build` (0 warnings, bundle size limits) and `ng test`. The editor style sheet must load with no change to
  `angular.json`, because this target file is not in the approved list. If the test fails, stop and ask (the
  fallback is Jodit).

### 9.2 Server

- On save, each `richtext` value goes through `HtmlSanitizer` before validation and before the write. The check is by
  control type, so each app gets it.
- Allowed tags: `p`, `br`, `strong`, `b`, `em`, `i`, `u`, `s`, `h2`, `h3`, `h4`, `ul`, `ol`, `li`, `blockquote`, `a`,
  `figure`, `table`, `thead`, `tbody`, `tr`, `th`, `td`.
- Allowed attributes: `href` on `a`; `colspan` and `rowspan` on cells; `class` only on `figure` and only with the
  value `table` (DR-17). No `style`, no `img`, no event attributes.
- Allowed link schemes: `http`, `https`, `mailto`. The server adds `rel="noopener noreferrer"` to each link.
- The list is one constant in the platform. It is not in the app JSON, so an app cannot make it weaker.
- A value that is empty after the clean-up is saved as NULL.
- Largest size: 200,000 characters after the clean-up; more gives a field error.
- The audit entry holds the old and the new value as plain text (tags removed, entities decoded). The History window
  shows plain text.
- The dashboard shows a `richtext` column as plain text, with the same converter. (P2 has no such column.)
- Rich text is not in the emails of P2.
- Package version: a stable `HtmlSanitizer` version whose dependencies (`AngleSharp`, `AngleSharp.Css`) are also
  stable versions. Build step 5 verifies this (DR-17).

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

Optional properties on the `Workflow` object of the section (partial class of the target `CPWorkflow`). `Key`,
`Roles` and `Actions` are necessary target properties and stay; the platform does not read `Roles` and `Actions`
(DR-03).

```json
"Workflow": {
  "Key": "ClientInflow",
  "Roles": [],
  "Actions": {},
  "BusinessObjectPrefix": "CFP-INFLOW-",
  "ActorRoles": "User",
  "StateField": "WorkflowState",
  "StatusField": "Status",
  "InitialState": "Draft",
  "StatusLabels": { "Draft": "Draft", "Submitted": "Submitted", "FundsReceived": "Funds Received" },
  "Steps": {
    "Draft": {
      "EngineActions": [
        { "Start": "Submit", "Recipients": [ "icg" ] },
        { "Execute": "NotifyInformational", "Recipients": [ "investmentLists", "relationshipManager" ] }
      ]
    },
    "Submitted": {
      "Set": { "CashSubmittedBy": "{User.LanID}", "CashSubmittedDate": "{Now}" },
      "EngineActions": [
        { "Execute": "ConfirmFunds",
          "Recipients": [ "investmentLists", "relationshipManager", "csTeam", "rmcc", "imrg", "starFasIfiCash" ] }
      ]
    }
  },
  "Recipients": {
    "icg": { "List": "Icg" },
    "csTeam": { "List": "CsTeam" },
    "imrg": { "List": "Imrg" },
    "starFasIfiCash": { "List": "StarFasIfiCash" },
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

- `Steps`: state key to one step. The key is the state of the record before the submit. A state with no entry has no
  submit (6.4).
- A step has `EngineActions` (necessary, one or more), and the optional keys `Set` and `Label` (6.4).
- `EngineActions`: the engine actions that the adapter runs for one submit, in order. `Start` starts the engine
  instance with this initial action; `Execute` runs an action on the instance. The first action can change the
  state. Each later action must keep it.
- `Recipients` of an engine action: the recipient keys that the adapter sends with this action. The engine refuses a
  key that the route does not declare, so each action names its own keys.
- Names against the earlier specs (DR-15): for a platform app, `Steps` replaces the key `InitialAction` of the
  binding and the key `Execute` of a button (module 1, sections 5.3 and 5.10). The reason: one submit can run more
  than one engine action, and the server selects the step from the state.
- The start-up check refuses: a `Steps` key that is not a key of `StatusLabels`; a step with no engine action; a
  `Start` in a step that is not the step of `InitialState`; a recipient key with no definition; a field name that is
  not a column; a `Map` value that is not an option of the field; a list key with no address in the settings.

### 10.3 Save order

For a save of a form with steps (each such save is a submit, 6.4):

1. The request has no `RequestId`: refuse the save (page error). The state has no step: refuse the save
   (`NotAllowed`).
2. Validate (rules, access, fields, files). Find the recipients (10.6) and the template data (10.7). No write.
3. Begin the transaction. Write the record with the `Set` values, the audit entries and the attachments.
   `SaveChanges`, no commit.
4. Run the first engine action (`Start` with the initial action, or `Execute`).
5. Engine `Success` is false: roll back. Return the engine code and message as a page error. Nothing changed.
6. Engine `Success` is true: write `WorkflowState` (the engine state key) and `Status` (the label of
   `StatusLabels`). Add one status line to the audit entry of this save (12). Commit.
7. After the commit, run each later engine action in order. After each one, compare the engine state with the
   `WorkflowState` of the record; a difference is an error log (DR-19). Then dispatch the notifications of all
   actions.

Failures after the commit do not undo the save:

- A later action fails (E2): the record stays saved. The adapter writes the error log `Atlas.WorkflowStepFailed` with
  the instance ID and the action key. The response says "Saved. One notification was not sent." No automatic second
  try in P2. The target support team acts on this log event: it sends the email by hand (DR-19).
- A dispatch fails: warning log; the engine outbox sends the email again (module 10, 4.5).
- The commit of step 6 fails after the engine committed: error log. Section 10.8 gives the two cases and the action.

Double submit (DR-16):

- Request field `RequestId`: a GUID that the client makes for each user action. It is optional in the `v1/ManagePage`
  contract, but a submit of a form with steps needs it (step 1). With no `RequestId`, the protection is lost, so the
  server does not make one itself.
- The idempotency key of engine action n is `RequestId` + `:` + n.
- The engine key is global and the engine compares the request hash. A second request with the same `RequestId` for
  a new record gets a second record ID, so the engine refuses it and the second record is rolled back. The adapter
  maps this engine result to the page error "This request was already processed."

### 10.4 Load

When a record with a binding loads, the adapter reads the engine context. If the engine state is not the
`WorkflowState` of the record, the platform writes the engine state and the status label to the record, writes one
audit entry (`WorkflowSync`) and a warning log. The engine is the source of truth for the state.

Each record with no engine instance is not checked (DR-21). This is a new record, and also a record that the target
team moved from SharePoint; such a record loads with no error.

### 10.5 Actor roles

`ActorRoles: "User"`: the adapter sends the names of the active roles of the user in the app, from the active roles
view. The engine role `Write` has the same name as the app role.

### 10.6 Recipients

| Source | Meaning |
|---|---|
| `List` | One fixed list key. Each key has one or more addresses in `AppSettings:Modules:<Code>:Workflow:Lists:<Key>` (separated by `;`). |
| `ListByField` + `Map` | The value of the field selects one or more list keys. The addresses come from the same setting as for `List`. |
| `FromNameField` | The field holds display names, separated by `;`. Each name is found in `CP_Atlas_User`. |
| `FromUserField` | The field holds a LAN ID (module 10, 4.6). Designed, not used in P2. |

Display name rules (D33, D34). The service `IAtlasUserDirectory` exists already; it gets one new method that finds
the email of a display name. No new service (DR-15).

- The compare ignores case, and spaces at the start, at the end and doubled.
- Exactly one active user (`IsDeleted` is not 1) with an email: use the email.
- No user, a user with no email, or two or more users: leave the name out. Write the warning log event
  `Atlas.RecipientSkipped` with the app, the record ID, the recipient key and the name.
- `Optional: true`: the step continues with no address for this key. `Optional: false`: an empty result is a field
  error and the save stops (before the first engine call).
- The adapter removes duplicate addresses.

The recipients are found in step 2 of 10.3, from the values of the record. At the second submit, part 1 is read-only
and the effects do not run again (6.5), so these are the saved values. Thus E3 uses the `RelationshipManager` and
`RMCC` of the first submit (Q8).

### 10.7 Template data

- `Field` + optional `Format`: the value of the record as text. `Format` is a .NET format text, used with the
  `en-US` culture. A date field is formatted as stored (no time zone change for a date with no time).
- `RecordLink`: `AppSettings:HostedClientURL` + `/form/<Code>/<ID>` (DR-13). The target has this setting already,
  with no slash at the end, and uses it for the links in the CDT and SQMS emails. No new setting. The start-up check
  gives an error when a binding uses `RecordLink` and the setting is empty.
- The adapter sends the same template data with each engine action.
- Empty value (DR-10): the engine refuses a template token with an empty value, with a technical message. So the
  adapter checks in step 2 of 10.3 that each `TemplateData` value is not empty. An empty value is a page error that
  names the field, for example "Account Name is necessary for the notification." Nothing is written.

### 10.8 Accepted limits

- A deleted record in state `Submitted` keeps its engine instance. No email goes out (no SLA). Accepted (gap N3).
- E1 and E2 are two engine commits. A failure between them sends E1 and not E2. Accepted with Q2.
- The engine commits and then the Atlas commit fails (DR-12). This needs a lost database connection at the exact
  moment of the commit; it is rare. The state check on load (10.4) cannot correct the two cases below. The error log
  `Atlas.WorkflowStepFailed` gives the instance ID, and the target team corrects the data by script.
  - First submit: the record does not exist. The engine keeps an instance with no record, and E1 goes out with a
    link that opens nothing.
  - Second submit: the check on load sets the record to "Funds Received", but the part 2 values and files are not
    saved. The record is closed with an empty part 2, and E3 is sent.

## 11. `--atlas-check` (B8)

- New severity `Info` in `AtlasStartupCheck`. An Info line is printed and logged. It is not counted: the summary stays
  `0 error(s), 0 warning(s)` and the exit code does not change. Info lines are never a reason to disable an app.
- Generic data report: for each Table provider with `UserNameColumns`, the check reads the not-deleted rows and
  lists each row where a name (split by `;`) does not match exactly one active user with an email. Format:
  `Info: AIN.AccountInventory AccountNumber 12345: RelationshipManager "John Smith": no user`.
- The report prints 200 lines at most for each table, then the count of the rest.
- The report runs only with the `--atlas-check` switch, not at each start-up of the host.

## 12. Export, currency format and History

- Export (Q9): the target registers `AllEnterpriseModule`, so each grid has the built-in menu with "Export" and
  "Excel Export". No platform button and no new package (14.1, answer 5). The local stand-in has
  `ag-grid-community` only: the Excel export is tested at the target.
- Currency format (D23, DR-09): today the column type `currency` only aligns the cell to the right. New: a generic
  formatter for `currency` columns in `dashboard/atlas-dashboard-columns.ts`: `$`, thousands separator, 2 decimals
  (`$1,250,000.00`). TTDV2 has no `currency` column, so it does not change. Build step 7 checks at the target if
  the Excel export gives the formatted text or the number, and the users select what they want.
- History (D27): the audit writer of today records each field of the form. New:
  - the entries for files, `AttachmentAdd` and `AttachmentRemove` (8.2);
  - plain text for `richtext` (9.2);
  - `WorkflowSync` (10.4);
  - one status line in the audit entry of each submit, for example "Status: Submitted to Funds Received" (DR-08). A
    new record gets "Status: Submitted".
- `CashSubmittedDate` and `RMCC` do not show in History (DR-08, answer A). The date of the History entry is the cash
  submitted date, and `RMCC` is not on the form.
- No workflow history on the form.

## 13. Build order

Each step ends with the platform gates and one commit. No push.

| Step | Content | Proof |
|---|---|---|
| 0 | Two tests, no product code. (a) Engine: an action that keeps the state runs and makes its outbox row; no engine change. (b) CKEditor 5 with Angular 21 in the stand-in: one editor, `ng build` with 0 warnings, `ng test`, no `angular.json` change. | (a) fails: stop and ask (Q2 falls back to its option B or C). (b) fails: stop and ask (fallback Jodit). |
| 1 | `release-p2` scripts 100 to 140, local tables and seed, manifest, form with the controls of today, dashboard with `Menus` and the `currency` formatter. | The app shows in the menu; a plain record saves; SQL tests for each script, run two times; a `Read` user gets "Edit" in the row menu and no "Add New" button. |
| 2 | Table provider, `LookupTables`, data-only effect target, the effects rule (6.5), the saved option rule (7.1), `Info` severity, name report. | Account list fills 4 fields + `RMCC`; a changed lookup row does not change a record whose control is read-only; a read-only user gets one option; `--atlas-check` prints the Info lines and `0 error(s), 0 warning(s)`; TTDV2 tests pass. |
| 3 | State in the rules, `MustBeTrue`, one submit button, `Steps` by state with `Set` and `Label` (no engine call yet). | Part 2 hidden in `Draft`; a save in a state with no step is refused; refusals tested; TTDV2 tests pass with no change. |
| 4 | Attachments: `varbinary` type, service, save, removal on save, limits, audit, controller, stand-in file picker from the target source, `filepicker` renderer, blob download. | Module 8 section 7 acceptance for a platform app; the list query has no `FileContent`. |
| 5 | `richtext`: packages, renderer, `HtmlSanitizer`, plain text in History. | Script and event attributes are removed (API test with raw HTML); stable package versions. |
| 6 | Engine adapter: script 150, binding, engine actions, recipients, template data with the empty value check, state check, `RequestId`, status line in History. | Module 10 section 8 acceptance; scenarios 1 and 2 with smtp4dev. |
| 7 | `INTEGRATION.md`, `CODE_TRACE.md`, `Config/README.md`, target package, onboarding template update. | Scenarios 1 to 6 of the requirements; all gates; package rebuilt. |

### 13.1 Tests

- .NET (xUnit, SQL Server): each script is idempotent; provider SQL is safe for bad names; rule context by state;
  `MustBeTrue`; `Set` tokens, with a control name and with a column name; a client value for a server-set field is
  refused; attachment limits, signature, control check, removal of a file of another record refused; download access
  (403, 404) and content type; HTML clean-up cases; name match cases (case, spaces, none, two users, no email); list
  map; template formats; an empty template value is a page error with no write.
- .NET, the review rules: an effect of a disabled source control sets nothing (lookup row changed after the first
  submit; the record, the form and the E3 recipients keep the saved values); a disabled dropdown gets only the saved
  option; a form with steps and no `Save` button, or two, fails the start-up check; a save in a state with no step
  is refused; a submit with no `RequestId` is refused; the `Add` button is hidden for a `Read` user and the server
  refuses their new record.
- .NET, the adapter: save order with a refused engine action (nothing changed); follow-up action failure (record
  saved, error log); state difference after a later action (error log); state sync on load; a record with no engine
  instance loads; double submit with one `RequestId` gives "This request was already processed."; the status line
  in the audit entry.
- Angular (Vitest): `filepicker` and `richtext` renderers; `MustBeTrue` with the shared cases; the submit sends a
  `RequestId`; state rules hide part 2; effects do not run for a disabled source control; the `currency` formatter;
  the blob download.
- Regression: all TTDV2 tests pass with no change to their expected results.
- `CodeCommentRulesTests` stays green: no D, Q, B, DR or module number in a code comment.

## 14. Items that needed your approval

These were the stop-and-ask items of draft 2. The answers Q1 to Q9 already cover the engine adapter, the
`nvarchar` change of `Description` and `Comment`, `HtmlSanitizer`, CKEditor and the `RMCC` columns.

| # | Item | Recommendation |
|---|---|---|
| 1 | `v1/ManagePage` gets three optional request fields: `Button`, `RequestId`, `RemovedAttachmentIds`. New route `GET v1/Attachment/{appCode}/{id}` (decided in module 8). All additive. | Approve |
| 2 | `CFP.InflowAttachment.FileName` from `varchar(1000)` to `nvarchar(1000)`, so a file name keeps its characters. The table has no platform data yet. | Approve |
| 3 | Target files that change (integration steps, the user does them): `program.cs` (one `AddAtlasModule` line), `package.json` (the two CKEditor packages replace the six 41.4.2 packages, after the target team checks its own use of them), the Service `.csproj` (`HtmlSanitizer`), `appsettings.json` (the six list addresses, `WebBaseUrl`), `tailwind.config.js` only if the form uses a new class. | Approve |
| 4 | New log events 41025 to 41029: `Atlas.RecipientSkipped`, `Atlas.WorkflowStepFailed`, `Atlas.WorkflowSync`, `Atlas.NotificationDispatchFailed`, `Atlas.AttachmentRefused`. New identifiers, none changed. | Approve |
| 5 | Export: the built-in grid menu, no new button. | Built-in menu only |
| 6 | Where the four fixed lists (ICG, CS Team, IMRG, Star FAS IFI Cash Team) live: static addresses in the engine script, or the app settings with the two Investment Type lists. | See the answer |

### 14.1 Answers

| # | Answer | Date |
|---|---|---|
| 1 | Approved: the three optional request fields and the new download route. | 2026-11-20 |
| 2 | Approved: `CFP.InflowAttachment.FileName` changes to `nvarchar(1000)`. | 2026-11-20 |
| 3 | Approved: the target file changes (`program.cs`, `package.json`, the Service `.csproj`, `appsettings.json`, and `tailwind.config.js` only if necessary). | 2026-11-20 |
| 4 | Approved: the log events 41025 to 41029. | 2026-11-20 |
| 5 | The built-in grid menu only. No export button. | 2026-11-20 |
| 6 | All six lists are in the app settings. The engine script has no email address; each recipient rule is a request input. | 2026-11-20 |

### 14.2 What draft 3 changes in the approved items

Each change makes an approved item smaller. Draft 3 has no new stop-and-ask item.

| Item | Change | Reason |
|---|---|---|
| 1 | The request field `Button` is not built. `v1/ManagePage` gets two optional fields: `RequestId` and `RemovedAttachmentIds`. | One submit button (DR-02): the server selects the step from the state. |
| 1 | `RequestId` stays optional in the contract. Only a submit of a form with steps needs it. No current caller has such a form. | DR-16 |
| 3 | `appsettings.json` gets only the six list addresses. The setting `WebBaseUrl` is not added. | The target setting `AppSettings:HostedClientURL` exists (DR-13). |
| 4.1 | The index `IX_Inflow_WorkflowState` is not made. | DR-14 |

## 15. Target integration steps (for `INTEGRATION.md`)

1. Prerequisites: the engine migrations are current and the engine scheduled job with the outbox retry runs
   (module 10, 4.5; module 11).
2. Run `sql/release-p2/` 100 to 150 in number order. The AIN team gets `140`.
3. Copy the platform folders as for P1, plus `Config/CFPINFLOW.app.json`, `CFPINFLOW.json`,
   `CFPINFLOW.Dashboard.json`.
4. Do the target file changes of section 14, item 3, with the change of 14.2.
5. Settings in each environment: `AppSettings:Modules:CFPINFLOW:Workflow:Lists:<Key>` for the six keys `Icg`,
   `CsTeam`, `Imrg`, `StarFasIfiCash`, `FixedIncome`, `Equity`. A missing address is a start-up check error. Make
   sure that `AppSettings:HostedClientURL` has the address of the client, with no slash at the end.
6. Grant the roles `Read` and `Write` with the grant template.
7. Run `--atlas-check`. Read the Info lines: each line is an RM or RMCC name that gets no email. Correct the data in
   AIN or in `CP_Atlas_User`.
8. Restart the app pool.
9. Support: the log event `Atlas.WorkflowStepFailed` needs an action. For a failed notification, send the email by
   hand. For a failed commit (10.8), correct the data by script.
10. The platform app writes `CFP.Inflow.AccountNumber` and does not use the column `Account`.
11. Old data: the target team moves the SharePoint records with LAN IDs in the person columns, UTC dates,
    `WorkflowState` and `Status` filled. A moved record has no engine instance and loads with no error (10.4). A
    moved record in state `Submitted` cannot do the second submit with no instance; this needs a decision of the
    target team before the move (start an instance in the move script, or move only closed records).

## 16. Backward compatibility

- No engine code change. No change to an existing engine key; `ClientInflow` and its keys are new.
- TTDV2: no JSON change. No change for a user who can edit. Two generic rules change what a user gets who cannot
  change a control: the form shows the saved values, not the values of the lookup row of today (6.5), and a disabled
  dropdown gets only the saved option (7.1). The TTDV2 tests must pass with no change to their expected results.
- The other new code paths run only for a form that has `Steps`, a `filepicker` or a `richtext`, or for a dashboard
  with a `currency` column.
- TTD, CDT, SQMSO, SQMSB, AIN: no change. The platform does not write to `AIN.AccountInventory`.
- `v1/...`: additive only (14.2).
- `CFP.Inflow` and `CFP.InflowAttachment`: no column removed or renamed. `CFP.Outflow` and `CFP.OutflowAttachment`
  are not touched.
- Rollback: set `AppSettings:Modules:CFPINFLOW:Enabled = false` and restart. The tables and the engine metadata stay.

## 17. Open points for the build (no decision necessary now)

- The email text is typed from the screenshots in build step 6; the user checks the three emails in smtp4dev.
- The CKEditor version numbers are fixed in build step 0, and the `HtmlSanitizer` version in build step 5. They are
  written to `INTEGRATION.md`.
- The way the local setup makes the engine tables is fixed in build step 0.
- The exact JSON of the rules (5.2) and of the `Menus` (5.3) is fixed against the tests in build steps 1 and 3.

## 18. Changes from draft 2

Each row is one finding of the review (module 26) with its answer.

| Finding | Answer | Change in this document |
|---|---|---|
| DR-01 | Accepted, A | New rule 6.5: an effect sets values only when the user can change the source control. 2.1, 7.3, 10.6. |
| DR-02 | Accepted with a change | One submit button, steps by state: 2.1, 5.2 (buttons), 6.4, 10.2, 10.3. `SubmitPart1`, `SubmitPart2`, the request field `Button` and `Permissions.Execute` are gone. |
| DR-03 | Accepted | 5.2 and 10.2: `Workflow` in the `Section`, `"Roles": []`, `Visible` and `Disabled` on each button. |
| DR-04 | Accepted | 5.3: `Menus` on each tab. |
| DR-05 | Accepted | 8.2, 8.3: download with `HttpClient` as a blob; the load response has no URL. |
| DR-06 | Accepted | 8.2: `varbinary` in the type map; the list query has no `FileContent`. |
| DR-07 | Accepted | 4.1, 5.2: the control `AccountNumber` writes the column `AccountNumber`; the column `Account` is not used. |
| DR-08 | Accepted, A | 6.4: `Set` accepts a column name. 12: one status line in History; no line for `CashSubmittedDate` and `RMCC`. |
| DR-09 | Accepted | 12: generic `currency` formatter. |
| DR-10 | Accepted | 10.7: empty template value is a page error before the first write. |
| DR-11 | Accepted | 9.1, 13: the CKEditor test is in build step 0. |
| DR-12 | Accepted | 10.8: two failure cases with the support action. |
| DR-13 | Accepted | 10.7, 14.2, 15: `AppSettings:HostedClientURL`; no new setting. |
| DR-14 | Accepted | 4.1: no index on `WorkflowState`. |
| DR-15 | Accepted | 10.2: `Steps` replaces `InitialAction` and the button `Execute` key. 8.2: event names of module 9. 10.6: one new method on `IAtlasUserDirectory`. |
| DR-16 | Accepted | 10.3: a submit needs a `RequestId`; message "This request was already processed." |
| DR-17 | Accepted | 9.2: `class` rule, `rel`, stable versions. 8.2: content type from the extension. |
| DR-18 | Accepted, A (in P2) | 2.1, 7.1: a disabled dropdown gets only the saved option. |
| DR-19 | Accepted | 10.3: state compare after a later action; who acts on `Atlas.WorkflowStepFailed`. |
| DR-20 | Accepted | 8.2, 8.3: stand-in file picker from the target source; the control check for a `filepicker`. |
| DR-21 | Accepted | 10.4: each record with no engine instance is not checked. |
| User note | 2026-11-20 | 2.1, 5.3: no "Add New" button for a read-only user (in the code today; stated and tested). |

## 19. Build record

### 19.1 Step 0 (2026-11-20)

(a) Engine test, `tests/WorkflowEngine.Tests/WorkflowStateKeepingActionTests.cs`. No engine code change.

- An action whose transition keeps the state runs. The state stays, the instance version goes up by one, and the
  action queues its outbox row with the request-input recipients and the rendered template.
- The same request again is a replay: no second outbox row, no version change.
- After the state-keeping action, the next action changes the state and completes the instance.
- Result: answer Q2 (option A) works as designed. All engine tests pass (567 passed, 5 SMTP tests skipped with no
  smtp4dev).
- Note for build step 6: for an `Execute` call, the engine renders the templates from
  `envelope.Workflow.NotificationContext`. `NotificationInput.TemplateData` is used only by `Start` and the request
  hash. The adapter copies the template data into `NotificationContext` for each `Execute`, as the SQMS service does.

(b) CKEditor 5 with Angular 21, in the stand-in (a probe page, removed after the test).

| Item | Result |
|---|---|
| Versions | `ckeditor5` 49.1.0 and `@ckeditor/ckeditor5-angular` 12.0.0, fixed versions. Wrapper 13.0.0 needs Angular 21.2.25 or later; the target `package.json` allows 21.1.1, so 12.0.0 (Angular 20.3.25 or later) is the safe choice. |
| Other packages | The install adds only CKEditor and its own dependencies. No version of an existing package changed. |
| Imports | The renderer imports the plugins from their own packages (`@ckeditor/ckeditor5-basic-styles`, ...), not from `ckeditor5`. An import from `ckeditor5` gives two CommonJS warnings (emoji and Markdown dependencies), and only an `angular.json` change can stop them. The plugin packages come with `ckeditor5`, at the same version; `package.json` gets no other package. |
| Lazy chunk | 823 kB (181 kB transfer), loaded only for a form with a `richtext` control. The first bundle does not change. |
| Style sheet | `@use 'ckeditor5/ckeditor5.css'` in `atlas-platform.scss`, which the target already lists in `angular.json`. No `angular.json` change. It adds 289 kB (34 kB transfer) to the global styles. The import is added in build step 5 with the renderer. |
| `ng build` | 0 warnings with the plugin imports. |
| `ng test` | The editor runs in Vitest (jsdom) with a `ResizeObserver` stub in the spec. It removes a `<script>` tag. |

Conditions for the target (for `INTEGRATION.md`, build step 7):

- The six 41.4.2 packages and `@ckeditor/ckeditor5-angular` 7.0.1 must go when the two new packages come (approval
  3). If an old `@ckeditor/ckeditor5-*` package stays, the plugin imports find the wrong version.
- The target build budget warns at 2 MB for the first bundle (the stand-in at 4 MB). The style sheet adds 289 kB.
  The target team checks its first bundle size at integration.

(c) Engine tables in the local database.

- `setup-local-db.ps1` (build step 1) makes the engine tables with the engine tooling of today:
  `dotnet ef migrations script --idempotent` (project `WorkflowEngine.Infrastructure`, start-up project
  `WorkflowEngine.Console`), then runs the script with `sqlcmd`. The script is made at each setup, so it always
  follows the engine migrations. Checked: the script has the 21 engine tables and no sample data.
- Not used: the console command `init-db`. It also loads the Portfolio Trade sample workflow.
