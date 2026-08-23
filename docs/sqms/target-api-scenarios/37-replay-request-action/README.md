# 37. Request Action replay

## Contract status

- **Implemented:** exact SQMS Action idempotency returns the stored success without repeating workflow side effects.
- **Recommended target facade:** the HTTP route is proposed and is not implemented in this repository.
- **Target implementation required:** the target API owns the outer transaction, target-row save, replay reload, and response mapping.

## Endpoint

Send the two identical requests in `request.http` to `POST /v1/sqms/opportunities/10000/workflow/actions`.

## Actor and required role

Trusted `IPG` actor; `relationshipManager` and `sellerRmTeam` recipients are resolved on the server.

## Implemented service method

`ISqmsWorkflowService.ExecuteOpportunityActionAsync`.

## Prerequisite

Complete [08-opportunity-scheduled-close-bidding](../08-opportunity-scheduled-close-bidding/README.md). Opportunity 10000 is `CLOSED TO BIDDING`, workflow version 5, and has valid seller-approval enrichment.

## Public request notes

The second request must be byte-for-byte the same action intent and expected version as the first. Actor, recipients, workflow instance, idempotency key, and `toState` remain server-owned.

## Matching in-process call

See `in-process.cs`. Calling the same effective request twice derives the same `SQMS:OPP:10000:5:RequestSellerApproval` key; the second result has `replay = true`.

## Target persistence ownership

On the first non-replay success, the target API saves returned `PENDING SELLER APPROVAL` inside the shared outer transaction. On replay it rolls back prerequisite writes, reloads the committed target row, and issues no second target update.

## Expected workflow effects

Both calls report the same authoritative state and version. There is one transition audit, one idempotency record, and one notification outbox row after both calls.

## Expected target-row effects

The request adapter itself never changes the Opportunity row. The first target-owned save changes its projection once; replay causes no second target mutation.

## Expected audit and idempotency effects

One `RequestSellerApproval` audit and one derived idempotency record exist. The replay inserts neither again.

## Expected notification effects

Exactly one `SqmsOpportunityPendingSellerApproval` immutable outbox snapshot exists.

## Expected available actions

Both responses describe the committed `PENDING SELLER APPROVAL` state and its metadata-derived actions.

## Failure behavior

Changing the effective request while reusing an identity is `IdempotencyConflict`, not replay. A replay-state mismatch after reload is an integration failure and must not be repaired by an unverified write.

## Next valid packs

Continue with [10-opportunity-mark-awaiting-allocation](../10-opportunity-mark-awaiting-allocation/README.md), [11-opportunity-mark-partially-allocated](../11-opportunity-mark-partially-allocated/README.md), or [12-opportunity-mark-not-fulfilled](../12-opportunity-mark-not-fulfilled/README.md).
