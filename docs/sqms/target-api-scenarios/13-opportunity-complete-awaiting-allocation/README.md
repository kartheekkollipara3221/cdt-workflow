# 13. Complete awaiting-allocation Opportunity

## Contract status

**Implemented** in-process SQMS transition. **Recommended target facade** HTTP only. **Target implementation required** controller, auth, shared transaction, target persistence, recipient resolution, and mapping.

## Endpoint

POST /v1/sqms/opportunities/{id}/workflow/actions is recommended and is not implemented here.

## Actor and required role

Trusted server-built SqmsActor with IPG; browser supplies no actor/role.

## Implemented service method

ISqmsWorkflowService.ExecuteOpportunityActionAsync.

## Prerequisite

Complete [10-opportunity-mark-awaiting-allocation](../10-opportunity-mark-awaiting-allocation/README.md); workflow is `AWAITING ALLOCATION` version 7.

## Public request notes

Only actionKey and expectedWorkflowVersion are public. No actor, recipient, toState, workflow-instance, idempotency, route, or template authority.

## Matching in-process call

See in-process.cs. WorkflowDbContext must share the open target-owned SQL transaction.

## Target persistence ownership

Request adapter never updates Opportunity. On non-replay success target persists returned Status/WorkflowState and commits once. Replay rolls back prerequisite writes, reloads, verifies, and performs no update.

## Expected workflow effects

Metadata moves AWAITING ALLOCATION to COMPLETED with CompleteOpportunity; version 8, runtime status Completed. No SQMS task or SLA.

## Expected target-row effects

Only target-owned Status/WorkflowState save; other business columns unchanged.

## Expected audit and idempotency effects

One CompleteOpportunity audit and one idempotency row; replay adds neither.

## Expected notification effects

No notification route for this transition.

## Expected available actions

None in the terminal/current actor state.

## Failure behavior

TargetNotFound, NotWorkflowManaged, Forbidden, stale version, invalid transition, enrichment error, or exception rolls back without partial target/runtime/audit/idempotency/outbox effects.

## Next valid packs

Use the root catalog Opportunity sequence.
