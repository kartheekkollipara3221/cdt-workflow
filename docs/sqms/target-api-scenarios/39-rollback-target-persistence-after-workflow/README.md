# 39. Target persistence failure after workflow execution

## Contract status

- **Implemented:** workflow services enlist in an existing `WorkflowDbContext` transaction and do not independently commit it.
- **Recommended target facade:** the Action route is proposed and is not implemented in this repository.
- **Target implementation required:** the target API must create the shared outer transaction and roll it back on target persistence failure.
- **Testing only:** deliberate repository failure belongs in an isolated integration test; there is no public failure control.

## Endpoint

The normal proposed route is `POST /v1/sqms/opportunities/10000/workflow/actions`; the failure is injected behind the target repository, never through public JSON.

## Actor and required role

Trusted `IPG` actor with server-resolved seller-approval recipients.

## Implemented service method

`ISqmsWorkflowService.ExecuteOpportunityActionAsync`.

## Prerequisite

Complete [08-opportunity-scheduled-close-bidding](../08-opportunity-scheduled-close-bidding/README.md): state `CLOSED TO BIDDING`, version 5.

## Public request notes

Use the ordinary `RequestSellerApproval` request. No `fail`, test, transaction, or persistence switch is part of the public contract.

## Matching in-process call

See `in-process.cs`. The test repository throws after the workflow call succeeds but before commit.

## Target persistence ownership

The target API owns one SQL connection and outer transaction shared by target persistence and `WorkflowDbContext`; it catches the target save failure and rolls back once.

## Expected workflow effects

After rollback, state remains `CLOSED TO BIDDING`, version remains 5, and no runtime success survives.

## Expected target-row effects

The complete target row is restored to its pre-request value, including prerequisite business edits made inside the transaction.

## Expected audit and idempotency effects

No `RequestSellerApproval` audit or derived idempotency record survives.

## Expected notification effects

No seller-approval outbox row survives; nothing can be delivered.

## Expected available actions

Reload context after rollback. Actions are derived from the unchanged `CLOSED TO BIDDING` state.

## Failure behavior

Return the target API's normal unexpected persistence Problem Details response. The target row, workflow state, audit, idempotency, and outbox all remain at their pre-request values.

## Next valid packs

After correcting persistence, refresh and retry [09-opportunity-request-seller-approval](../09-opportunity-request-seller-approval/README.md).
