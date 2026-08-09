# Workflow API Envelope Optimization Design

**Status:** Approved conversational design, pending written-spec review

**Date:** 2026-08-09

**Scope:** Public `WorkflowStart` and `WorkflowAction` request envelopes, metadata-driven notification-recipient resolution, and the runtime recipient snapshot used by SLA reminders

## 1. Goal

Simplify the UI-facing workflow API request bodies without changing the generic, metadata-driven workflow model.

The UI or upstream form API sends only information that is genuinely known at request time. The workflow engine derives workflow state, destination state, task behavior, notification templates, static notification recipients, SLA configuration, actor evidence, and other server-owned values.

The optimized contract remains workflow-only. It does not accept form payloads, UI echo fields, `toState`, workflow options, or application-specific business rules.

## 2. Locked Design Decisions

- Backward compatibility with the current public request DTOs is not required.
- Existing development/runtime workflow data may be discarded when applying the new schema.
- The existing `POST /v1/WorkflowStart` and `POST /v1/WorkflowAction` capabilities remain; endpoint-route redesign is outside this envelope-focused change.
- The internal console `WorkflowActionEnvelope` remains separate from the public API DTOs.
- The UI sends `actionKey`; it never sends `toState`.
- Actor identity and roles continue to come from the trusted integration boundary rather than the request body.
- `businessObjectType` is workflow-definition metadata and is not supplied by the Start request.
- `initialActionKey` is renamed to `actionKey` so Start and Action use the same command name.
- The only caller-controlled SLA value is a flat optional `slaTargetDate` date.
- SLA-only synchronization continues through `POST /v1/WorkflowAction`; no second SLA endpoint is added.
- Notification template values and dynamic recipient inputs are grouped under one optional `notification` block.
- Notification subject, plain-text body, HTML body, template selection, and language remain workflow metadata.
- Metadata owns recipient routing: it determines which sources contribute to To and Cc.
- Static recipient addresses are stored directly in workflow metadata.
- The original Maker is supplied once when the workflow starts, stored as an immutable workflow participant, and reused by later routes.
- Selected UI-field recipients are supplied only on actions whose route metadata declares the corresponding request input.
- The final resolved To/Cc arrays are snapshotted when a task or notification is created.
- A task's immediate notification and every later SLA reminder use the same task recipient snapshot.
- The task snapshot is immutable. A newly created task receives a newly resolved snapshot.
- Address strings are opaque. The engine performs no syntax, directory, mailbox, domain, or SMTP validation.
- One outbox row remains one complete message with one message-level delivery/retry lifecycle.
- No per-recipient delivery tracking is introduced.

## 3. Public Request Contracts

### 3.1 Start workflow

`POST /v1/WorkflowStart`

```json
{
  "workflowKey": "PortfolioTrade",
  "businessObjectId": "PT-1001",
  "actionKey": "SubmitForChecker",
  "slaTargetDate": "2030-01-20",
  "notification": {
    "recipientInputs": {
      "originalMaker": [
        {
          "userId": "maker123",
          "emailAddress": "maker@example.test",
          "displayName": "Maker One"
        }
      ]
    },
    "templateData": {
      "submitterDisplayName": "Maker One",
      "deskName": "Taxable",
      "tradeLink": "https://example.test/trades/PT-1001"
    }
  }
}
```

Required fields:

- `workflowKey`
- `businessObjectId`

Optional fields:

- `actionKey`, when Start should immediately execute the first metadata-valid action
- `slaTargetDate`, when that first action creates or synchronizes an application-target-date task
- `notification`, when the selected route requires participant data, request recipient inputs, or template values

The Start service resolves `businessObjectType` from the selected workflow definition. It captures `originalMaker` only when metadata declares that participant input. The seeded workflow declares exactly one required immutable `originalMaker` participant at Start.

### 3.2 Execute an action

`POST /v1/WorkflowAction`

```json
{
  "workflowInstanceId": "WF-PT-1001",
  "actionKey": "CheckerReturnToMaker",
  "notification": {
    "recipientInputs": {
      "selectedCcUsers": [
        {
          "userId": "manager456",
          "emailAddress": "manager@example.test",
          "displayName": "Manager One"
        },
        {
          "userId": "compliance789",
          "emailAddress": "compliance@example.test",
          "displayName": "Compliance One"
        }
      ]
    },
    "templateData": {
      "returnReason": "Supporting documentation is missing"
    }
  }
}
```

Required fields for a transition:

- `workflowInstanceId`
- `actionKey`

Optional fields:

- `assignedRoleKey`, only when task metadata permits dynamic role assignment
- `slaTargetDate`, only as application-owned target-date input
- `notification`, when route metadata requires dynamic recipient inputs or template data

The request does not echo workflow key, definition version, business-object identity, current state, current task, or workflow-instance version. Those values are read from persistence and metadata.

### 3.3 SLA-only synchronization

An actionless request remains available through the existing Action capability:

```json
{
  "workflowInstanceId": "WF-PT-1001",
  "slaTargetDate": "2030-01-25"
}
```

This request updates only the active application-target-date schedule. It does not move workflow state, increment `WorkflowInstance.Version`, create workflow audit, or replace the active task's recipient snapshot.

### 3.4 Fields removed from public JSON

| Current public field | Optimized treatment |
|---|---|
| `businessObjectType` | Derived from workflow-definition metadata |
| `initialActionKey` | Renamed to `actionKey` |
| `idempotencyKey` | Generated internally for Day 1; a future public idempotency contract may use an HTTP header |
| `slaInput.targetDate` | Flattened to `slaTargetDate` |
| `notificationContext` | Renamed to `notification.templateData` |
| `notificationRecipients` | Replaced by metadata rules, persisted participants, and named `notification.recipientInputs` |

## 4. Recipient Source Model

Recipient placement is metadata-driven. A notification route contains ordered recipient rules. Each rule has a recipient kind and one source.

Supported source types for this design:

| Source type | Meaning | Example |
|---|---|---|
| `StaticAddress` | Opaque address stored directly in metadata | `workflow-operations@example.test` |
| `WorkflowParticipant` | Immutable participant captured on the workflow instance | `originalMaker` |
| `RequestInput` | Values supplied for this action under an allowed input key | `selectedCcUsers` |

Example metadata for `RouteReturnedToMakerFromChecker`:

```text
1. To <- WorkflowParticipant("originalMaker")
2. Cc <- RequestInput("selectedCcUsers")
3. Cc <- StaticAddress("workflow-operations@example.test")
```

The UI cannot choose To/Cc placement. It can only supply values for named request inputs declared by the effective route metadata. Unexpected recipient-input keys are rejected as a workflow request error.

A rule may mark a participant or request input as required or optional. A missing required source fails the action before workflow/task/outbox effects commit. A notification-producing route that resolves no To addresses also fails before commit. These are recipient-resolution completeness checks, not validation of the address strings themselves.

## 5. Original Maker Lifecycle

`originalMaker` is a generic workflow participant key configured by metadata; it is not hardcoded as Portfolio Trade engine behavior.

For the seeded workflow:

1. The UI or upstream form API resolves the Maker and supplies the participant during Workflow Start.
2. The engine stores exactly one runtime participant snapshot containing the supplied user ID, email address, and display name.
3. The participant cannot be replaced, removed, or resubmitted on later Action requests.
4. Routes that notify the Maker reference `WorkflowParticipant("originalMaker")`.
5. Routes that do not reference the Maker do not add the Maker merely because the participant exists.

The engine does not perform a user lookup and does not compare the supplied address with SMTP or a directory.

## 6. UI-Selected CC Lifecycle

Some application form fields select people who should receive copied notifications. The application maps those selections to a metadata-declared request key such as `selectedCcUsers`.

- The input is action-specific.
- Metadata decides whether it contributes to To or Cc.
- It is accepted only on routes declaring that key.
- If the action creates a task, the selected users become part of that task's fixed recipient snapshot.
- The immediate task notification and every SLA reminder for that task reuse the same snapshot.
- Edits to the form or recipient metadata do not silently change the snapshot of an existing task.
- When a later transition creates a new task, its action resolves a fresh snapshot and may supply a different selected-user list.

No separate operation for changing recipients on an existing pending task is included.

## 7. Recipient Merge and Duplicate Rules

The engine evaluates recipient rules by metadata `SortOrder`. Values inside a participant or request-input collection retain their supplied order.

Final arrays are produced deterministically:

1. Resolve all To rules in order.
2. Resolve all Cc rules in order.
3. Within To, retain the first occurrence of an opaque address using case-insensitive comparison.
4. Within Cc, retain the first occurrence using case-insensitive comparison.
5. If the same address appears in both arrays, retain it in To and remove it from Cc.

Deduplication does not trim, parse, normalize, or rewrite an address. The first retained string is persisted exactly as supplied or configured. For example, strings that differ only by surrounding whitespace remain different opaque values and may later cause a sender failure.

## 8. Persistence Changes

### `WorkflowDefinition`

Add required `BusinessObjectType`. Workflow Start reads it from the active workflow definition rather than accepting it from public JSON.

### `WorkflowParticipantDefinition`

Versioned metadata declares participant inputs:

```text
Id
WorkflowDefinitionVersionId
ParticipantKey
Cardinality              -- Single or Many
CaptureStage             -- Start for this design
IsRequired
IsImmutable
IsActive
```

The seeded `originalMaker` definition is `Single`, `Start`, required, immutable, and active.

### `WorkflowParticipant`

Runtime participant snapshots:

```text
Id
WorkflowInstanceId
ParticipantKey
UserId nullable
EmailAddress
DisplayName nullable
SortOrder
CreatedAtUtc
```

The engine treats `EmailAddress` as an opaque string. Structural metadata rules enforce participant cardinality, but the address value is not validated.

### `NotificationRecipientRule`

Versioned route metadata:

```text
Id
NotificationRouteId
RecipientKind            -- To or Cc
SourceType               -- StaticAddress, WorkflowParticipant, RequestInput
SourceKey nullable
StaticAddress nullable
SortOrder
IsRequired
IsActive
```

Seed verification checks structural consistency: valid route, supported recipient kind/source type, required source fields, unique ordering, and resolvable participant keys. It does not validate address syntax or existence.

### Existing recipient snapshots

`WorkflowTask.NotificationRecipientsJson` and `NotificationOutbox.NotificationRecipientsJson` remain. They store the final resolved To/Cc arrays and continue to be the durable handoff to SLA processing and notification delivery.

The dormant `NotificationRoute.RecipientRoleKey` and `RecipientUserId` columns are removed because backward compatibility is not required and the new rules replace them.

## 9. Runtime Flow

For a notification-producing transition:

```text
Resolve workflow instance and transition
-> resolve versioned notification route and recipient rules
-> load immutable workflow participants
-> bind permitted request recipient inputs
-> add static metadata addresses
-> build deterministic To/Cc arrays
-> apply duplicate rules
-> render metadata templates with notification.templateData
-> close/create task as directed by transition metadata
-> save the task recipient snapshot when a task is created
-> save the same final snapshot on the immediate outbox row
-> commit workflow effects transactionally
```

Notification delivery remains outside the workflow transaction. An SMTP or address-parsing failure after commit leaves the complete outbox message pending under the existing retry lifecycle.

## 10. SLA Reminder Flow

When the SLA monitor creates a reminder:

1. Read the active pending task and its SLA schedule.
2. Read the task's stored recipient snapshot.
3. Copy the snapshot unchanged to the new SLA outbox row.
4. Render the appropriate metadata notification template.
5. Commit the reminder occurrence under the existing duplicate-prevention rules.

The monitor does not re-read recipient rules, workflow participants, request inputs, application data, a directory, or SMTP. This ensures every reminder for a task uses the recipients established when that task was created.

## 11. Address Handling and Delivery Failure

The engine does not:

- Parse or validate email syntax before persistence.
- Trim, normalize, or rewrite an address.
- Check a domain, directory, mailbox, or SMTP server.
- Expand an address into individual distribution-list members.
- Track delivery independently for each recipient.

MailKit necessarily interprets each opaque value while constructing the outbound message. If message construction or SMTP sending fails because of any recipient value:

- The complete email attempt fails.
- The outbox row remains or returns to `Pending` under the existing processing behavior.
- One `RetryCount` is incremented and one `LastError` is stored for the message.
- Other addresses are not marked independently successful or failed.

`Smtp:RecipientOverride` remains the only configured delivery-address override for development and test environments.

## 12. Generic Engine Boundary

The generic workflow engine owns:

- Participant metadata and immutable participant snapshots.
- Versioned notification-recipient rules.
- Recipient-source resolution and To/Cc placement.
- Deterministic merge behavior.
- Task/outbox recipient snapshots.
- SLA reuse of the task snapshot.
- Message-level delivery and retry evidence.

The UI or upstream application owns:

- Resolving the original Maker before Start.
- Resolving users selected in application fields.
- Supplying only the participant and request-input keys declared by workflow metadata.
- Supplying notification template values.
- Persisting and validating its own business/form data.

The engine never knows Portfolio Trade field names and never reads the application database.

## 13. Errors and Transaction Behavior

- Missing required public request fields produce API validation failure.
- An unknown workflow, action, recipient-input key, participant key, or dynamic assignment remains a workflow validation failure.
- Missing required participant or request recipient input fails before workflow side effects commit.
- A notification-producing route resolving zero To recipients fails before workflow side effects commit.
- Address contents are not validated. Invalid strings may therefore commit successfully and fail only during delivery.
- Delivery failure after commit does not roll back the workflow transition; it returns the existing notification-delivery failure response and retains the outbox row for retry.
- SLA-only synchronization never modifies a task recipient snapshot.
- All new timestamps use injected `TimeProvider`.

## 14. Response Contracts

This change does not redesign the successful response shapes:

- Workflow Start returns its action result, when applicable, and refreshed workflow context.
- Workflow Action returns the action result and refreshed workflow context.
- Workflow Context remains the form-open read model.

Recipient-definition metadata does not need to be returned to a generic browser UI in this scope. The upstream form integration already knows which controlled fields map to declared participant and request recipient-input keys.

## 15. Migration and Seed Strategy

Because backward compatibility and runtime-data preservation are not required:

- Delete or recreate existing runtime task, outbox, participant, audit, idempotency, and workflow-instance data through an explicit migration/deployment step.
- Add the new definition, participant, and recipient-rule tables/columns.
- Seed `WorkflowDefinition.BusinessObjectType`.
- Seed the `originalMaker` participant definition.
- Replace dormant route-role recipient hints with explicit ordered recipient rules.
- Seed real environment-appropriate static/shared-mailbox addresses through the existing deployment-owned metadata process.
- Do not build a compatibility fallback to caller-supplied final To/Cc arrays.

The application and database are never silently reset at runtime.

## 16. Verification Strategy

### Contract tests

- Start accepts the optimized fields and derives `businessObjectType`.
- Start and Action use `actionKey` consistently.
- Public JSON rejects removed fields.
- Actor identity cannot be supplied in the body.
- SLA accepts only the flat `slaTargetDate` value.
- `notification.templateData` remains template-only data.
- Only route-declared `notification.recipientInputs` keys are accepted.

### Metadata and participant tests

- Seed verification validates participant/rule structure but not address contents.
- Start captures exactly one immutable `originalMaker`.
- Later actions cannot replace or remove the Maker.
- Static, participant, and request-input sources resolve through generic metadata.
- Route metadata controls To/Cc placement.
- Missing required sources and zero-To resolution roll back the action.

### Merge and persistence tests

- Rule and input ordering is deterministic.
- Case-insensitive duplicates retain the first opaque value.
- To wins over Cc for a cross-list duplicate.
- No trimming or syntax validation occurs.
- The created task and immediate outbox row receive the same final snapshot.
- A later task receives a newly resolved snapshot.

### SLA and delivery tests

- Every reminder copies the task snapshot unchanged.
- SLA-only synchronization cannot replace recipients.
- Invalid address strings can be persisted and cause complete-message delivery failure.
- Delivery failure returns the row to `Pending` with one retry count/error lifecycle.
- Existing SMTP override and one-message behavior remain intact.

### Quality gates

Implementation completion must use the applicable commands from `PROJECT.md` section 21, including sequential warnings-as-errors builds, automated tests, SQL-backed tests, clean migration/seed verification, sample JSON execution, and the live notification/SLA acceptance path.

## 17. Superseded Decisions and Required Documentation Updates

This approved design supersedes only the recipient-source and public-envelope portions of `2026-08-03-multiple-notification-recipients-design.md`:

- The caller no longer supplies the complete final To/Cc arrays.
- The caller no longer owns To/Cc placement.
- The engine now performs metadata and participant lookup.
- The engine applies the approved duplicate rules.
- The engine does not validate address contents.

The earlier design remains authoritative for task/outbox snapshots, SLA snapshot reuse, one-message delivery, message-level retry, and no per-recipient tracking.

After written-spec approval and before implementation, update `PROJECT.md`, `workflow-api/WORKFLOW_API_BOUNDARIES.md`, samples, and integration documentation so the repository source of truth no longer states the superseded caller-owned-recipient contract.

## 18. Non-Goals

- Form or business-object persistence.
- User authentication, user-to-role mapping, or directory lookup.
- SMTP mailbox or address validation.
- Per-recipient delivery status or retry.
- Mutable participants or recipient updates on an active task.
- Runtime expansion of distribution-list members.
- A second SLA synchronization endpoint.
- Changes to workflow transition authority, task assignment authorization, audit semantics, or SLA calculations.
- Portfolio Trade-specific behavior in reusable engine code.
- CQRS, MediatR, event sourcing, BPMN, message brokers, microservices, or plugin frameworks.
