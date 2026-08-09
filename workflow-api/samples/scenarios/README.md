# Public Workflow API scenario envelopes

This catalog contains the valid public JSON request shapes for the seeded Portfolio Trade workflow. It is an integration aid for the UI/API team; it is not the internal console `WorkflowActionEnvelope` used by `execute-json`.

## How to run a pack

1. Start the API at `http://localhost:5058`.
2. Open a scenario's `request.http` in a REST Client that supports file-body inclusion and variables.
3. For Start packs, make the sample `businessObjectId` unique if it has already been used.
4. For Action packs, replace `REPLACE_WITH_PERSISTED_INSTANCE_ID` in `request.http`. The variable is substituted into `request.json` as `{{workflowInstanceId}}`.
5. Send the request and compare the response/database effects with that pack's README.

The generated instance ID is available in the Start response at `result.workflowContext.workflow.workflowInstanceId`. The API database must already be initialized and seeded.

## Public boundary

- `POST /v1/WorkflowStart` creates a workflow and may execute its first metadata action.
- `POST /v1/WorkflowAction` acts on a persisted workflow or synchronizes its application-date SLA.
- Actor identity comes from `X-Workflow-UserId`, `X-Workflow-Roles`, and `X-Workflow-DisplayName`.
- The API resolves metadata-configured To recipients. The request supplies only route-declared inputs such as `originalMaker` and optional `selectedCcUsers`.
- Email-address strings are opaque and are stored without SMTP or directory validation.
- The UI sends `slaTargetDate`; callers never send `dueAtUtc` or scheduling configuration.
- `GET /v1/WorkflowContext?workflowInstanceId=...` uses query/header inputs and therefore has no JSON envelope pack.

## Scenario catalog

| # | Pack | Source | Result |
|---|---|---|---|
| 01 | [Start only](./01-start-only/) | New business object | Draft |
| 02 | [Start and save draft](./02-start-save-draft/) | New business object | Draft |
| 03 | [Start and submit](./03-start-submit-for-checker/) | New business object | CheckerReview |
| 04 | [Save existing draft](./04-draft-save-draft/) | Draft | Draft |
| 05 | [Submit existing draft](./05-draft-submit-for-checker/) | Draft | CheckerReview |
| 06 | [Save returned draft](./06-returned-to-maker-save-draft/) | ReturnedToMaker | ReturnedToMaker |
| 07 | [Resubmit for Checker](./07-returned-to-maker-resubmit-for-checker/) | ReturnedToMaker | CheckerReview |
| 08 | [Checker return](./08-checker-return-to-maker/) | CheckerReview | ReturnedToMaker |
| 09 | [Send to Taxable](./09-checker-send-to-trading-taxable/) | CheckerReview | TradingDeskReview |
| 10 | [Send to Tax Exempt](./10-checker-send-to-trading-tax-exempt/) | CheckerReview | TradingDeskReview |
| 11 | [Send to Equity](./11-checker-send-to-trading-equity/) | CheckerReview | TradingDeskReview |
| 12 | [SLA no-change sync](./12-trading-desk-sla-sync-no-change/) | TradingDeskReview | TradingDeskReview |
| 13 | [SLA reschedule](./13-trading-desk-sla-reschedule/) | TradingDeskReview | TradingDeskReview |
| 14 | [Trading return](./14-trading-desk-return-to-maker/) | TradingDeskReview | ReturnedToMaker |
| 15 | [Trade executed](./15-trading-desk-trade-executed/) | TradingDeskReview | BdrmClientServiceReview |
| 16 | [Trade fulfilled](./16-bdrm-trade-fulfilled/) | BdrmClientServiceReview | Completed |
| 17 | [Return for Trading rework](./17-bdrm-trade-not-executed-properly/) | BdrmClientServiceReview | TradingDeskRework |
| 18 | [Execute reworked trade](./18-trading-desk-rework-trade-executed/) | TradingDeskRework | BdrmClientServiceReview |

## Valid sequences

- Happy path: 03 -> 09, 10, or 11 -> 15 -> 16.
- Existing Draft path: 01 or 02 -> 04 or 05.
- Checker return: 03 -> 08 -> 06 or 07.
- Trading return: 03 -> 09, 10, or 11 -> 14 -> 06 or 07.
- BDRM rework: 03 -> 09, 10, or 11 -> 15 -> 17 -> 18 -> 16.
- Trading SLA synchronization: 03 -> 09, 10, or 11 -> 12 or 13.

Scenario packs describe prerequisites instead of creating hidden test state. This keeps each displayed envelope identical to what the UI sends.
