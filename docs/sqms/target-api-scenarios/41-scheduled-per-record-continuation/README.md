# 41. Scheduled per-record continuation

## Contract status

- **Implemented:** the scheduled loop records a candidate failure and continues later candidates in deterministic input order.
- **Target implementation required:** the operator host must surface failures and return a nonzero exit.
- **Testing only:** the middle-candidate failure injection belongs in an isolated test double.

## Endpoint

No public HTTP endpoint exists or is recommended.

## Actor and required role

The scheduled service supplies the trusted `System` actor and role.

## Implemented service method

`ISqmsScheduledService.RunAsync` and `ISqmsScheduledTransitionService.ExecuteScheduledBidActionAsync`.

## Prerequisite

Prepare one closed Opportunity with three valid submitted Bids in an isolated fixture, following [29-scheduled-mixed-bid-classification](../29-scheduled-mixed-bid-classification/README.md).

## Public request notes

There is no browser request and no public failure selector.

## Matching in-process call

See `in-process.cs`. **Testing only:** the transition test double fails Bid 20001 while Bids 20000 and 20002 succeed.

## Target persistence ownership

Each candidate has its own atomic scheduled transition/projection transaction. A failure does not roll an earlier committed candidate back or share a commit with a later one.

## Expected workflow effects

In a three-candidate sequence, the first and third candidates succeed and reach `IPG REVIEW`; the middle candidate fails and stays `SUBMITTED`. The attempted order is 20000, 20001, 20002.

## Expected target-row effects

The first and third Bid projections change through the scheduled four-column path. Every middle-row column and every non-owned column of successful rows remains unchanged.

## Expected audit and idempotency effects

Successful candidates each have one transition audit/idempotency record. The failed candidate has none that survives.

## Expected notification effects

These classification transitions queue no email. No task or SLA rows are created.

## Expected available actions

Refresh each record separately: successful Bids expose `IPG REVIEW` actions; the failed Bid still exposes its `SUBMITTED` scheduled classification action.

## Failure behavior

`SqmsScheduledRunResult.Success` is false and `Failures` contains the middle Bid. The operator command must use a nonzero exit even though later work succeeded.

## Next valid packs

Retry the corrected middle candidate, then continue successful Bids with [21-bid-send-to-seller-review](../21-bid-send-to-seller-review/README.md).
