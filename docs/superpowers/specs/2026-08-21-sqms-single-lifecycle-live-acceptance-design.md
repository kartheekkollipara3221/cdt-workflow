# SQMS Single-Lifecycle Live Acceptance Design

## Goal

Execute the compiled SQMS integration through one complete Opportunity lifecycle with three related Bids, deliver every applicable notification to smtp4dev, and retain SQL and mailbox evidence for manual inspection.

## Approved scenario

The run creates one Opportunity and three Bids in a new dedicated database. The Opportunity follows:

`Draft -> Open to Bidding -> Closed to Bidding -> Pending Seller Approval -> Awaiting Allocation -> Completed`

The Bids follow their complete submission and close/classification paths, then finish as:

1. `Accepted`
2. `Partially Accepted`
3. `Not Accepted`

The Opportunity and all three Bids must have matching target `Status`, target `WorkflowState`, and workflow `CurrentStateKey` after every directed action.

## Expected email evidence

The run produces exactly six messages:

1. One Opportunity-open notification.
2. One Opportunity reminder.
3. One Pending Seller Approval notification.
4. One accepted-Bid notification.
5. One partially-accepted-Bid notification.
6. One not-accepted-Bid notification.

Every outbox row must be `Sent` with `RetryCount = 0` and must match exactly one smtp4dev message by decoded subject and expected To/Cc recipients. Opening and reminder messages use the static dashboard link. Seller approval adds `opportunityId`; each Bid result adds its `bidId`.

## Isolation and retention

The runner uses a new database, run marker, and smtp4dev data directory dedicated to this execution. It refuses to proceed if the database or data directory already exists or if the required SMTP/UI ports are occupied.

The existing databases and smtp4dev data directories remain unchanged:

- `workflowengine_sqms_live_acceptance`
- `workflowengine_sqms_email_live_acceptance`
- `C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-live-acceptance-20260821`
- `C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-email-live-acceptance-20260821`

The new database and mailbox are retained after execution so the user can inspect smtp4dev and run read-only SQL queries. The one-shot execution is never replayed against retained evidence.

## Implementation approach

### Selected: explicit single-lifecycle acceptance profile

Add a distinct scenario/profile to the existing compiled acceptance tool, with its own fixed expected counts and state/email plan. Add a safety-pinned PowerShell launcher and an independent read-only SQL verifier.

This reuses the production services and established evidence verification while keeping the earlier 3-Opportunity/15-Bid profiles backward compatible.

### Alternatives considered

- Reuse the existing runner unchanged: rejected because it always creates 3 Opportunities and 15 Bids.
- Drive services from an ad hoc PowerShell/C# snippet: rejected because it would be harder to review, reproduce, and verify safely.
- Manually insert final SQL rows: rejected because it would not exercise the compiled workflow lifecycle or SMTP delivery path.

## Compatibility constraints

- Do not change public HTTP contracts, trusted actor headers, target API integration surfaces, workflow keys, or existing acceptance-profile defaults.
- Do not add an SQMS HTTP endpoint or a second state machine.
- Do not update UI-owned `TotalBid`, `LargestBid`, or `LowestBid` columns.
- Do not add an Effective Date scheduling rule; retain opened-date-relative notification scheduling.
- Do not push, merge, deploy, execute production SQL, configure scheduling, or remove retained evidence.

## Verification and handoff

Development follows test-first coverage for profile selection, counts, state plan, email plan, and safety pins. Before the one-shot run, build with warnings as errors and verify that the new database/data directory are absent.

After execution, run:

- The compiled verification-only mode.
- The production SQMS deployment verifier.
- The dedicated independent SQL verifier.
- Read-only summaries of Opportunity/Bid states, workflow versions, outbox routes/status/retries, audit/task/SLA totals, and smtp4dev message count.

The final handoff records exact IDs, final states, email subjects/routes, database name, smtp4dev URL/data directory/process, and copyable inspection queries.
