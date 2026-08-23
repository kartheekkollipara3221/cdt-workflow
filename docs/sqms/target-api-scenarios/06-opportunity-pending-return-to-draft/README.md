# 06. Return a pending Opportunity to Draft

## Contract status

**Implemented** in-process SQMS behavior. **Recommended target facade** HTTP sample only. **Target implementation required** controller, authentication, transaction, target save, and mapping.

## Endpoint

POST /v1/sqms/opportunities/{id}/workflow/actions is recommended and is not implemented here.

## Actor and required role

Trusted server-built SqmsActor with IPG; public JSON supplies no actor or role.

## Implemented service method

ISqmsWorkflowService.ExecuteOpportunityActionAsync.

## Prerequisite

Complete [05-opportunity-submitted-mark-pending](../05-opportunity-submitted-mark-pending/README.md); workflow is `PENDING` at version 3.

## Public request notes

Route ID is authoritative. JSON has only approved fields and no actor, recipient, toState, workflow-instance, idempotency, route, or template authority.

## Matching in-process call

See in-process.cs. Enlist WorkflowDbContext in the target-owned SQL transaction first.

## Target persistence ownership

Request SQMS code never updates Opportunity. On non-replay success target persists returned Status and WorkflowState and commits once; replay rolls back prerequisite writes, reloads, verifies, and issues no update.

## Expected workflow effects

Metadata moves PENDING to Draft by ReturnOpportunityToDraft; result version 4. No SQMS task or SLA.

## Expected target-row effects

Target-owned Status/WorkflowState save only; other business columns remain unchanged.

## Expected audit and idempotency effects

One audit and one idempotency row. Matching Action replay adds neither.

## Expected notification effects

No notification route for this path.

## Expected available actions

Metadata-derived and role-filtered; this sample has none for its actor/state.

## Failure behavior

TargetNotFound, NotWorkflowManaged, Forbidden, ConcurrencyConflict, InvalidTransition, or exception produces no partial effects; outer transaction rolls back.

## Next valid packs

Use the root catalog sequence.
