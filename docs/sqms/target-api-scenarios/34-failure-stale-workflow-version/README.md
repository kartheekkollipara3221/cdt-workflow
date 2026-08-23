# 34. Stale workflow version

## Contract status

- **Implemented:** The in-process service behavior shown here exists in the current workflow/SQMS assemblies.
- **Recommended target facade:** The HTTP route and DTO are proposed for the external target API and are not implemented in this repository.
- **Target implementation required:** The target API must supply authentication, actor/recipient resolution, target persistence, transaction enlistment, HTTP mapping, and response DTOs.

## Endpoint

`POST /v1/sqms/opportunities/{id}/workflow/actions` is recommended and maps `ConcurrencyConflict` to HTTP 409.

## Actor and required role

Trusted `IPG` actor, which is otherwise authorized for `MarkOpportunityPending`.

## Implemented service method

`ISqmsWorkflowService.ExecuteOpportunityActionAsync`.

## Prerequisite

Complete [01-opportunity-start-submit](../01-opportunity-start-submit/README.md); persisted state is `SUBMITTED`, current version 2. The deliberate request uses stale version 1.

## Public request notes

`expectedWorkflowVersion` is the last version seen by the UI. It is not optional and does not authorize a target state.

## Matching in-process call

See `in-process.cs`.

## Target persistence ownership

The target API performs no target-row save for the conflict.

## Expected workflow effects

`Success = false`, `ResultCode = ConcurrencyConflict`, recommended HTTP 409. Current state remains `SUBMITTED`, version 2; the returned workflow context is the authority for refresh.

## Expected target-row effects

No adapter or target-owned workflow projection write.

## Expected audit and idempotency effects

No audit or idempotency row is added by the stale request.

## Expected notification effects

No notification, task, or SLA side effect.

## Expected available actions

The UI discards its stale action selection, refreshes context, and renders the refreshed `availableActions`.

## Failure behavior

The UI must not automatically retry the stale click. The user refreshes/reselects using version 2; the target never substitutes a version or state.

## Next valid packs

After refresh and user reselection, use [05-opportunity-submitted-mark-pending](../05-opportunity-submitted-mark-pending/README.md).
