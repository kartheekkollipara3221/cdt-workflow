# 43. Complete Opportunity and four-Bid acceptance

## Contract status

- **Implemented:** The in-process service behavior shown here exists in the current workflow/SQMS assemblies.
- **Recommended target facade:** The HTTP routes and DTOs are proposed for the external target API and are not implemented in this repository.
- **Target implementation required:** The target API must supply authentication, actor/recipient resolution, target persistence, transaction enlistment, HTTP mapping, and response DTOs.

## Purpose

This pack joins the preceding focused packs into one acceptance journey for one Opportunity and four Bids. It proves all four Bid outcomes, the complete Opportunity path, request-versus-scheduled persistence ownership, and all six configured SQMS notification routes.

The IDs and totals below are retained evidence from the successful 2026-08-22 live acceptance. They are a reference result, not instructions to reuse or mutate that retained database or mailbox. A target team executes `sequence.http` and `in-process.cs` with newly saved rows in its own isolated environment.

## Endpoint

The proposed, target-owned facade uses:

- `POST /v1/sqms/opportunities/{id}/workflow/start`
- `POST /v1/sqms/opportunities/{id}/workflow/actions`
- `POST /v1/sqms/bids/{id}/workflow/start`
- `POST /v1/sqms/bids/{id}/workflow/actions`

These routes are **Recommended target facade** only. Scheduled processing is not a browser-callable endpoint; an operator invokes the implemented `ISqmsScheduledService` through the approved scheduler/console host.

## Actor and required role

The target server constructs trusted `SqmsActor` values from authentication: MFO starts/submits, IPG performs review and Opportunity actions, Seller chooses Bid dispositions, and System is scheduler-owned. The browser supplies no actor, role, recipient, route, template, idempotency, workflow-instance, or target-state authority.

## Implemented service methods

The journey uses `ISqmsWorkflowService.StartOpportunityAsync`, `ExecuteOpportunityActionAsync`, `StartBidAsync`, and `ExecuteBidActionAsync`, plus `ISqmsScheduledService.RunAsync` for open/reminder/close processing.

## Prerequisite

In an isolated acceptance database, save one Opportunity and four related Bids with the required enrichment columns before calling Start. Configure the six notification routes and a fake/local SMTP sender. Use a controlled `TimeProvider` so open, reminder, and close runs occur at deterministic configured dates.

Do not rerun `scripts/run-sqms-projection-ownership-live-acceptance.ps1` against the retained acceptance database. Its data and smtp4dev mailbox are deliberately preserved.

## Public request notes

Start has an empty body. Action bodies contain only `actionKey` and `expectedWorkflowVersion`. The route owns the entity ID, and metadata owns every destination state. `sequence.http` uses example IDs `10000` through `10003`; replace them with the IDs of newly saved isolated target rows.

## Matching in-process call

See `in-process.cs`. `actorFactory`, `recipientResolver`, `targetErrorMapper`, target repositories/entities, and the shared-transaction coordinator are target-owned integration placeholders and are not classes implemented by this repository.

For each request-driven call, the target API begins one outer transaction, saves prerequisite business changes, enlists `WorkflowDbContext` on the same SQL connection and transaction, invokes `ISqmsWorkflowService`, and persists the returned state only when `replay = false`. It commits once. A replay rolls back prerequisite writes, reloads the committed target row, verifies its projection, and issues no target update.

## Target persistence ownership

The request-driven SQMS adapter does not update an Opportunity or Bid row. The target API records authoritative returned `Status` and `WorkflowState` after each non-replay success. Scheduled close/classification is the only adapter-owned target write and changes exactly `Status`, `WorkflowState`, `ModifiedBy`, and `ModifiedDate`; every other target column remains unchanged.

## Acceptance sequence

1. Start Opportunity; return it to Draft; submit it again; mark it Pending; open it for bidding.
2. Start all four Bids. Return and resubmit the first Bid to cover the existing-Draft path.
3. Run scheduled open and reminder occurrences, including their idempotent replays.
4. Run scheduled close. It closes the Opportunity, moves three qualifying Bids to `IPG REVIEW`, and moves the Discount Bid to `NOT ALLOCATED`.
5. Send the three reviewable Bids to Seller and finish them as `ACCEPTED`, `PARTIALLY ACCEPTED`, and `NOT ACCEPTED`.
6. Request Opportunity seller approval, mark it `AWAITING ALLOCATION`, and complete it.
7. Process the notification outbox through fake/local SMTP and verify the immutable recipient snapshots and rendered messages.

## Retained reference result

| Entity | ID | Final state | Version |
|---|---:|---|---:|
| Opportunity | 10000 | `COMPLETED` | 10 |
| Bid | 10000 | `ACCEPTED` | 7 |
| Bid | 10001 | `PARTIALLY ACCEPTED` | 5 |
| Bid | 10002 | `NOT ACCEPTED` | 5 |
| Bid | 10003 | `NOT ALLOCATED` | 3 |

| Count | Retained proof |
|---:|---|
| 20 | Request-driven target rows remained unchanged during the adapter call (`RequestUnchanged`) |
| 20 | Target owner persisted the returned state afterward (`TargetOwnerPersisted`) |
| 5 | Scheduled adapter projected the four owned columns (`ScheduledProjected`) |
| 25 | Workflow audit rows |
| 25 | Idempotency rows |
| 6 | Sent outbox/SMTP messages with retry count zero |

There were five workflow instances, zero SQMS tasks and zero SLA schedules. Target `Status`, target `WorkflowState`, and workflow `CurrentStateKey` matched for every entity. Opportunity sentinel business fields remained unchanged.

## Expected audit and idempotency effects

Each of the 20 successful request-driven Start/Action calls contributes one audit and one idempotency row. The five scheduled transitions contribute the remaining five audits and idempotency rows. Replaying the two notification occurrences or a matching request must not add audit, idempotency, target-write, or outbox side effects.

## Expected notification effects

Exactly one message is delivered for each configured route:

- `SqmsOpportunityOpen`
- `SqmsOpportunityReminder`
- `SqmsOpportunityPendingSellerApproval`
- `SqmsBidAccepted`
- `SqmsBidPartiallyAccepted`
- `SqmsBidNotAccepted`

The `NOT ALLOCATED` Bid has no disposition email route. See `expected-emails.md` for retained subjects and target-team checks.

## Expected available actions

At each step, use the role-filtered `availableActions` returned by the target facade. After the final states above, the Opportunity and all four Bids are terminal and return no further actions.

## Failure behavior

Any unsuccessful request or exception rolls back target changes, runtime state, audit, idempotency, and outbox effects together. A scheduled candidate failure rolls that candidate back, records a failure, continues later candidates, and yields a nonzero operator result. No Bid action rolls up or changes its parent Opportunity.

## Next valid packs

This is the terminal composite acceptance pack. Return to the [root catalog](../README.md) for individual transition, failure, replay, rollback, and ownership proofs.
