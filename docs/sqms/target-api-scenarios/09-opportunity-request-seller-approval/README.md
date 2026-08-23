# 09. Request seller approval

## Contract status

**Implemented** in-process SQMS transition. **Recommended target facade** HTTP only. **Target implementation required** controller, auth, shared transaction, target persistence, recipient resolution, and mapping.

## Endpoint

POST /v1/sqms/opportunities/{id}/workflow/actions is recommended and is not implemented here.

## Actor and required role

Trusted server-built SqmsActor with IPG; browser supplies no actor/role.

## Implemented service method

ISqmsWorkflowService.ExecuteOpportunityActionAsync.

## Prerequisite

Complete [08-opportunity-scheduled-close-bidding](../08-opportunity-scheduled-close-bidding/README.md); workflow is `CLOSED TO BIDDING` version 5 and required trusted Opportunity/link data exists.

## Public request notes

Only actionKey and expectedWorkflowVersion are public. No actor, recipient, toState, workflow-instance, idempotency, route, or template authority.

## Matching in-process call

See in-process.cs. WorkflowDbContext must share the open target-owned SQL transaction.

## Target persistence ownership

Request adapter never updates Opportunity. On non-replay success target persists returned Status/WorkflowState and commits once. Replay rolls back prerequisite writes, reloads, verifies, and performs no update.

## Expected workflow effects

Metadata moves CLOSED TO BIDDING to PENDING SELLER APPROVAL with RequestSellerApproval; version 6, runtime status Active. No SQMS task or SLA.

## Expected target-row effects

Only target-owned Status/WorkflowState save; other business columns unchanged.

## Expected audit and idempotency effects

One RequestSellerApproval audit and one idempotency row; replay adds neither.

## Expected notification effects

Queues exactly one SqmsOpportunityPendingSellerApproval outbox row. The target server resolves relationshipManager and sellerRmTeam; public JSON contains no addresses. Required Opportunity template data and configured dashboard link are trusted enrichment.

## Expected available actions

MarkOpportunityAwaitingAllocation, MarkOpportunityPartiallyAllocated, MarkOpportunityNotFulfilled.

## Failure behavior

TargetNotFound, NotWorkflowManaged, Forbidden, stale version, invalid transition, enrichment error, or exception rolls back without partial target/runtime/audit/idempotency/outbox effects.

## Next valid packs

Use the root catalog Opportunity sequence.
