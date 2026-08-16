# SQMS Console Mock Integration Design

**Date:** 2026-08-16
**Status:** Implemented and verified 2026-08-16
**Authoritative engine requirements:** Repository-root `PROJECT.md`

## Goal

Create faithful mock SQMS Opportunity and Bid application tables inside the existing `workflowengine` database, then prove through the existing Console that real application record identities can start, query, and execute the already-seeded SQMS workflows.

The proof must not add UI, duplicate transition rules, or make the generic workflow engine own or update SQMS application records.

## Approved boundaries

- The actual UI is outside scope.
- The generic workflow engine continues to support multiple applications.
- Portfolio Trade and current SQMS behavior are protected regression baselines.
- No generic-engine C# behavior change is expected for this proof.
- The mock application tables live under schema `SSQMS` in database `workflowengine`; no separate `cp_sharepoint` database is required.
- The mock tables reproduce the user-supplied production column names, SQL types, identity seeds, primary keys, and Opportunity-to-Bid foreign key.
- Workflow transitions remain metadata-driven. Console fixtures send `actionKey`; they never send authoritative `toState`.
- The engine does not read or write `SSQMS.Opportunity` or `SSQMS.Bid`. The console proof uses their IDs as application-owned business object identities.
- The proof may use console `--reset` only for deterministic local test data. This remains demo/test behavior and is not a production restart mechanism.

## Mock application schema

Create `SSQMS.Opportunity` before `SSQMS.Bid`.

### `SSQMS.Opportunity`

| Column | SQL type | Nullability |
|---|---|---|
| `ID` | `int IDENTITY(10000,1)` | NOT NULL, primary key |
| `SellerRMTeam` | `varchar(max)` | NOT NULL |
| `Account` | `varchar(max)` | NOT NULL |
| `AccountName` | `varchar(1000)` | NOT NULL |
| `AccountNumber` | `varchar(1000)` | NOT NULL |
| `RelationshipManager` | `varchar(1000)` | NULL |
| `FundConsideredSecondarySale` | `varchar(1000)` | NULL |
| `Vehicle` | `varchar(1000)` | NULL |
| `ClientCommitmentToVehicle` | `varchar(100)` | NULL |
| `CommitmentAmountSale` | `varchar(100)` | NULL |
| `ClientRelationshipLeavingBBH` | `varchar(100)` | NULL |
| `RationaleForSecondarySale` | `varchar(max)` | NULL |
| `EffectiveDateForSale` | `datetime` | NULL |
| `Status` | `varchar(200)` | NULL |
| `FundManagementFee` | `varchar(100)` | NULL |
| `MinimumBid` | `varchar(100)` | NULL |
| `RecentDateOfValuation` | `datetime` | NULL |
| `ValuationAsRecentDate` | `varchar(100)` | NULL |
| `ContributionSinceValuation` | `varchar(1000)` | NULL |
| `DistributionSinceValuation` | `varchar(1000)` | NULL |
| `EstimatedFairValue` | `varchar(1000)` | NULL |
| `RemainingCapitalCommitment` | `varchar(1000)` | NULL |
| `IsValuationForPrivateFundVehicle` | `bit` | NULL |
| `TotalBid` | `int` | NULL |
| `LargestBid` | `varchar(100)` | NULL |
| `LowestBid` | `varchar(100)` | NULL |
| `WorkflowState` | `nvarchar(100)` | NULL |
| `CreatedBy` | `nvarchar(50)` | NULL |
| `CreatedDate` | `datetime` | NULL |
| `ModifiedBy` | `nvarchar(50)` | NULL |
| `ModifiedDate` | `datetime` | NULL |
| `IsDeleted` | `bit` | NULL |

Use clustered primary key `PK_SSQMS_Opportunity` on `ID`, with fill factor 100.

### `SSQMS.Bid`

| Column | SQL type | Nullability |
|---|---|---|
| `ID` | `int IDENTITY(10000,1)` | NOT NULL, primary key |
| `OpportunityId` | `int` | NOT NULL, foreign key |
| `BuyerRmteam` | `varchar(max)` | NOT NULL |
| `BidForFullCommitment` | `varchar(10)` | NOT NULL |
| `CommitmentAmountRequested` | `varchar(100)` | NULL |
| `AccountName` | `varchar(1000)` | NOT NULL |
| `AccountNumber` | `varchar(1000)` | NOT NULL |
| `RelationshipManager` | `varchar(1000)` | NULL |
| `BBHAffiliateAccount` | `varchar(1000)` | NULL |
| `BidFairValue` | `varchar(1000)` | NULL |
| `BidExpressedAsFairValue` | `varchar(100)` | NULL |
| `Status` | `varchar(200)` | NULL |
| `WorkflowState` | `nvarchar(100)` | NULL |
| `CreatedBy` | `nvarchar(50)` | NULL |
| `CreatedDate` | `datetime` | NULL |
| `ModifiedBy` | `nvarchar(50)` | NULL |
| `ModifiedDate` | `datetime` | NULL |
| `IsDeleted` | `bit` | NULL |

Use clustered primary key `PK_SSQMS_Bid` on `ID`, with fill factor 100. Add checked foreign key `FK_SSQMS_Opportunity_Bid` from `Bid.OpportunityId` to `Opportunity.ID`.

## Deployment and safety behavior

The setup script must be transactional and rerunnable:

- Create schema `SSQMS` when absent.
- Create each table only when absent.
- If either table already exists, verify its required columns and constraints instead of dropping, renaming, or rebuilding it.
- Never delete or overwrite an existing application row.
- Insert deterministic fixture rows only when their IDs are absent.
- Use `TimeProvider` only in .NET code; SQL fixture timestamps may use fixed literal values for deterministic evidence.
- Fail closed if an existing table conflicts with the approved mock shape.

## Deterministic fixture data

Create one Opportunity and one related Bid with ID `10000` when those IDs do not already exist.

### Opportunity fixture

- `ID`: `10000`
- `SellerRMTeam`: `seller-rm-team@example.test`
- `Account`: `SQMS-CONSOLE-ACCOUNT`
- `AccountName`: `SQMS Console Mock Opportunity`
- `AccountNumber`: `SQMS-OPP-10000`
- `Status`: `DRAFT`
- `WorkflowState`: `Draft`
- `CreatedBy`: `sqms.mfo`
- `CreatedDate`: `2030-01-15T14:00:00`
- `IsDeleted`: `0`

Optional financial and descriptive fields may be populated with deterministic non-sensitive test values.

### Bid fixture

- `ID`: `10000`
- `OpportunityId`: `10000`
- `BuyerRmteam`: `buyer-rm-team@example.test`
- `BidForFullCommitment`: `Yes`
- `AccountName`: `SQMS Console Mock Bid`
- `AccountNumber`: `SQMS-BID-10000`
- `Status`: `DRAFT`
- `WorkflowState`: `Draft`
- `CreatedBy`: `sqms.mfo`
- `CreatedDate`: `2030-01-15T14:05:00`
- `IsDeleted`: `0`

## Workflow identity mapping

| Application row | Workflow key | Business object ID | Generated workflow instance ID |
|---|---|---|---|
| `SSQMS.Opportunity.ID = 10000` | `SqmsOpportunity` | `SQMS-OPP-10000` | `WF-SQMS-OPP-10000` |
| `SSQMS.Bid.ID = 10000` | `SqmsBid` | `SQMS-BID-10000` | `WF-SQMS-BID-10000` |

The prefixes prevent an Opportunity and Bid sharing numeric ID `10000` from colliding in the generic engine.

## Console proof

### Opportunity

1. Start `SqmsOpportunity` for `SQMS-OPP-10000` as `sqms.mfo`.
2. Get context for `WF-SQMS-OPP-10000` with role `MFO`.
3. Verify state `Draft` and available action `SubmitOpportunity`.
4. Execute `SubmitOpportunity` using an SQMS console envelope.
5. Get context as `sqms.ipg` with role `IPG`.
6. Verify state `Submitted` and actions `ReturnOpportunityToDraft` and `MarkOpportunityPending`.

### Bid

1. Start `SqmsBid` for `SQMS-BID-10000` as `sqms.mfo`.
2. Get context for `WF-SQMS-BID-10000` with role `MFO`.
3. Verify state `Draft` and available action `SubmitBid`.
4. Execute `SubmitBid` using an SQMS console envelope.
5. Get context as `sqms.ipg` with role `IPG`.
6. Verify state `Submitted` and actions `MarkBidNotAllocated` and `MoveBidToIpgReview`.

## Verification

After both paths:

- Each workflow instance is version 2 in state `Submitted`.
- Each instance has one ordered audit row for its submit action.
- Each instance has zero tasks, zero active SLA schedules, and zero notification-outbox rows.
- The mock Opportunity and Bid rows still have their original `Status`, `WorkflowState`, and modification fields; the workflow engine did not update them.
- The exact SQMS metadata verifier still reports 31 Opportunity and 9 Bid transitions.
- Existing Portfolio Trade seed verification and regression tests continue to pass.

## Files to add

- `sql/sqms/testing/004_create_sqms_mock_application_tables.sql`
- `sql/sqms/testing/005_verify_sqms_console_mock.sql`
- `samples/sqms/01-submit-opportunity.json`
- `samples/sqms/02-submit-bid.json`
- Automated SQL-backed console coverage in `tests/WorkflowEngine.Tests/`

## Explicitly excluded

- UI code
- Production `cp_sharepoint` access
- Application-table state synchronization
- New workflow API or Console commands
- Generic-engine behavior changes
- EF migrations for application-owned tables
- Tasks, SLA, notifications, scheduling, aggregation, or terminal-state changes
