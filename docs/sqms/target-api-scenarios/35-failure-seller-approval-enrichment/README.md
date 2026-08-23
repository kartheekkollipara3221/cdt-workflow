# 35. Seller-approval enrichment failure

## Contract status

- **Implemented:** The in-process service behavior shown here exists in the current workflow/SQMS assemblies.
- **Recommended target facade:** The HTTP route and DTO are proposed for the external target API and are not implemented in this repository.
- **Target implementation required:** The target API must supply authentication, actor/recipient resolution, target persistence, transaction enlistment, HTTP mapping, and response DTOs.
- **Testing only:** `setup.testing.sql` deliberately removes trusted data and is restricted to an isolated disposable test database.

## Endpoint

`POST /v1/sqms/opportunities/{id}/workflow/actions` is recommended and maps `ValidationFailed` to HTTP 422.

## Actor and required role

Trusted `IPG` actor with server-resolved `relationshipManager` and `sellerRmTeam` recipient inputs.

## Implemented service method

`ISqmsWorkflowService.ExecuteOpportunityActionAsync`.

## Prerequisite

Complete [08-opportunity-scheduled-close-bidding](../08-opportunity-scheduled-close-bidding/README.md); state is `CLOSED TO BIDDING`, version 5. In an isolated test database only, run `setup.testing.sql` to set required trusted `Opportunity.EffectiveDateForSale` to NULL. A failed configured dashboard link is an equivalent validation path.

## Public request notes

Public JSON remains only `actionKey = RequestSellerApproval` and `expectedWorkflowVersion = 5`. Missing trusted enrichment is never a browser-controlled failure switch.

## Matching in-process call

See `in-process.cs`. The target persists prerequisite business edits before the workflow call so enrichment reads the same enlisted transaction.

## Target persistence ownership

The target API rolls back the outer transaction on the unsuccessful result and performs no `Status`/`WorkflowState` save.

## Expected workflow effects

`ValidationFailed` (recommended HTTP 422). State remains `CLOSED TO BIDDING`, version 5. Seller approval does not transition.

## Expected target-row effects

The failed Action changes no target workflow columns. The deliberate NULL is testing-only setup, not an Action effect; rollback it with the isolated test transaction or restore the fixture.

## Expected audit and idempotency effects

No `RequestSellerApproval` audit or idempotency record is created.

## Expected notification effects

No `SqmsOpportunityPendingSellerApproval` email/outbox row is queued. No task or SLA row is created.

## Expected available actions

Existing state/version and actions remain authoritative after refresh; correct trusted data/configuration before retrying.

## Failure behavior

Required Opportunity fields are `RelationshipManager`, `SellerRMTeam`, `FundConsideredSecondarySale`, `Vehicle`, `CommitmentAmountSale`, and `EffectiveDateForSale`; the configured seller-approval dashboard link must also build successfully. Any missing requirement prevents transition, audit, idempotency, and email side effects.

## Next valid packs

Restore valid trusted data/configuration, refresh context, then retry [09-opportunity-request-seller-approval](../09-opportunity-request-seller-approval/README.md).
