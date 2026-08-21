# SQMS Thin-Layer Workflow Integration Design

**Date:** 2026-08-20

**Status:** Written specification approved on 2026-08-20

**Implementation status:** Milestones 1-6 implemented and workspace-verified on 2026-08-20. Production DBA execution, application deployment, and scheduler enablement remain pending explicit authorization.

**Target branch:** `sqms_clean_slate`

## 1. Purpose

Add SQMS Opportunity and Bid workflow support around the existing reusable workflow engine without creating a second state machine, a second API, or SQMS behavior inside the generic engine.

The target API already hosts the workflow engine and its UI is implemented separately. This change adds a thin in-process SQMS adapter, metadata, scheduled processing, and notification support. It preserves the existing CDT/Portfolio Trade contracts and runtime behavior.

This document supersedes the rejected SQMS implementation and any retained clean-slate note that conflicts with the explicit decisions recorded here.

## 2. Governing constraints

- Follow `PROJECT.md`, including its architecture, backward-compatibility, stop-and-ask, and quality-gate rules.
- Use .NET 10, SQL Server, EF Core, and `TimeProvider`.
- Keep the workflow engine generic and metadata-driven.
- The UI and target API send `actionKey`; they never send authoritative `toState`.
- SQMS is taskless and has no SLA.
- Do not add cancellation, cross-row workflow orchestration, Seller tasks, or automatic Opportunity allocation roll-up.
- Do not introduce a new HTTP API. The target API calls the adapter in process.
- Do not alter the supplied `SSQMS.Opportunity` or `SSQMS.Bid` schemas.
- Production DDL and seed DML are delivered as explicit SQL Server files for DBA review and execution.
- Production application startup must not apply migrations, create schema, or seed metadata.

## 3. Explicit supersession of stale retained decisions

`Opportunity.TotalBid`, `Opportunity.LargestBid`, and `Opportunity.LowestBid` are UI/application-owned calculated columns and are entirely outside this work.

The SQMS adapter, generic workflow engine, scheduler, SQL DML, and tests must not read them for decisions, calculate them, validate them, update them, reconcile them, or assert their values. This decision supersedes items 9 and 10 and the `BidssubmittedActions` mapping in `docs/sqms/CLEAN-SLATE-CONTEXT.md`.

## 4. Architecture

```text
SQMS UI
  -> existing target API
       -> SQMS thin adapter
            -> existing generic workflow application services
            -> SQMS data access in the shared SQL Server database

Windows Task Scheduler
  -> WorkflowEngine.Console run-sqms-scheduled
       -> SQMS scheduled runner
            -> SQMS data access in the shared SQL Server database
            -> existing generic workflow application services
            -> generic notification-only queue service

Shared SQL Server database
  -> SSQMS.Opportunity and SSQMS.Bid
  -> existing workflow metadata/runtime/audit/outbox tables
```

### 4.1 Ownership

The target API owns:

- authentication and authorization;
- normalization of actors and roles;
- target-form validation;
- request-driven SQMS persistence;
- resolution of SQMS user/team identifiers into email addresses;
- the outer request transaction and rollback;
- target API response DTOs.

The SQMS adapter owns:

- deterministic workflow and business-object identities;
- mapping Opportunity and Bid operations to generic Start, Context, and Execute services;
- SQMS-specific validation and data conversion;
- synchronization of returned workflow state to `Status` and `WorkflowState`;
- SQMS link construction and trusted template values;
- scheduled close, Bid classification, and notification candidate selection.

The generic workflow engine owns:

- metadata-derived transition authority;
- workflow runtime state and version;
- role enforcement;
- optimistic concurrency and idempotency;
- workflow audit;
- notification metadata, rendering, recipient snapshots, and durable outbox behavior.

The UI owns display and form behavior. It does not own workflow next-state selection, email addresses, routes, templates, links, scheduled calculations, or the three excluded calculated Opportunity columns.

## 5. Target SQMS schema boundary

The supplied `SSQMS.Opportunity` and `SSQMS.Bid` tables already exist in the target database. The deployment package neither creates nor changes them.

The integration uses these fields:

| Entity | Purpose | Columns |
|---|---|---|
| Opportunity | Identity and relationship | `ID`, `Account`, `AccountName`, `AccountNumber` |
| Opportunity | Recipient identifiers | `SellerRMTeam`, `RelationshipManager` |
| Opportunity | Email and scheduling data | `FundConsideredSecondarySale`, `Vehicle`, `CommitmentAmountSale`, `EffectiveDateForSale` |
| Opportunity | Workflow projection | `Status`, `WorkflowState` |
| Opportunity | Audit and filtering | `ModifiedBy`, `ModifiedDate`, `IsDeleted` |
| Bid | Identity and parent | `ID`, `OpportunityId` |
| Bid | Recipient identifiers | `BuyerRmteam`, `RelationshipManager` |
| Bid | Email and classification data | `AccountName`, `AccountNumber`, `CommitmentAmountRequested`, `BidExpressedAsFairValue` |
| Bid | Workflow projection | `Status`, `WorkflowState` |
| Bid | Audit and filtering | `ModifiedBy`, `ModifiedDate`, `IsDeleted` |

For Bid email tokens, the adapter loads `FundConsideredSecondarySale`, `Vehicle`, and `EffectiveDateForSale` from the parent Opportunity.

Rows where `IsDeleted = 1` are not workflow candidates. A null `IsDeleted` value is treated as active because the supplied schema permits null.

`SellerRMTeam`, `BuyerRmteam`, and `RelationshipManager` contain target-system user/team identifiers, not email addresses. The target API resolves them before calling a notification-producing transition.

## 6. Public in-process SQMS surface

The additive SQMS service exposes equivalent Opportunity and Bid operations:

```text
StartOpportunity(opportunityId, actor)
StartBid(bidId, actor)

GetOpportunityContext(opportunityId, actor)
GetBidContext(bidId, actor)

ExecuteOpportunityAction(opportunityId, actionKey, expectedWorkflowVersion, actor)
ExecuteBidAction(bidId, actionKey, expectedWorkflowVersion, actor)
```

The implementation may use request records rather than positional parameters, but the semantic inputs are fixed. Additive overloads or interfaces must preserve every existing DI registration and public .NET call site.

### 6.1 Deterministic identity

| Entity | Business object ID | Workflow instance ID |
|---|---|---|
| Opportunity `{id}` | `SQMS-OPP-{id}` | `WF-SQMS-OPP-{id}` |
| Bid `{id}` | `SQMS-BID-{id}` | `WF-SQMS-BID-{id}` |

The IDs are derived; no new SQMS column is required.

### 6.2 New records

For a newly created Opportunity or Bid after cutover:

1. The target API validates and saves the SQMS row so its identity value exists.
2. Within the same request transaction, the adapter starts the workflow and executes its initial submit action.
3. The workflow may transiently persist internal `Draft`, but the committed and user-visible state is `SUBMITTED`.
4. The adapter writes the authoritative returned state to both `Status` and `WorkflowState`.
5. Any failure rolls back the target row, workflow runtime, audit, and outbox effects together.

`Draft` remains a valid visible state after IPG returns an existing record.

### 6.3 Form open

Context is read-only and never lazily creates a workflow. A post-cutover record with no matching workflow returns `NotWorkflowManaged`. The UI receives metadata-derived available actions for the normalized actor.

### 6.4 Execute

- The UI supplies only the selected `actionKey` and expected workflow version.
- The target API supplies the trusted normalized actor and resolved notification recipient inputs.
- The adapter loads trusted SQMS data by entity ID.
- The generic engine determines the next state from active metadata.
- The adapter returns the authoritative workflow result and refreshed context.

## 7. State and action metadata

`Draft` is the only mixed-case SQMS state. Every other SQMS state key is the exact uppercase, space-separated value shown below. These exact values are projected into both SQMS state columns.

SQMS transitions create no workflow tasks and attach no SLA policy.

### 7.1 Opportunity matrix

| From state | Action key | Role | To state | Trigger |
|---|---|---|---|---|
| Internal start | `SubmitOpportunity` | MFO | `SUBMITTED` | New-record request |
| `Draft` | `SubmitOpportunity` | MFO | `SUBMITTED` | Manual |
| `SUBMITTED` | `ReturnOpportunityToDraft` | IPG | `Draft` | Manual |
| `SUBMITTED` | `MarkOpportunityPending` | IPG | `PENDING` | Manual |
| `PENDING` | `ReturnOpportunityToDraft` | IPG | `Draft` | Manual |
| `PENDING` | `OpenOpportunityForBidding` | IPG | `OPEN TO BIDDING` | Manual |
| `OPEN TO BIDDING` | `CloseOpportunityBidding` | System | `CLOSED TO BIDDING` | Scheduled |
| `CLOSED TO BIDDING` | `RequestSellerApproval` | IPG | `PENDING SELLER APPROVAL` | Manual, queues email |
| `PENDING SELLER APPROVAL` | `MarkOpportunityAwaitingAllocation` | IPG | `AWAITING ALLOCATION` | Manual |
| `PENDING SELLER APPROVAL` | `MarkOpportunityPartiallyAllocated` | IPG | `PARTIALLY ALLOCATED` | Manual |
| `PENDING SELLER APPROVAL` | `MarkOpportunityNotFulfilled` | IPG | `NOT FULFILLED` | Manual |
| `AWAITING ALLOCATION` | `CompleteOpportunity` | IPG | `COMPLETED` | Manual |
| `PARTIALLY ALLOCATED` | `CompleteOpportunity` | IPG | `COMPLETED` | Manual |

`NOT FULFILLED` and `COMPLETED` are terminal. Opportunity allocation is manual. Bid disposition never changes the Opportunity automatically.

### 7.2 Bid matrix

| From state | Action key | Role | To state | Trigger |
|---|---|---|---|---|
| Internal start | `SubmitBid` | MFO | `SUBMITTED` | New-record request |
| `Draft` | `SubmitBid` | MFO | `SUBMITTED` | Manual |
| `SUBMITTED` | `ReturnBidToDraft` | IPG | `Draft` | Manual |
| `SUBMITTED` | `MarkBidNotAllocated` | System | `NOT ALLOCATED` | Scheduled classification only |
| `SUBMITTED` | `MoveBidToIpgReview` | System | `IPG REVIEW` | Scheduled classification only |
| `IPG REVIEW` | `SendBidToSellerReview` | IPG | `UNDER REVIEW BY SELLER` | Manual, one selected Bid |
| `UNDER REVIEW BY SELLER` | `AcceptBid` | Seller | `ACCEPTED` | Manual, queues email |
| `UNDER REVIEW BY SELLER` | `PartiallyAcceptBid` | Seller | `PARTIALLY ACCEPTED` | Manual, queues email |
| `UNDER REVIEW BY SELLER` | `RejectBid` | Seller | `NOT ACCEPTED` | Manual, queues email |

`NOT ALLOCATED`, `ACCEPTED`, `PARTIALLY ACCEPTED`, and `NOT ACCEPTED` are terminal. There is no manual `NOT ALLOCATED` action and no batch move from IPG review to Seller review.

### 7.3 Generic terminal-state correction

The current engine recognizes terminal runtime completion by comparing the destination key with literal `Completed`. SQMS has several terminal state keys, so the engine must instead use `WorkflowStateDefinition.IsTerminal`.

This is a generic metadata-driven correction:

- entering any state with `IsTerminal = 1` sets `WorkflowInstance.Status = Completed` and `CompletedAtUtc`;
- entering a nonterminal state preserves active status;
- existing CDT `Completed` metadata remains terminal, so its behavior is unchanged;
- CDT compatibility tests are mandatory before this correction can ship.

## 8. Persistence and transaction boundary

The SQMS tables and workflow tables are hosted in the same SQL Server database. The separation is logical, not physical.

The SQMS integration data-access component reads `SSQMS.Opportunity` and `SSQMS.Bid`. It converts required `varchar` business values into validated application values before invoking the generic engine. The generic workflow infrastructure does not query or interpret SQMS business columns.

### 8.1 State projection

After each successful action:

- write the exact returned state to both `Status` and `WorkflowState`;
- set `ModifiedBy` from the normalized actor;
- set `ModifiedDate` from `TimeProvider` as UTC;
- validate that the actor value fits the target column rather than truncating it.

No SQMS projection is written when the workflow operation fails.

### 8.2 Request transaction

```text
Target API transaction
  -> save or validate SQMS row
  -> execute workflow start/action
  -> persist workflow runtime, audit, idempotency, and outbox
  -> update SQMS Status and WorkflowState
  -> commit once
```

SQMS and workflow EF contexts must enlist in the same SQL transaction. Existing engine services already join a current workflow transaction when present; any new transaction-sharing surface must be additive and preserve current callers.

### 8.3 Scheduled transactions

- Close one Opportunity and project its state in one transaction.
- Commit the Opportunity before classifying its Bids.
- Classify and project each Bid in its own transaction.
- A later Bid failure does not reopen the Opportunity or roll back successful Bid transitions.

## 9. Email routes, recipients, and templates

Six independent notification routes are seeded:

- `SqmsOpportunityOpen`
- `SqmsOpportunityReminder`
- `SqmsOpportunityPendingSellerApproval`
- `SqmsBidAccepted`
- `SqmsBidPartiallyAccepted`
- `SqmsBidNotAccepted`

Routes own notification type, subject, plain text, HTML, required tokens, and recipient-rule metadata. The UI supplies none of these values.

The DBA DML contains the complete subject/body literals and independent template records. It must not use incomplete placeholder copy. The three Bid outcome templates preserve their distinct accepted, partially accepted, and not-accepted wording from the retained legacy evidence. Environment-specific static recipient values are required SQL script inputs and are verified after seeding.

### 9.1 Open notification

- Candidate state: `OPEN TO BIDDING`.
- Due date: calendar day after the successful `OpenOpportunityForBidding` audit date.
- Group key: effective-sale quarter, effective-sale year, and intended occurrence date.
- One email is queued per group.
- To/Cc recipients are static metadata.
- Required tokens: `quarter`, `year`, `responseDate`, `dashboardUrl`.
- `responseDate` is the opening local date plus 15 calendar days.

### 9.2 Reminder

- Candidate state: still `OPEN TO BIDDING`.
- Due date: opening local date plus 14 calendar days.
- Grouping and static recipients match the open notification.
- It uses its own route and template.
- The legacy App Dev daily email is not reproduced.

### 9.3 Pending Seller approval

The `RequestSellerApproval` transition and email outbox row commit atomically.

- Dynamic recipient identifiers: `Opportunity.RelationshipManager` and `Opportunity.SellerRMTeam`.
- The target API resolves those identifiers to normalized To addresses.
- Cc recipients are static metadata.
- Required tokens: `quarter`, `year`, `vehicle`, `accountNumber`, `accountName`, `fund`, `saleAmount`, `sellerResponseDate`, `opportunityUrl`.
- `fund` comes from `FundConsideredSecondarySale`.
- `saleAmount` comes from `CommitmentAmountSale`.
- Quarter and year come from `EffectiveDateForSale`.
- `sellerResponseDate` is the transition local date plus five calendar days.

### 9.4 Bid disposition

Each terminal Seller action uses its own route and template.

- Dynamic recipient identifiers: `Bid.RelationshipManager` and `Bid.BuyerRmteam`.
- The target API resolves those identifiers to normalized To addresses.
- Cc recipients are static metadata.
- Required tokens: `quarter`, `year`, `buyerAccountNumber`, `buyerAccountName`, `fund`, `vehicle`, `requestedAmount`, `effectiveDate`, `bidUrl`.
- Parent Opportunity data supplies fund, vehicle, effective date, quarter, and year.
- `CommitmentAmountRequested` supplies the requested amount.

### 9.5 Failure behavior

- Missing required business data, unresolved required recipients, an invalid URL, or a missing template token fails the action before commit.
- A committed outbox row is an immutable rendered message and recipient snapshot.
- SMTP happens after commit through the existing dispatcher.
- SMTP retries never revert workflow or SQMS state.

## 10. Hyperlink contract

Base URLs are environment-specific host configuration, not database business data and not UI request values.

```json
{
  "Sqms": {
    "Links": {
      "OpenBiddingDashboardUrl": "https://environment-host/open-bidding",
      "SellerApprovalBidsDashboardUrl": "https://environment-host/bids",
      "BidFormUrl": "https://environment-host/bid"
    }
  }
}
```

Deployment must replace the illustrative hosts with valid environment URLs. Startup validation rejects missing or invalid configured URLs when SQMS is enabled.

| Email | Final link behavior |
|---|---|
| Open and reminder | Use `OpenBiddingDashboardUrl` unchanged; no query parameters are injected. |
| Pending Seller approval | Append URL-encoded `opportunityId={Opportunity.ID}` to `SellerApprovalBidsDashboardUrl`. |
| Bid disposition | Append URL-encoded `bidId={Bid.ID}` to `BidFormUrl`. |

Link construction must preserve existing query parameters and fragments; it must not use naive string concatenation.

## 11. Scheduled processing

Windows Task Scheduler invokes one additive console command:

```text
run-sqms-scheduled
```

The deployment may configure three triggers, but every invocation checks all due categories. Task Scheduler must use **Do not start a new instance**. Database idempotency remains the correctness guard if two processes overlap despite host configuration.

### 11.1 Business clock

- All current time comes from `TimeProvider`.
- Calendar decisions use configured `Sqms:Scheduling:TimeZoneId`.
- `Sqms:Scheduling:BiddingDays` is 15.
- `Sqms:Scheduling:SellerResponseDays` is 5.
- No holiday or business-day calendar is introduced.

### 11.2 Processing order

1. Close every due Opportunity.
2. Classify eligible Bids under closed Opportunities.
3. Queue due open/reminder groups only for Opportunities still `OPEN TO BIDDING`.

This ordering prevents a catch-up run from sending an obsolete reminder for an Opportunity that should already be closed.

### 11.3 Due dates

| Operation | Due local date |
|---|---|
| Open notification | Opening audit date + 1 day |
| Reminder | Opening audit date + 14 days |
| Response deadline | Opening audit date + 15 days |
| Automatic close | Opening audit date + 16 days |

The successful `OpenOpportunityForBidding` workflow audit timestamp is the schedule anchor. No new date column is added to `SSQMS.Opportunity`.

### 11.4 Bid classification

For each closed Opportunity with currently `SUBMITTED` Bids:

1. Parse `Opportunity.CommitmentAmountSale` and each applicable `Bid.CommitmentAmountRequested` as invariant monetary values after trimming accepted currency formatting.
2. Normalize `Bid.BidExpressedAsFairValue` case-insensitively into `FAIR VALUE`, `PREMIUM`, or `DISCOUNT`.
3. Sum requested amounts for `FAIR VALUE` and `PREMIUM` Bids.
4. If that sum is strictly greater than the Opportunity sale amount, execute `MarkBidNotAllocated` for each `DISCOUNT` Bid.
5. Execute `MoveBidToIpgReview` for every remaining currently `SUBMITTED` Bid.
6. If the sum is not greater, all currently `SUBMITTED` Bids move to `IPG REVIEW`.

Invalid or missing required values fail that candidate. They are never converted silently to zero. Successful records remain committed; failures are retried on a later run while they remain eligible.

The command continues after individual failures, writes a per-record summary, and exits nonzero if any candidate fails.

## 12. Generic notification-only queue

Open and reminder emails are grouped messages, not workflow transitions. They must not create fake transitions, fake tasks, fake workflow instances, or send SMTP directly.

Add a generic `QueueWorkflowNotification` application service with trusted inputs equivalent to:

```text
workflowKey
workflowDefinitionVersion
notificationRouteKey
notificationOccurrenceKey
businessObjectType
businessObjectId
normalized recipient inputs
template values
correlationId
```

The service:

- loads active versioned route and recipient metadata;
- resolves the final recipient snapshot;
- validates and renders subject, text, and HTML;
- inserts one pending outbox row;
- treats a duplicate occurrence key as an idempotent already-queued result.

Transition-created and SLA-created notifications continue using their existing paths unchanged.

### 12.1 Outbox schema relaxation

The existing `dbo.NotificationOutbox.WorkflowInstanceId` is required, but one grouped message represents several Opportunities. The approved production-safe design is:

- change `WorkflowInstanceId` from `nvarchar(150) NOT NULL` to `nvarchar(150) NULL`;
- preserve its foreign key for non-null values;
- add `NotificationOccurrenceKey nvarchar(300) NULL`;
- add a filtered unique index on non-null `NotificationOccurrenceKey`;
- add a check constraint requiring `WorkflowInstanceId IS NOT NULL OR NotificationOccurrenceKey IS NOT NULL`.

Existing CDT outbox rows remain unchanged and continue using non-null workflow instance IDs. Notification-only rows use null `WorkflowInstanceId`, a deterministic occurrence key, `BusinessObjectType = SqmsOpportunityGroup`, and a group identifier in `BusinessObjectId`.

Occurrence key formats are stable:

```text
SQMS:OpportunityOpen:{year}:Q{quarter}:{occurrenceDate:yyyy-MM-dd}
SQMS:OpportunityReminder:{year}:Q{quarter}:{occurrenceDate:yyyy-MM-dd}
```

## 13. Error and retry behavior

| Condition | Result |
|---|---|
| Missing workflow for post-cutover row | `NotWorkflowManaged` |
| Missing/deleted Opportunity or Bid | Not found; no workflow side effect |
| Invalid action/state/role | Preserve generic structured workflow result |
| Stale expected workflow version | `ConcurrencyConflict` |
| Required SQMS value missing or invalid | Validation failure; transaction rolls back |
| Required recipient identifier unresolved | Validation failure; transaction rolls back |
| Required URL/template token invalid | Validation failure; transaction rolls back |
| Duplicate notification occurrence | Idempotent already-queued result |
| SMTP failure | Existing outbox retry behavior; workflow state remains committed |
| Scheduled candidate failure | Record failure, continue others, exit nonzero |

No failure audit model is added. Successful transitions retain the existing workflow audit behavior.

## 14. DBA-managed database package

Production deployment artifacts are ordered SQL Server files:

```text
sql/sqms/
  001_sqms_notification_outbox_ddl.sql
  002_sqms_workflow_metadata_dml.sql
  003_verify_sqms_deployment.sql
```

### 14.1 DDL file

`001` contains every production DDL statement for the nullable workflow-instance change, occurrence key, index, and check constraint.

- Use `SET XACT_ABORT ON` and an explicit transaction.
- Run precondition checks before changing dependencies.
- Preserve data and existing FK/index semantics.
- Be safe to rerun: an existing object must be validated for the expected shape, not blindly skipped.
- Fail clearly on an incompatible existing definition.
- Do not create or modify `SSQMS` tables.

An EF migration may mirror the DDL only to preserve local/test `init-db` behavior and model-snapshot integrity. The DBA SQL file is the production deployment contract, and semantic-parity tests must compare the EF model with the DBA script. Production never calls `Database.Migrate()` or `EnsureCreated()`.

### 14.2 DML file

`002` contains all SQMS workflow definition, version, state, action, role, transition, route, recipient-rule, and template DML.

- Seed data is not inserted through `WorkflowSeedService` or EF model seeding.
- Use explicit SQL transactions and rerunnable insert/update logic.
- Never delete, rename, or reinterpret an existing stable key.
- Fail if an existing key has incompatible ownership or meaning.
- Accept required environment-specific static email addresses as explicit SQLCMD/deployment inputs.
- Store complete subject, plain-text, and HTML template literals.

### 14.3 Verification file

`003` is read-only and independently verifies:

- expected column nullability, length, index, FK, and check constraint;
- exact active SQMS state/action/role/transition matrices;
- terminal-state flags;
- six independent routes and templates;
- required template tokens and recipient rules;
- absence of SQMS tasks and SLA policies;
- absence of changes to the supplied `SSQMS` schema.

The production order is DBA DDL, DBA DML, DBA verification, application deployment, application smoke test, then scheduler enablement.

## 15. CDT production-compatibility gate

Because CDT is already running in production and shares `dbo.NotificationOutbox`, compatibility is a release blocker.

### 15.1 Workspace upgrade test

Create a representative local SQL database using the current baseline application and seed. Before applying the SQMS DDL, populate it with CDT evidence covering:

- active and completed workflow instances;
- pending and completed tasks;
- active/completed SLA schedules;
- transition, due-today, overdue, sent, pending, processing, and cancelled outbox records where supported;
- recipient snapshots, HTML/text bodies, correlations, retry counts, and audit/idempotency rows.

Capture row counts and immutable-column hashes. Apply the exact DBA `001` script twice. Prove:

- every pre-existing row and value is unchanged;
- old-style non-null `WorkflowInstanceId` inserts still succeed;
- null workflow instance with no occurrence key is rejected;
- null workflow instance with a valid occurrence key succeeds;
- duplicate occurrence keys are rejected/idempotently handled;
- all existing FKs, indexes, and status constraints still operate.

### 15.2 CDT behavioral regression

After the DDL and new application build:

- run the complete existing warnings-as-errors build;
- run the full non-SMTP engine test suite;
- run the complete Workflow API test suite;
- run schema and migration tests;
- run existing `verify-seed`;
- execute the checked-in CDT sample JSON path;
- execute a full CDT Start -> Context -> Action sequence through `Completed`;
- create and close CDT tasks;
- run CDT SLA due/overdue processing;
- process CDT notification outbox rows through the configured test sender;
- prove recipient snapshots, templates, retries, and state/audit results remain unchanged.

Then run SQMS-focused tests. SQMS scheduling remains disabled until both compatibility groups pass.

### 15.3 Operational deployment protection

- DBA checks outbox table size and active workload before choosing the change window.
- The DDL may take a schema-modification lock while altering nullability and rebuilding dependencies.
- No notification-only rows are created during the DDL-to-application deployment interval.
- Rollback disables SQMS scheduling first and cancels/removes only unsent SQMS notification-only rows under an approved DBA script; existing CDT rows are never targeted.
- The relaxed nullable column may remain after code rollback because CDT continues writing non-null values.

## 16. SQMS verification

Automated and SQL-backed coverage must prove:

- deterministic Opportunity and Bid identities;
- exact state/action/role matrices and terminal flags;
- metadata-derived available actions;
- no client `toState` authority;
- new-record start plus initial submit rollback;
- context does not lazily start workflows;
- `Status` and `WorkflowState` synchronization;
- request and scheduled transaction boundaries;
- identifier-to-address resolution and immutable recipient snapshots;
- all six routes, token requirements, and distinct Bid wording;
- the three approved hyperlink contracts;
- `TimeProvider`-controlled calendar timing in the configured timezone;
- close-first catch-up behavior;
- strict-greater-than Bid classification and invalid-value failures;
- per-Bid continuation and retry;
- group occurrence idempotency and overlap safety;
- the scheduler's nonzero failure exit;
- zero reads/writes of `TotalBid`, `LargestBid`, and `LowestBid`;
- DBA DDL/DML rerun safety and independent verification;
- all CDT compatibility requirements in section 15.

SMTP integration remains opt-in and uses the existing fake/local SMTP test boundary. No test sends real email.

## 17. Scope exclusions

- UI implementation and dashboard filtering behavior.
- Creation or modification of `SSQMS` tables.
- `TotalBid`, `LargestBid`, and `LowestBid` calculation or synchronization.
- SQMS workflow tasks or SLA.
- Seller task assignment.
- Cancellation or withdrawal.
- Automatic parent allocation/not-fulfilled roll-up.
- Batch movement of Bids to Seller review.
- A manual `NOT ALLOCATED` action.
- Bid submission gates based on parent state or other Bids.
- Row-locking/UI-locking features.
- App Dev daily reconciliation email.
- Business-day/holiday calendars.
- Direct SMTP from the SQMS scheduler.
- A new SQMS HTTP API, message broker, microservice, or plugin framework.

## 18. Delivery sequence

1. User reviews and approves this written specification.
2. Produce a file-by-file implementation plan using the writing-plans workflow.
3. User approves implementation.
4. Implement with test-driven development on `sqms_clean_slate`.
5. Run the DBA-script tests, CDT compatibility gate, SQMS tests, and all `PROJECT.md` quality gates.
6. Provide the required completion summary:

```text
Changed:
Why:
Tests run:
Seed verification run:
Sample JSON executed:
Files updated:
Open issues:
```

No implementation or database change is authorized by this document alone.
