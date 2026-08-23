# 36. Bid-disposition enrichment failure

## Contract status

- **Implemented:** The in-process service behavior shown here exists in the current workflow/SQMS assemblies.
- **Recommended target facade:** The HTTP route and DTO are proposed for the external target API and are not implemented in this repository.
- **Target implementation required:** The target API must supply authentication, actor/recipient resolution, target persistence, transaction enlistment, HTTP mapping, and response DTOs.
- **Testing only:** `setup.testing.sql` deliberately removes trusted Bid data and is restricted to an isolated disposable test database.

## Endpoint

`POST /v1/sqms/bids/{id}/workflow/actions` is recommended and maps `ValidationFailed` to HTTP 422.

## Actor and required role

Trusted `Seller` actor with server-resolved `relationshipManager` and `buyerRmTeam` recipient inputs.

## Implemented service method

`ISqmsWorkflowService.ExecuteBidActionAsync`.

## Prerequisite

Complete [21-bid-send-to-seller-review](../21-bid-send-to-seller-review/README.md); state is `UNDER REVIEW BY SELLER`, version 4. In an isolated test database only, run `setup.testing.sql` to set required `Bid.CommitmentAmountRequested` to NULL. A missing active parent Opportunity is another implemented validation path.

## Public request notes

Public JSON remains only the chosen disposition `actionKey` and `expectedWorkflowVersion = 4`. Bid, parent, recipients, route, template, and target state are trusted server inputs.

## Matching in-process call

See `in-process.cs`.

## Target persistence ownership

On the failed result the target rolls back the outer transaction and performs no target workflow-state save.

## Expected workflow effects

`ValidationFailed` (recommended HTTP 422). Bid remains `UNDER REVIEW BY SELLER`, version 4; no disposition transition is committed.

## Expected target-row effects

The failed Action changes no Bid workflow columns. The deliberate NULL is testing-only setup, not an Action effect; rollback it with the isolated test transaction or restore the fixture.

## Expected audit and idempotency effects

No `AcceptBid` audit or `SQMS:BID:20000:4:AcceptBid` idempotency row is created. All attempted disposition side effects roll back.

## Expected notification effects

No accepted/partially accepted/not accepted outbox row or email. No task or SLA row.

## Expected available actions

Existing Seller actions remain authoritative after refresh; correct the trusted Bid/parent data before retrying.

## Failure behavior

Disposition requires active Bid and parent Opportunity data, including Bid `RelationshipManager`, `BuyerRmteam`, `CommitmentAmountRequested`, Opportunity `FundConsideredSecondarySale`, `Vehicle`, `EffectiveDateForSale`, and a valid configured Bid link. Missing required Bid or parent data rolls back transition, audit, idempotency, target projection, and outbox side effects.

## Next valid packs

Restore trusted data, refresh context, then retry [22-bid-seller-accept](../22-bid-seller-accept/README.md), [23-bid-seller-partially-accept](../23-bid-seller-partially-accept/README.md), or [24-bid-seller-reject](../24-bid-seller-reject/README.md).
