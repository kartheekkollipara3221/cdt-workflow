<!-- generated-by: gsd-doc-writer -->
# Workflow API JSON Envelopes

This document tells an integration caller which Workflow API endpoint to call, which JSON object to send, and which values apply to each Portfolio Trade scenario.

## 1. Which endpoint should I call?

Use this rule:

| Situation | Endpoint |
|---|---|
| The business record does not yet have a `workflowInstanceId` | `POST /v1/WorkflowStart` |
| The business record already has a `workflowInstanceId` | `POST /v1/WorkflowAction` |

After a successful Start request, save the returned value from:

```text
result.workflowContext.workflow.workflowInstanceId
```

Use that value for every later Action request. Do not call Start again for an existing workflow.

## 2. Actor headers

Actor identity is not part of the JSON body. The trusted calling application supplies these headers:

| Header | Required | Example | Purpose |
|---|---|---|---|
| `X-Workflow-UserId` | Yes | `maker1` | User performing the operation |
| `X-Workflow-Roles` | Yes | `Maker` | User's workflow role or comma/semicolon-separated roles |
| `X-Workflow-DisplayName` | No | `Maker One` | Display name recorded for the operation; defaults to user ID when omitted |

The scenario tables below show the required actor role for each request.

## 3. Start envelope

Endpoint:

```text
POST /v1/WorkflowStart
```

Canonical JSON structure:

```json
{
  "workflowKey": "PortfolioTrade",
  "businessObjectId": "<unique-business-record-id>",
  "actionKey": "<optional-initial-action>",
  "slaTargetDate": "YYYY-MM-DD",
  "notification": {
    "recipientInputs": {
      "originalMaker": [
        {
          "userId": "<maker-user-id>",
          "emailAddress": "<maker-email-address>",
          "displayName": "<maker-display-name>"
        }
      ],
      "selectedCcUsers": [
        {
          "userId": "<cc-user-id>",
          "emailAddress": "<cc-email-address>",
          "displayName": "<cc-display-name>"
        }
      ]
    },
    "templateData": {
      "<template-key>": "<value>"
    }
  }
}
```

Only include optional properties when the scenario requires them. Do not send placeholder values or `null` properties.

### Start fields

| Field | Required | Applicable value |
|---|---|---|
| `workflowKey` | Yes | `PortfolioTrade` |
| `businessObjectId` | Yes | Unique identifier from the calling application |
| `actionKey` | No | Omitted, `SaveDraft`, or `SubmitForChecker` |
| `slaTargetDate` | No | Not used by the currently seeded Portfolio Trade Start scenarios |
| `notification.recipientInputs.originalMaker` | Yes | Exactly one Maker recipient object |
| `notification.recipientInputs.selectedCcUsers` | Submit only | Complete current CC list; use an empty array when there are no selected CC users |
| `notification.templateData` | Submit only | Assignment template profile A |

`originalMaker` is captured during Start and is immutable. Later Action requests must not send or replace it.

### Start scenarios

| # | Scenario | `actionKey` | Actor role | Notification | Resulting state | Exact sample |
|---|---|---|---|---|---|---|
| 01 | Start only | Omit | `Maker` | `originalMaker` | `Draft` | [request.json](../samples/scenarios/01-start-only/request.json) |
| 02 | Start and save draft | `SaveDraft` | `Maker` | `originalMaker` | `Draft` | [request.json](../samples/scenarios/02-start-save-draft/request.json) |
| 03 | Start and submit to Checker | `SubmitForChecker` | `Maker` | `originalMaker`, `selectedCcUsers`, profile A | `CheckerReview` | [request.json](../samples/scenarios/03-start-submit-for-checker/request.json) |

### Example: Start and save draft

```json
{
  "workflowKey": "PortfolioTrade",
  "businessObjectId": "PT-1001",
  "actionKey": "SaveDraft",
  "notification": {
    "recipientInputs": {
      "originalMaker": [
        {
          "userId": "maker1",
          "emailAddress": "maker1@example.com",
          "displayName": "Maker One"
        }
      ]
    }
  }
}
```

## 4. Action envelope

Endpoint:

```text
POST /v1/WorkflowAction
```

Canonical JSON structure:

```json
{
  "workflowInstanceId": "<persisted-workflow-instance-id>",
  "actionKey": "<action-for-the-current-state>",
  "assignedRoleKey": "<optional-dynamic-assignment-role>",
  "slaTargetDate": "YYYY-MM-DD",
  "notification": {
    "recipientInputs": {
      "selectedCcUsers": [
        {
          "userId": "<cc-user-id>",
          "emailAddress": "<cc-email-address>",
          "displayName": "<cc-display-name>"
        }
      ]
    },
    "templateData": {
      "<template-key>": "<value>"
    }
  }
}
```

Only include optional properties when the scenario requires them.

### Action fields

| Field | Required | Applicable value |
|---|---|---|
| `workflowInstanceId` | Yes | ID returned by the original Start request |
| `actionKey` | Normally yes | One action allowed by the current state; omit only for SLA-only synchronization |
| `assignedRoleKey` | Only when sending to Trading Desk | `TradingDeskTaxable`, `TradingDeskTaxExempt`, or `TradingDeskEquity` |
| `slaTargetDate` | Trading Desk assignment or SLA sync | Application target date in `YYYY-MM-DD` format |
| `notification` | Notification-producing actions | `selectedCcUsers` and the applicable template profile |

For scenarios 09-11, `assignedRoleKey` selects both the persisted task assignment and the applicable versioned recipient rules. A desk can have multiple matching To addresses; the engine aggregates them into one email and saves the complete recipient snapshot for later SLA messages. `notification.templateData.deskName` only renders the email body and subject. It does not select the desk or its addresses.

## 5. Action values by scenario

| # | Current state | Scenario | `actionKey` | Actor role | Additional JSON values | Notification | Resulting state | Exact sample |
|---|---|---|---|---|---|---|---|---|
| 04 | `Draft` | Save existing draft | `SaveDraft` | `Maker` | None | Omit | `Draft` | [request.json](../samples/scenarios/04-draft-save-draft/request.json) |
| 05 | `Draft` | Submit existing draft | `SubmitForChecker` | `Maker` | None | CC list + profile A | `CheckerReview` | [request.json](../samples/scenarios/05-draft-submit-for-checker/request.json) |
| 06 | `ReturnedToMaker` | Save returned draft | `SaveDraft` | `Maker` | None | Omit | `ReturnedToMaker` | [request.json](../samples/scenarios/06-returned-to-maker-save-draft/request.json) |
| 07 | `ReturnedToMaker` | Resubmit to Checker | `ResubmitForChecker` | `Maker` | None | CC list + profile A | `CheckerReview` | [request.json](../samples/scenarios/07-returned-to-maker-resubmit-for-checker/request.json) |
| 08 | `CheckerReview` | Checker returns to Maker | `CheckerReturnToMaker` | `Checker` | None | CC list + profile B | `ReturnedToMaker` | [request.json](../samples/scenarios/08-checker-return-to-maker/request.json) |
| 09 | `CheckerReview` | Send to Taxable Trading Desk | `CheckerSendToTradingDesk` | `Checker` | `assignedRoleKey: TradingDeskTaxable`; `slaTargetDate` | CC list + profile A | `TradingDeskReview` | [request.json](../samples/scenarios/09-checker-send-to-trading-taxable/request.json) |
| 10 | `CheckerReview` | Send to Tax Exempt Trading Desk | `CheckerSendToTradingDesk` | `Checker` | `assignedRoleKey: TradingDeskTaxExempt`; `slaTargetDate` | CC list + profile A | `TradingDeskReview` | [request.json](../samples/scenarios/10-checker-send-to-trading-tax-exempt/request.json) |
| 11 | `CheckerReview` | Send to Equity Trading Desk | `CheckerSendToTradingDesk` | `Checker` | `assignedRoleKey: TradingDeskEquity`; `slaTargetDate` | CC list + profile A | `TradingDeskReview` | [request.json](../samples/scenarios/11-checker-send-to-trading-equity/request.json) |
| 12 | `TradingDeskReview` | Synchronize unchanged SLA date | Omit | `TradingDesk` | Current `slaTargetDate` | Omit | `TradingDeskReview` | [request.json](../samples/scenarios/12-trading-desk-sla-sync-no-change/request.json) |
| 13 | `TradingDeskReview` | Reschedule SLA date | Omit | `TradingDesk` | New `slaTargetDate` | Omit | `TradingDeskReview` | [request.json](../samples/scenarios/13-trading-desk-sla-reschedule/request.json) |
| 14 | `TradingDeskReview` | Trading Desk returns to Maker | `TradingReturnToMaker` | `TradingDesk` | None | CC list + profile B | `ReturnedToMaker` | [request.json](../samples/scenarios/14-trading-desk-return-to-maker/request.json) |
| 15 | `TradingDeskReview` | Trade executed | `TradingTradeExecuted` | `TradingDesk` | None | CC list + profile C | `BdrmClientServiceReview` | [request.json](../samples/scenarios/15-trading-desk-trade-executed/request.json) |
| 16 | `BdrmClientServiceReview` | Trade fulfilled | `BdrmTradeFulfilled` | `BDRMClientService` | None | CC list + profile D | `Completed` | [request.json](../samples/scenarios/16-bdrm-trade-fulfilled/request.json) |
| 17 | `BdrmClientServiceReview` | Trade not executed properly | `BdrmTradeNotExecutedProperly` | `BDRMClientService` | None | CC list + profile E | `TradingDeskRework` | [request.json](../samples/scenarios/17-bdrm-trade-not-executed-properly/request.json) |
| 18 | `TradingDeskRework` | Execute reworked trade | `TradingTradeExecuted` | `TradingDesk` | None | CC list + profile C | `BdrmClientServiceReview` | [request.json](../samples/scenarios/18-trading-desk-rework-trade-executed/request.json) |

## 6. Notification object

### Recipient inputs

There are only two caller-supplied recipient source keys for the seeded Portfolio Trade workflow:

| Key | When sent | Rule |
|---|---|---|
| `originalMaker` | Start only | Exactly one Maker. Captured once and reused by the workflow. |
| `selectedCcUsers` | Every notification-producing Start or Action request | Send the complete current cumulative CC list, not only the newly added users. Use `[]` when the list is empty. |

Recipient object:

```json
{
  "userId": "user1",
  "emailAddress": "user1@example.com",
  "displayName": "User One"
}
```

Do not send `to` or `cc` arrays. Workflow metadata determines final To/Cc placement:

- Assignment notifications: task owner is To; original Maker and `selectedCcUsers` are Cc.
- Return and completion notifications: original Maker is To; `selectedCcUsers` are Cc.
- SLA notifications reuse the task's stored recipient snapshot. The caller does not provide a new SLA recipient list.

### Template data profiles

The values are strings supplied by the calling application. Use plain text values; do not send HTML.

| Profile | Used by | `templateData` keys |
|---|---|---|
| A: Assignment | Submit, resubmit, send to Trading Desk | `referenceNumber`, `submitterDisplayName`, `deskName`, `securityType`, `accountShortName`, `accountDisplayName`, `tradeLink` |
| B: Return to Maker | Checker return, Trading Desk return | `referenceNumber`, `returnedByDisplayName`, `recipientDisplayName`, `accountShortName`, `accountDisplayName`, `submittedDate`, `tradeLink` |
| C: BDRM assignment | Trade executed, reworked trade executed | `accountShortName`, `accountDisplayName`, `traderDisplayName`, `deskName`, `securityType`, `tradeLink` |
| D: Completion | Trade fulfilled | `confirmerDisplayName`, `accountShortName`, `accountDisplayName`, `tradeLink` |
| E: Trading Desk rework | Trade not executed properly | `accountShortName`, `accountDisplayName`, `deskName`, `tradeLink` |

## 7. Copy-ready examples

### Submit an existing draft

```json
{
  "workflowInstanceId": "WF-PT-1001",
  "actionKey": "SubmitForChecker",
  "notification": {
    "recipientInputs": {
      "selectedCcUsers": [
        {
          "userId": "manager1",
          "emailAddress": "manager1@example.com",
          "displayName": "Manager One"
        }
      ]
    },
    "templateData": {
      "referenceNumber": "PT-1001",
      "submitterDisplayName": "Maker One",
      "deskName": "Taxable",
      "securityType": "Equity",
      "accountShortName": "ACC1001",
      "accountDisplayName": "Example Account",
      "tradeLink": "https://application.example/trades/PT-1001"
    }
  }
}
```

### Checker sends to Trading Desk

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
          "emailAddress": "manager1@example.com",
          "displayName": "Manager One"
        },
        {
          "userId": "checker1",
          "emailAddress": "checker1@example.com",
          "displayName": "Checker One"
        }
      ]
    },
    "templateData": {
      "referenceNumber": "PT-1001",
      "submitterDisplayName": "Maker One",
      "deskName": "Taxable",
      "securityType": "Equity",
      "accountShortName": "ACC1001",
      "accountDisplayName": "Example Account",
      "tradeLink": "https://application.example/trades/PT-1001"
    }
  }
}
```

### SLA-only synchronization

```json
{
  "workflowInstanceId": "WF-PT-1001",
  "slaTargetDate": "2030-01-25"
}
```

This request updates or confirms the active Trading Desk SLA schedule. It does not move workflow state and does not require `actionKey`.

## 8. Fields the integration caller must not send

The public Start and Action DTOs reject unknown root or nested properties. Do not send:

- `toState`
- `currentState` or expected-state fields
- task IDs or task keys
- workflow definition version
- business object fields on an Action request
- `dataSave` or form payload
- UI fields such as `sectionKey`, `buttonKey`, `buttonLabelShown`, or `saveAsDraft`
- `reason` or `actionReason`
- `idempotencyKey`, client action ID, or correlation ID
- direct `to` or `cc` recipient arrays
- `dueAtUtc`, reminder time, timezone, overdue offset, cadence, or notification route keys

The Workflow API derives state, task, workflow metadata, business object identity, correlation, idempotency, To/Cc placement, and SLA schedule rules on the server.

## 9. Common valid sequences

| Flow | Scenario sequence |
|---|---|
| Happy path | 03 -> 09, 10, or 11 -> 15 -> 16 |
| Existing draft | 01 or 02 -> 04 or 05 |
| Checker return and resubmit | 03 -> 08 -> 06 or 07 |
| Trading Desk return and resubmit | 03 -> 09, 10, or 11 -> 14 -> 06 or 07 |
| BDRM rework | 03 -> 09, 10, or 11 -> 15 -> 17 -> 18 -> 16 |
| Trading Desk SLA synchronization | 03 -> 09, 10, or 11 -> 12 or 13 |

The complete runnable request catalog is available in [Public Workflow API scenario envelopes](../samples/scenarios/README.md).
