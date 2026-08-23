# SQMS Target API Integration

The SQMS adapter is an in-process application service. The target API continues to own its HTTP DTOs, authentication, authorization, target-row persistence, address resolution, and response mapping. The generic workflow engine remains the only transition authority.

## Executable target-team scenarios

The complete Opportunity, Bid, scheduling, notification, failure, replay, rollback, and persistence-ownership handoff is in [SQMS Target API scenario catalog](./target-api-scenarios/README.md). The catalog pairs the implemented in-process contracts with clearly labeled recommended target-owned HTTP routes and DTOs.

## Transaction sequence

For a create-and-submit request, the target API must:

1. Open one SQL transaction on its scoped connection.
2. Save the `SSQMS.Opportunity` or `SSQMS.Bid` row and obtain its identity `ID`.
3. Enlist the scoped workflow context on that same connection and transaction.
4. Call `StartOpportunityAsync` or `StartBidAsync` with the persisted ID and trusted normalized actor.
5. Treat an unsuccessful `SqmsOperationResult` or an exception as a reason to roll back the outer transaction.
6. Read the authoritative state from `SqmsOperationResult` and persist that state to the target row through the target API's own data-access layer.
7. Commit the transaction once, after the target row, workflow runtime, audit, idempotency, outbox, and target-owned state persistence have all succeeded.
8. Map `SqmsOperationResult` into target-owned response DTOs.

For later actions, the target API uses the same sequence and calls `ExecuteOpportunityActionAsync` or `ExecuteBidActionAsync`. It passes only the target ID, `actionKey`, expected workflow version, trusted actor, and server-resolved recipient inputs. It never supplies a next state. Request-driven Start and Action calls return authoritative state/context but do not update `SSQMS.Opportunity` or `SSQMS.Bid`; the target API must persist the returned state.

When no outer transaction exists, the SQMS service creates and owns a workflow-database transaction for workflow runtime changes. When the target API already owns the transaction, the start and workflow execution services join it and do not commit it independently. Scheduled close/classification is the only thin-layer path that directly projects `Status`, `WorkflowState`, `ModifiedBy`, and `ModifiedDate` to the target row; that scheduled projection remains atomic with the workflow transition.

Existing target API integrations that relied on request-driven adapter projection must be changed before adopting this build: after every successful Start or Action, persist the returned authoritative state through the target API's own repository inside its application transaction.

## Trusted inputs

Actor identity, roles, and recipient addresses are server-owned values. The target API resolves `RelationshipManager`, `SellerRMTeam`, and `BuyerRmteam` identifiers into ordered `WorkflowRecipientInput` arrays under the exact metadata source keys requested by the workflow route.

The browser never sends or controls:

- actor identity or workflow roles;
- recipient addresses;
- notification route or template keys;
- template values or hyperlinks;
- `WorkflowState`, `Status`, or `toState`;
- workflow instance identifiers or idempotency keys.

The target API returns the authoritative workflow state and version from `SqmsOperationResult`. The UI must refresh from that result or a subsequent context read.
