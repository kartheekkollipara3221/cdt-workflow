# Multiple Notification Recipients Design

Date: 2026-08-03
Status: Partially superseded by the approved 2026-08-09 Workflow API Envelope Optimization design

## Supersession Note

The following parts of this design remain authoritative:

- One resolved task/outbox recipient snapshot.
- Exact snapshot reuse by immediate notification and later SLA reminders.
- One email and one message-level outbox status/retry lifecycle.
- No syntax, directory, mailbox, or SMTP validation of stored address strings.
- No backward compatibility with singular or delimited recipient fields.

The following parts are superseded by `2026-08-09-workflow-api-envelope-optimization-design.md`:

- Calling applications no longer supply the final To/Cc arrays through public `notificationRecipients`.
- Versioned `NotificationRecipientRule` metadata owns recipient source selection and To/Cc placement.
- Workflow Start captures `originalMaker` once as an immutable workflow participant; actions may supply only route-declared named recipient inputs.
- The engine resolves and case-insensitively deduplicates final addresses, retains the first opaque string, and gives To precedence over Cc.
- Neither the UI nor the engine is required to validate an address before it is stored.

Historical sections below describe the original caller-owned snapshot design and must not be used as the current public API contract.

## Goal

Allow any workflow integration, including CDT and SQMS, to provide multiple primary and copied email recipients for an action-created notification. The reusable workflow engine must persist the supplied recipient lists, send the initial notification to those lists, and reuse the same lists for later SLA reminder notifications.

The engine remains generic. CDT, SQMS, and future calling applications decide who receives each notification and validate the email addresses before calling the workflow API.

## Approved Decisions

- Calling applications supply concrete email addresses.
- A notification supports multiple `To` recipients and multiple `Cc` recipients.
- One outbox row represents one email message containing all supplied recipients.
- Delivery status and retry information remain message-level, not recipient-level.
- The engine does not validate, normalize, deduplicate, resolve, or look up recipient addresses.
- The exact recipient lists supplied when the task is created are stored as the task's recipient snapshot.
- The initial notification and all SLA reminders for that task use the saved snapshot.
- SLA processing does not call CDT, SQMS, a directory, or another external API to refresh recipients.
- Existing notification and in-flight task data may be discarded. No legacy recipient-data migration is required.
- Backward compatibility with singular or delimited recipient fields is not required.

## Non-Goals

- Do not track delivery independently for each recipient.
- Do not retry one recipient separately from the rest of the message.
- Do not verify that an email address or mailbox exists.
- Do not add user-directory integration or SQL user-to-role mappings.
- Do not resolve email addresses from workflow roles or SMTP role maps.
- Do not refresh recipients when the SLA monitor runs.
- Do not change workflow transitions, task assignment, SLA calculation, audit behavior, or notification templates as part of this capability.
- Do not add business-specific CDT or SQMS behavior to the reusable notification sender.
- Do not support the legacy singular `recipientEmail` field or delimited `ccEmailAddresses` string.

## Request Contract

The workflow action carries a typed recipient block:

```json
"notificationRecipients": {
  "toEmailAddresses": [
    "person1@example.com",
    "person2@example.com"
  ],
  "ccEmailAddresses": [
    "manager@example.com",
    "operations@example.com"
  ]
}
```

The same typed block is available to any start or action request that can create a notification or an SLA-enabled task.

Contract expectations:

- The calling application supplies the complete `To` and `Cc` lists.
- The calling application is responsible for address validation and recipient business rules.
- `toEmailAddresses` contains the primary recipients.
- `ccEmailAddresses` contains copied recipients and may be empty.
- Array order and values are preserved as supplied.
- Recipient lists are included in the effective request and idempotency hash. Reusing an idempotency key with different recipient lists is an idempotency conflict.
- Notification template data remains in `notificationContext`; recipient lists are not embedded in template context.

## Persistence Model

Recipients are stored as a structured snapshot separate from template context.

- `WorkflowTask` stores the complete recipient snapshot for future SLA reminders.
- `NotificationOutbox` stores the complete recipient snapshot for the message being delivered.
- Application delivery contracts expose typed `To` and `Cc` collections rather than reading addresses from `PayloadJson`.
- Notification template payload and recipient payload remain separate concerns.

Because backward compatibility and existing runtime data preservation are not required, implementation may replace legacy scalar recipient persistence with a structured recipient snapshot without backfilling existing rows. SMTP role-map recipient resolution is removed from the delivery path.

Workflow role assignment remains unchanged. A task's `AssignedRoleKey` continues to control workflow authorization and task ownership, but it no longer determines email addresses.

## Initial Notification Flow

1. CDT, SQMS, or another caller determines and validates all `To` and `Cc` addresses.
2. The caller sends the typed recipient block with the workflow start or action request.
3. Workflow execution resolves transition and notification metadata normally.
4. If a task is created, workflow execution stores the recipient block as the task snapshot.
5. Workflow execution copies the recipient block to the initial notification outbox row.
6. The outbox processor passes the typed lists to the configured notification sender.
7. The SMTP sender creates one message, adds every `To` address to `MimeMessage.To`, and adds every `Cc` address to `MimeMessage.Cc`.

The notification route continues to select notification type and templates. It does not select or resolve concrete email recipients.

## SLA Reminder Flow

1. The SLA monitor finds an overdue pending task using the existing task-driven SLA rules.
2. It reads the recipient snapshot stored on that task.
3. It copies the snapshot unchanged into the SLA reminder outbox row.
4. The notification sender sends one reminder message to the saved `To` and `Cc` lists.

The SLA reminder intentionally uses the original saved recipients. Changes in CDT or SQMS after task creation do not silently change the reminder recipients. Updating recipients for a pending task would require a separately designed, explicit operation and is outside this design.

The existing rule that queues at most one `SlaTaskOverdue` notification per workflow task remains unchanged.

## Delivery and Failure Behavior

The workflow engine performs no pre-send email syntax validation. MailKit necessarily parses each persisted address while constructing the SMTP message.

If message construction or SMTP sending fails because of a recipient value:

- The entire email attempt fails.
- The outbox row returns to `Pending` under the existing retry behavior.
- `RetryCount` and `LastError` are updated for the message.
- No recipient is tracked or retried independently.

This is acceptable because CDT and SQMS validate recipients before submitting them and per-recipient delivery guarantees are not required.

`Smtp:RecipientOverride` remains a development and testing safety feature. When configured, the sender delivers only to the override address and suppresses all original `To` and `Cc` recipients while retaining enough diagnostic information to identify the intended recipient lists.

## Generic Engine Boundary

The reusable engine owns:

- Typed recipient transport in workflow requests.
- Recipient snapshot persistence on tasks and outbox messages.
- Reuse of task recipient snapshots for SLA reminders.
- Durable message-level outbox processing and retry evidence.

The calling CDT or SQMS application owns:

- Selecting the people who belong in `To` and `Cc`.
- Validating addresses.
- Applying business rules, delegation, subscriptions, or distribution logic.
- Sending complete concrete recipient lists with the workflow request.

This boundary allows future workflows to use the same recipient contract without adding workflow-specific code or external lookups to the engine.

## Testing Strategy

Implementation must add focused coverage for:

- API and application contracts accept multiple `To` and multiple `Cc` addresses.
- Workflow execution persists the exact lists on the initial outbox row.
- Task creation persists the same recipient snapshot.
- SLA processing copies the exact task snapshot to the reminder outbox row.
- SMTP message construction adds every supplied address to the correct `To` or `Cc` collection.
- Notification processing retains one status and retry count for the complete message.
- A MailKit parsing or SMTP failure returns the complete outbox row to `Pending` and records the error.
- `Smtp:RecipientOverride` suppresses all original `To` and `Cc` addresses.
- Different recipient lists with the same idempotency key produce an idempotency conflict.
- Recipient order and values survive persistence and SLA reuse unchanged.
- No SMTP role-map, singular recipient, or delimited-recipient fallback is used.
- Existing SLA duplicate prevention remains one overdue notification per task.

## Approval Notes

The user approved:

- CDT and SQMS supply and validate concrete recipient addresses.
- Notifications contain multiple `To` and multiple `Cc` recipients in one email.
- Per-recipient delivery tracking is not required.
- The engine performs no duplicate address validation.
- SLA reminders use the original recipient snapshot saved with the task.
- Existing runtime task and notification data may be discarded.
- No backward compatibility is required for legacy recipient fields or role-map resolution.
