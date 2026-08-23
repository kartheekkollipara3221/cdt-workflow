# 31. Target row not found

## Contract status

- **Implemented:** The in-process service behavior shown here exists in the current workflow/SQMS assemblies.
- **Recommended target facade:** The HTTP route and DTO are proposed for the external target API and are not implemented in this repository.
- **Target implementation required:** The target API must supply authentication, actor/recipient resolution, target persistence, transaction enlistment, HTTP mapping, and response DTOs.

## Endpoint

`POST /v1/sqms/opportunities/{id}/workflow/start` is recommended and maps `TargetNotFound` to HTTP 404.

## Actor and required role

The target constructs a trusted `SqmsActor` with the `MFO` role.

## Implemented service method

`ISqmsWorkflowService.StartOpportunityAsync` (the same target precheck applies to Bid Start, Context, and Action entry points).

## Prerequisite

Use an ID for which no active `[SSQMS].[Opportunity]` row exists. No workflow instance may exist for that ID.

## Public request notes

The recommended Start body is `{}`. The route ID is the only entity authority; the browser supplies no actor, role, workflow identity, target state, recipient, or idempotency value.

## Matching in-process call

See `in-process.cs`.

## Target persistence ownership

No target row exists to save. The target API rolls back or ends its request transaction without inserting a row or invoking a separate workflow start.

## Expected workflow effects

`Success = false`, `ResultCode = TargetNotFound`, no context, and recommended HTTP 404. No workflow instance is created.

## Expected target-row effects

None. This route never creates an Opportunity.

## Expected audit and idempotency effects

No workflow audit or idempotency record.

## Expected notification effects

No notification outbox row, task, or SLA schedule.

## Expected available actions

None because no workflow context exists.

## Failure behavior

The target lookup fails before workflow execution. The result is `TargetNotFound`, not `NotWorkflowManaged`, and all runtime side-effect counts stay zero for `WF-SQMS-OPP-999999`.

## Next valid packs

Persist a real target row, then use [01-opportunity-start-submit](../01-opportunity-start-submit/README.md).
