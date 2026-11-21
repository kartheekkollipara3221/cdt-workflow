# Module 27: review of the ClientInflows (P2) code

Status: 2026-11-20. Independent review of the P2 build, commits `ca194af` to `f9b5bc5` (build steps 0 to 6). Review
only. No code, no SQL and no design text is changed by this review. The user decides which findings change the code.

## 1. Method

The review compares the code with:

- `AGENTS.md`, section "Atlas platform" (the rule set);
- [25-client-inflows-p2-design.md](25-client-inflows-p2-design.md) (the approved design; section 19 = the build record);
- [26-p2-design-review.md](26-p2-design-review.md), section 8 (the accepted answers);
- [10-engine-adapter.md](10-engine-adapter.md) and [08-attachments.md](08-attachments.md).

Read: each product file of `git diff ca194af^ f9b5bc5` in `atlas-platform/`, and the tests. The workflow engine
(`src/`) was read only to see how the platform uses it. The diff has no change in `src/` and `workflow-api/`.

Paths: `Service/` = `atlas-platform/src/BBH.CP.Atlas.Service/`, `App/` = `atlas-platform/src/BBH.CP.Atlas.App/`,
`WebUi/` = `atlas-platform/WebUi/src/app/platform/`, `Tests/` = `atlas-platform/tests/BBH.CP.Atlas.Platform.Tests/`.
The line numbers are the lines at commit `f9b5bc5`.

### 1.1 Gates

All gates of `AGENTS.md` ran on the code of `f9b5bc5`. No local API was in operation.

| Gate | Result |
|---|---|
| `dotnet build ... -warnaserror` | 0 warnings, 0 errors |
| `dotnet test` | 435 passed, 0 failed |
| `--atlas-check` | `0 error(s), 0 warning(s)`, 3 Info lines, exit code 0 |
| `ng build` | complete, 0 warnings |
| `ng test --watch=false` | 127 passed |
| `npm run check:target-flags` | clean |

### 1.2 Probes

Some findings need a run, not only a read. For these, the review used two small programs in a temporary folder
outside the repository:

- They call the real platform classes (`ConfigSectionService`, `AtlasRichText`, `AtlasAttachmentRules`,
  `AtlasConfigLint`) and, where the text says "real engine", the real workflow engine with script 150.
- They use a throw-away database that the test fixture `SqlServerDatabaseFixture` makes and drops.
- A probe that needs another form JSON changes a copy of the `Config` folder in a temporary folder.
- No file of the repository changed. `CPAtlasPlatformDev` was not changed; only the gate `--atlas-check` read it.

Each finding says "Evidence: code" (the code was read) or "Evidence: probe" (a probe ran and gave the result).

## 2. Result

The build follows the design in its structure. The save order of design 10.3 is correct, with one exception (CR-01).
The access checks, the file checks, the HTML clean-up and the SQL from metadata names gave no defect. The C# and
TypeScript code has no app name. All TTDV2 tests pass with no change.

The review found 14 points:

| Severity | Count | Meaning |
|---|---|---|
| High | 1 | A failed save leaves a change in the engine, in a case that the design does not accept |
| Medium | 3 | A wrong result for the user, or a generic rule that is broken for the next app |
| Low | 10 | Small risk, a check that is missing, a test gap or a comment that is not correct |

| ID | Severity | Area | Defect |
|---|---|---|---|
| CR-01 | High | Save path | The audit entries are written after the engine commit |
| CR-02 | Medium | Files, generic rule | The server ignores the effects that change a file picker |
| CR-03 | Medium | Client, idempotency | A second click after a lost answer makes a second record |
| CR-04 | Medium | Options, generic rule | A dropdown that an effect enables gets no options |
| CR-05 | Low | Start-up check | The check accepts a control name where the adapter needs a column |
| CR-06 | Low | Security | The History shows the file names of a file picker that a rule hides |
| CR-07 | Low | Idempotency | The server does not check the format of `RequestId` |
| CR-08 | Low | Files | A file name can have control characters |
| CR-09 | Low | Logs | `Atlas.RecipientSkipped` has record ID 0 at the first submit |
| CR-10 | Low | Tests | No test finds an email from a display name |
| CR-11 | Low | Tests | Three tests or test groups prove less than their names or the design say |
| CR-12 | Low | Comments | Three comments say something that the code does not do |
| CR-13 | Low | Security | The load response sends the engine binding to the browser |
| CR-14 | Low | Files, audit | A display name that is longer than the column stops each save with a file |

CR-02, CR-04 and CR-06 do not change ClientInflows or TTDV2 as they are today. They are defects of the generic
platform: the next app can get them with JSON only.

## 3. Findings

### CR-01 (High) The audit entries are written after the engine commit

- **Where:** `Service/Application/ConfigSectionService.cs`, `SaveWithAudit` (lines 930 to 944) and `Persist`
  (lines 736 to 741). `Service/Application/ConfigSectionService.Workflow.cs`, `RunFirstEngineAction` (lines 72 to 103).
- **Defect:** The save writes all audit rows after the first engine action, but design 10.3 (step 3) writes them
  before it.
- **Failure case:** The insert into the audit table fails. Examples: the API login has no INSERT permission on the
  new table `CFP.AuditLog`, or the database gives a time-out. Result:
  - The user gets "The record was not saved." The record and the files roll back.
  - The engine keeps the new instance in state `Submitted`. The E1 email stays in the outbox, and the engine job
    sends it. The link in the email opens nothing.
  - Each new try of the user makes one more instance and one more email.
  - A second cause, for a form with no required file: the user has a display name of more than 50 characters.
    `AuditLog.CreatedBy` is `nvarchar(50)`, so the audit insert fails at each submit of this user. (ClientInflows
    stops earlier, at the file row, before the engine call. See CR-14.)
- **Evidence: code.** The order in `SaveWithAudit` is: record `SaveChanges` (line 931); `afterRecord` (line 934),
  which writes the files, runs the engine action and saves the state; then `Audit.Add` (lines 937 to 939) and a
  second `SaveChanges` (line 941); then the commit. The comment at line 739 says "the engine call is the last write
  before the commit". This is the intent of the design, but it is not what the code does.
- **Evidence: probe (real engine).** A CHECK constraint on `CFP.AuditLog` refused each new row. Then a first submit:

  ```text
  save: ID=0 Errors=[|SaveFailed|The record was not saved. Contact support if this occurs again.]
  CFP.Inflow rows: before 0, after 0
  engine rows: WF-CFP-INFLOW-10000 state=Submitted
  outbox: WF-CFP-INFLOW-10000 ClientInflow.ActionRequired Pending
  log: Atlas.WorkflowStepFailed ... Reason=AtlasCommitFailed: the engine changed the state, the record was not saved
  ```

  A second probe gave the same result with no constraint: a user with a display name of 60 characters, and a copy
  of the form in which `FlowAttachments` is not required (no file sent).
- **Why it is not an accepted limit:** Design 10.8 accepts this end state only for a lost connection at the moment
  of the commit. Here an ordinary write error gives it, and the error can occur at each submit.
- **Proposed correction:** Write and save the audit entries of the fields and of the files before the engine call.
  After the engine call, write only the state columns and the status line.

### CR-02 (Medium) The server ignores the effects that change a file picker

- **Where:** `Service/Application/ConfigSectionService.Attachments.cs`, `FilePickers` (lines 47 to 58), used by
  `ReadNewFiles` (line 138), `CheckFileCounts` (lines 208 to 218), `LoadAttachments` (line 74) and `OpenAttachment`
  (line 296). `Service/Platform/Sections/AtlasServerEffects.cs`, `Targets` (line 134). Client:
  `WebUi/form/atlas-form.component.ts`, `setRequired` (line 186); `WebUi/form/atlas-files.ts`, `fileParts` (line 113).
  Start-up check: `App/Platform/AtlasConfigLint.cs`, `EffectTargets` (lines 229 to 236).
- **Defect:** The server reads the state of a file picker from the JSON and the rules only. The effects Show, Hide,
  Enable, Disable, Require and Unrequire change a file picker on the client only, and the start-up check accepts them.
- **Failure cases:**
  1. A check box has `"Require": [ "CashAttachments" ]`, and the file picker is not required in the JSON. A save
     with the box selected and no file is saved. The client stops such a save; a direct request does not.
  2. A check box has `"Hide": [ "CashAttachments" ]`, and the file picker is required in the JSON. The client hides
     the file picker and sends no file. The server refuses each save: "... is required." The user cannot save.
  3. The JSON hides a file picker and an effect shows it. The client lets the user add a file. The server refuses
     it ("Files cannot be added to ..."), gives no stored files on load, and gives 404 for a download.
- **Evidence: probe (cases 1 and 2), code (case 3).** With a changed copy of `CFPINFLOW.json`:

  ```text
  case 1: lint: no error, no warning
          second submit, FundsReceived = true, no cash file: ID=10002 Errors=[]   (0 cash files stored)
  case 2: lint: no error, no warning
          second submit: ID=0 Errors=[CashAttachments|Required|Please attach proof of custodian receipt. is required.]
  ```

  A file picker has no `Field`, so `AtlasFormFields` does not hold it, and `AtlasServerEffects.Apply` finds no target.
- **Scope:** No form of today has such an effect. ClientInflows uses rules for its file pickers, and rules work.
- **Proposed correction:** Apply the six effects to the file pickers on the server, in the same order as for the
  fields. Until then, make the start-up check refuse an effect that names a file picker.

### CR-03 (Medium) A second click after a lost answer makes a second record

- **Where:** `WebUi/form/atlas-form.component.ts`, `save` (line 372 and lines 388 to 391) and `newRequestId` (line 441).
- **Defect:** The client makes a new `RequestId` at each click, so a second click after a lost answer is a new
  request for the server.
- **Failure case:** The user does the first submit. The server commits the record, but the answer does not come
  back (a gateway time-out or a network error). A slow mail server can cause this, because the email send runs in the
  request after the commit. The form shows the HTTP error and stays open with ID 0. The user clicks Submit again.
  Result: a second record, a second engine instance, and E1 and E2 two times.
- **Evidence: code.** `save` calls `newRequestId()` when it builds the request (line 372). The error handler only
  shows the error (lines 388 to 391). The server test `A_double_submit_with_one_RequestId_makes_one_record` sends the
  same `RequestId` two times; the client never does this after an error.
- **Note:** The code follows the words of design 10.3 ("a GUID that the client makes for each user action"). But
  then the protection of DR-16 is only for a request that the browser or a proxy sends again. It is not for the more
  usual case: the user tries again.
- **Proposed correction:** Keep the `RequestId` of a submit until the server gives an answer, and send the same
  `RequestId` again after an HTTP failure. With the answer "This request was already processed.", tell the user to
  open the dashboard.

### CR-04 (Medium) A dropdown that an effect enables gets no options

- **Where:** `Service/Application/ConfigSectionService.cs`, `KeepOnlySavedOptions` (lines 255 to 273).
- **Defect:** The load removes the options of each dropdown that the JSON disables, also when an effect of a
  different control can enable it.
- **Failure case:** A dropdown with an `OptionsSource` has `"Disabled": true`. A different control has an effect
  `"Enable": [ ... ]` for it. On a new record the dropdown gets an empty list. When the effect enables the dropdown,
  the user can select nothing.
- **Evidence: probe.** `AccountNumber` got `"Disabled": true`, and `InvestmentType` got an `Enable` effect for it:

  ```text
  lint: no error, no warning
  load of a new record: AccountNumber Disabled=True, options=0      (real config: options=4)
  save with InvestmentType and AccountNumber 700100: ID=10004 Errors=[]
  ```

  The save shows that the server reads the control as enabled (`AtlasServerEffects.Apply`, line 42). Only the load
  does not.
- **Scope:** Release P1 permitted this combination. TTDV2 and ClientInflows have no `Enable` effect, so no form of
  today changes.
- **Proposed correction:** Keep the full list for a control that is the target of an `Enable` effect. Or make the
  start-up check refuse an `Enable` effect on a control with an `OptionsSource` that the JSON disables.

### CR-05 (Low) The start-up check accepts a control name where the adapter needs a column

- **Where:** `App/Platform/AtlasConfigLint.cs`, `EngineBinding` (line 123, the local function `Known`).
  `Service/Application/ConfigSectionService.Workflow.cs`, `FieldValue` (lines 248 to 249).
- **Defect:** The check accepts each control name for `TemplateData.Field`, `FromNameField` and `ListByField`, but
  the adapter reads the value by column name only.
- **Failure case:** `"accountName": { "Field": "FlowAttachments" }` (a control with no column). `--atlas-check`
  gives no error. Each submit is refused: "FlowAttachments is necessary for the notification." With
  `FromNameField` and `"Optional": true`, there is no error at all and the recipient is always empty.
- **Evidence: probe** (template value) and **code** (recipient). Design 10.2 names this check: "a field name that
  is not a column".
- **Proposed correction:** Make the check accept only a column of the table, or a control that has a column. Or
  read the value through the control, as the `Set` of a step does (`MapStepSet`).

### CR-06 (Low) The History shows the file names of a file picker that a rule hides

- **Where:** `Service/Application/ConfigSectionService.cs`, `GetAuditLog` (lines 1028 to 1043) and
  `RuleHiddenFields` (lines 276 to 290).
- **Defect:** The History masks the fields that a rule hides from the user, but not the `AttachmentAdd` and
  `AttachmentRemove` entries of a hidden file picker.
- **Failure case:** A rule hides the cash receipt part from the role `Read`. A `Read` user opens the History.
  The value of `Comment` shows as `***`. The file name of the cash receipt file shows in clear text.
- **Evidence: probe.** With such a rule in a copy of the form:

  ```text
  reader load: CashReceipt visible=False, files=0, Comment=(null)
  reader history has 'secret comment': False
  reader history has 'secret-receipt.pdf': True
  ```

- **Scope:** ClientInflows hides part 2 only in state `Draft`, when the part has no files. No form of today is open.
- **Proposed correction:** Add the name of each file picker that a rule hides to the hidden names, and mask the
  file name in its entries.

### CR-07 (Low) The server does not check the format of `RequestId`

- **Where:** `Service/Application/ConfigSectionService.cs`, `SelectStep` (lines 499 to 504).
  `ConfigSectionService.Workflow.cs`, `RunEngineAction` (line 183).
  `Service/Platform/Workflow/AtlasWorkflowAdapter.cs`, `Execute` (lines 129 to 130).
- **Defect:** The server accepts each text that is not empty as `RequestId`, but the engine columns have a largest
  length (`IdempotencyKey` 200, `CorrelationId` 150).
- **Failure case:** A caller sends a `RequestId` of 160 characters. The first action (`Start`) works. For the later
  action, the adapter uses the key as `CorrelationId`, and the engine write fails. The record is saved, E2 is not
  sent, and the log gets `Atlas.WorkflowStepFailed`, which the support team must act on.
- **Evidence: probe (real engine).**

  ```text
  save: ID=10001 Errors=[]
  outbox of the record: ClientInflow.ActionRequired
  log: Atlas.WorkflowStepFailed ... Action=NotifyInformational Reason=EngineError EX=DbUpdateException
  ```

- **Scope:** The platform client always sends a GUID. Only a different caller can do this.
- **Proposed correction:** Refuse a `RequestId` that is not a GUID, as a page error before the first write.

### CR-08 (Low) A file name can have control characters

- **Where:** `Service/Platform/Attachments/AtlasAttachmentRules.cs`, `TrySplitPartName` (lines 86 to 99).
  `ConfigSectionService.Attachments.cs`, `FileError` (lines 167 to 183) and `Refuse` (lines 224 to 228).
- **Defect:** The file name check removes the path and limits the length, but it does not refuse control characters
  (CR, LF, NUL).
- **Failure case:** A part has the name `FlowAttachments#$#a<CR><LF>Atlas.Forged line.pdf`. The server stores the
  name with the line break. The name then shows in the file list and in the History. For a refused file, the log
  entry `Atlas.AttachmentRefused` gets the line break, so a user can write a line that looks like a different log
  entry.
- **Evidence: probe.** The save gave `ID=10006`, and the stored `FileName` has CR LF. The probe gave the name
  through `IFormFile`; the same request through HTTP (`filename*` with `%0D%0A`) was not tested. The download header
  is safe: ASP.NET encodes the name.
- **Proposed correction:** Refuse a file name that has a control character, as the error `FileName`.

### CR-09 (Low) `Atlas.RecipientSkipped` has record ID 0 at the first submit

- **Where:** `ConfigSectionService.Workflow.cs`, `PrepareEngine` (lines 62 to 63).
  `Service/Platform/Workflow/AtlasWorkflowInputs.cs`, `Recipients` (line 61).
- **Defect:** The recipients are found before the record has an ID, so the warning log of a new record has
  `RecordId=0`.
- **Failure case:** The first submit of a record whose relationship manager has no user. The log says
  `Atlas.RecipientSkipped App=CFPINFLOW RecordId=0 Key=relationshipManager Name=...`. The record gets ID 10001. The
  support team cannot find the record from the log. The first submit is the submit that sends E2 to the manager.
- **Evidence: probe.** The log line above is the result of each first submit in the probes. Design 10.6 wants the
  record ID in this log event.
- **Proposed correction:** Keep the skipped names of the check, and write the log entries after the record has its
  ID.

### CR-10 (Low) No test finds an email from a display name

- **Where:** `Tests/Sections/CfpInflowWorkflowTests.cs` (line 94); `Tests/Data/SqlServerDatabaseFixture.cs`,
  `SeedUserAsync` (lines 124 to 146). The code with no test: `Service/Platform/Security/AtlasUserContext.cs`,
  `EmailOfDisplayName` (line 181).
- **Defect:** In each test the relationship manager is skipped, because no test user has an email. Design 13.1
  lists the name match cases (case, spaces, none, two users, no email) as tests; none is there.
- **Failure case:** A later change breaks the name match. The managers and the RMCC persons get no email. All
  tests pass.
- **Evidence: code** (a search of the tests: no call of `EmailOfDisplayName`, and `SeedUserAsync` writes no
  `Email`). **Probe:** the code is correct today. With users that have an email (one name stored as
  `'  riley   MANAGER '`), the real engine gave:

  ```text
  E2 to: eq@test.local, riley@test.local
  E3 to: eq@test.local, riley@test.local, cs@test.local    cc: casey@test.local, imrg@test.local, star@test.local
  skipped: Sam Twin (2 users), No Mail (no email), Gone User (no user, deleted), Nobody Known (no user)
  ```

- **Proposed correction:** Add unit tests for `EmailOfDisplayName` with the cases of design 13.1. Add one engine
  test where a name gives an address in E2 and in E3 (To and Cc).

### CR-11 (Low) Three tests or test groups prove less than their names or the design say

- **Where and defect:**
  1. `Tests/Data/ReleaseP2ScriptTests.cs`, `Each_script_runs_two_times_and_changes_the_target_tables_in_place`
     (line 19; the script list is at lines 128 to 132). The test runs the scripts 100 to 140. Script 150 runs one
     time, in the fixture. No test runs it two times or looks for its `CP_Atlas_ScriptLog` row.
  2. `WebUi/platform.component.spec.ts`, `sends a new RequestId with each save` (line 174). The spec does one save
     and checks only the format of the value.
  3. Design 13.1 lists a test for "state difference after a later action (error log)". There is none. There is
     also no test for the error `NoRecipient` (a necessary key with no address), for the engine exception path
     (`EngineError`) and for the log of a failed commit (`AtlasCommitFailed`).
- **Failure case:** A change makes script 150 fail on its second run, or the client sends the same `RequestId` for
  two different submits, or one of the four error paths stops its work. All tests pass.
- **Evidence: code.** **Probe** for script 150: a second run changed nothing (the row counts and the checksums of
  the rules and the texts are the same, and the log has one row). Thus the script is correct today.
- **Proposed correction:** Add script 150 to the two-run test. Make the spec do two saves and compare the two
  values. Add one test for each of the four error paths.

### CR-12 (Low) Three comments say something that the code does not do

- **Where and defect:**
  1. `Service/Application/ConfigSectionService.cs`, line 739: "the engine call is the last write before the
     commit". The state write and all audit rows come after it (CR-01).
  2. `ConfigSectionService.cs`, line 705 (`RunBeforeSave`) and `ConfigSectionService.Workflow.cs`, line 43
     (`PrepareEngine`): the two doc comments say "save step 10". They are two different steps.
  3. `Service/Platform/Sections/AtlasRichText.cs`, line 16: "A link can use only http, https or mailto". A link
     with no scheme stays (`/relative` and `//host/x`).
- **Failure case:** A developer reads comment 1 and adds a write after the engine call, because the code already
  has such writes. A developer reads comment 3 and thinks that each link has a full address.
- **Evidence: code** (1 and 2) and **probe** (3): `<a href="//evil.example/x">` came back with its `href`.
  These links are not a script risk: the browser opens them as `https` links.
- **Proposed correction:** Correct the three texts. For comment 1, correct the code order first (CR-01).

### CR-13 (Low) The load response sends the engine binding to the browser

- **Where:** `Service/Application/ConfigSectionService.cs`, `LoadPage` (lines 194 to 220).
- **Defect:** The load returns the section with the full `Workflow` object, but the platform client reads no part
  of it.
- **Failure case:** Each user who can open a record, also a `Read` user, gets the steps, the engine action keys,
  the recipient keys, the list keys, the field names of the recipients and the template formats. There is no email
  address in it.
- **Evidence: probe.** The load response of a record had `Workflow.Recipients` (7 entries), `Workflow.Steps` (2)
  and `Workflow.TemplateData` (5). A search of `WebUi/` found no read of `Workflow`.
- **Proposed correction:** Remove the platform keys of the binding from the load response. Keep the target keys
  (`Key`, `Roles`, `Actions`), which the target class needs.

### CR-14 (Low) A display name that is longer than the column stops each save with a file

- **Where:** `Service/Platform/Attachments/AtlasAttachmentService.cs`, `Add` (lines 101 and 104) and `Remove`
  (line 131). The same write is in `Service/Platform/Audit/AtlasAuditWriter.cs`, `Add` (line 71), from release P1.
- **Defect:** The platform writes the display name of the user to `CreatedBy` and `ModifiedBy` with no length check.
  These columns are `nvarchar(50)` in `CFP.InflowAttachment` and `CFP.AuditLog`; `CP_Atlas_User.DisplayName` is
  `nvarchar(1000)`.
- **Failure case:** A user with a display name of more than 50 characters does a submit with a file. The file row
  is refused by the database. The user gets "The record was not saved. Contact support if this occurs again." at
  each try. Each ClientInflows submit needs a new file, so this user cannot submit.
- **Evidence: probe.** A user with a display name of 60 characters and the real config:

  ```text
  save: ID=0 Errors=[|SaveFailed|The record was not saved. Contact support if this occurs again.]
  engine instances: 0
  log: Atlas.SaveFailed ... SqlException: String or binary data would be truncated in table '...CFP.InflowAttachment', column 'CreatedBy'
  ```

  Nothing is written, and the engine is not called. The LAN ID columns (`CreatedByLanId`, `ModifiedByLanId`) hold the
  person in each case.
- **Proposed correction:** Cut the display name to the length of the column before the write (the column
  metadata has the length). The LAN ID column stays the exact identifier.

## 4. Checked, no defect found

### 4.1 Save path

- The order is: validation, record, files, first engine action, state and status, commit, later engine actions,
  dispatch. The only difference from design 10.3 is CR-01.
- A refused engine action rolls back the record, the `Set` values and the files. The engine message is a page error.
- A submit with no `RequestId`, and a submit in a state with no step, stop before the rules and before each write.
- The state comes from the stored record only. A client value for `WorkflowState` or `Status` is refused.
- The recipients and the template data are found before the first write. An empty template value writes nothing.
- The engine result `IdempotencyConflict` gives "This request was already processed." and rolls back the second
  record.
- Two users who do the second submit at the same time: the row lock and the version check stop the second one
  before its engine call (read of the code; no test with two connections ran).
- The later actions and the dispatch run after the commit. A failure there is a log entry only (owner decision).
- The state check on load writes the engine state, one `WorkflowSync` audit entry and a warning log. A record with
  no engine instance is not checked. When the engine cannot be read, the load continues.
- The second submit uses the stored values of part 1 for the recipients (probe: E3 went to the manager and the RMCC
  person of the first submit).

### 4.2 Security

- Load needs `View` (or `Create` for a new record). Save needs `Edit` or `Create`. The download needs `View` on the
  record, and gives 404 for a removed file, a removed record and a file picker that a rule hides.
- A file is accepted only for a file picker that is visible and enabled after the rules. A removed file must be of
  this record and of such a file picker.
- File checks: type list, file signature, 10 MB for each file, 25 MB for one save, the name with no path, 255
  characters. The content type comes from the stored name. The download has `Content-Disposition: attachment` and
  `nosniff`.
- HTML clean-up (probe, 30 inputs): no `script`, no event attribute, no `style`, no picture, no `javascript:`,
  `data:` or `vbscript:` link (also with entities, spaces and a line break in the scheme) came back. `svg`, `math`,
  `iframe`, `form` and `base` are removed. A clean value gives the same value again. The one exception is a table
  `caption`, which the editor does not make.
- SQL: each table and column name comes from the table metadata and goes into the text in brackets. Each value is
  a parameter. A table of the Table provider must be in `LookupTables`.
- The client cannot set: system columns, Computed columns, effect targets (also the data-only target `RMCC`), the
  `Set` names of a step, the fields of a part that is hidden or read-only in the state.
- A user who cannot change a dropdown gets only the saved option, with no `Data`.

### 4.3 The generic rule

- A search of all `.cs` and `.ts` files in `atlas-platform/src` and `WebUi/src` for `CFPINFLOW`, `CFP`, `AIN` and
  `Inflow` found only the registration line (`App/StandIn/StandInHost.cs`, line 58) and the stand-in enum value
  `AIN`, which was there before P2.
- The provider, the attachments, the rich text, the steps and the adapter read all app data from the manifest, the
  form JSON and the settings.

### 4.4 Backward compatibility

- No change in `src/` and `workflow-api/`.
- All TTDV2 tests pass with no change to their expected results.
- The new request fields (`RequestId`, `RemovedAttachmentIds`), the new JSON keys and the new response properties
  are optional. A form with no `Steps`, no file picker and no rich text control saves as before.
- The new interface members (`IAtlasOptionProvider.Check`, `IAtlasUserDirectory.EmailOfDisplayName`) have a default.
  The new constructor parameters of `AtlasModuleServices` are optional. `AtlasCheckSeverity.Info` is the last value.
- The log event IDs 41025 to 41029 and the route `v1/Attachment/{appCode}/{id}` are new. No identifier is renamed.

### 4.5 SQL scripts

- Scripts 100 to 140: each change runs only when the column, the index, the table or the row is not there, or the
  column has the old type. The data stays. Each script stops when its table is not there, and writes its log row in
  its transaction. The tests run them two times.
- Script 150: a second run changes nothing (probe). It has the same form as `sql/sqms/002_sqms_workflow_metadata_dml.sql`
  (it sets version 1 as the active version, and makes the recipient rules again). It has no email address.
- Observation, not a finding: script 150 writes its `CP_Atlas_ScriptLog` row only when this table is in the engine
  database. The script header says this.

### 4.6 The start-up check

- The check has each case that the design names: the workflow columns, the states and the steps, the `Set` names
  and tokens, exactly one `Save` button, the engine actions (`Start` only as the first action of the initial step),
  the recipient keys and sources, the `Map` values, the template fields, the list addresses, `HostedClientURL`, the
  lookup tables and the provider parameters, the attachment table and columns, the file picker limits, the `Info`
  severity and the name report. The exception is CR-05.
- `--atlas-check` on the real files gives `0 error(s), 0 warning(s)` and the Info lines of the name report.
- Observations, not in the design: the check does not find an option of a `ListByField` field that has no `Map`
  entry (the submit then gives the error `NoRecipient`). It does not find a host with no engine services.

### 4.7 Angular client

- The save request has the `Version`, a `RequestId`, the removed file IDs and each new file as a part
  `<ControlName>#$#<FileName>`. A file picker gives no output. A hidden or disabled part sends nothing.
- The client checks the file type, the size, the number of files and the 25 MB total before it sends.
- The download uses `HttpClient` and a blob, so it goes through the interceptors of the target.
- The buttons do not start a second save while a save runs (`saving`). A server field error shows at its field;
  the other errors show at the top.
- The editor code loads only for a form with a rich text control. A disabled control is read-only.
- `MustBeTrue` and the effects rule are the same on the client and on the server (shared cases; `setsValues`).

### 4.8 Tests

- The refusal tests look at the database after the refusal ("nothing written"), not only at the error.
- The real-engine tests use the engine migrations and script 150, not a copy of the metadata.
- The gaps are CR-10 and CR-11.

### 4.9 Code comments

- No review, decision or spec ID is in a comment of the changed `.cs`, `.ts`, `.html`, `.scss` and `.sql` files
  (`CodeCommentRulesTests` passes; a second search found none).
- Each new public type and member has a summary. The history words ("was", "now", "fix for") are not used for
  history.
- Observation: the three `CFPINFLOW*.json` files have "module 25, 5.x" in their first comment, as the TTDV2 files
  have "spec 12". The comment test does not read JSON files.

## 5. Not reported

These points were seen and are decided or out of scope:

- The accepted limits of design 10.8, also the instance with no record after a lost connection at the commit.
- The dispatcher stand-in in `Service/StandIn/Engine/`, and a later engine action that fails as log only (19.7).
- The engine message of a refused action goes to the user as it is (design 10.3, step 5).
- Missing API authentication. Engine code. `050`, `090` and R01 to R04.
