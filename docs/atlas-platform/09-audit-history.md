# Module 9: Audit and history

Status: Draft 2, 2026-10-25. All open questions answered (section 9). Design only. No target code is changed.

## 1. Purpose

Each app must keep a trusted record of who changed what, and show it in the History window of the record. Modules 1 and 5 already move the write of the audit entry to the server (module 1, 5.12; module 5, 6.6). This module completes the design: which events are audited, how the History window reads them, who can read them, and how the workflow steps of the engine appear in the same history.

Scope: the audit events, the entry format, `v1/AuditLog`, `v1/GetAuditLog`, the History window, the workflow history, the time and the user name. Out of scope: the audit table shape (module 5, 6.6) and the engine audit table (owned by the engine).

## 2. Current behaviour

### 2.1 Write

- The client writes the audit. An HTTP interceptor sends the response of each successful `POST`, `PUT` or `DELETE` (except `AuditLog`) to `v1/AuditLog`. Evidence: `WebUi/.../Interceptors/postrequestinterceptor.ts.txt:16-28`; `cp-atlas-service.ts.txt:123-132`.
- `v1/AuditLog` writes the `Outputs` of that body as JSON to the audit table of the app (found by class name). It catches all errors and writes nothing. Evidence: `Service/CPApplicationService.cs.txt:587-628`; module 5, DM-05.
- The `Outputs` of `ManagePage` are the changed fields with the new value only (`Name`, `Value`). `RemovePage` returns `IsDeleted = Yes`. Evidence: `CPApplicationService.cs.txt:83-107`; `CPTTDService.cs.txt:125-143`.

### 2.2 Read

- `v1/GetAuditLog?appCode=&refID=` returns all entries of the record, with `CreatedBy` and `CreatedDate` as text `MM-dd-yyyy HH:mm:ss` (server local time). There is no sort order. In a family, the entries are filtered by `RefType`. Evidence: `CPApplicationService.cs.txt:630-651`.
- The History window shows each entry as "Modified On", "Modified By" and lines `Name - Value`. `Name` is the database field name, not the label. Evidence: `WebUi/.../Control/history-report-modal.ts.txt:12-55`.
- The workflow engine writes its own audit (`dbo.WorkflowAudit`: action, from state, to state, user, role, reason, UTC time). The History window does not show it. Evidence: `CP_Sharepoint.txt:616-633`.

## 3. Findings

### AU-01 The client decides what is audited (High, security)

- A direct API call (with no browser) writes no audit entry.
- A user can call `v1/AuditLog` with any `Code`, `ID` and `Outputs`, and so write a false entry in the history of any record.
- If the browser closes or the network fails after the save, there is no entry.
This is module 1, SM-17. Evidence: `postrequestinterceptor.ts.txt:16-28`; `CPAtlasController.cs.txt:108-112`.

### AU-02 Some changes are never audited (High)

- `Execute` (for example `MakeBid`, which makes a bid) is a `GET`, so the interceptor does not audit it.
- `RemoveAttachment` returns no body, so the removal is not audited. Uploads are audited only as field changes, if at all.
- Changes by the workflow engine (state, status) are in `dbo.WorkflowAudit` only.
Evidence: `cp-atlas-service.ts.txt:113-120`; `CPAtlasController.cs.txt:92-97`.

### AU-03 Anyone can read any history (Medium, security)

`v1/GetAuditLog` does not check the app role or the record visibility. Any signed-in user can read the change history of any record (module 6, SE-01). Evidence: `CPAtlasController.cs.txt:116-120`.

### AU-04 The history has no old value, no order and no labels (Medium)

- The entry has the new value only. The reader cannot see what changed from what (module 5, question 5 adds `Old`).
- No `ORDER BY`: the order of the entries can change.
- The window shows the database field names (for example `TaxPreparerName`), not the labels of the form.

### AU-05 Time and user are not exact (Low)

`CreatedDate` is server local time as text with no time zone. `CreatedBy` is the display name (module 6, SE-04). The engine uses UTC and the user ID.

## 4. Reusable design

### 4.1 Audit events

The platform writes one entry, on the server, in the same transaction as the change, for each of these events:

| Event | When | Content |
|---|---|---|
| `Create` | `ManagePage` with `ID` = 0 | All saved fields (`Name`, `Value`) |
| `Update` | `ManagePage` with `ID` > 0 | The changed fields (`Name`, `Old`, `Value`) |
| `Delete` | `RemovePage` | `IsDeleted` (`Old` = No, `Value` = Yes) |
| `Action` | `Execute` | The action name and the fields that it changed |
| `AttachmentAdd` | upload (module 8) | The control name and the file name |
| `AttachmentRemove` | `RemoveAttachment` | The control name and the file name (`Old`) |

- An entry is written only if something changed. A save with no change writes no entry.
- If the entry cannot be written, the change fails (module 5, 6.6). This removes AU-01 and AU-02 for the app data.
- The workflow engine keeps its own audit; the platform does not copy it (4.4).

### 4.2 Entry format

The `LogMessage` stays a JSON list with the keys `Name` and `Value` (so that the current History window continues to work) and adds new keys:

```json
[
  { "Name": "TaxPreparerName", "Label": "Name of Tax Preparer", "Old": "A. Smith", "Value": "B. Jones" },
  { "Name": "MondayMinute", "Label": "Monday Minutes", "Old": "60", "Value": "90" }
]
```

- The event type is the first item of the list: `{ "Name": "_Event", "Value": "Update" }`. Thus no new column is necessary, and `RefType` keeps its current use (the child app code in a family).
- `Label` comes from the form JSON. `Old` and `Value` use the wire format of module 5 (6.4).
- Hidden or secret fields can be marked in the form JSON with `"Audit": false`; their values are written as `"***"`. Decision 2026-10-25 (question 4): no field needs this today; `"Audit": false` stays an optional feature for later.
- `CreatedBy` keeps the display name (the History window shows it). A new nullable column `CreatedByLanId` is added only to new tables (new-app template); the existing tables are not changed. `CreatedDate` is UTC for new tables and local time for the existing tables (4.5).

### 4.3 `v1/AuditLog`

- New setting for each app: `AuditMode` = `Client` | `Server` (manifest, module 7).
- `Client` (default for the existing apps until migration): the current behaviour.
- `Server`: the platform writes the entries (4.1). `v1/AuditLog` returns 200 and writes nothing for this app, so the current client interceptor causes no error and no double entries.
- New apps use `Server`. The existing apps move one at a time (section 7). When all apps use `Server`, the client interceptor can be removed.

### 4.4 Read: `v1/GetAuditLog`

- The access check of module 6 runs with the operation `History` (`Permissions.History`, default: `View`). No access returns 403. This removes AU-03.
- The entries are sorted by `CreatedDate` descending, then `ID` descending.
- The response keeps the current fields (`AppCode`, `RefID`, `CreatedBy`, `CreatedDate`, `LogMessage`) and adds optional fields: `Event`, `CreatedDateUtc` (ISO 8601), `Source` (`App` or `Workflow`).
- Workflow history: if the app has a workflow binding (module 10), the server also reads the workflow history of the record through the public engine read interface (`IWorkflowInspectionReader`, history part; no direct table access, module 5, DM-09). Each workflow step becomes one entry with `Source` = `Workflow`, `CreatedBy` = the user, and a `LogMessage` list: `_Event` = `Workflow`, `Action`, `From`, `To`, `Reason`. The two lists are merged by time. Decision 2026-10-25 (question 1): the History window shows the workflow steps and the field changes in one list, sorted by time.
- The History window shows `Label` when it is there, else `Name`; shows `Old → Value` when `Old` is there; and shows the workflow entries in the same list. With an old server response, it works as today. This removes AU-04.

### 4.5 Time and user

- Decision 2026-10-25 (question 2): the existing audit tables keep server local time for new entries (one time zone in each table). New tables (new apps) use UTC. The time comes from `TimeProvider` in both cases (local time = `TimeProvider.GetLocalNow()`).
- `v1/GetAuditLog` returns `CreatedDateUtc` for all entries. For an existing table, the server converts the local time to UTC with the server time zone (setting `AppSettings:AuditTimeZone`, default the server zone). The client shows the local time of the user. The workflow entries are already UTC, so the merge (4.4) sorts all entries correctly.
- The user name in the window stays the display name. The LAN ID is kept for the record (new tables) and is used by the access logs.

## 5. Backward compatibility

- `v1/AuditLog` and `v1/GetAuditLog` keep their routes, parameters and response fields. New response fields are optional.
- The `LogMessage` JSON keeps `Name` and `Value`. The current History window shows the new entries correctly (it ignores `Label`, `Old` and `_Event`; `_Event` shows as one extra line `_Event - Update` until the window is updated).
- `AuditMode` defaults to `Client` for the existing apps, so nothing changes until an app moves.
- The existing audit tables and rows do not change. No DDL for existing tables.
- The engine audit is read only through the public engine interface. No engine change.

## 6. Migration steps

| Step | Change | Risk |
|---|---|---|
| 1 | Add the access check and the sort order to `v1/GetAuditLog` (4.4), log-only first. | Low |
| 2 | Add the server audit writer (4.1, 4.2) with `AuditMode`. Default `Client`. | Low |
| 3 | Update the History window: `Label`, `Old`, `_Event`, workflow entries (4.4). | Low |
| 4 | Move TTD (pilot) to `AuditMode: Server`. Compare the entries with the old ones for the same changes. | Medium |
| 5 | Add the workflow history merge for apps with a workflow binding (4.4). | Low |
| 6 | Move AIN, SQMSO, SQMSB and CDT to `Server`, one at a time. | Medium |
| 7 | When all apps use `Server`, remove the client interceptor. | Low |

## 7. Acceptance

- A save by a direct API call (no browser) writes one entry.
- A call to `v1/AuditLog` for an app in `Server` mode writes nothing.
- `MakeBid` writes an `Action` entry. An attachment removal writes an `AttachmentRemove` entry.
- A user with no access to the record gets 403 from `v1/GetAuditLog` (enforce mode).
- The History window shows the label, the old value and the new value, newest first, and the workflow steps of the record in the same list.
- A save with no change writes no entry. A failed audit write leaves the record unchanged.

## 8. Notes

- The audit is for the History window of each record only. There are no audit reports (module 5, question 5).
- Retention (decision 2026-10-25, question 3): no rule. The audit rows stay as long as the record. Records are only soft-deleted, so the rows are kept with no limit. No purge script.

## 9. Open questions

1. Answered (2026-10-25): yes. One list with the workflow steps and the field changes, sorted by time (4.4).
2. Answered (2026-10-25): the existing tables keep local time; new tables use UTC; the API returns `CreatedDateUtc` for all (4.5).
3. Answered (2026-10-25): no retention rule. The entries stay as long as the record (section 8).
4. Answered (2026-10-25): no field needs it today. `"Audit": false` stays optional (4.2).
