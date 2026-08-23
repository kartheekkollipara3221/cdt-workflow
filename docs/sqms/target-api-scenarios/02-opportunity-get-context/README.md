# 02. Read Opportunity workflow context

## Contract status

**Implemented** in-process SQMS behavior. **Recommended target facade** HTTP sample only. **Target implementation required** controller, authentication, transaction, target save, and mapping.

## Endpoint

GET /v1/sqms/opportunities/{id}/workflow is recommended and is not implemented here.

## Actor and required role

Trusted server-built SqmsActor with IPG; public JSON supplies no actor or role.

## Implemented service method

ISqmsWorkflowService.GetOpportunityContextAsync.

## Prerequisite

Complete [01-opportunity-start-submit](../01-opportunity-start-submit/README.md), or use any Opportunity whose workflow already exists.

## Public request notes

Route ID is authoritative. JSON has only approved fields and no actor, recipient, toState, workflow-instance, idempotency, route, or template authority. GET has no body.

## Matching in-process call

GetOpportunityContextAsync(10000, actor, cancellationToken) is read-only.

## Target persistence ownership

No target or workflow write.

## Expected workflow effects

No state/version change; context never lazily starts.

## Expected target-row effects

None.

## Expected audit and idempotency effects

None; before/after counts match.

## Expected notification effects

No notification route for this path.

## Expected available actions

For the IPG actor in `SUBMITTED`, metadata returns `ReturnOpportunityToDraft` and `MarkOpportunityPending`.

## Failure behavior

TargetNotFound, NotWorkflowManaged, Forbidden, ConcurrencyConflict, InvalidTransition, or exception produces no partial effects; outer transaction rolls back.

## Next valid packs

Use the root catalog sequence.
