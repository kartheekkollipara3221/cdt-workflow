# BDRM/Client Service returns the trade for Trading Desk rework

**Endpoint:** `POST /v1/WorkflowAction`
**Actor:** `bdrm.scenario` with role `BDRMClientService`

## Prerequisite

The workflow is in `BdrmClientServiceReview` with one pending `BdrmClientServiceReviewTask`; pack 15 or 18 creates that state.

## Request notes

The JSON is the public Workflow Action DTO only. Actor identity is supplied by the trusted headers in `request.http`.

## Expected effects

- Closes `BdrmClientServiceReviewTask`, cancels its future reminders, and moves the workflow to `TradingDeskRework`.
- Creates one pending `TradingDeskReworkTask` with the two-minute test SLA.
- Queues `RouteTradingDeskReworkAssigned` to the metadata Trading Desk address.

## Next valid packs

Use pack 18 after Trading Desk completes the rework.
