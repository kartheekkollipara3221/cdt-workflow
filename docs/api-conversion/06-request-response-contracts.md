# Request and Response Contracts

The existing application contracts are already API-shaped. Do not duplicate them unless the external API has a strong reason to wrap or restrict fields.

## Contract Recommendation Table

| Console Contract / Input / Output | API Contract Recommendation | Expose Directly? | Changes Needed | Notes |
|---|---|---|---|---|
| `WorkflowActionEnvelope` | Use as execute action body or inner payload. | Yes, with safeguards | Stamp `Actor`; ignore/reject `TestExpectations`; route/body ID check. | Standard button-click envelope. |
| `WorkflowActionResultEnvelope` | Return for workflow outcomes. | Yes | Map result code to HTTP status. | Contains `Result` and optional `WorkflowContext`. |
| `WorkflowContextResponse` | Return for context endpoint. | Yes | Supply actor from auth. | Standard form-open workflow object. |
| `AvailableWorkflowAction` | Return inside context. | Yes | None | Derived from SQL metadata. |
| `StartWorkflowRequest` | Use internally from API DTO. | No direct public need | Create `StartWorkflowApiRequest` without `StartedByUserId` and `Reset`. | Actor and reset are server-side decisions. |
| `StartWorkflowResult` | Use service return internally. | Not directly | Return `StartWorkflowResult.Envelope`. | `ResetCounts` should not be public to normal users. |
| `WorkflowInspectionSnapshot` | Optional admin response. | Yes for admin | Gate with authorization. | Read model, not EF entities. |
| `WorkflowTaskInspection` | Optional admin response. | Yes for admin | Gate with authorization. | Useful for tasks endpoint. |
| `WorkflowAuditInspection` | Optional admin response. | Yes for admin | Redact if needed. | Contains user IDs and reasons. |
| `NotificationInspection` | Optional admin response. | Yes for admin | Redact payload if added later. | Current record excludes raw payload. |
| `RunSlaResult` | Admin/job response. | Yes for admin/job | Gate or keep internal. | Contains queued notification summaries and warnings. |
| `ProcessNotificationOutboxResult` | Admin/job response. | Yes for admin/job | Gate and avoid exception details in public routes. | Contains failure exception object. Consider admin-only serialization shape. |
| Sample JSON `testExpectations` | Test fixture metadata only. | No | Ignore or reject in production. | Used by tests/demo only. |

## Reuse Directly

Reusable direct API responses:

- `WorkflowContextResponse`
- `WorkflowActionResultEnvelope`
- `AvailableWorkflowAction` inside context
- `WorkflowInspectionSnapshot` for admin endpoints
- `RunSlaResult` for admin/job endpoints

Reusable direct or inner request:

- `WorkflowActionEnvelope`

Recommended API wrapper:

- `StartWorkflowApiRequest`, because `StartWorkflowRequest` includes server-controlled fields: `StartedByUserId` and `Reset`.

## Fields To Ignore, Overwrite, Or Remove

| Field | API Handling | Reason |
|---|---|---|
| `actor.userId` | Overwrite from authenticated user. | Request body identity is untrusted. |
| `actor.displayName` | Overwrite from authenticated user/profile if available. | Request body display name is untrusted. |
| `actor.roles` | Overwrite from authenticated claims/groups/authorization. | Request body roles are untrusted. |
| `testExpectations` | Ignore or reject outside test fixtures. | Console sample/test metadata only. |
| `workflow.options` | Accept only as advisory diagnostics if you keep it; never let it override metadata. | Current engine warns and metadata wins. |
| `workflow.workflowInstanceId` | Must match route if route contains instance ID. | Prevents ambiguous execution target. |
| `businessObject.objectId` | Must match route if route contains domain object ID. | Prevents moving one workflow while saving another object's data. |
| `request.requestedAtUtc` | Do not use as authoritative server time. | Use `TimeProvider` server time. |

## Actor Fields

The console accepts actor fields from JSON because it simulates a user. The API must not trust those fields. Stamp them before validation and hashing:

```csharp
var actor = actorResolver.Resolve(User);
envelope.Actor = new WorkflowActor
{
    UserId = actor.UserId,
    DisplayName = actor.DisplayName,
    Roles = actor.WorkflowRoles.ToList()
};
```

This matters because `CanonicalRequestHasher` includes `actor.userId` and `actor.roles`.

## Idempotency Keys

`WorkflowActionEnvelopeNormalizer` sets the effective idempotency key to:

1. `workflow.idempotencyKey` if present.
2. Otherwise `request.clientActionId`.

The API should require clients to send a stable `idempotencyKey` or `clientActionId` per user click. Matching replay returns the stored result. Same key with a different normalized request returns `IdempotencyConflict`.

## Route Versus Body Workflow Instance ID

If route is:

```text
POST /api/workflows/instances/{workflowInstanceId}/actions
```

then:

- If body `workflow.workflowInstanceId` is missing, set it from the route.
- If body `workflow.workflowInstanceId` is present and differs from the route, return `400 Bad Request`.

Do not silently run against a different workflow instance.

## `requestDefaults` From Context

`WorkflowContextResponse.WorkflowContext.AvailableActions[*].RequestDefaults` includes:

- `workflowKey`
- `workflowInstanceId`
- `workflowDefinitionVersion`
- `expectedCurrentState`
- `expectedCurrentTaskId`
- `expectedCurrentTaskKey`
- `expectedInstanceVersion`

The UI should echo these into the later `WorkflowActionEnvelope.Workflow` block. The API should enforce these guards through `WorkflowExecutionService`, not by manually checking them in the controller.

## Response Diagnostics

`WorkflowActionResultEnvelope.Result.Diagnostics` may include:

- `requestHash`
- `resultHash`
- `replay`
- `originalCreatedAtUtc`
- `validationErrors`
- `storedRequestHash`
- `storedActionKey`

Keep diagnostics for development and support if acceptable to the API's data policy. If the API hides diagnostics externally, do it consistently through response shaping, not by changing core engine behavior.

## Example Start Request

```json
{
  "businessObjectType": "PortfolioTrade",
  "businessObjectId": "PT-1001"
}
```

The API supplies `workflowKey` from the route and `startedByUserId` from authentication. It should pass `Reset: false` for normal users.

## Example Execute Request

Based on `samples/workflow-actions/02-submit-for-checker.json`, with actor stamped by the API:

```json
{
  "request": {
    "commandType": "ExecuteWorkflowAction",
    "commandVersion": 1,
    "clientActionId": "sample-02-submit-for-checker",
    "correlationId": "corr-sample-02",
    "sourceSystem": "ExternalApi"
  },
  "businessObject": {
    "objectType": "PortfolioTrade",
    "objectId": "PT-1001",
    "objectVersion": 1
  },
  "uiAction": {
    "sectionKey": "Maker",
    "buttonKey": "MakerPrimary",
    "buttonLabelShown": "Submit",
    "saveAsDraft": false
  },
  "dataSave": {
    "sectionKey": "Maker",
    "payload": {
      "comment": "Submit for checker."
    }
  },
  "workflow": {
    "workflowKey": "PortfolioTrade",
    "workflowInstanceId": "WF-PT-1001",
    "workflowDefinitionVersion": 1,
    "expectedCurrentState": "Draft",
    "expectedCurrentTaskId": null,
    "expectedCurrentTaskKey": null,
    "expectedInstanceVersion": 1,
    "actionKey": "SubmitForChecker",
    "actionReason": "Maker submitted for Checker review.",
    "idempotencyKey": "sample-02-submit-for-checker"
  }
}
```

Production requests may omit `actor`; the API should stamp it before validation. If the external API requires `actor` to be absent from public DTOs, create a wrapper DTO that excludes it and then builds `WorkflowActionEnvelope` internally.

## Example Action Response

Shape based on `WorkflowActionResultEnvelope`:

```json
{
  "result": {
    "success": true,
    "resultCode": "Success",
    "correlationId": "corr-sample-02",
    "workflowInstanceId": "WF-PT-1001",
    "businessObject": {
      "objectType": "PortfolioTrade",
      "objectId": "PT-1001"
    },
    "actionKey": "SubmitForChecker",
    "message": "Action 'SubmitForChecker' completed successfully.",
    "oldState": "Draft",
    "newState": "CheckerReview",
    "createdTask": {
      "taskKey": "CheckerReviewTask",
      "assignedRole": "Checker"
    },
    "notification": {
      "queued": true,
      "notificationType": "WorkflowTaskAssigned",
      "recipientRole": "Checker"
    },
    "audit": {
      "written": true,
      "actionKey": "SubmitForChecker"
    },
    "diagnostics": {
      "replay": false
    }
  },
  "workflowContext": {}
}
```

## Recommended DTOs

Keep DTOs minimal.

```csharp
public sealed record StartWorkflowApiRequest(
    string BusinessObjectType,
    string BusinessObjectId);

public sealed record WorkflowActorSnapshot(
    string UserId,
    string DisplayName,
    IReadOnlyList<string> WorkflowRoles);
```

Optional if the API wants a central map object:

```csharp
public sealed record WorkflowHttpResultMapping(
    string ResultCode,
    int StatusCode);
```

Admin request DTOs are only needed if you expose `now` or dry-run flags. Keep them small and admin-only.

