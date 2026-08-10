# Trading Desk Recipient Routing Design

## Summary

When Checker sends a Portfolio Trade request to Trading Desk, the upstream UI/target API passes the already-supported `assignedRoleKey`. The workflow engine uses that stable role key to select one or more Trading Desk recipient rules from versioned SQL metadata.

The public API does not accept email addresses for Trading Desk routing. `notification.templateData.deskName` remains display-only and never determines recipients.

## Goals

- Keep the existing `POST /v1/WorkflowAction` request contract unchanged.
- Keep `CheckerSendToTradingDesk` as the single Checker approval action.
- Use the existing metadata-validated `assignedRoleKey` as the recipient-selection key.
- Allow any selected desk to resolve to multiple ordered email addresses.
- Preserve one message-level outbox row containing all resolved To/Cc recipients.
- Preserve the task recipient snapshot so due-today and overdue messages use the same audience.
- Keep the resolver generic and free of Portfolio Trade role-name conditions.

## Non-Goals

- Do not add email-address arrays to the public API.
- Do not route from free-text `deskName` template data.
- Do not add a user directory, role-to-email configuration map, or `DemoUser` table.
- Do not add separate workflow actions or notification routes for each Trading Desk.
- Do not change workflow state, transition, task-assignment, SLA, or public API contracts.
- Do not change the BDRM-to-Trading-Desk rework route in this change. It retains its current common Trading Desk recipient metadata.

## Public API

The existing Checker action remains:

```json
{
  "workflowInstanceId": "WF-PT-123",
  "actionKey": "CheckerSendToTradingDesk",
  "assignedRoleKey": "TradingDeskTaxable",
  "slaTargetDate": "2030-01-20",
  "notification": {
    "recipientInputs": {
      "selectedCcUsers": []
    },
    "templateData": {
      "deskName": "Taxable"
    }
  }
}
```

`assignedRoleKey` is authoritative for task assignment and recipient-rule selection. `deskName` is only a notification-template value.

## Metadata Model

Add nullable `AssignedRoleKey` to `NotificationRecipientRule`:

```text
NotificationRecipientRule
  AssignedRoleKey nullable
```

Semantics:

- `AssignedRoleKey = null`: the rule applies regardless of the selected task-assignment role.
- `AssignedRoleKey = <role>`: the rule applies only when the action selected the same role.
- Matching uses ordinal, case-sensitive role-key semantics, consistent with existing workflow metadata keys.
- Multiple matching rules are evaluated in existing `SortOrder` order and contribute multiple addresses.

This is a generic metadata filter. The resolver does not know the Portfolio Trade desk keys.

## Seed Metadata

`RouteTradingDeskAssigned` replaces its one common Trading Desk static To rule with role-filtered static To rules. Sample `.example.test` addresses prove multiple addresses per selected desk:

| Sort | Kind | AssignedRoleKey | StaticAddress |
|---:|---|---|---|
| 1 | To | `TradingDeskTaxable` | `taxable-trading-desk@example.test` |
| 2 | To | `TradingDeskTaxable` | `taxable-trading-operations@example.test` |
| 3 | To | `TradingDeskTaxExempt` | `tax-exempt-trading-desk@example.test` |
| 4 | To | `TradingDeskTaxExempt` | `tax-exempt-trading-operations@example.test` |
| 5 | To | `TradingDeskEquity` | `equity-trading-desk@example.test` |
| 6 | To | `TradingDeskEquity` | `equity-trading-operations@example.test` |
| 7 | Cc | null | immutable `originalMaker` participant |
| 8 | Cc | null | optional `selectedCcUsers` request input |

Production integration replaces the sample addresses through metadata deployment; the UI contract does not change.

All other routes remain unchanged. In particular, `RouteTradingDeskReworkAssigned` keeps its current common static Trading Desk To address.

## Resolution Flow

1. Existing assignment validation confirms the supplied `assignedRoleKey` is allowed for `TradingDeskReviewTask`.
2. Workflow execution passes the normalized assignment role to `NotificationRecipientResolver`.
3. The resolver loads active route rules where `AssignedRoleKey` is null or equals the supplied role.
4. Existing source resolution loads static, participant, and permitted request-input values.
5. Existing case-insensitive deduplication, first-value retention, and To-over-Cc precedence remain unchanged.
6. No matching To address produces the existing validation failure before workflow side effects.
7. The final ordered arrays are serialized once onto the created task and initial outbox row.
8. SLA processing copies that task snapshot unchanged.

## Seed Verification

Seed verification compares `AssignedRoleKey` in addition to the existing recipient-rule fields. For `RouteTradingDeskAssigned`, it also proves that every active role allowed for `TradingDeskReviewTask` resolves at least one applicable active To rule.

This prevents a valid UI role choice from passing assignment validation but failing later because its email mapping was omitted.

## Migration

Add one nullable `nvarchar(100)` column to `dbo.NotificationRecipientRule`. Existing rows remain valid with null, preserving their current route-wide behavior. No backfill is required beyond normal deterministic seed upsert for the Portfolio Trade sample.

## Error Handling

- Missing or invalid `assignedRoleKey` continues to fail through existing dynamic-assignment validation.
- A valid assignment role without an applicable To rule returns `ValidationFailed` before section data, task, outbox, audit, or idempotency side effects commit.
- Unknown request recipient inputs continue to fail against the applicable rules.
- Address strings remain opaque; no syntax, directory, mailbox, or SMTP validation is added.

## Testing

Add or update tests that prove:

- Resolver selects only Taxable rules and aggregates both Taxable addresses.
- Tax Exempt and Equity select only their own ordered address lists.
- Null role filters preserve all existing common-route behavior.
- A selected role with no applicable To rule fails closed.
- Duplicate matching addresses retain existing case-insensitive deduplication and To precedence.
- Checker approval stores the selected desk's complete recipient snapshot on both task and outbox.
- SLA continues copying the saved task snapshot unchanged.
- Seed verification and migration schema include the new field and desk mappings.
- The public API request shape remains unchanged.

## Superseded Notification Detail

The earlier dynamic-assignment design described resolving desk addresses from `Smtp:RoleEmailMap`. That mechanism is no longer part of the active recipient architecture. This design supersedes only that notification detail: SMTP sends the persisted To/Cc snapshot and does not resolve roles or metadata at delivery time.
