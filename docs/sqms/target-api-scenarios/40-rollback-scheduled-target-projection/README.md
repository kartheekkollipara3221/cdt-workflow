# 40. Scheduled target projection rollback

## Contract status

- **Implemented:** scheduled transition and four-column target projection share one workflow-owned transaction when no outer transaction exists.
- **Testing only:** replace `ISqmsDataStore` with a throwing test double in an isolated automated test. Never deploy this failure injection.

## Endpoint

No public HTTP endpoint exists or is recommended.

## Actor and required role

The scheduled service supplies the trusted `System` actor and role.

## Implemented service method

`ISqmsScheduledTransitionService.ExecuteScheduledOpportunityActionAsync`, reached through `ISqmsScheduledService.RunAsync`.

## Prerequisite

Complete [07-opportunity-pending-open-for-bidding](../07-opportunity-pending-open-for-bidding/README.md), then arrange the documented due date in an isolated fixture.

## Public request notes

There is no public request and no public field that triggers a projection failure.

## Matching in-process call

See `in-process.cs`. **Testing only:** scheduled target projection throws from the injected data-store test double.

## Target persistence ownership

Only the scheduled adapter may write the four projection columns. Its projection runs before the owned transaction commits.

## Expected workflow effects

The thrown projection error causes the workflow transaction to roll back; state and version remain `OPEN TO BIDDING` and 4.

## Expected target-row effects

No target column changes because the projection never completes.

## Expected audit and idempotency effects

No `CloseOpportunityBidding` audit or `SQMS:OPP:10000:4:CloseOpportunityBidding` idempotency record survives.

## Expected notification effects

No transition outbox row survives. No task or SLA row is created.

## Expected available actions

The unchanged workflow still exposes the metadata-valid scheduled close to the trusted system role when due.

## Failure behavior

The scheduler records the candidate failure, continues later candidates, and returns unsuccessful. The workflow transition, audit, idempotency, outbox, and target projection roll back together.

## Next valid packs

After correcting persistence, retry [08-opportunity-scheduled-close-bidding](../08-opportunity-scheduled-close-bidding/README.md).
