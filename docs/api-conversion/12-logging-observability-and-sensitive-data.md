# Logging, Observability, and Sensitive Data

The console uses `Console.Out` and `Console.Error`. The API should use structured logging, correlation IDs, and the external API's observability standard.

## Current Output Behaviors

- `Program.cs` writes command success to stdout and failure to stderr.
- `ExecuteJsonCommandHandler` returns readable text or JSON.
- `RunSlaCommandHandler` builds text summary.
- `ProcessNotificationOutboxCommandHandler` writes header lines directly to `Console.Out`.
- `ConsoleNotificationSender` writes notification delivery lines to the console.
- `WorkflowExecutionService` writes `workflow.options` conflict warnings to `Console.Error`.

For API integration, prefer `ILogger<T>` and structured fields. Do not log raw form payloads by default.

## Logging Table

| Event | Current Behavior | Recommended API Log Level | Fields To Log | Fields Not To Log |
|---|---|---|---|---|
| Action received | File read by command handler. | Information | `workflowInstanceId`, `businessObjectId`, `actionKey`, `correlationId`, authenticated `userId`. | `dataSave.payload`. |
| Validation failed | Result envelope. | Warning | `resultCode`, validation field names, `correlationId`. | Raw payload, full request body. |
| Forbidden | Result envelope. | Warning | `workflowInstanceId`, `actionKey`, effective role count, `userId`. | Untrusted body roles, raw claims dump. |
| Invalid transition | Result envelope. | Warning | `workflowInstanceId`, `actionKey`, current state if available. | Raw payload. |
| Concurrency conflict | Result envelope. | Information or Warning | failed guard, expected/actual, `workflowInstanceId`. | Raw payload. |
| Idempotent replay | Result envelope. | Information | idempotency key hash/prefix, `workflowInstanceId`, `actionKey`. | Full idempotency key if sensitive. |
| Idempotency conflict | Result envelope. | Warning | stored/current hash prefixes, `workflowInstanceId`, `actionKey`. | Full payload. |
| Successful transition | Result envelope. | Information | old/new state, closed/created task keys, notification queued, audit written. | Raw payload. |
| Start workflow | Text/result envelope. | Information | `workflowKey`, `businessObjectId`, started-by user, result code. | Reset details for normal user logs if not exposed. |
| Context read | JSON output. | Debug or Information | `workflowInstanceId`, user ID, role count, available action count. | Raw claims dump. |
| SLA scan | Text summary. | Information | pending count, queued count, already queued count, warnings. | Notification payload JSON. |
| Notification outbox processing | Text summary. | Information | pending count, processed count, sender name. | SMTP body if it contains sensitive fields. |
| Notification send failure | Command failure. | Error | notification ID, type, recipient role, workflowInstanceId, exception type/message. | SMTP credentials, raw payload. |
| Seed verification failure | Command failure. | Error | failed verification messages. | Connection string secrets. |
| Unexpected exception | Process/controller exception. | Error | trace/correlation ID, route, exception type. | Raw request body, secrets. |

## Correlation ID Handling

The envelope contains `request.correlationId`, and result envelopes echo the effective correlation ID.

Recommended API behavior:

- Prefer the API's standard correlation ID header if it exists.
- If the envelope has no correlation ID, generate one or use the request trace identifier.
- Do not trust correlation ID as identity or authorization.
- Include correlation ID in logs and response headers if the API standard supports it.

## Identifiers To Log

Generally safe operational fields:

- `workflowInstanceId`
- `workflowKey`
- `businessObject.objectType`
- `businessObject.objectId`
- `actionKey`
- result code
- old/new state keys
- task keys
- notification type
- authenticated user ID, if allowed by policy

Use caution with:

- Full role lists.
- Action reason.
- Audit reason.
- SMTP recipient addresses.
- Raw claims values.

Do not log by default:

- `dataSave.payload`
- full request body
- SMTP credentials
- connection strings
- raw exception details to client responses

## Raw Payload Prohibition

`dataSave.payload` is opaque form-section JSON. It may contain business-sensitive form fields. The API should not log it by default. If a support scenario requires payload capture, use explicit secure redaction and retention policy.

## SQL Audit Versus Application Logs

SQL audit rows are workflow evidence. Application logs are operational telemetry.

`WorkflowAudit` records successful actions only in v1. Failed actions do not write audit rows. Do not rely on audit rows to investigate failed validation, forbidden actions, or concurrency conflicts; log those at the API layer.

## Console Sender In API

`ConsoleNotificationSender` can be useful in local development but should be clearly configured as development diagnostics. Production API should use `Smtp` or another approved sender implementation.

