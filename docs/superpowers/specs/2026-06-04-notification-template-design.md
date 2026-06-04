# Notification Template Design

Date: 2026-06-04
Status: Approved for implementation planning

## Goal

Add route-specific email subject and body templating while keeping the reusable workflow engine generic.

The external API owns business/form data. It will pass a generic notification context dictionary when it executes workflow actions. The workflow engine will snapshot that context and use workflow metadata to render notifications. The engine must not query external business tables and must not hardcode Directed Trade wording in reusable workflow logic.

## Non-Goals

- Do not add user lookup, role lookup, or SQL user-to-role mapping.
- Do not make the hosted SLA service call the external API for business data.
- Do not add template scripting, loops, conditionals, or third-party template packages.
- Do not require a return reason in any template.
- Do not move form validation or business object persistence into the workflow engine.

## Data Flow

1. External API receives or already has the business details needed for notifications.
2. External API calls workflow execution with a generic `notificationContext` dictionary.
3. Workflow execution validates metadata, creates or closes tasks, and resolves the route-specific `NotificationRoute`.
4. Workflow execution snapshots the notification context for created task notifications.
5. Workflow execution renders the route `SubjectTemplate` and `BodyTemplate` from the snapshot and persists the generated notification outbox row.
6. SMTP dispatch sends the persisted subject/body. Retry uses the persisted outbox data and does not re-render from live external data.
7. Hosted SLA processing scans overdue workflow tasks, reuses the task notification route/context snapshot, prefixes the subject with SQL metadata such as `Overdue: `, and queues/sends an overdue notification without calling the external API.

## Metadata Model

Extend `NotificationRoute` metadata:

- `SubjectTemplate`: existing route-specific subject template.
- `BodyTemplate`: new route-specific plain-text body template.
- `OverdueSubjectPrefix`: new metadata value with default `Overdue: `.

Templates are route-specific, not notification-type-specific. `WorkflowTaskAssigned` remains a category; exact email wording belongs to the route, such as `RouteCheckerAssignedOnSubmit` or `RouteTradingDeskAssigned`.

## Template Rendering

Templates use simple tokens:

```text
{{tokenName}}
```

Rules:

- Tokens are replaced from the action's `notificationContext` snapshot and workflow-owned values.
- Missing required tokens fail before a broken notification is sent.
- Whitespace is preserved except normal line ending handling.
- No conditionals, loops, scripting, or business-specific renderer code.
- Values are rendered as plain text for the current SMTP implementation.

## External API Context Keys

The external API should provide these keys when they are needed by the selected route:

```text
referenceNumber
submitterDisplayName
returnedByDisplayName
recipientDisplayName
traderDisplayName
confirmerDisplayName
deskName
securityType
accountShortName
accountDisplayName
submittedDate
tradeLink
```

The workflow engine can also add generic workflow values such as `workflowInstanceId`, `actionKey`, `oldState`, `newState`, and `taskKey`.

## Seed Templates

### RouteCheckerAssignedOnSubmit

Recipient: `Checker`

Subject:

```text
Directed Trade Pending Checker Approval
```

Body:

```text
This is to advise that {{submitterDisplayName}} has submitted a request for the {{deskName}} desk for security type {{securityType}} to execute a trade for account {{accountShortName}}, {{accountDisplayName}}.
The trade detail for account information can be accessed by clicking on the following
Sharepoint link = {{tradeLink}}
```

### RouteCheckerAssignedOnResubmit

Recipient: `Checker`

Subject:

```text
Directed Trade Pending Checker Approval
```

Body:

```text
This is to advise that {{submitterDisplayName}} has submitted a request for the {{deskName}} desk for security type {{securityType}} to execute a trade for account {{accountShortName}}, {{accountDisplayName}}.
The trade detail for account information can be accessed by clicking on the following
Sharepoint link = {{tradeLink}}
```

### RouteReturnedToMakerFromChecker

Recipient: `Maker`

Subject:

```text
Updates Required to Directed Trade Request
```

Body:

```text
Reference {{referenceNumber}} was returned from {{returnedByDisplayName}} to {{recipientDisplayName}}.
This is for account {{accountShortName}}, {{accountDisplayName}} originally submitted on {{submittedDate}}
The account and trade information can be accessed by clicking on the following SharePoint link=> {{tradeLink}}
```

### RouteTradingDeskAssigned

Recipient: dynamic task assignment role such as `TradingDeskTaxable`, `TradingDeskTaxExempt`, or `TradingDeskEquity`.

Subject:

```text
Directed Trade Request Pending Execution
```

Body:

```text
This is to advise that {{submitterDisplayName}} has submitted a request for the {{deskName}} desk for security type {{securityType}} to execute a trade for account {{accountShortName}}, {{accountDisplayName}}.
The trade detail for account information can be accessed by clicking on the following Sharepoint link => {{tradeLink}}
```

### RouteReturnedToMakerFromTradingDesk

Recipient: `Maker`

Subject:

```text
Updates Required to Directed Trade Request
```

Body:

```text
Reference {{referenceNumber}} was returned from {{returnedByDisplayName}} to {{recipientDisplayName}}.
This is for account {{accountShortName}}, {{accountDisplayName}} originally submitted on {{submittedDate}}
The account and trade information can be accessed by clicking on the following SharePoint link=> {{tradeLink}}
```

### RouteBdrmAssigned

Recipient: `BDRMClientService`

Subject:

```text
Trade Execution Pending Confirmation
```

Body:

```text
This is to confirm that the trade request for account {{accountShortName}}, {{accountDisplayName}} has been completed by trader {{traderDisplayName}} on the {{deskName}} desk for security type {{securityType}}.
The trade detail and account information can be accessed by clicking on the following Sharepoint link => {{tradeLink}}
```

### RouteWorkflowCompleted

Recipient: `Maker`

Subject:

```text
Confirmation that Trade settled in account
```

Body:

```text
This is to confirm that {{confirmerDisplayName}} has verified that the trade was executed and settled in account {{accountShortName}}, {{accountDisplayName}}. The trade request on Sharepoint is now closed.
The trade detail and account information can be accessed by clicking on the following Sharepoint link => {{tradeLink}}
```

### RouteTradingDeskReworkAssigned

Recipient: `TradingDesk`

Subject:

```text
Directed Trade Request Pending Rework
```

Body:

```text
This is to advise that the trade request for account {{accountShortName}}, {{accountDisplayName}} was returned to the {{deskName}} desk for rework.
The trade detail and account information can be accessed by clicking on the following Sharepoint link => {{tradeLink}}
```

## Overdue Notifications

Overdue notifications use the same route-specific body template and context snapshot as the task assignment notification. The subject is the original rendered subject with the SQL metadata prefix prepended.

Example:

```text
Overdue: Directed Trade Pending Checker Approval
```

The hosted SLA process reads only workflow database state. It must not call the external API for current trade/account data.

## Failure Behavior

If a required template token is missing, workflow action execution should fail before queueing a malformed notification. For action-triggered notifications, this means no successful workflow response should hide a broken notification. For SLA processing, the overdue row should remain pending or be reported as a processing failure with a clear missing-token message, matching the existing outbox retry model.

## Testing

Implementation should add focused coverage for:

- Template token replacement with plain-text values.
- Missing-token failure behavior.
- Seed verification that route subject/body templates exist.
- Action execution persists rendered subject/body and the source context snapshot.
- `SubmitForChecker` renders the Checker approval email with external context values.
- Return-to-Maker routes render without a return reason.
- Dynamic Trading Desk assignment renders recipient role and body from the selected desk context.
- SLA overdue rendering reuses the task route/context and prefixes the subject.
- SMTP sender sends the persisted body rather than building a generic key/value body for templated notifications.

## Approval Notes

The user approved:

- External API supplies notification data.
- Templates live in SQL workflow metadata.
- Overdue subject prefix lives in SQL metadata.
- Hosted SLA service reuses stored workflow notification context and does not call the external API.
- Return reason is removed from all templates.
