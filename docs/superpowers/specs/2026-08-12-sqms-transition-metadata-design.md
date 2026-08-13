# SQMS Transition Metadata Design

## Goal

Seed complete Opportunity and Bid transition metadata directly in SQL Server so the existing generic workflow engine controls every legal state change without application-code seed logic.

The Opportunity matrix was approved on 2026-08-12 with IPG return-to-Draft from both `Submitted` and `Pending`, and cancellation owned by IPG or System. The Bid matrix was approved without corrections.

## Iteration 1 boundary

This iteration contains only workflow definitions, version 1, states, roles, actions, and transitions for `SqmsOpportunity` and `SqmsBid`.

It does not seed tasks, SLA policies, notification routes, recipient rules, schedules, aggregation logic, or automatic execution. Those capabilities will be layered onto the same stable transition keys in later iterations.

MFO and IPG are the human roles. `System` is a technical role used only when a trusted coordinator invokes a scheduled or calculated transition.

The engine's existing Start behavior requires the exact initial state key `Draft`. Requests provide an action key; they never provide the destination state.

## Opportunity transitions

| From | Action | Role | To |
|---|---|---|---|
| Draft | SubmitOpportunity | MFO | Submitted |
| Submitted | ReturnOpportunityToDraft | IPG | Draft |
| Submitted | MarkOpportunityPending | IPG | Pending |
| Pending | ReturnOpportunityToDraft | IPG | Draft |
| Pending | OpenOpportunityForBidding | IPG | OpenToBidding |
| OpenToBidding | CloseOpportunityBidding | System | ClosedToBidding |
| ClosedToBidding | RequestSellerApproval | IPG | PendingSellerApproval |
| ClosedToBidding | MarkOpportunityNotFulfilled | System | NotFulfilled |
| PendingSellerApproval | MarkOpportunityAwaitingAllocation | IPG or System | AwaitingAllocation |
| PendingSellerApproval | MarkOpportunityPartiallyAllocated | IPG or System | PartiallyAllocated |
| PendingSellerApproval | MarkOpportunityNotFulfilled | System | NotFulfilled |
| AwaitingAllocation | CompleteOpportunity | IPG | Completed |
| PartiallyAllocated | CompleteOpportunity | IPG | Completed |
| Any nonterminal state | CancelOpportunity | IPG or System | Cancelled |

The eight cancellable states are `Draft`, `Submitted`, `Pending`, `OpenToBidding`, `ClosedToBidding`, `PendingSellerApproval`, `AwaitingAllocation`, and `PartiallyAllocated`. The seed stores separate IPG and System transition rows for each state. When upgrading the earlier draft seed, its MFO cancellation rows are retained for history but made inactive.

## Bid transitions

| From | Action | Role | To |
|---|---|---|---|
| Draft | SubmitBid | MFO | Submitted |
| Submitted | MarkBidNotAllocated | IPG or System | NotAllocated |
| Submitted | MoveBidToIpgReview | IPG or System | IpgReview |
| IpgReview | SendBidToSellerReview | IPG | UnderReviewBySeller |
| UnderReviewBySeller | AcceptBid | MFO | Accepted |
| UnderReviewBySeller | PartiallyAcceptBid | MFO | PartiallyAccepted |
| UnderReviewBySeller | RejectBid | MFO | NotAccepted |

## Deployment behavior

The seed is transactional and rerunnable. It inserts missing expected rows and refreshes presentation fields, but it fails instead of silently changing an existing transition's destination or accepting an unexpected active transition. It does not delete metadata or runtime workflow records.

An independent verification script compares both active matrices in both directions, so missing and unexpected rows fail validation.

## Known engine limitation

The current engine marks `WorkflowInstance.Status = Completed` only when the destination state key is literally `Completed`. The SQL correctly marks all other final states as terminal metadata, but their runtime instance status remains `Active` until a later minimal generic-engine change teaches execution to use `WorkflowStateDefinition.IsTerminal`. This does not permit additional transitions because terminal states have none.

Workflow instance IDs are currently generated from `BusinessObjectId` alone. The SQMS caller must therefore use namespaced identifiers such as `SQMS-OPP-1001` and `SQMS-BID-501`, rather than passing the bare numeric table ID, to prevent an Opportunity and Bid with the same numeric ID from competing for `WF-1001`.
