# 32. Target exists but workflow has not started

## Contract status

- **Implemented:** The in-process service behavior shown here exists in the current workflow/SQMS assemblies.
- **Recommended target facade:** The HTTP route and DTO are proposed for the external target API and are not implemented in this repository.
- **Target implementation required:** The target API must supply authentication, actor/recipient resolution, target persistence, transaction enlistment, HTTP mapping, and response DTOs.

## Endpoint

`GET /v1/sqms/opportunities/{id}/workflow` and `POST /v1/sqms/opportunities/{id}/workflow/actions` are recommended and map `NotWorkflowManaged` to HTTP 409.

## Actor and required role

Trusted actors are target-built. The Context example uses `MFO`; the Action example uses `IPG`.

## Implemented service method

`ISqmsWorkflowService.GetOpportunityContextAsync` and `ISqmsWorkflowService.ExecuteOpportunityActionAsync`.

## Prerequisite

An active target Opportunity with ID 10000 exists, but `WF-SQMS-OPP-10000` does not.

## Public request notes

Context is a bodyless GET. Action accepts only `actionKey` and `expectedWorkflowVersion`; neither request can request or imply Start.

## Matching in-process call

See `in-process.cs`.

## Target persistence ownership

The target API performs no workflow-state target save for either unsuccessful result.

## Expected workflow effects

Both calls return `Success = false`, `ResultCode = NotWorkflowManaged`, no context, and recommended HTTP 409. Context and Action do not lazily start a workflow.

## Expected target-row effects

The existing Opportunity remains byte-for-byte unchanged by the SQMS adapter.

## Expected audit and idempotency effects

No instance, audit, or idempotency row is created.

## Expected notification effects

No outbox row, task, or SLA schedule.

## Expected available actions

None until Start succeeds.

## Failure behavior

The target must not catch `NotWorkflowManaged` and silently call Start. It returns HTTP 409 so the caller can follow the explicit Start flow.

## Next valid packs

Use [01-opportunity-start-submit](../01-opportunity-start-submit/README.md), then [02-opportunity-get-context](../02-opportunity-get-context/README.md).
