# SQMS End-to-End Workflow Integration Design

**Date:** 2026-08-16

**Status:** Approved for implementation

**Scope:** SQMS application thin layer, taskless workflow coordination, automated notifications, scheduled close/classification, and end-to-end verification
**UI scope:** Excluded

## Objective

Integrate the existing `SSQMS.Opportunity` and `SSQMS.Bid` application tables with the current generic workflow engine without duplicating transition logic, breaking Portfolio Trade, or moving workflow internals into UI-owned tables.

The result must support:

- create/start, get/context, and execute/action flows;
- atomic projection of workflow results to SQMS `Status` and `WorkflowState`;
- aggregate initial and reminder open-bidding emails;
- automatic bidding close and Bid classification;
- seller-approval orchestration;
- accepted, partially accepted, and not accepted Bid emails;
- durable outbox delivery and retry;
- taskless SQMS workflows with no SQMS SLA;
- exact SQL-backed, console, API, notification, and regression verification.

## Locked boundaries

### Generic engine

The reusable engine remains the sole workflow-transition authority. It owns:

- workflow definitions and versions;
- workflow instances and versions;
- transition validation;
- actor-role authorization;
- workflow audit;
- idempotency and concurrency;
- notification route/template/recipient metadata;
- notification outbox and SMTP processing.

Generic engine code must not read `SSQMS.Opportunity` or `SSQMS.Bid` columns and must not contain SQMS state, action, classification, date, email, or SharePoint-link rules.

### SQMS application

The SQMS application owns:

- Opportunity and Bid business data;
- business validation;
- authenticated users and resolved roles;
- parent/child validation;
- scheduled-date interpretation;
- Bid classification calculations;
- construction of notification template values and dynamic recipient inputs;
- projection of engine results into the existing `Status` and `WorkflowState` columns.

### UI

The UI renders `availableActions` returned by workflow context and sends only the selected `actionKey`. It never supplies `toState`, workflow roles, notification routes, static recipients, HTML templates, internal workflow identities, or scheduling identities.

## Current baseline

The existing metadata remains stable:

- `SqmsOpportunity`: 11 states, 11 actions, 3 roles, 31 active transitions.
- `SqmsBid`: 8 states, 7 actions, 3 roles, 9 active transitions.

The exact mock application tables are created by `sql/sqms/testing/004_create_sqms_mock_application_tables.sql`. Existing console Start, Context, and Execute paths already prove both transition-only workflows without changing application rows.

## Application business fields

Only SQMS business inputs are added to UI-owned tables. No workflow instance IDs, notification outbox IDs, delivery markers, retry counts, cycle keys, or SharePoint URLs are added.

### `SSQMS.Opportunity`

Recommended additive nullable columns:

| Column | SQL type | Meaning |
|---|---|---|
| `OpenBiddingNoticeAt` | `datetimeoffset(0)` | Exact initial aggregate-email instant |
| `OpenBiddingReminderAt` | `datetimeoffset(0)` | Exact reminder-email instant |
| `BidResponseDueDate` | `date` | Response date displayed in open-bidding email |
| `BiddingCloseAt` | `datetimeoffset(0)` | Exact automatic close instant |
| `SellerResponseDueDate` | `date` | Response date displayed in seller-approval email |
| `OpportunityRelationshipManagerEmail` | `varchar(320)` | Dynamic Opportunity RM recipient |
| `SellerRmTeamEmailsJson` | `nvarchar(max)` | Ordered JSON array of Seller RM Team addresses |

### `SSQMS.Bid`

Recommended additive nullable columns:

| Column | SQL type | Meaning |
|---|---|---|
| `BidPricingCategory` | `varchar(20)` | `FairValue`, `Premium`, or `Discount` |
| `BidRelationshipManagerEmail` | `varchar(320)` | Dynamic Bid RM recipient |
| `BuyerRmTeamEmailsJson` | `nvarchar(max)` | Ordered JSON array of Buyer RM Team addresses |

`BidPricingCategory` has a check constraint. Team JSON columns have `ISJSON` constraints when non-null. Production static recipient addresses remain workflow metadata, not application columns.

## Workflow identity

The thin layer deterministically derives identities from application keys:

- Opportunity business object: `SQMS-OPP-{ID}`
- Opportunity workflow instance: `WF-SQMS-OPP-{ID}`
- Bid business object: `SQMS-BID-{ID}`
- Bid workflow instance: `WF-SQMS-BID-{ID}`

The prefixes prevent Opportunity and Bid rows with the same numeric ID from colliding in `WorkflowInstance.WorkflowInstanceId`.

## Thin-layer architecture

The integration is hosted in-process by the SQMS application API through the existing workflow dependency-injection registration. The browser does not call the generic Workflow API directly.

### Components

- `SqmsWorkflowCoordinator`: Start, Context, Action, transaction coordination, and application-state projection.
- `SqmsWorkflowIdentity`: deterministic business-object and workflow-instance IDs.
- `SqmsNotificationInputFactory`: trusted recipient/template input construction.
- `SqmsScheduleRunner`: due-cycle discovery, notice/reminder execution, and close processing.
- `SqmsBidClassificationService`: pure decimal classification rules.
- SQMS repositories: application-state reads, due-row queries, Bid loading, and conditional projection updates.

The Workflow HTTP API remains unchanged and continues to support existing callers.

## Transaction model

The SQMS and Workflow EF Core contexts use the same scoped SQL connection and enlist in the same caller-owned transaction.

For create/start or an ordinary action:

1. begin the SQMS transaction;
2. read the SQMS row and expected workflow state;
3. enlist `WorkflowDbContext` in the same SQL transaction;
4. start or execute the generic workflow;
5. update `WorkflowState` from `CurrentState`;
6. update `Status` from the upper-cased `CurrentStateLabel`;
7. persist workflow audit/idempotency/outbox changes;
8. commit once.

SMTP delivery runs after commit through the existing notification-outbox processor. SMTP failure never rolls back a committed business transition.

The generic Start service must own a transaction only when `WorkflowDbContext.Database.CurrentTransaction` is null. Existing standalone Start callers retain current behavior.

## Application operations

### Start

After an application row receives its SQL identity, the coordinator starts the appropriate workflow. Start without an initial action leaves the row in `Draft`. Start with `SubmitOpportunity` or `SubmitBid` may atomically create and submit.

### Context

The coordinator derives the workflow instance ID and calls `IWorkflowContextReader` with the trusted actor. The application response combines SQMS business data with the generic workflow context. The UI renders only returned `availableActions`.

### Execute

The application action request contains `actionKey` and optional reason only. The coordinator loads the row, performs SQMS business validation, executes the action, conditionally projects the returned state, and returns refreshed context.

### Bid submission gate

Before `SubmitBid`, SQMS verifies:

- parent Opportunity exists and is not deleted;
- parent `WorkflowState` is `OpenToBidding`;
- the initial sale-cycle notification outbox row is `Sent`;
- the close instant has not passed;
- amount and pricing category are valid.

## Taskless sale-cycle workflow

A metadata-only `SqmsSaleCycle` workflow reuses action audit, metadata templates, idempotency, recipient rules, and the existing outbox without adding a generic scheduling subsystem or an SQMS SaleCycle application table.

States:

- `Draft`
- `NoticeQueued`
- `ReminderQueued`
- `Completed`

System actions:

- `SendOpenBiddingNotice`: `Draft -> NoticeQueued`
- `SendOpenBiddingReminder`: `NoticeQueued -> ReminderQueued`
- `CompleteSaleCycle`: `ReminderQueued -> Completed`

The workflow has no task and no SLA. Its deterministic business identity is derived from effective sale date, notice instant, reminder instant, and response due date.

## Scheduled notice flow

At or after `OpenBiddingNoticeAt`, the runner:

1. selects non-deleted Opportunities in `OpenToBidding`;
2. groups exact matching cycle values;
3. derives the cycle identity;
4. starts `SqmsSaleCycle` with `SendOpenBiddingNotice` as `System`;
5. supplies quarter, year, response date, and configured SharePoint view URL;
6. queues one aggregate outbox row.

Unique workflow identity and action idempotency make repeated scans safe. A late-opened Opportunity appears through the dynamic SharePoint view but does not create a duplicate initial message.

## Scheduled reminder flow

At or after `OpenBiddingReminderAt`, the runner:

1. resolves the existing cycle;
2. verifies the initial-notice outbox row is `Sent`;
3. executes `SendOpenBiddingReminder` as `System`;
4. queues one aggregate reminder message.

The reminder never overtakes an initial message still pending delivery.

## Automatic close and classification

### Parent close transaction

For each due `OpenToBidding` Opportunity:

1. read the Opportunity and expected workflow state;
2. execute `CloseOpportunityBidding` as `System`;
3. project `ClosedToBidding` to the application row;
4. commit.

The committed parent closure prevents new Bid submissions.

### Child classification transaction

In a second transaction:

1. load all non-deleted submitted Bids for the closed Opportunity;
2. ignore Draft Bids;
3. parse Opportunity and Bid money using the approved culture/format contract;
4. require `FairValue`, `Premium`, or `Discount` on every submitted Bid;
5. sum requested commitment for `FairValue` and `Premium` Bids;
6. when that total is strictly greater than the sale amount, execute `MarkBidNotAllocated` for every `Discount` Bid;
7. execute `MoveBidToIpgReview` for every remaining submitted Bid;
8. project all returned Bid states;
9. set `TotalBid` to eligible Bid count and update `LargestBid`/`LowestBid`;
10. commit all child changes together.

If child processing fails, the child transaction rolls back while the parent remains closed. A later run selects closed Opportunities with submitted Bids and retries the same stable set.

When no submitted Bids exist, execute `MarkOpportunityNotFulfilled`.

## Seller approval

`RequestSellerApproval` is an atomic parent/child coordination operation:

1. read the closed Opportunity;
2. load its `IpgReview` Bids;
3. execute `SendBidToSellerReview` for each Bid;
4. project all Bids to `UnderReviewBySeller`;
5. execute `RequestSellerApproval` on the Opportunity;
6. project `PendingSellerApproval`;
7. queue one seller-approval email;
8. commit.

If any child transition fails, the entire operation rolls back and no seller email is committed.

## Bid dispositions and Opportunity roll-up

For each Bid under seller review:

- `AcceptBid` queues the accepted email and enters `Accepted`.
- `PartiallyAcceptBid` queues the partially accepted email and enters `PartiallyAccepted`.
- `RejectBid` queues the not-accepted email and enters `NotAccepted`.

After the final Bid is decided, the coordinator executes an existing System roll-up transition:

- all fully accepted -> `MarkOpportunityAwaitingAllocation`;
- at least one accepted or partially accepted, but not all fully accepted -> `MarkOpportunityPartiallyAllocated`;
- all not accepted -> `MarkOpportunityNotFulfilled`.

IPG later executes `CompleteOpportunity` after downstream allocation work.

## Notification routes

| Route key | Trigger | To source | Cc source |
|---|---|---|---|
| `RouteSqmsOpenBiddingNotice` | `SendOpenBiddingNotice` | three static metadata addresses | two static metadata addresses |
| `RouteSqmsOpenBiddingReminder` | `SendOpenBiddingReminder` | three static metadata addresses | two static metadata addresses |
| `RouteSqmsSellerApprovalRequested` | `RequestSellerApproval` | `opportunityRelationshipManager`, `sellerRmTeam` | two static metadata addresses |
| `RouteSqmsBidAccepted` | `AcceptBid` | `bidRelationshipManager`, `buyerRmTeam` | two static metadata addresses |
| `RouteSqmsBidPartiallyAccepted` | `PartiallyAcceptBid` | `bidRelationshipManager`, `buyerRmTeam` | two static metadata addresses |
| `RouteSqmsBidNotAccepted` | `RejectBid` | `bidRelationshipManager`, `buyerRmTeam` | two static metadata addresses |

Test seed uses addresses under `example.test` and links under `https://example.test/sqms/`. Production values are deployment metadata/configuration.

Template tokens:

- `quarter`
- `year`
- `responseDueDate`
- `effectiveDateForSale`
- `vehicle`
- `accountNumber`
- `accountName`
- `fundName`
- `saleAmount`
- `purchaseAmount`
- `viewUrl`

Templates are versioned plain-text and HTML metadata. The application supplies plain token values and the engine HTML-encodes them.

## Generic compatibility changes

Only two generic behavior corrections are approved:

1. Start joins a caller-owned transaction when present and retains standalone ownership otherwise.
2. terminal runtime completion is derived from destination `WorkflowStateDefinition.IsTerminal`, not the literal state key `Completed`.

No public HTTP endpoint, header, request DTO, response DTO, console command, dependency-injection method, Portfolio Trade key, or existing metadata meaning changes.

## Performance and concurrency

- due-candidate queries are set-based and bounded;
- scheduler actions use deterministic identities and idempotency keys;
- engine instance versions remain the concurrency authority;
- SQMS projection updates compare the expected previous state;
- parent Opportunities process independently;
- each Opportunity loads its Bids in one query;
- SMTP remains outside request/business transactions;
- outbox leasing and atomic claims remain generic;
- all current time comes from `TimeProvider`;
- scheduled work supports cancellation tokens and structured logging.

Recommended indexes:

- filtered Opportunity indexes for open notice, reminder, and close scans;
- Bid index on `(OpportunityId, WorkflowState, IsDeleted)` including amount and category.

## Failure behavior

- business validation failure: no workflow action;
- invalid/stale action: conflict and refreshed context;
- workflow or SQMS persistence failure: shared transaction rollback;
- SMTP failure: committed action remains valid and outbox retries;
- invalid classification input: parent stays closed and children stay submitted;
- one Opportunity failure: other due Opportunities continue;
- scheduler interruption: next scan resumes from persisted state;
- duplicate scan: deterministic identity/idempotency prevents duplicate effects.

## Verification requirements

Implementation is incomplete until all of the following pass:

- pure classification unit tests;
- shared-transaction Start and Action tests including rollback;
- application projection tests;
- Bid parent/notice/close gate tests;
- scheduled initial/reminder exact-time and rerun tests;
- concurrent scheduled-run tests;
- automatic close/classification matrix tests;
- seller-approval atomicity tests;
- recipient snapshot and exact template tests;
- all SQMS terminal-state tests;
- exact SQMS metadata and seed-twice verification;
- complete Portfolio Trade core and Workflow API regression suites;
- console Start/Get/Execute/schedule/outbox demonstration against mock SQMS tables;
- smtp4dev acceptance for every SQMS email template.

## Delivery slices

1. Generic transaction participation and terminal completion corrections.
2. Additive SQMS test-table business fields and verification.
3. Thin-layer Start, Context, Action, and state projection.
4. SQMS notification metadata and sale-cycle workflow.
5. Scheduled notice and reminder execution.
6. Automatic close and Bid classification.
7. Seller approval, Bid disposition, and Opportunity roll-up.
8. Full regression, console demonstration, SMTP acceptance, documentation, and handoff.
