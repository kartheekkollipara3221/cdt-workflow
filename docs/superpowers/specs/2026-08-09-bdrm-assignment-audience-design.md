# BDRM Assignment Notification Audience Design

## Goal

When Trading Desk executes a trade, send the BDRM assignment notification to BDRM/Client Service and copy the original Maker, the Checker, and any other recipients selected by the calling application along the workflow path.

Keep recipient selection generic and metadata-driven. Do not add Portfolio Trade branching to the reusable workflow engine.

## Approved recipient placement

For `RouteBdrmAssigned`:

1. `To` continues to resolve from the existing static BDRM/Client Service address.
2. `Cc` resolves the immutable `originalMaker` workflow participant captured at Workflow Start.
3. `Cc` resolves the optional route-declared `selectedCcUsers` request input supplied with `TradingTradeExecuted`.

The calling application includes the Checker and any other desired participants in `selectedCcUsers`. It should source or validate those users from application-owned state before forwarding the request. The workflow API and engine continue treating recipient address strings as opaque values.

## Public request

The existing `POST /v1/WorkflowAction` DTO is sufficient. A Trading Desk request uses the current `notification.recipientInputs` contract:

```json
{
  "workflowInstanceId": "{{workflowInstanceId}}",
  "actionKey": "TradingTradeExecuted",
  "notification": {
    "recipientInputs": {
      "selectedCcUsers": [
        {
          "userId": "checker1",
          "emailAddress": "checker@example.test",
          "displayName": "Checker One"
        },
        {
          "userId": "manager1",
          "emailAddress": "manager@example.test",
          "displayName": "Manager One"
        }
      ]
    },
    "templateData": {
      "accountShortName": "API015",
      "accountDisplayName": "Scenario Executed Account",
      "traderDisplayName": "Scenario Trader",
      "deskName": "Taxable",
      "securityType": "Equity",
      "tradeLink": "https://example.test/trades/PT-API-SCENARIO-15"
    }
  }
}
```

The caller does not resend `originalMaker`; the engine resolves that immutable participant from workflow persistence.

## Metadata changes

Add these ordered rules after the existing BDRM `To` rule:

| Order | Kind | Source type | Source key | Required |
|---:|---|---|---|---|
| 1 | To | `StaticAddress` | existing BDRM address | Yes |
| 2 | Cc | `WorkflowParticipant` | `originalMaker` | Yes |
| 3 | Cc | `RequestInput` | `selectedCcUsers` | No |

The same `RouteBdrmAssigned` metadata serves both `TradingDeskReview` and `TradingDeskRework`, so both execution paths receive identical recipient behavior.

## Runtime behavior

The existing generic resolver:

- rejects undeclared recipient-input keys;
- preserves route order;
- removes duplicate addresses case-insensitively;
- gives `To` precedence over `Cc`;
- stores the final To/Cc snapshot on both the BDRM task and assignment outbox record.

All BDRM SLA reminders reuse the task snapshot unchanged. No new DTO, engine algorithm, persistence entity, database column, or migration is required.

Already-created tasks and outbox rows are immutable historical snapshots and are not rewritten. The behavior applies when a new BDRM task is created after the updated metadata has been seeded.

## Validation and failure behavior

- Omitting `selectedCcUsers` is valid; BDRM and the required original Maker still receive the assignment.
- Supplying an undeclared recipient key remains a validation failure before workflow side effects.
- Missing required `originalMaker` remains a validation failure.
- Recipient syntax and deliverability remain the calling application's responsibility.

## Verification

Update and run coverage proving:

1. Seed metadata contains the exact three ordered `RouteBdrmAssigned` rules.
2. Both Trading Desk execution public samples deserialize with `selectedCcUsers`.
3. A SQL-backed `TradingTradeExecuted` action produces BDRM as `To`, original Maker plus supplied recipients as ordered `Cc`, with duplicate/To precedence preserved by existing generic behavior.
4. The BDRM task and assignment outbox contain identical recipient snapshots.
5. A BDRM overdue notification copies that task snapshot unchanged.
6. Warning-as-error builds, test suites, `init-db`, and `verify-seed` pass.

## Scope exclusions

- No automatic traversal of prior task/outbox recipient history.
- No directory lookup or email validation inside the workflow engine.
- No change to the current live BDRM task or already-sent messages.
- No change to BDRM SLA timing or recurrence.
