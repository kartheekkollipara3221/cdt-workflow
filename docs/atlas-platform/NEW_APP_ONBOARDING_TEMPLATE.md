# CP Atlas platform: new app onboarding template

Version 1, 2026-11-12. The team that owns the new app fills in this document. The platform team uses it to write the app metadata (JSON and SQL) and to find the work that is not metadata.

## How to fill in this template

- Give facts about the app. Do not write JSON and do not design the solution.
- Fill in each table. Add rows as necessary. Do not change the columns.
- If you do not know an answer, write `not known`. Do not leave a cell empty and do not guess.
- If an item does not apply, write `none`.
- Attach evidence where the template asks for it: screenshots, current code, spreadsheets, sample records, sample emails.
- The rows marked *Example* show the level of detail. They come from the pilot app TTDV2 or from a sample workflow. Delete them.
- Names of roles, states, actions and fields become permanent identifiers after the release. Write the names that the business uses.

After the first pass, the platform team sends a gap report. It puts each item in one of three groups: metadata only, new generic platform code, workflow engine or adapter change. It also lists questions. Code starts after you answer the questions.

---

## 1. App identity

| Item | Answer |
|---|---|
| App name (shown to the user) | |
| Proposed app code (short, capitals, for example `TTDV2`) | |
| Menu position (parent app or top level) | |
| Business owner (name, team) | |
| Technical contact (name) | |
| Purpose of the app (2 to 4 sentences) | |
| Does it replace a current app, spreadsheet or mailbox? Which one? | |
| Must old data move into the new app? (see section 11) | |
| Number of users | |
| Number of new records in one year | |
| Largest number of rows a user sees in one list | |
| Date you need it in the development region | |

**Form shape.** Select one:

- [ ] One page. The user sees all sections on one page (as TTD, SQMSO, AIN).
- [ ] Steps. The user goes through pages in a sequence (as CDT). Give the list of steps in section 3.
- [ ] Not known.

## 2. Roles and access

One row for each role.

| Role name | Who gets it, and who approves | View | Create | Edit own records | Edit all records | Delete | See history |
|---|---|---|---|---|---|---|---|
| *Example:* Prepare | Tax preparers; approved by the team lead | yes | yes | yes | no | no | yes |
| *Example:* Owner | Tax team managers | yes | yes | yes | yes | yes | yes |
| | | | | | | | |

| Question | Answer |
|---|---|
| Which fields make a user the "owner" of a record? (for example: the creator, the user in the field "Reviewer") | |
| Can each user see all records, or only some? Give the rule in words. | |
| Can one user have two or more of these roles? If yes, which role wins? | |
| Is there a read-only role (audit, support)? | |

## 3. Pages and sections

One row for each section of the form, in screen order. Attach a screenshot or a sketch of each page.

| # | Step (only for a step form) | Section title | Columns in the layout (1, 2 or 3) | Who sees it | When it shows (always, or a condition) |
|---|---|---|---|---|---|
| *Example:* 1 | none | Account Details | 3 | all roles | always |
| | | | | | |

## 4. Fields

One row for each field, in screen order.

**Type** must be one of the types the platform has today, or `NEW` (then fill in section 7):

| Type | What the user sees |
|---|---|
| `text` | One line of text |
| `textarea` | Text with many lines |
| `currency` | Amount |
| `percent` | Percentage |
| `select` | Drop-down list, one value |
| `ngselect` | Drop-down list with search, one value or many values |
| `date` | Date picker |
| `checkbox` | Check box |
| `option` | Radio buttons |
| `user` | A person, shown by name (read-only) |
| `grid` | Table inside the form (fill in section 6) |

**Option source** must be one of these, or a description of a new source:

| Source | Gives |
|---|---|
| Fixed list | The values you write in the "Rules and notes" column |
| `PBAccounts` | Accounts (number and name) |
| `UsersInRoles` | Users who have the roles you name |
| `Years` | A range of years around the current year |
| `WeekEndings` | The last week-ending dates |
| New source | Write: the table, procedure or system; the value; the label; any filter |

| # | Section | Label | Type | Required | Limits (length, min, max, format) | Option source | Default value | Read-only when | Hidden when | Rules and notes |
|---|---|---|---|---|---|---|---|---|---|---|
| *Example:* 1 | Account Details | Account | `ngselect`, one value | yes | none | `PBAccounts` | none | never | never | Fills fields 2 and 3 |
| *Example:* 2 | Account Details | Selected Account Number | `text` | yes | none | none | from field 1 | always | never | |
| *Example:* 5 | Work Details | Category | `select` | yes | none | Fixed list | none | never | never | OTHER, STMT_N, TAX_N |
| | | | | | | | | | | |

## 5. Field behaviour

### 5.1 When one field changes another

| # | When this occurs | Then |
|---|---|---|
| *Example:* 1 | The user selects an Account | Set "Selected Account Number" and "Selected Account Name" from the account |
| *Example:* 2 | The user clears the Account | Clear the two fields |
| | | |

Include: show or hide, make required, make read-only, set a value, clear a value, change the options of a list.

### 5.2 Calculated values

| Field | Formula in words | When it calculates again |
|---|---|---|
| *Example:* Weekly Total | Sum of Monday to Sunday minutes | When a day changes |
| | | |

### 5.3 Checks across fields

| # | Check | Message to the user (exact text) | Stops the save? |
|---|---|---|---|
| *Example:* 1 | End date is on or after start date | "End date must not be before start date." | yes |
| | | | |

## 6. Tables inside the form

One block for each `grid` field in section 4. Copy the block.

| Item | Answer |
|---|---|
| Field label | |
| Number of rows: one fixed row, a fixed list of rows, or the user adds rows | |
| Can the user add, change and delete rows? Which roles? | |
| Largest number of rows | |
| Totals row? Which columns? | |
| Must each row have its own history? | |

| Column label | Type | Editable | Required | Limits | Notes |
|---|---|---|---|---|---|
| | | | | | |

## 7. New components

A new component is a control or a screen part that the platform does not have (section 4 list). Each one is new platform code, so the platform team needs the most detail here. Copy the block for each component.

| Item | Answer |
|---|---|
| Name of the component | |
| What the user does with it, step by step | |
| Where it is in CP Atlas today (app, page, file name), or `new` | |
| Library control it uses (`@pbdev`, ag-grid, ng-select, other) | |
| What it needs to show itself (data, options, settings) | |
| What it saves (one value, a list, rows, a file) | |
| Behaviour when read-only | |
| Behaviour when the value is not valid (message text) | |
| Does it call a service or another system? Which one? | |
| Do you think other apps will use it? | |
| Screenshot or sketch attached? | |

Rule: the platform cannot add a package. The component must use the libraries that CP Atlas has today.

## 8. Buttons

| Label | What it does | Who sees it | When it shows | Question before it runs (exact text, or `none`) | Message after success (exact text) |
|---|---|---|---|---|---|
| *Example:* Save | Saves the record | Prepare (own records), Owner | always | none | default |
| *Example:* Approve | Workflow action "Approve" (section 10.3) | Reviewer | state "In Review" | "Approve this request?" | "Request approved." |
| | | | | | |

## 9. Dashboard

### 9.1 Tabs

| Tab label | Records it shows (rule in words) | Who sees the tab | Sort order | Largest number of rows |
|---|---|---|---|---|
| *Example:* Current Tax Timesheet | Records that I created, that I changed, or where I am the preparer | all roles | Last change, newest first | 500 |
| | | | | |

### 9.2 Columns

One table for each tab.

| Column label | Field | Type (text, number, date, currency, user) | Opens the record? | Total at the bottom? |
|---|---|---|---|---|
| | | | | |

### 9.3 Other dashboard items

| Question | Answer |
|---|---|
| Is there an "Add New" button? Who sees it? | |
| Row menu items (Edit, Delete, History, other) and who sees each one | |
| Does a current stored procedure give the data of a tab? Name it. | |
| Export to Excel necessary? | |

## 10. Workflow

Fill in this section if a record goes through approval or review states. If not, write `none` here and go to section 11.

### 10.1 General

| Question | Answer |
|---|---|
| Name of the workflow | |
| When does it start? (first save, or a button such as "Submit") | |
| Can the user save a draft before the workflow starts? | |
| Can the user save changes in a state and not do an action? | |
| Can a record have more than one workflow at the same time? | |
| Can a finished record start again? | |
| Is there a current workflow in the engine that this app must use? Give the key. | |

### 10.2 States

| State name | Meaning | Status text the user sees | End state? |
|---|---|---|---|
| *Example:* Draft | The maker prepares the request | Draft | no |
| *Example:* In Review | The reviewer examines the request | Pending Review | no |
| *Example:* Approved | The request is complete | Approved | yes |
| | | | |

### 10.3 Transitions

One row for each movement. This table is the most important part of the workflow.

The engine selects the next state from the current state and the action. If the next state depends on a field value, write one row for each value and put the value in the "Condition" column.

| # | From state | Action (button label) | Who can do it (role, or the user in a field) | Condition (or `none`) | Fields or comment that must be filled | To state |
|---|---|---|---|---|---|---|
| *Example:* 1 | Draft | Submit | Maker (the creator) | none | all required fields | In Review |
| *Example:* 2 | In Review | Approve | Reviewer | none | none | Approved |
| *Example:* 3 | In Review | Send Back | Reviewer | none | Comment | Draft |
| | | | | | | |

### 10.4 Tasks and time limits (SLA)

| State | Who has the task (role, or the user in a field) | Time limit | Clock (calendar hours or business hours) | Reminder (when, to whom) | After the limit (who gets the escalation) |
|---|---|---|---|---|---|
| *Example:* In Review | The user in the field "Reviewer" | 48 hours | business hours | 24 hours before, to the reviewer | The reviewer's manager mailbox |
| | | | | | |

### 10.5 Emails

One row for each email. Attach the subject and the body text of each one, or a sample of the current email.

| # | Sent when (transition #, reminder, escalation) | To | Cc | Subject | Body attached? |
|---|---|---|---|---|---|
| *Example:* 1 | Transition 1 | The reviewer | The maker | "Request {ID} is ready for review" | yes |
| | | | | | |

For "To" and "Cc" write one of: a role, the user in a named field, or a fixed mailbox address.

### 10.6 Fields by state

Which sections or fields can each role change in each state? Write `edit`, `read` or `hidden`. A section name is enough if all its fields are the same.

| Section or field | State | Role | Access | Required in this state? |
|---|---|---|---|---|
| *Example:* Review Comments | In Review | Reviewer | edit | yes for "Send Back" |
| *Example:* Review Comments | Draft | Maker | hidden | no |
| | | | | |

### 10.7 Special cases

Answer `yes` or `no`. For each `yes`, give the rule in words.

| Question | Answer |
|---|---|
| Can a user take back (recall) a submitted record? | |
| Can a task move to another user (reassign, delegate)? | |
| Do two or more persons approve at the same time (parallel approval)? | |
| Must the maker and the approver be different persons? | |
| Can an administrator move a record to a state by hand? | |
| Does an action start work in another system? | |
| Must the form show the workflow history (who, action, date, comment)? | |

## 11. Data

| Question | Answer |
|---|---|
| Does a table for this data exist today? Give the database, schema and table. | |
| Must old records move into the new app? Number of records, and where they are. | |
| Which lists come from reference data? Who owns that data? | |
| Is "delete" a real delete, or does the record stay hidden? | |
| Which fields must the history show? (all, or a list) | |
| Fields with personal or confidential data | |
| How long must the records stay? | |

Attach 3 to 5 sample records with real-looking values (no confidential data).

## 12. Attachments

| Question | Answer |
|---|---|
| Can the user attach files? | |
| File types and largest file size | |
| Who can add, see and delete a file? | |
| Do these rights change with the workflow state? | |
| Are attachments tied to the record, or to a section or step? | |

## 13. Other systems and reports

| System or report | Direction (in or out) | Data | When | Contact |
|---|---|---|---|---|
| | | | | |

## 14. Test scenarios

Give one normal path and 3 to 5 other paths. The platform team makes its tests from these.

| # | Scenario | Start (role, record state) | Steps | Expected result |
|---|---|---|---|---|
| *Example:* 1 | Normal approval | Maker, no record | Create, fill all fields, Submit; Reviewer: Approve | State "Approved"; email 1 sent to the reviewer |
| *Example:* 2 | Wrong role | Maker, record in "In Review" | Open the record | No "Approve" button; all fields read-only |
| | | | | |

Test users in the development region (one for each role):

| Role | User (LAN ID) |
|---|---|
| | |

## 15. Open points

| # | Question or item you cannot answer now | Who can answer | Date expected |
|---|---|---|---|
| | | | |

## 16. Evidence checklist

- [ ] Screenshot or sketch of each page (section 3)
- [ ] Screenshot or sketch of each new component (section 7)
- [ ] Current code, spreadsheet or form, if the app replaces something (section 1)
- [ ] Subject and body of each email (section 10.5)
- [ ] Workflow diagram, if you have one (the table in 10.3 stays the source)
- [ ] Sample records (section 11)
- [ ] Table definitions of current tables (section 11)
