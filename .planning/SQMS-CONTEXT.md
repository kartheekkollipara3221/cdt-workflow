# SQMS Integration Context

**Updated:** 2026-08-16
**Status:** Iterations 1 and 2 complete; console integration mock is next
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

## Next iteration objective: console integration mock

Mock the SQMS application boundary through console scenarios/tests; do not build UI.

The intended proof covers:

1. **Start**
   - Select or mock an Opportunity or Bid record identity.
   - Use workflow key `SqmsOpportunity` or `SqmsBid`.
   - Derive a collision-safe business object identity such as `SQMS-OPP-{ID}` or `SQMS-BID-{ID}`.
   - Start the workflow through the existing generic service/console boundary.

2. **Get Context**
   - Retrieve workflow context for the resulting workflow instance.
   - Display current workflow state and role-filtered `availableActions`.
   - Do not implement UI rendering.

3. **Execute**
   - Submit only the selected `actionKey` plus the existing trusted actor inputs required by the console/service boundary.
   - Let generic transition metadata determine the next state.
   - Return and display the action result and refreshed workflow context.

## Decisions still required during design

- Use persistent mock `SSQMS` tables in `workflowengine`; do not create or write to `cp_sharepoint`.
- Define how the SQMS application will persist or derive `workflowInstanceId`; neither supplied table currently lists a workflow-instance ID column.
- Define the exact ownership and synchronization semantics of application columns `Status` and `WorkflowState`.
- Use the exact user-supplied SQL types, nullability, identity seeds, primary keys, and foreign key captured in the approved console-mock design.
- Decide whether the first console proof covers Opportunity only, Bid only, or one representative path for each.

## Explicitly deferred

- UI implementation
- Task ownership
- SLA policies
- Notification metadata and delivery
- Automatic scheduled System actions
- Opportunity aggregation and Bid classification
- Generic non-`Completed` terminal-state synchronization
- Any change that would alter existing Portfolio Trade or current SQMS behavior
