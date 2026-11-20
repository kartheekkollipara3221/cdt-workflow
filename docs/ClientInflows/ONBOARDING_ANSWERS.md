# ClientInflows: onboarding answers (P2)

Follows `docs/atlas-platform/NEW_APP_ONBOARDING_TEMPLATE.html` (version 2). The answers are recorded one question at a time.
Markers: **confirmed** = the user gave the answer. **to confirm** = read from the screenshots in this folder, not yet checked.
**open** = no answer yet.

Evidence in this folder: `form#1.jpeg` (form 1), `form2.jpeg` (Cash Receipt Confirmation), `fields.jpeg` (SharePoint
list fields), `email1.jpeg` to `email3.jpeg` (SharePoint 2013 workflow `Inflow_Workflow_1.0`; `email4.jpeg` = copy of
email3). Current SharePoint site: `imsp.bbh.com/sites/imops/inflowOutflow`.

## Decisions

| # | Decision | Date |
|---|---|---|
| D1 | App name: ClientInflows. | 2026-11-19 |
| D2 | Use the target table `CFP.Inflow` (and `CFP.InflowAttachment`). The target team made them ahead, with no platform integration. We can change them (additive SQL scripts). | 2026-11-19 |
| D3 | App codes: `CFPINFLOW` (P2) and `CFPOUTFLOW` (later, separate app, not in P2). Each app has its own top-level menu item. Labels: "Client Flows Portal - Inflow" and "Client Flows Portal - Outflow" (CFP = Client Flows Portal). | 2026-11-19 |
| D4 | One form, not two. The section "Cash Receipt Confirmation" opens only after the first submit. The ICG group fills both parts. | 2026-11-19 |
| D5 | All other groups in the emails get emails and read-only access to the app: PM/other groups (chosen by Investment Type: Fixed Income, Equity, Fixed Income & Equity), the RM ("RM Manager", from the form), RMCC (tied to the RM), CS Team (= Client Services team), Star FAS IFI Cash Team. | 2026-11-19 |
| D6 | Investment Type groups get new distribution lists, one per type, named like `cp-atlas-inflow-fixedincome-alerts@domain.com`. Investment Type groups get no Cc. | 2026-11-19 |
| D7 | IMRG is a risk group; it gets a new distribution list and stays in the Cc of the "funds received" email. "Fixed Income & Equity" sends to both the Fixed Income list and the Equity list (no third list). | 2026-11-19 |
| D8 | Account Number list comes from the target AIN table. When the user selects an account, the app fills Account Name, STAR Account Number, Relationship Manager and Custodian from the AIN row of that account. The same account lookup is needed in at least 4 other apps (a reusable requirement; design later). | 2026-11-19 |
| D9 | The AIN table has RM names only. The user asks the target team to add the RM LAN ID and the RMCC to the AIN data. Requirement only; no design now. | 2026-11-19 |
| D10 | Investment Type is a fixed list in this app: Fixed Income, Equity, Fixed Income & Equity. | 2026-11-19 |
| D11 | The 4 fields filled from AIN (Account Name, STAR Account Number, Relationship Manager, Custodian) are read-only in this app. So the RM and the RMCC for emails come from the AIN row of the selected account. (Replaces the first answer "user can change", same day.) | 2026-11-19 |
| D12 | Description (rich text): text formats, tables, lists and links only. No pictures; a user attaches a screenshot as a file. | 2026-11-19 |
| D13 | SharePoint fields Account Names, Notification, Stage, RS Confirmation, RS Sumitted By and Expiration Date are not needed. Status values: Draft, Submitted, Funds Received (workflow detail in section 10). Fund Received = the check box "Funds have been received". | 2026-11-19 |
| D14 | Date of destruction = created date + 7 years. Calculated; not on the form; shown on the dashboard. A difference of one day (leap years) is acceptable. | 2026-11-19 |
| D15 | Workflow: no draft save now; part 1 always ends with Submit. Draft stays as a placeholder state for later (confirmed). No cancel and no withdraw. Funds Received is the end state; the record is closed for good. | 2026-11-19 |
| D16 | Part 1 (Account Information, Flow Information, its attachments) is read-only after the first submit. After Funds Received the whole record is read-only. | 2026-11-19 |
| D17 | At the first submit (T1) two emails go out: E1 "Action Required" to a new ICG distribution list (own notification route); E2 "Informational" to the Investment Type list(s) and the RM. | 2026-11-19 |
| D18 | At Funds Received (T2), E3 "Cash Inflow - Informational": To = Investment Type list(s), RM, CS Team (new DL). Cc = RMCC, IMRG (new DL), Star FAS IFI Cash Team. ICG does not get E3. | 2026-11-19 |
| D19 | Emails keep the old subjects and body text (email1-3.jpeg). "this form" / "here" link to the record in CP Atlas. Amount as USD (`$1,250,000.00`), Inflow Date as `MM/DD/YYYY`. | 2026-11-19 |
| D20 | No time limit (SLA), reminder or escalation for now. | 2026-11-19 |
| D21 | ICG and all read-only groups see all records. Access as in TTDV2: users are in the CP Atlas user table with app roles (the menu shows an app when the user has an active role there); AD group handling is the target's existing logic. Two roles: `Read` and `Write` (see section 2). | 2026-11-19 |
| D22 | Roles `Read` (IsRead) and `Write` (IsWrite). Any Write user can submit part 2 on any record. No admin role. (Delete: see D25.) | 2026-11-19 |
| D23 | Dashboard: tab "Inflows" (all records, newest first) and tab "Inflow Pipeline" (records where Funds have been received = No). Columns: ID (opens the record), Account Number, Account Name, Investment Type, Amount (USD), Inflow Date, Status, Submitted By (part 1), Created, Funds confirmed by, Funds confirmed date, Date of destruction. Export: the ag-grid export the target uses today. | 2026-11-19 |
| D24 | "Add New" for Write users only. Row menu as in the target: Edit, Delete, History. A Read user who selects Edit sees the form disabled (keep the menu name "Edit"). | 2026-11-19 |
| D25 | Write users can delete a record (soft delete; the record is hidden). This replaces "nobody deletes" in D22. Inflow Pipeline is sorted by created date, newest first. | 2026-11-19 |
| D26 | Delete is allowed in every state (also after Funds Received); the target team recovers a record if needed. No email on delete. The Delete menu item is hidden for Read users. | 2026-11-19 |
| D27 | History window shows all fields and each file added or removed. No workflow history on the form; the History window is enough. | 2026-11-19 |
| D28 | Date of destruction is for display only (no automatic delete or hide). Normal CP Atlas data handling; no special confidential fields. No other systems except the AIN account data. | 2026-11-19 |
| D29 | Account Number list: all accounts in the AIN account inventory that are not deleted. Control: ngselect with search (number and name). Each item shows "number - name". | 2026-11-19 |
| D30 | Test scenarios 1-6 in section 14 accepted. Test users are mock values: A035313 (Write), A023456 (Read). | 2026-11-19 |
| D31 | Section 1 details (owner, contact, user count, dev date) are not needed for development. Comment has no pictures (as D12). Distribution lists: real DLs at the target; local tests use smtp4dev. | 2026-11-19 |
| D32 | One new AIN column only: RMCC, added in this repo (local script) so the app works here; the user asks the target team to make the same change in `AIN.AccountInventory`. It holds display names that match `CP_Atlas_User.DisplayName`, many values allowed, separated by `;`. RMCC is usually one person for each account. | 2026-11-19 |
| D33 | No RM LAN ID column. The RM email comes from `CP_Atlas_User`: `AIN.AccountInventory.RelationshipManager` matches `CP_Atlas_User.DisplayName`, and the app uses that user's `Email`. RMCC names resolve the same way. Replaces the open item of D9. | 2026-11-19 |
| D34 | Name match rules: case and extra spaces ignored. One active user with an email: send. No match, or two or more users with the same name: leave that person out, send to the others, write a warning to the log. `--atlas-check` lists the AIN accounts whose RM or RMCC names do not match exactly one user. | 2026-11-19 |

## 1. App identity

| Item | Answer | Status |
|---|---|---|
| App name | ClientInflows | confirmed |
| App code | CFPINFLOW | confirmed |
| Menu position | Top level, own menu item, label "Client Flows Portal - Inflow" | confirmed |
| Business owner | open | open |
| Technical contact | open | open |
| Purpose | Record a client cash inflow instruction; the CS Team confirms when the custodian receives the funds; PM, RM and teams get emails. | confirmed |
| Replaces | SharePoint list and workflow at `imsp.bbh.com/sites/imops/inflowOutflow` (Inflow part) | confirmed |
| Old data must move? | Yes. The target team moves the data with its own approach (not platform work). About 500 records in the last 3 years. | confirmed |
| Records per year | Up to about 500 (plan figure) | confirmed |
| Users / largest list | open | open |
| Date needed in dev | open | open |
| Form shape | One page (section form) | confirmed (P2 scope) |

## 2. Roles and access

| Role | Who | Access | Status |
|---|---|---|---|
| `Write` | ICG group | View all; create and submit part 1; submit part 2 on any record | confirmed |
| `Read` | Investment Type lists, RM, RMCC, CS Team, IMRG, Star FAS IFI Cash Team | View all; get emails | confirmed |
| (no admin) | none | Write users delete (D25) | confirmed |

## 3. Pages and sections

| # | Section | Shows when | Status |
|---|---|---|---|
| 1 | Account Information | always | confirmed |
| 2 | Flow Information | always | confirmed |
| 3 | Submitted By + confirm (part 1) | always | confirmed |
| 4 | Cash Receipt Confirmation | after the first submit (D4) | confirmed |
| 5 | Submitted By + confirm (part 2) | after the first submit (D4) | confirmed |

## 4. Fields

| # | Section | Label | Type (guess) | Required | `CFP.Inflow` column | Status |
|---|---|---|---|---|---|---|
| 1 | Account Information | Account Number | ngselect with search, source AIN account inventory (not deleted), shows "number - name"; fills 2-5 | yes | AccountNumber (+ Account?) | confirmed (source) |
| 2 | Account Information | Account Name | text, filled from AIN, read-only (D11) | auto (from AIN) | AccountName | confirmed |
| 3 | Account Information | STAR Account Number | text, filled from AIN, read-only (D11) | auto (from AIN) | StarNumber | confirmed |
| 4 | Account Information | Relationship Manager | text, filled from AIN, read-only (D11) | auto (from AIN) | RelationshipManager | confirmed |
| 5 | Account Information | Custodian | text, filled from AIN, read-only (D11) | auto (from AIN) | Custodian | confirmed |
| 6 | Account Information | Investment Type | select, fixed list: Fixed Income, Equity, Fixed Income & Equity | yes | InvestmentType | confirmed |
| 7 | Flow Information | Amount | currency, USD format, 2 decimals, more than 0, no largest value | yes | Amount | confirmed |
| 8 | Flow Information | Inflow Date | date, past and future allowed, no limit | yes | InflowDate | confirmed |
| 9 | Flow Information | Description | richtext (CKEditor 5), no length limit, no pictures (D12), not in emails | no | Description | confirmed |
| 10 | Flow Information | Attachments ("Please attach email/signed instruction from the client.") | filepicker, many files, default types and 10 MB per file, read-only after submit | yes | CFP.InflowAttachment | confirmed (largest number of files: not given, take as no limit) |
| 11 | part 1 | Submitted By | user, filled with the current user at submit, read-only | yes (auto) | FlowSubmittedBy | confirmed |
| 12 | part 1 | I confirm the information given in this form is correct | checkbox, must be ticked to submit | yes | FlowConfirm | confirmed |
| 13 | Cash Receipt Confirmation | Funds have been received | checkbox, must be ticked to submit part 2 | yes | none (SP: Fund Received) | confirmed |
| 14 | Cash Receipt Confirmation | Comment | richtext (CKEditor 5), no pictures (D31) | no | Comment | confirmed |
| 15 | Cash Receipt Confirmation | Attachments ("Please attach proof of custodian receipt.") | filepicker, many files, default types and 10 MB per file, read-only after the second submit | yes | CFP.InflowAttachment | confirmed (largest number of files: not given, take as no limit) |
| 16 | part 2 | Submitted By | user, filled with the current user at submit, read-only | yes (auto) | CashSubmittedBy | confirmed |
| 17 | part 2 | I confirm ... correct | checkbox, must be ticked to submit | yes | CashConfirm | confirmed |

Other SharePoint fields: see D13 and D14. Only Status (workflow) and Date of destruction (dashboard) stay.

## 10. Workflow

| # | From | Action | Who | To | Emails | Status |
|---|---|---|---|---|---|---|
| T1 | (new) | Submit (part 1) | ICG | Submitted | E1, E2 | confirmed |
| T2 | Submitted | Submit (part 2, funds received) | ICG | Funds Received (end, closed) | E3 | confirmed |

No draft save (D15), no cancel, no withdraw.

### Emails (D17, D18, D19)

| # | Sent when | To | Cc | Subject | Status |
|---|---|---|---|---|---|
| E1 | T1 | ICG distribution list (new) | none | `[Account Name] Cash Inflow Instruction – Action Required` | confirmed |
| E2 | T1 | Investment Type list(s) (D6, D7); RM (from AIN, D11) | none | `[Account Name] Inflow Instruction - Informational` | confirmed |
| E3 | T2 | Investment Type list(s); RM; CS Team (new DL) | RMCC; IMRG (new DL); Star FAS IFI Cash Team | `[Account Name] Cash Inflow - Informational` | confirmed |

Body fields in all emails: Client Name (= Account Name), Account Number, Amount, Inflow Date, and a link.

## 9. Dashboard

See D23. Same columns on both tabs (assumed; the user accepted the columns for "Inflows").

## 14. Test scenarios

| # | Scenario | Expected result |
|---|---|---|
| 1 | Write user submits part 1; another Write user submits part 2 | Submitted, then Funds Received; E1 + E2, then E3; record closed |
| 2 | Investment Type "Fixed Income & Equity" | E2 and E3 go to the FI list and the Equity list |
| 3 | Submit with a check box not ticked, no attachment, or Amount 0 | Submit blocked with a message |
| 4 | Read user opens a record | Form disabled; no Add New, no Delete |
| 5 | Write user deletes a Funds Received record | Record hidden; no email; History shows the delete |
| 6 | Select an account | 4 fields filled from AIN, read-only |

Test users (mock): A035313 = Write, A023456 = Read.

## Still open

None for the first pass. Next: the gap report.
