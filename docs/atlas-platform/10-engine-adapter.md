# Module 10: Workflow engine adapter

Status: Draft 2, 2026-10-25. All open questions answered (section 9). Design only. No target code and no workflow engine code is changed.

## 1. Purpose

CDT, SQMSO and SQMSB call the workflow engine. Each service builds the engine request itself, in about 150 lines of code, and each service calls the engine inside its own database transaction. A new app with a workflow must get the engine calls from its JSON (module 1, 5.10). This module defines one adapter between Atlas and the engine: one way to build the request, one order of the database writes and the engine call, and one way to read the result.

Scope: start, execute, SLA synchronization, the notification dispatch, the context read and the history read (module 9). The engine stays as it is: the adapter uses only the public engine types that the services use today. Related items: the workflow binding (module 1, 5.10), the error contract (module 1, 5.11), the save pipeline (module 1, 5.9), the role scope (module 1, section 11).

## 2. Current behaviour

### 2.1 Request

Each service (`CPCDTService`, `CPSQMSOService`, `CPSQMSBService`) has its own `Start` and `Execute` (`IWorkflowService`). Each one:

- makes the instance ID in code: `"WF-CDT-" + ID`, `"WF-SQMS-OPP-" + ID`, `"WF-SQMS-BID-" + ID`;
- reads the roles of the user from all apps, and in some calls uses the fixed list `["MFO"]`;
- finds each notification recipient by display name in `CP_Atlas_User` (`.ToList()[0]`);
- builds the `WorkflowActionEnvelope`, then makes `new WorkflowActionEnvelopeNormalizer()` and `new CanonicalRequestHasher()`;
- uses a new GUID as the idempotency key for each call.

Evidence: `Service/Application/CPSQMSOService.txt:330-470`; `CPSQMSBService.cs.txt:465-510`; `CPCDTServices.txt:689-702`.

### 2.2 Order and result

- The service opens an Atlas transaction, saves the record, calls the engine, sets `WorkflowState` from `Result.NewState`, saves again and commits. On an exception it rolls back and returns the result with no error (module 1, SM-16). Evidence: `CPSQMSOService.txt:178-237`.
- The engine uses its own `DbContext` and commits its own transaction (issue H6).
- The service reads only `NewState`. It does not read `Success`, `ResultCode` or `Message` (issue H8). Evidence: `CPSQMSOService.txt:474, 490`.
- After the engine call, the service dispatches the notifications at once (`DispatchCreatedNotifications`). A dispatch failure changes `Success` to false, but no code reads it. Evidence: `CPSQMSOService.txt:495-545`.

## 3. Findings

### EA-01 The engine state and the Atlas data can disagree (High, issue H6)

The engine commits its state change and queues the emails in its own transaction, inside the Atlas transaction. If a later Atlas step fails, Atlas rolls back, but the engine state and the emails stay. Example (CDT): the Checker approves; the engine moves to `TradingDeskReview` and queues the email; the security identifier save then fails; `CDT_General.WorkflowState` stays `CheckerReview`. Evidence: `CPCDTServices.txt:156-328`.

### EA-02 The engine result is not checked (High, issue H8)

The engine can refuse an action (wrong role, wrong state, duplicate). The service ignores `Success` and keeps `WorkflowState`, so the user sees "saved" and the form data is changed, but the state is not. An SLA validation error is caught and ignored, and then `synchronization!.Changed` fails with a null reference. Evidence: `CPSQMSOService.txt:432-450, 474`.

### EA-03 Each service builds the engine request itself (High for reuse)

About 150 lines for each service and each call: instance ID, roles, recipients, envelope, normalizer, hasher, SLA, context read, dispatch. A new app with a workflow needs a copy of this code (module 1, SM-15).

### EA-04 Roles and recipients are not exact (Medium)

- The roles sent to the engine come from all apps, not from the app (module 1, SM-08). Some calls send the fixed role `MFO`. Evidence: `CPSQMSOService.txt:315, 399-403, 464`.
- Recipients are found by display name. If the name is not in `CP_Atlas_User`, `.ToList()[0]` stops the save with an exception; two users with the same display name give the wrong user (module 6, SE-04). Evidence: `CPSQMSOService.txt:337, 361, 381`.

### EA-05 No protection from a double submit (Medium)

Each call makes a new GUID for `ClientActionId` and `IdempotencyKey`. If the user clicks two times, or the browser sends the request again, the engine sees two different actions. The engine idempotency (`IdempotencyRecord`) is not used.

## 4. Reusable design

### 4.1 One adapter

`IAtlasWorkflowAdapter` (platform):

```csharp
WorkflowStepResult Start(AtlasRecord record, AtlasUserContext user, string requestId);
WorkflowStepResult Execute(AtlasRecord record, AtlasUserContext user, string actionKey, string requestId);
WorkflowSlaResult SynchronizeSla(AtlasRecord record, AtlasUserContext user, string requestId);
WorkflowContextResponse ReadContext(AtlasRecord record, AtlasUserContext user);
IReadOnlyList<WorkflowHistoryItem> ReadHistory(AtlasRecord record);   // module 9
void DispatchNotifications(WorkflowStepResult result);
```

- It reads the binding from the app (module 1, 5.10; module 7): `Key`, `BusinessObjectPrefix`, `InitialAction`, `ActorRoles`, `Recipients`, `Notifications`, `Actions`.
- It uses the engine services from dependency injection (`IStartWorkflowService`, `IWorkflowExecutionService`, `IWorkflowSlaSynchronizationService`, `IWorkflowActionNotificationDispatcher`, `IWorkflowContextReader`, `IWorkflowInspectionReader`) and the registered `WorkflowActionEnvelopeNormalizer` and `CanonicalRequestHasher` (already in `program.cs.txt:30-33`). No `new`.
- The services of CDT, SQMSO and SQMSB keep their `Start` / `Execute` methods; the methods call the adapter. This removes EA-03.

### 4.2 Instance ID

The instance ID stays `"WF-" + BusinessObjectPrefix + ID` with the current prefixes (`CDT-`, `SQMS-OPP-`, `SQMS-BID-`). The current engine instances keep their IDs. A new app gets its prefix from its binding.

### 4.3 Order of the writes

The save pipeline (module 1, 5.9) uses this order:

1. Validate the request, the rules and the access (modules 4, 6). No write yet.
2. Begin the Atlas transaction. Write the record, the audit entry and the attachments. `SaveChanges`, but do not commit.
3. Call the engine (start or execute) **last**.
4. If `Success` is false: roll back the Atlas transaction. Return the engine `ResultCode` and `Message` as a page error (module 1, 5.11). Nothing changed, in the engine or in Atlas.
5. If `Success` is true: write `WorkflowState` (and `Status`, if the binding maps it) from `NewState`, then commit the Atlas transaction.
6. After the commit, dispatch the notifications (4.5).

The only remaining gap: the engine committed (step 3) and the Atlas commit (step 5) fails, for example because the database connection is lost. For this case:

- The adapter writes an error log with the instance ID, the action and the new state.
- On the next load of the record, the platform reads the engine context. If `WorkflowState` in Atlas is not the engine state, the platform writes the engine state to the record, writes an audit entry (`_Event` = `WorkflowSync`), and logs it. The engine is the source of truth for the state.

Decision 2026-10-25 (question 1): the check on the next load and the error log are enough. No background check job. This removes EA-01, with no engine change. (A shared transaction between the two contexts needs an engine change, so it is not in this design.)

### 4.4 Result

- The adapter returns `WorkflowStepResult`: `Success`, `ResultCode`, `Message`, `NewState`, `InstanceId`, `CorrelationId`.
- The pipeline always reads `Success` (4.3, step 4). This removes EA-02.
- Decision 2026-10-25 (question 3): a refused engine action always stops the save. Nothing is saved; the user sees the engine message, corrects the form and tries again. There is no "save the data but not the action" mode. (A save with no workflow action, for example a draft save where the binding selects no action, does not call the engine and is not affected.)
- An SLA validation error is a field error with the SLA field name, not an exception that is ignored.

### 4.5 Notifications

- The notifications are dispatched after the Atlas commit, never inside the transaction.
- A dispatch failure does not change the saved data. The adapter writes a warning log and the response shows "Saved. The notification will be sent again." (the engine outbox retries). Decision 2026-10-25 (question 2): the target runs the engine scheduled job with the outbox retry, and the missing engine migrations (issue H9, for example `NotificationOutbox.NextAttemptAtUtc`) will be applied at the target. Module 11 lists them as a deployment prerequisite.

### 4.6 Roles and recipients

- `ActorRoles: "User"` sends the roles of the user in the app and its parent (`RoleScope`, module 1, 11.2), from the active roles view (module 6, 4.4). No fixed role in code.
- `Recipients` find users by LAN ID (module 6, 4.5). If a recipient field is empty or the user is not found, the action stops with a field error ("No user found for Seller RM Team"), not an exception. This removes EA-04.

### 4.7 Double submit

- The client sends an optional `RequestId` (a new GUID for each user action, the same GUID when the browser sends the request again) with `ManagePage` and `Execute`.
- The adapter uses `RequestId` as `ClientActionId` and `IdempotencyKey`. The engine then returns the first result for a repeated request and does not run the action two times.
- If there is no `RequestId` (old client), the adapter makes a new GUID, as today. This removes EA-05 for the updated client.

## 5. Example: binding for a new app

```json
"Workflow": {
  "Key": "PbspReview",
  "BusinessObjectPrefix": "PBSP-",
  "InitialAction": "Submit",
  "ActorRoles": "User",
  "StateField": "WorkflowState",
  "StatusField": "Status",
  "Recipients": {
    "maker":    { "FromUserField": "CreatedByLanId" },
    "reviewer": { "FromUserField": "ReviewerLanId" }
  },
  "Actions": { "Approve": [ { "Field": "Decision", "Equals": "Approve" } ] }
}
```

The workflow definition (states, actions, roles, SLA, notification routes) is metadata in the engine, delivered as SQL scripts, as today. The adapter does not define workflows.

Decision 2026-10-25 (question 4): the next Step Master app (not CDT) uses the same engine and this adapter, with its own binding in JSON. Thus the adapter must support the step form too: the binding can be on a step (`CPApplicationStep.Workflow`, as CDT) or on the section. Module 2 (Step Master) uses this adapter.

## 6. Backward compatibility

- No engine change. The adapter uses the same public engine types and calls as the current services.
- The instance IDs, `SourceSystem` values and workflow keys stay the same for CDT, SQMSO and SQMSB.
- The order change (4.3) changes the behaviour only for failures: an engine refusal now gives an error and no data change, in place of a silent partial save. This is the correction of H6 and H8.
- `RequestId` is optional.
- The `ActorRoles` change for SQMS follows module 1 (5.10): run part 5 of the role scope script first.

## 7. Migration steps

| Step | Change | Risk |
|---|---|---|
| 1 | Read `Success` in the current services; return the engine error (4.4). | Low. Removes H8. |
| 2 | Move the engine call to the end of the transaction in the current services (4.3). | Medium. Removes most of H6. Test each CDT step. |
| 3 | Add `IAtlasWorkflowAdapter`; change CDT, SQMSO, SQMSB to call it. Compare the engine requests (envelope JSON) before and after for the same actions. | Medium |
| 4 | Move the dispatch after the commit (4.5). | Low |
| 5 | Add the state check on load (4.3). | Low |
| 6 | Add `RequestId` to the client and the adapter (4.7). | Low |
| 7 | Change the recipients to LAN ID (4.6), after the LAN ID backfill of module 6. | Medium |

Steps 1 and 2 correct two High issues in the current code and can go first.

## 8. Acceptance

- The engine refuses an action (for example a wrong role): the response has the engine `ResultCode` and message; the record and the engine state do not change.
- A failure after the engine call (test: a forced error in step 5) leaves the engine state changed; the next load of the record shows the engine state and writes one `WorkflowSync` audit entry.
- A repeated request with the same `RequestId` runs the action one time.
- A recipient field with an unknown user gives a field error, not HTTP 500.
- For the same actions, the envelope JSON of CDT, SQMSO and SQMSB is the same before and after step 3 (except `ClientActionId` and `IdempotencyKey`).
- A new app with a binding and no C# code starts and executes its workflow.

## 9. Open questions

1. Answered (2026-10-25): yes. Engine last, then commit; the state check on the next load and an error log are enough. No background check (4.3).
2. Answered (2026-10-25): yes. The target runs the scheduled job with the outbox retry; the missing engine migrations (H9) will be applied (prerequisite in module 11).
3. Answered (2026-10-25): yes, a refused engine action always stops the save (4.4).
4. Answered (2026-10-25): yes. The next Step Master app uses the same engine and adapter, with its own JSON binding (section 5).
