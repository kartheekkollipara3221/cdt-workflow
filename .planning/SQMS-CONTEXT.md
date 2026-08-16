# SQMS Integration Context

**Updated:** 2026-08-16
**Status:** Iterations 1 through 3 complete; select the next single SQMS capability
**Source of truth:** Repository-root `PROJECT.md` remains authoritative for the generic engine.

## Agreed baseline

- The workflow engine is generic shared infrastructure intended to support multiple applications.
- Both the existing Portfolio Trade workflow and the current SQMS Opportunity/Bid workflows are protected regression baselines.
- Any change to generic-engine code must remain application-neutral and backward compatible with both baselines.
- SQMS-specific behavior belongs in workflow metadata or a thin SQMS integration boundary, not in hardcoded engine branches.
- The engine remains the transition authority. Callers send `actionKey`; they never calculate or send authoritative `toState`.
- UI work is outside this project's SQMS scope.
- Console scenarios/tests will stand in for the SQMS application boundary while Start, Get Context, and Execute behavior is designed and proven.

## Completed SQMS work

### Iteration 1: transition metadata

- Added and applied `SqmsOpportunity` version 1 metadata:
  - 11 states
  - 11 actions
  - 3 roles (`MFO`, `IPG`, `System`)
  - 31 active transitions
- Added and applied `SqmsBid` version 1 metadata:
  - 8 states
  - 7 actions
  - 3 roles (`MFO`, `IPG`, `System`)
  - 9 active transitions
- Added transactional, rerunnable SQL seed, exact verifier, and seed-twice rollback validation.
- Kept task, SLA, notification, scheduling, aggregation, and application-integration behavior outside Iteration 1.

Relevant files:

- `sql/sqms/001_seed_sqms_transition_metadata.sql`
- `sql/sqms/002_verify_sqms_transition_metadata.sql`
- `sql/sqms/003_validate_sqms_seed_in_rollback.sql`
- `docs/superpowers/specs/2026-08-12-sqms-transition-metadata-design.md`

### Iteration 2: existing-engine runtime proof

- Made no generic-engine, public-contract, EF-migration, or SQMS application code changes.
- Used the existing public Workflow Start, Context, and Action surfaces.
- Proved Opportunity `Draft -> Submitted -> Pending -> OpenToBidding`, ending at version 4.
- Proved Bid `Draft -> Submitted -> IpgReview -> UnderReviewBySeller`, ending at version 4.
- Verified role-filtered `availableActions`, three audit rows per instance, and zero tasks, SLA schedules, or notification-outbox rows.
- Used namespaced identifiers such as `SQMS-OPP-*` and `SQMS-BID-*` to avoid ID collisions.

### Iteration 3: exact-table console integration proof

- Added rerunnable `SSQMS.Opportunity` and `SSQMS.Bid` mock-table SQL using the exact supplied column types, nullability, identity seeds, primary keys, and foreign key.
- Inserted deterministic related application fixtures at ID `10000` without updating pre-existing rows.
- Added Console envelopes for `SubmitOpportunity` and `SubmitBid` using namespaced business object and workflow instance IDs.
- Proved Start, Get Context, and Execute for both workflows in an isolated SQL-backed automated test and against `DOLLY/workflowengine`.
- Both workflows reached `Submitted`, version 2, with one audit each and no tasks, SLA schedules, or notification-outbox rows.
- Proved the generic engine left the application-owned `Status`, `WorkflowState`, `ModifiedBy`, and `ModifiedDate` fields unchanged.
- Made no generic engine, public HTTP contract, EF migration, or UI change.

Relevant files:

- `sql/sqms/testing/004_create_sqms_mock_application_tables.sql`
- `sql/sqms/testing/005_verify_sqms_console_mock.sql`
- `samples/sqms/01-submit-opportunity.json`
- `samples/sqms/02-submit-bid.json`
- `tests/WorkflowEngine.Tests/SqmsConsoleMockIntegrationTests.cs`

## SQMS application tables supplied by the user

These tables remain owned by the SQMS application database. They are not workflow-engine tables.

### `[cp_sharepoint].[SSQMS].[Bid]`

- `ID`
- `OpportunityId`
- `BuyerRmteam`
- `BidForFullCommitment`
- `CommitmentAmountRequested`
- `AccountName`
- `AccountNumber`
- `RelationshipManager`
- `BBHAffiliateAccount`
- `BidFairValue`
- `BidExpressedAsFairValue`
- `Status`
- `WorkflowState`
- `CreatedBy`
- `CreatedDate`
- `ModifiedBy`
- `ModifiedDate`
- `IsDeleted`

### `[cp_sharepoint].[SSQMS].[Opportunity]`

- `ID`
- `SellerRMTeam`
- `Account`
- `AccountName`
- `AccountNumber`
- `RelationshipManager`
- `FundConsideredSecondarySale`
- `Vehicle`
- `ClientCommitmentToVehicle`
- `CommitmentAmountSale`
- `ClientRelationshipLeavingBBH`
- `RationaleForSecondarySale`
- `EffectiveDateForSale`
- `Status`
- `FundManagementFee`
- `MinimumBid`
- `RecentDateOfValuation`
- `ValuationAsRecentDate`
- `ContributionSinceValuation`
- `DistributionSinceValuation`
- `EstimatedFairValue`
- `RemainingCapitalCommitment`
- `IsValuationForPrivateFundVehicle`
- `TotalBid`
- `LargestBid`
- `LowestBid`
- `WorkflowState`
- `CreatedBy`
- `CreatedDate`
- `ModifiedBy`
- `ModifiedDate`
- `IsDeleted`

The user supplied the production table-creation DDL on 2026-08-16. Exact column types, nullability, identity seeds, primary keys, and the `Bid.OpportunityId -> Opportunity.ID` foreign key are captured in `docs/superpowers/specs/2026-08-16-sqms-console-mock-integration-design.md`. Live production records remain outside this test.

## Current boundary and next decision

- The test convention derives `SQMS-OPP-{ID}` / `WF-SQMS-OPP-{ID}` and `SQMS-BID-{ID}` / `WF-SQMS-BID-{ID}`; the production SQMS application still needs an approved persistence or deterministic-derivation convention for `workflowInstanceId`.
- `SSQMS.Status` and `SSQMS.WorkflowState` remain application-owned and were deliberately not synchronized by the engine in this iteration.
- Before more implementation, select one narrowly scoped capability. Candidates include the thin application persistence boundary or one approved task/notification capability; do not bundle them.

## Explicitly deferred

- UI implementation
- Task ownership
- SLA policies
- Notification metadata and delivery
- Automatic scheduled System actions
- Opportunity aggregation and Bid classification
- Generic non-`Completed` terminal-state synchronization
- Any change that would alter existing Portfolio Trade or current SQMS behavior
