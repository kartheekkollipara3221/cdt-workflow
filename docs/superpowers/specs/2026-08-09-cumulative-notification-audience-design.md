# Cumulative Notification Audience Design

## Goal

Every Portfolio Trade notification must include the people accumulated along the workflow path without adding directory lookup, email resolution, or Portfolio Trade branching to the reusable engine.

The current task owner remains the authoritative `To` recipient. The original Maker and the complete caller-owned cumulative audience are copied on task assignments, return notifications, completion notifications, and every SLA reminder derived from a task.

## Approved ownership model

The calling application owns the cumulative audience.

- Workflow Start supplies the original Maker as the existing `originalMaker` recipient input. The engine captures that address once as an immutable workflow participant.
- Every later notification-producing request supplies the complete current audience under the existing `notification.recipientInputs.selectedCcUsers` key.
- `selectedCcUsers` is a full snapshot, not a delta containing only the people added during the current step.
- The caller includes the acting Checker, Trader, or BDRM user and anyone that actor added when those people must remain copied later in the workflow.
- The caller supplies the email addresses directly. The workflow API and engine perform no directory, role, mailbox, or email-address resolution.

Keeping `selectedCcUsers` avoids a new public request field. Its documented meaning changes from a route-local selection to the complete cumulative audience for that action.

## Recipient placement

For task assignment notifications and their SLA reminders:

1. The metadata-defined current task owner remains `To`.
2. The persisted immutable `originalMaker` is `Cc`.
3. The complete caller-supplied `selectedCcUsers` list is appended to `Cc`.

For Return-to-Maker and completion notifications:

1. The persisted immutable `originalMaker` is `To`.
2. The complete caller-supplied `selectedCcUsers` list is `Cc`.

The existing generic resolution rules remain authoritative: route-order evaluation, case-insensitive duplicate comparison, first opaque value retention, and `To` precedence over `Cc`. A Maker or task-owner address repeated in `selectedCcUsers` therefore appears only once in the final message.

## Caller data flow

The calling application carries the audience with its business record and sends a complete snapshot on each notification-producing action.

Example happy-path progression:

1. Maker submits: `originalMaker` contains Maker; `selectedCcUsers` contains people selected by Maker.
2. Checker sends to Trading Desk: `selectedCcUsers` contains the Maker-selected people, Checker, and people added by Checker.
3. Trader executes: `selectedCcUsers` contains the prior audience, Trader, and people added by Trader.
4. BDRM fulfills or returns the trade: `selectedCcUsers` contains the prior audience, BDRM actor, and people added by BDRM.

The engine does not merge the new request with recipients from prior tasks or outbox rows. Omitting an address from a later request intentionally omits that address from the later task, its SLA reminders, and later transition notification snapshots.

## Metadata rules

Seed the following ordered rules for every Portfolio Trade notification route:

| Route | Order | Kind | Source type | Source value | Required |
|---|---:|---|---|---|---|
| `RouteCheckerAssignedOnSubmit` | 1 | To | `StaticAddress` | Checker address | Yes |
|  | 2 | Cc | `WorkflowParticipant` | `originalMaker` | Yes |
|  | 3 | Cc | `RequestInput` | `selectedCcUsers` | No |
| `RouteCheckerAssignedOnResubmit` | 1 | To | `StaticAddress` | Checker address | Yes |
|  | 2 | Cc | `WorkflowParticipant` | `originalMaker` | Yes |
|  | 3 | Cc | `RequestInput` | `selectedCcUsers` | No |
| `RouteReturnedToMakerFromChecker` | 1 | To | `WorkflowParticipant` | `originalMaker` | Yes |
|  | 2 | Cc | `RequestInput` | `selectedCcUsers` | No |
|  | 3 | Cc | `StaticAddress` | Workflow Operations address | No |
| `RouteTradingDeskAssigned` | 1 | To | `StaticAddress` | Trading Desk address | Yes |
|  | 2 | Cc | `WorkflowParticipant` | `originalMaker` | Yes |
|  | 3 | Cc | `RequestInput` | `selectedCcUsers` | No |
| `RouteReturnedToMakerFromTradingDesk` | 1 | To | `WorkflowParticipant` | `originalMaker` | Yes |
|  | 2 | Cc | `RequestInput` | `selectedCcUsers` | No |
| `RouteBdrmAssigned` | 1 | To | `StaticAddress` | BDRM/Client Service address | Yes |
|  | 2 | Cc | `WorkflowParticipant` | `originalMaker` | Yes |
|  | 3 | Cc | `RequestInput` | `selectedCcUsers` | No |
| `RouteTradingDeskReworkAssigned` | 1 | To | `StaticAddress` | Trading Desk address | Yes |
|  | 2 | Cc | `WorkflowParticipant` | `originalMaker` | Yes |
|  | 3 | Cc | `RequestInput` | `selectedCcUsers` | No |
| `RouteWorkflowCompleted` | 1 | To | `WorkflowParticipant` | `originalMaker` | Yes |
|  | 2 | Cc | `RequestInput` | `selectedCcUsers` | No |

SLA routes continue to have no recipient rules. `run-sla` copies the exact saved task snapshot unchanged.

## Public API contract

No DTO shape change is required. Start and Action continue using the existing typed recipient-input dictionary.

```json
{
  "workflowInstanceId": "WF-PT-1001",
  "actionKey": "CheckerSendToTradingDesk",
  "assignedRoleKey": "TradingDeskTaxable",
  "slaTargetDate": "2030-01-20",
  "notification": {
    "recipientInputs": {
      "selectedCcUsers": [
        {
          "userId": "manager1",
          "emailAddress": "manager@example.test",
          "displayName": "Manager One"
        },
        {
          "userId": "checker1",
          "emailAddress": "checker@example.test",
          "displayName": "Checker One"
        }
      ]
    },
    "templateData": {}
  }
}
```

Recipient address strings remain opaque. Existing typed records and validation of declared input keys remain unchanged.

## Runtime and persistence behavior

The existing generic resolver produces one ordered final To/Cc object and stores it on both the created task and initial outbox row. SLA reminders validate and copy the task snapshot unchanged.

No new DTO, engine algorithm, persistence entity, database column, or migration is required. This is a versioned Portfolio Trade metadata, documentation, sample, and verification change.

Already-created tasks and outbox rows remain immutable historical snapshots. Applying updated seed metadata affects only notification-producing actions executed after the seed update.

## Validation and failure behavior

- Omitting optional `selectedCcUsers` remains valid. The current owner and original Maker still receive the notification according to route placement.
- Supplying an input key not declared by the selected route remains a validation failure before workflow side effects.
- Missing required `originalMaker` remains a validation failure.
- Duplicate addresses retain existing case-insensitive, first-value, and To-precedence behavior.
- Recipient syntax, directory membership, mailbox existence, and deliverability remain caller responsibilities.
- Notification delivery retains one outbox row and one message-level retry lifecycle for the complete To/Cc snapshot.

## Verification

Update coverage to prove:

1. Seed metadata contains the exact ordered rules above for all eight Portfolio Trade routes.
2. `verify-seed` rejects a missing, reordered, or structurally incorrect cumulative-audience rule.
3. Public scenario packs supply complete `selectedCcUsers` snapshots on every notification-producing action.
4. A SQL-backed happy path produces the expected cumulative task and assignment-outbox snapshots for Checker, Trading Desk, and BDRM.
5. Return-to-Maker, Trading rework, and completion notifications use the approved To/Cc placement.
6. Checker, Trading Desk, and BDRM SLA reminders copy their task snapshots unchanged.
7. Existing duplicate handling and To precedence remove repeated Maker or task-owner addresses from Cc.
8. Warning-as-error builds, full engine/API test suites, `init-db`, and `verify-seed` pass.

## Scope exclusions

- No automatic traversal or merging of prior task, audit, participant, or outbox history.
- No persisted engine-owned cumulative-audience aggregate.
- No user directory, role-address map, mailbox, syntax, or SMTP validation.
- No singular recipient fields or delimited recipient strings.
- No change to workflow transitions, SLA timing, recurrence, notification templates, or delivery retry semantics.
- No rewriting of existing tasks, outbox rows, or smtp4dev messages.
