# SQMS Clean-Slate Context

**Date:** 2026-08-17

**Status:** Previous SQMS implementation rejected and reverted

## Purpose

Preserve the supplied SQMS evidence and the decisions learned from the rejected implementation without retaining its code, SQL, samples, tests, or design as an active baseline.

Future SQMS work starts from the generic workflow engine at pre-SQMS commit `1362541e8c28b7a6b5bdf1bbe203c532c91c7735`. The target API already embeds the generic workflow engine and is already integrated with its UI. SQMS work is therefore an incremental target-application adapter, not a second API, a replacement engine, or a fresh `init-db` deployment.

## Source Authority

The raw legacy files under `C:/projects/cdt-workflow-archive/20261014-cleanup/snapshot/SQMS/` are preserved as evidence. Before proposing an SQMS behavior, trace it to one of these supplied functions or to an explicit user decision:

- `NewOpportunitySetup`
- `OpportunitystatusDailyActivities`
- `opportunitystatuscloseToBid`
- `OpportunityStatusChangePendingSellerApproval`
- `BidsstatusDailyActivities`
- `BidssellerDisposition`
- `BidssubmittedActions`

Written user clarifications outrank inferred behavior. Do not treat a convenient modern orchestration as a legacy requirement.

## Required Thin-Layer Boundary

The SQMS thin layer maps existing target-API/UI operations and legacy scheduled functions to generic workflow-engine calls. It must not invent SQMS business orchestration.

The generic engine remains responsible for metadata-derived transition validation, workflow runtime state, audit, idempotency/concurrency, notification metadata, and the durable outbox.

The target API/UI remains responsible for authenticated actors and roles, SQMS business data, manual IPG/MFO decisions, form validation, and calling the applicable SQMS action.

The thin layer may coordinate only behavior that is explicitly automated by the supplied legacy process or explicitly approved by the user.

## Confirmed Decisions

1. Keep the generic workflow engine reusable and backward compatible for existing applications.
2. SQMS is taskless and has no SQMS SLA unless a later explicit requirement changes that boundary.
3. UI row locking and explicit Opportunity/Bid row locks are out of scope.
4. The target API already hosts the engine. Do not require the browser to call the reference Workflow API.
5. The target API/UI sends an entity ID and selected `actionKey`; metadata determines the destination state.
6. The exact engine state key is written to both SQMS `WorkflowState` and `Status`. Do not translate through an uppercase display label.
7. Bid-submission blocking and cross-entity submission gates are out of scope for the thin layer. UI/target application validation owns those rules.
8. Preserve the legacy automatic close-to-bidding Bid classification: when FairValue plus Premium requested amount is strictly greater than the Opportunity sale amount, Discount Bids become NotAllocated and the remaining applicable Bids move to IPG review.
9. `TotalBid` means Bid count, not a monetary total.
10. After every successful Bid submission, recalculate the parent Opportunity Bid count, largest requested amount, and lowest requested amount. The exact included-state rule must be confirmed before implementation; the legacy query did not filter by Bid status.
11. Closing the Opportunity and classifying its Bids may use separate transactions so the parent stays closed if child classification fails and a later scheduled run can retry.
12. IPG manually chooses and moves each Bid to seller review. The system must not batch-move all IPG-review Bids.
13. Moving an Opportunity to pending seller approval must not change related Bid workflow states.
14. Seller Bid decisions change only the selected Bid and queue its corresponding email.
15. Opportunity allocation/not-fulfilled roll-up remains a manual IPG action. The system must not infer or automatically change the parent after the final Bid decision.
16. Multiple Opportunities in the same open-bidding sale cycle use one aggregate email and the same dashboard hyperlink.
17. Production database upgrades must be additive, idempotent SQL files applied to the existing selected database. Do not run `init-db`, recreate SQMS tables, or insert mock fixtures.

## Rejected Implementation Assumptions

The reverted implementation incorrectly or prematurely included these behaviors:

- deriving UI `Status` from an uppercase workflow display label;
- blocking Bid submission until the open-bidding email was sent and other parent fields passed thin-layer validation;
- automatically moving every IPG-review Bid to seller review when requesting seller approval;
- requiring the Opportunity action to discover or select child Bids;
- automatically rolling up the Opportunity after the last Bid disposition;
- treating the thin layer as the owner of new SQMS orchestration rather than as a legacy-function adapter;
- updating Bid summaries only during closure instead of mapping the legacy post-submission calculation trigger.

Do not reintroduce these assumptions without explicit user approval.

## Legacy Function Mapping Baseline

| Legacy function or event | Clean-slate integration intent |
|---|---|
| UI creates Opportunity | Start the Opportunity workflow after the application row has its ID. |
| UI manually changes Opportunity status | Execute the matching metadata action and project the exact state key. |
| UI creates Bid | Start the Bid workflow after the application row has its ID. |
| UI manually changes Bid status | Execute the matching metadata action and project the exact state key. |
| `NewOpportunitySetup` | SharePoint view/web-part behavior is UI-specific and outside the workflow thin layer. |
| Original open-bidding notification | Map to scheduled aggregate outbox creation. |
| Open-bidding reminder | Map to scheduled aggregate reminder outbox creation. |
| `opportunitystatuscloseToBid` | Map automatic close and legacy Bid classification. |
| `OpportunityStatusChangePendingSellerApproval` | Queue the seller email after the manual Opportunity action; do not change Bid states. |
| Manual IPG Bid review selection | Execute `SendBidToSellerReview` only for the selected Bid. |
| `BidssellerDisposition` | Queue the applicable disposition email after the selected Bid decision. |
| `BidssubmittedActions` | Recalculate parent Bid count/largest/lowest after successful Bid submission. |
| `BidsstatusDailyActivities` | Consider only as reconciliation behavior after its production need is confirmed. |
| Legacy person-field copying | Do not reproduce SharePoint denormalization when the SQL model can read related recipient data directly. |

## Database State

On 2026-08-17, after separate user approval, the local `workflowengine` database was inventoried and cleaned in one rollback-on-error transaction. The cleanup removed the `SqmsOpportunity` and `SqmsBid` definitions and their versions, 19 state rows, 18 action rows, 40 transition rows, 6 role rows, 4 workflow instances, 8 audit rows, 8 idempotency rows, 2 mock-section rows, and the mock `SSQMS.Bid` and `SSQMS.Opportunity` tables and schema. Post-cleanup SQL inspection found zero SQMS definitions, runtime rows, mock-section rows, or `SSQMS` objects. Generic `verify-seed` passed; the Portfolio Trade definition remains. No `init-db` was run.

## Target-API Bundle Baseline

On 2026-08-18, `restore-repo.ps1` was restored into an isolated temporary directory and verified its embedded ZIP checksum. The bundle contains 417 files from clean commit `e2ccd86aaa4b8b0ab83e41598f265cad93cf78ad` on `console-code-first-0518`, generated on 2026-08-10 with no working-tree changes.

The restored bundle was compared path-by-path and by SHA-256 with this clean branch. All 411 comparable bundled files are unchanged. There are zero differences under `src/`, `workflow-api/`, `tests/`, or `samples/`. The only six changed bundled paths are repository governance and handoff files: `.gitignore`, `AGENTS.MD`, `.planning/.continue-here.md`, `.planning/HANDOFF.json`, `.planning/STATE.md`, and `CODEX_HANDOFF.md`.

The stale `docs/sqms-approved-workflow-flowchart.html` was removed because it described the rejected custom coordinator, notification-batch workflows, reconciliation commands, and cross-row orchestration. The retained post-bundle files are knowledge-only: the original `restore-repo.ps1`, raw `C:/projects/cdt-workflow-archive/20261014-cleanup/snapshot/SQMS/` legacy evidence, `C:/projects/cdt-workflow-archive/20261014-cleanup/snapshot/tareget-api-integration/` reference snapshots, and this corrected clean-slate context.

## Clean-Slate Next Step

Before writing code or SQL:

1. Build a source-backed table for each legacy function: trigger, inputs, row changes, email, timing, and manual versus automatic ownership.
2. Compare that table with the current generic engine integration surfaces already present in the target API.
3. Ask only about genuine gaps or contradictions.
4. Obtain approval for the complete mapping.
5. Produce a file-by-file incremental code package and ordered idempotent SQL upgrade scripts.
