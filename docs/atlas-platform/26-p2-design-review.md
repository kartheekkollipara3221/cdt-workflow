# Module 26: review of the ClientInflows (P2) design

Status: 2026-11-20. Review of [25-client-inflows-p2-design.md](25-client-inflows-p2-design.md), draft 2. No code, no SQL
and no design text is changed by this review. The user decides which findings change the design.

## 1. Method

The review compares each statement of the design with:

- the platform code of today (`atlas-platform/src/`, `atlas-platform/WebUi/`);
- the workflow engine code (`src/`), read only;
- the target copy of 2026-11-18 (`Program_cs.txt`, `CPAtlasController_cs.txt`, the interceptors, the settings key
  names; no key value or password was read out);
- the earlier specs: module 1 (5.3, 5.9 to 5.13), module 8, module 9, module 10;
- the requirements D1 to D34.

Paths: `Service/` = `atlas-platform/src/BBH.CP.Atlas.Service/`, `Data/` = `atlas-platform/src/BBH.CP.Atlas.Data/`,
`App/` = `atlas-platform/src/BBH.CP.Atlas.App/`, `WebUi/` = `atlas-platform/WebUi/src/app/platform/`.

## 2. Result

The design is sound in its structure: the split into generic parts is correct, the engine does not need a change, and
the save order follows module 10. But the design has 3 defects that make the app wrong or open as written, and 9
points that are not complete. All have a small correction. None needs an engine change or a new package.

| Severity | Count | Meaning |
|---|---|---|
| High | 3 | The app does not work as written, or an approved decision is broken |
| Medium | 9 | A requirement is missed, or a part is not designed, or a risk is not controlled |
| Low | 9 | Simpler solution, unclear text, or a detail for the build |

## 3. High

### DR-01 The account fields and RMCC change again at the second submit

- Design: 5.2 (`SetFromOption`), 7.3, 10.6 ("E3 uses the `RelationshipManager` and `RMCC` of the first submit").
- Code: the server runs the effects of each control at each save, with the stored value when the client sends none
  (`Service/Application/ConfigSectionService.cs`, `ReadAndFilter` and `MapRecord`;
  `Service/Platform/Sections/AtlasServerEffects.cs`, `Values`). The client also runs the effects when the form is
  built (`WebUi/form/atlas-form.component.ts`).
- Effect: at the second submit, the server reads the AIN row again and writes `AccountName`, `StarNumber`,
  `RelationshipManager`, `Custodian` and `RMCC` again. If the RM changed in AIN after the first submit, E3 goes to the
  new RM, and History shows a change to part 1. This breaks answer Q8 and D16. Also, when a user opens the record, the
  read-only fields show the AIN values of today, not the saved values.
- Correction: one generic rule, the same on the server and the client: an effect sets values (`Set`, `Clear`,
  `SetFromOption`) only when its source control is enabled for the user and the state. A source that a rule disables
  or hides sets nothing; the saved values stay.
- TTDV2: no change for a user who can edit (the account control is enabled). A read-only user then sees the saved
  values. The TTDV2 tests must prove it.

### DR-02 A save with no button name goes past the workflow

- Design: 6.4 ("With no `Button`, the save is a plain save, as today").
- Code: the save checks only the `Create` or `Edit` permission and the fields
  (`ConfigSectionService.SavePage`, `LoadStored`). It has no button check.
- Effect: a `Write` user, or a client defect, can send a save with no `Button`. For a new record, this makes a record
  with no engine instance, no state and no email. In state `Submitted`, it writes part 2 ("Funds have been received",
  the files) with no second submit and no E3.
- Correction: a generic rule. A save with no `Button` is the action of the `Save` button. If the form has no button
  with `Action.Type` = `Save`, the server refuses the save (`NotAllowed`). ClientInflows has no Save button, so each
  save must name `SubmitPart1` or `SubmitPart2`. TTDV2 has a Save button, so it does not change.

### DR-03 The form JSON of the design does not fit the DTO classes

Three points. Each one stops the app as written.

| Point | Design (5.2, 10.2) | Code | Correction |
|---|---|---|---|
| Place of `Workflow` | Top level of the form | The property is on the section: `CPApplicationSection.Workflow` (`Data/StandIn/DTO/CPAtlasApplicationConfig.cs`; the target reads `Section.Workflow`) | Put the binding in `Section` |
| `Roles` of the binding | Not there | `CPWorkflow.Roles` is `required` in the target class | Add `"Roles": []`; the adapter uses `ActorRoles` |
| Buttons | No `Visible` key | `CPButton.Visible` is `false` by default | Add `"Visible": true, "Disabled": false` to each button, as in `TTDV2.json` |

## 4. Medium

### DR-04 A `Read` user has no "Edit" item in the row menu (D24)

- Code: with no `Menus` in the dashboard JSON, the menu shows "Edit" only for a user with the `Edit` permission
  (`Service/Platform/Dashboard/AtlasDashboardService.cs`, `Menus`).
- Effect: a `Read` user can open a record only with the link on the ID. D24 wants the "Edit" item for all users.
- Correction: JSON only. Each tab gets `Menus` with a rule: `Edit` and `History` for `@Read`, `Delete` for `Write`.

### DR-05 The file download with a plain link can give 401 at the target

- Design: 8.2 and 8.3 ("download URL", "download links").
- Target: the API uses JWT bearer authentication (`Program_cs.txt`). The client adds the credentials in HTTP
  interceptors (`winauthinterceptor.ts`, `apiinterceptor.ts`). The API has a different address than the client
  (`HostedURL`, `HostedClientURL`). A browser link does not go through the interceptors.
- Correction: the client gets the file with `HttpClient` as a blob, through the same interceptors as each other call,
  and then gives the blob to the browser. The load response needs only the file ID. Check at the target how the CDT
  download works today.

### DR-06 The platform cannot read `varbinary`

- Code: `Data/Platform/AtlasSqlTypes.cs` has no `varbinary`. The start-up already reads the columns of
  `AttachmentTable` into the table registry (`App/Platform/AtlasPlatformStartup.cs`).
- Effect: the design does not say how the attachment service reads and writes `FileContent`.
- Correction: add `varbinary` to the type map (generic). The list query of the attachments must not select
  `FileContent`; only the download reads it. If not, each form load reads all files of the record (up to 10 MB each).

### DR-07 The account control and its columns are not consistent

- Design 5.2: the control `Account` fills `AccountNumber` with `SetFromOption`. But `AccountNumber` has no control, and
  the `Data` list of the provider (7) does not have it.
- Correction: the `ngselect` control writes the column `AccountNumber` (name `AccountNumber`, value = the account
  number). `SetFromOption` fills the 4 shown fields and `RMCC`. The column `CFP.Inflow.Account` is not used. This is
  an integration note for the target team.

### DR-08 Some values are not in History, and `Set` cannot write a column with no control

- Design 6.4: `Action.Set` sets `CashSubmittedDate`. This column has no control.
- Code: the audit lists only the fields of the form (`ConfigSectionService.Changes`).
- Effect: `Set` needs the same rule as 7.3 (a control or a column of the table). `Status`, `CashSubmittedDate` and
  `RMCC` do not show in History. A user who reads History does not see when the record became "Funds Received".
- Correction: `Set` accepts a control name or a column name. The audit entry of a save with a workflow step gets one
  line for the status ("Status: Submitted to Funds Received"). Decide if the two other columns must show.

### DR-09 The dashboard does not format the Amount (D23)

- Gap report A10 says the column type `currency` exists. Code: the type only aligns the cell to the right
  (`WebUi/dashboard/atlas-dashboard-columns.ts`). `CPTabColumn.Format` is not read.
- Effect: the Amount shows as a raw number with 4 decimals, not as USD.
- Correction: a generic formatter for `currency` columns (2 decimals, thousands separator, `$`). Small client code.
  Check at the target that the Excel export gives the formatted or the numeric value, as the users want.

### DR-10 An empty template value stops the engine action with a technical message

- Engine: a template token with an empty value is a validation error; the action fails
  (`src/WorkflowEngine.Application/Notifications/NotificationTemplateRenderer.cs`).
- Effect: `AccountName` comes from AIN and can be empty text. The user then gets "Template token 'accountName'
  requires ...".
- Correction: before the first write, the adapter checks that each `TemplateData` value is not empty. An empty value
  is a page error that names the field.

### DR-11 The CKEditor risk is tested too late

- Design 13: the editor is build step 5. Not proved yet: `@ckeditor/ckeditor5-angular` with Angular 21; `ng build`
  with 0 warnings (bundle size limits); the editor style sheet with no change to `angular.json` (a target file that
  is not in the approved list).
- Correction: a short test before step 1 (with step 0): add the two packages to the stand-in, show one editor, run
  `ng build` and `ng test`. If it fails, stop and ask (the earlier fallback was Jodit).

### DR-12 The state check on load does not correct two failure cases

- Design 10.3: "the state check on the next load corrects the record".
- Case 1, first submit: the engine commits, then the Atlas commit fails. The record does not exist. The engine keeps
  an instance with no record, and E1 goes out later from the outbox with a link that opens nothing.
- Case 2, second submit: the same failure. The check on load sets the record to "Funds Received", but the part 2
  values and files are not saved. The record is then closed for good with an empty part 2, and E3 is sent.
- The two cases need a lost database connection at the exact moment of the commit. They are rare.
- Correction: text only. Add the two cases to "Accepted limits" (10.8) with the action for support: the error log
  `Atlas.WorkflowStepFailed` gives the instance ID; the target team corrects the data by script.

## 5. Low

| # | Finding | Correction |
|---|---|---|
| DR-13 | New setting `AppSettings:Atlas:WebBaseUrl` (10.7). The target has `AppSettings:HostedClientURL` already, with no slash at the end, and uses it for the links in the CDT and SQMS emails. | Use `HostedClientURL`. One setting less. |
| DR-14 | Index `IX_Inflow_WorkflowState`, filtered `IsDeleted = 0` (4.1). `IsDeleted` is nullable, so the dashboard query uses `ISNULL([IsDeleted], 0) = 0` and cannot use this index. The table gets about 500 rows each year. | Remove the index from the plan. |
| DR-15 | Names that differ from the earlier specs: module 1 (5.3, 5.10) has `Action.Execute` on the button and `InitialAction` in the binding; module 9 has the audit events `AttachmentAdd` and `AttachmentRemove`. `IAtlasUserDirectory` exists already. | Say in the design that `Steps` replaces `InitialAction` and the button `Execute` key for platform apps (reason: more than one step). Use the module 9 event names. Add a method to the directory; do not make a new service. |
| DR-16 | Double submit of a new record (10.3). The engine idempotency key is global and compares the request hash (`WorkflowExecutionService.TryBuildIdempotencyReplay`). A second request with the same `RequestId` makes a second record ID, so the engine refuses it and the second record is rolled back. This is correct, but the message is technical. With no `RequestId`, the protection is lost. | Map the engine result to "This request was already processed." For a save with a `Button` that has steps, `RequestId` is necessary; refuse the save with no `RequestId`. |
| DR-17 | HTML clean-up details (9.2). | `class` only on `figure` and only the value `table`. Add `rel="noopener noreferrer"` to links. Select a stable `HtmlSanitizer` version whose dependencies (`AngleSharp`, `AngleSharp.Css`) are also stable versions; verify at build step 5. The download sets the content type from the file extension on the server, not from the client value. |
| DR-18 | The account list (7.1) goes to each user who opens a record, also to a `Read` user and after the first submit. The list has the RM, custodian and RMCC of all accounts. | When the control is disabled for the user, send only the saved option. Smaller response and less data for read-only users. Can be a later step. |
| DR-19 | A later step that changes the state is not found (10.2). A failed E2 has no second try. | After a later step, compare the engine state with the record; a difference is an error log. Say who acts on `Atlas.WorkflowStepFailed` (support sends the email by hand). |
| DR-20 | The `@pbdev` stand-in has no file picker. A `filepicker` control has no `Field`, so it is not in `AtlasFormFields`. | Build step 4: make the stand-in file picker from the real target source. The attachment check reads the state of the control itself. The start-up check must not report "no column" for a `filepicker`. |
| DR-21 | 10.4: "A record with no engine instance (new) is not checked". | Say "each record with no engine instance", so a record that the target team moved from SharePoint loads with no error. |

## 6. Checked and correct

| Statement of the design | Proof |
|---|---|
| An engine action can keep the state | No engine code refuses it. The state update writes the same key (`WorkflowExecutionService`). Build step 0 stays as the proof by test. |
| Recipient keys at the start go to the first action | With no participant definition, `WorkflowParticipantCaptureService` gives all keys to the initial action. |
| Each step must name its own recipient keys | The resolver refuses a key that the route does not declare (`NotificationRecipientResolver`). |
| A route with no To address stops the action before a change | The check is before the side effects (`WorkflowExecutionService`). |
| The rule table of 5.2 works with `MostPermissive` and `RuleDefault: ReadOnly` | `AtlasRuleEngine.Evaluate`. The rules are on the section controls, not on each field. Part 2: `{ "States": ["Draft"], "Visible": false, "Disabled": true }` and `{ "Roles": ["Write"], "States": ["Submitted"], "Disabled": false }`. |
| A hidden or disabled field is not validated and not accepted | `ConfigSectionService.ValidateFields`, `AtlasAllowList.Check`. |
| A nullable `IsDeleted` works; a new record gets `IsDeleted` = 0 | `AtlasDataExtensions.ActiveByKey`, `AtlasTabQueryBuilder`, `ComputeAndSystemFields`. |
| A computed column is read and not written | `AtlasColumn.IsDatabaseGenerated`. |
| `LookupTables` fits the start-up | The start-up already loads `Table`, `AuditTable` and `AttachmentTable` into the registry. |
| No target controller change for the upload | The target `ManagePage` passes `files` to the service. The target registers the engine services (`AddWorkflowInfrastructure`). |
| The link in the HTML email is safe | The engine HTML-encodes each template value. |
| A `user` column exports the name | The server puts the display name into the row data. |

## 7. Decisions

Most findings have one correction and need no decision. Four need an answer from the user:

| # | Question |
|---|---|
| DR-01 | The rule for effects of a control that the user cannot change |
| DR-02 | The rule for a save with no button name |
| DR-08 | Which values of a workflow step show in History |
| DR-18 | The short option list for a disabled control: in P2 or later |

After the answers, the design becomes draft 3 with all corrections.

## 8. Answers from the walkthrough

The user and the reviewer go through the findings one at a time. This table records each answer.

| # | Answer | Date | Result for draft 3 |
|---|---|---|---|
| DR-01 | Accepted, correction A (first answer "rejected" was changed after discussion) | 2026-11-20 | Generic rule, the same on the server and the client: an effect sets values only when the user can change its source control. AIN is read only at the submit of section 1. After that, the saved values stay. Answer Q8 stays: E3 uses the RM and RMCC of the first submit. The TTDV2 tests must prove no change for a user who can edit. |
| DR-02 | Accepted with a change | 2026-11-20 | Platform rule (also in `AGENTS.md`): a section form with a workflow has one submit button. The server selects the workflow step from the state of the record; the form metadata lists the steps by state. Each save of such a form runs the step for the current state, with or without a button name, so no save goes past the workflow. ClientInflows: buttons `Submit` and `Close`; `SubmitPart1` and `SubmitPart2` go away. New record: section 1, E1 and E2. State `Submitted`: section 2, E3. State `FundsReceived`: no submit button. TTDV2 has no workflow and does not change. |
| DR-03 | Accepted | 2026-11-20 | Text only. Draft 3 corrects the JSON examples: `Workflow` in the `Section`, `"Roles": []`, and `"Visible": true, "Disabled": false` on each button. No code change. |
| DR-04 | Accepted | 2026-11-20 | JSON only. Each dashboard tab gets `Menus`: `Edit` and `History` for all users who can read, `Delete` for `Write`. |
| DR-05 to DR-07, DR-09 to DR-17, DR-19 to DR-21 | Accepted as written (one answer for the 15 findings) | 2026-11-20 | Draft 3 takes each correction as the finding gives it. Two change with the one-button rule of DR-02. DR-15: the binding lists the steps by state, not by button. DR-16: each submit of a form with a workflow must have a `RequestId`; the server refuses a submit that has none. |
