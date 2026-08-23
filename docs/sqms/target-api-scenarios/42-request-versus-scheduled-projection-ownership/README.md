# 42. Request versus scheduled projection ownership

The scheduled half invokes the implemented `ISqmsScheduledService`; it has no public HTTP route.

## Contract status

- **Implemented:** request Start/Action uses no adapter projection; scheduled transition uses the four-column adapter projection.
- **Recommended target facade:** resource Action routes are proposed and are not implemented in this repository.
- **Target implementation required:** the target API owns request-side target persistence and the outer transaction.
- **Testing only:** capture before/after row snapshots in an isolated integration database.

## Endpoint

Use a normal proposed request Action route for the request half. The scheduled half has no public HTTP endpoint.

## Actor and required role

Request half uses a trusted metadata-authorized actor. Scheduled half uses the server-owned `System` actor.

## Implemented service method

`ISqmsWorkflowService.ExecuteOpportunityActionAsync` and `ISqmsScheduledTransitionService.ExecuteScheduledOpportunityActionAsync`.

## Prerequisite

For the request proof, complete [04-opportunity-submitted-return-to-draft](../04-opportunity-submitted-return-to-draft/README.md). For the scheduled proof, complete [07-opportunity-pending-open-for-bidding](../07-opportunity-pending-open-for-bidding/README.md) in a separate isolated record.

## Public request notes

The request contains only `actionKey` and `expectedWorkflowVersion`; it never delegates projection ownership or supplies `toState`.

## Matching in-process call

See `in-process.cs`. Capture full rows around each service boundary, not just the two state columns.

## Target persistence ownership

After request execution, the complete target row remains byte-for-byte unchanged after the request adapter call. The target API then persists the authoritative returned `Status` and `WorkflowState` in the shared outer transaction. Scheduled execution directly projects exactly `Status`, `WorkflowState`, `ModifiedBy`, and `ModifiedDate`.

## Expected workflow effects

Both operations transition from metadata and return authoritative state. Neither creates an SQMS task or SLA.

## Expected target-row effects

Request adapter: zero target changes. Target-owned save: only the fields chosen by the target's existing persistence contract. Scheduled adapter: exactly the four projection columns change and every other target column remains unchanged.

## Expected audit and idempotency effects

Each non-replay transition creates its normal single audit/idempotency set. Ownership changes neither identity nor counts.

## Expected notification effects

Only metadata-configured transition notifications are queued; ownership does not add a route or recipient source.

## Expected available actions

Refresh context after the single commit and render returned metadata-derived `availableActions`.

## Failure behavior

Request target-save failure rolls all enlisted work back as in [39](../39-rollback-target-persistence-after-workflow/README.md). Scheduled projection failure rolls its workflow transition back as in [40](../40-rollback-scheduled-target-projection/README.md).

## Next valid packs

Use the corresponding Opportunity/Bid path from the root catalog; ownership rules remain the same for every transition.
