# Phase 3: Workflow Contracts and Console Actions - Research

**Date:** 2026-05-07
**Mode:** Inline fallback; GSD planning agents are not installed in this environment.

## Implementation Approach

Phase 3 should add contract and command behavior without pulling in Phase 4 workflow execution. The cleanest split is:

1. Application-layer DTOs, JSON parsing, validation, override normalization, request hashing, and `execute-json` result shaping.
2. SQL-backed `show-context` read model using Phase 2 metadata/runtime entities.

This preserves the chosen boundary:

- `show-context` becomes useful and SQL-backed now.
- `execute-json` parses, validates, normalizes, and returns a `WorkflowActionResult` shape, but it does not close/create tasks, move workflow state, write audit, queue notifications, persist idempotency replay records, or run persisted concurrency checks.

## DTO and Serialization Research

Use `System.Text.Json` with explicit DTO records/classes in the application layer. Keep DTO names generic:

- `WorkflowActionEnvelope`
- `WorkflowActionResult`
- `WorkflowContextResponse`
- `AvailableWorkflowAction`

Recommended patterns:

- Keep DTOs in `WorkflowEngine.Application/Contracts`.
- Use stable camelCase JSON through `JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase`.
- Avoid dynamic string manipulation for canonical request hashing; parse JSON payloads as `JsonElement` and write canonical JSON recursively with sorted object properties.
- Preserve `dataSave.payload` as opaque JSON for validation and hashing. Phase 4 will decide when/how to save it to `MockSectionData`.

## Validation Research

Phase 3 can fully validate all envelope rules that do not require persisted state:

- Required `request`, `businessObject`, `actor`, `uiAction`, `dataSave`, and `workflow` blocks.
- Required non-empty string fields for actor, roles, section keys, workflow key, workflow instance id, and action key.
- `dataSave.payload` must be a JSON object; empty object is valid.
- `actor.roles` must be present and non-empty.
- `dataSave.sectionKey` is required and non-empty.
- MakerPrimary consistency:
  - `SaveDraft` requires `uiAction.saveAsDraft = true`.
  - `SubmitForChecker` and `ResubmitForChecker` require `uiAction.saveAsDraft = false`.
- `toState` must not be accepted as an authoritative workflow field.

Phase 4 should own:

- Transition existence and authorization against current persisted state.
- Role ambiguity and forbidden actor-role cases.
- Persisted workflow definition version mismatch.
- Persisted current state/task/version concurrency guards.
- Any transaction rollback and side-effect behavior.

## Override and Hashing Research

Console overrides must be applied before validation, idempotency key derivation, request hash, and result shaping:

- `--business-object <id>` replaces `businessObject.objectId`.
- `--workflow-instance <id>` replaces `workflow.workflowInstanceId`.
- Effective `idempotencyKey` uses `workflow.idempotencyKey` when present, otherwise `request.clientActionId`.
- Effective `clientActionId`, idempotency key, and correlation id should be derived consistently from the effective workflow/business object/action values when override retargeting requires it.

The request hash includes only the normalized effective request fields listed in `PROJECT.md` section 9 and excludes `clientActionId`, `idempotencyKey`, `correlationId`, and `requestedAtUtc`.

## show-context Research

`show-context` should be implemented as a read-only SQL query service:

- Load `WorkflowInstance` by `WorkflowInstanceId`.
- Join/load `WorkflowDefinitionVersion` to expose the client-visible `VersionNumber`.
- Load state metadata for `currentStateLabel`.
- Load the current pending `WorkflowTask`, if any.
- Load task definition metadata for task label/SLA policy where needed.
- Compute SLA directly from `WorkflowTask.DueAtUtc` and `TimeProvider.GetUtcNow()`.
- Load transitions for current state whose `AllowedRoleKey` intersects actor roles.
- Load action definitions for available action presentation hints.
- Build `requestDefaults` from the persisted snapshot.

Important constraints:

- `show-context` must not write data.
- `actorAccess.displayName` defaults to the `--user` argument.
- `sla` is always an object, never null.
- Available actions are binary; do not add `enabledByWorkflow` or `disabledReason`.

## Command Parsing Research

Current `CommandRegistry` dispatches by string and receives raw args. This can be extended conservatively:

- Add application command services for `execute-json` and `show-context`.
- Keep later commands as stubs.
- Parse flags with small internal helpers rather than adding a command-line framework.
- Support `--format json` for `execute-json`; make `show-context` JSON default while accepting `--format json`.

## Testing Strategy

Add focused tests:

- DTO deserialization and required shape.
- Envelope validator failures for missing actor, empty roles, missing dataSave, null/non-object payload, missing workflow/action key, and MakerPrimary conflicts.
- Override normalization and stable request hash across JSON property order variations.
- `execute-json` readable default output and `--format json` output for valid and invalid envelopes.
- `show-context` SQL-backed JSON for seeded/runnable rows created in test setup.
- `show-context` read-only behavior by asserting no row counts change.

SQL-backed `show-context` tests should use the same local SQL Server pattern as Phase 2 and may need approved local SQL access outside the default sandbox.

## Risks

- Phase 3 needs a minimal test fixture to create `WorkflowInstance` and pending `WorkflowTask` rows because Phase 4 `start-workflow` execution does not exist yet.
- Contract shapes are stop-and-ask guarded. Do not rename fields or add conditional-action fields beyond `PROJECT.md`.
- Keep `execute-json` honest: valid envelopes can produce a contract-shaped deferred result, but they must not pretend a transition executed.

