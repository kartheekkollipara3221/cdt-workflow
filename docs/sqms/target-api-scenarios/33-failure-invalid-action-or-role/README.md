# 33. Invalid action or role

## Contract status

- **Implemented:** The in-process service behavior shown here exists in the current workflow/SQMS assemblies.
- **Recommended target facade:** The HTTP route and DTO are proposed for the external target API and are not implemented in this repository.
- **Target implementation required:** The target API must supply authentication, actor/recipient resolution, target persistence, transaction enlistment, HTTP mapping, and response DTOs.

## Endpoint

`POST /v1/sqms/opportunities/{id}/workflow/actions` is recommended. Map `Forbidden` to HTTP 403 and `InvalidTransition` to HTTP 422.

## Actor and required role

The `MarkOpportunityPending` transition from `SUBMITTED` requires a trusted `IPG` actor. A trusted `MFO` actor is deliberately used for the role failure.

## Implemented service method

`ISqmsWorkflowService.ExecuteOpportunityActionAsync`.

## Prerequisite

Complete [01-opportunity-start-submit](../01-opportunity-start-submit/README.md); Opportunity workflow is `SUBMITTED`, version 2.

## Public request notes

The browser selects an `actionKey` returned by current context and sends `expectedWorkflowVersion`. It never sends a role or `toState`.

## Matching in-process call

See `in-process.cs`, which separately demonstrates wrong-role and invalid-state/action rejection.

## Target persistence ownership

The target API does not save returned workflow state for either unsuccessful result.

## Expected workflow effects

Wrong role returns `Forbidden` (403). `OpenOpportunityForBidding` from `SUBMITTED` has no matching transition and returns `InvalidTransition` (422). State remains `SUBMITTED`, version 2.

## Expected target-row effects

No target-owned state save and no adapter target update.

## Expected audit and idempotency effects

Neither failed attempt adds an audit or idempotency row; only the prerequisite Start/Submit evidence remains.

## Expected notification effects

No new notification, task, or SLA row.

## Expected available actions

Refresh context. Valid actions depend on the trusted actor; an `IPG` actor can select `ReturnOpportunityToDraft` or `MarkOpportunityPending`.

## Failure behavior

Role authorization and metadata transition selection are engine-owned. The target does not reinterpret either failure or persist a requested state.

## Next valid packs

Use [02-opportunity-get-context](../02-opportunity-get-context/README.md), then a returned action such as [05-opportunity-submitted-mark-pending](../05-opportunity-submitted-mark-pending/README.md).
