# Expected SMTP messages

## Contract status

- **Implemented:** The six route keys, immutable recipient snapshots, templates, and SMTP outbox sender exist in the current workflow/SQMS assemblies.
- **Recommended target facade:** None; notification delivery is an operator/background outbox operation, not a browser route.
- **Target implementation required:** Configure a fake/local SMTP endpoint and server-owned recipient source data in the isolated acceptance environment.

## Target-team verification

After the lifecycle succeeds, run `process-notification-outbox` against fake/local SMTP. Verify exactly six mailbox messages and exactly six matching outbox rows with `Status = Sent`, `RetryCount = 0`, and non-null `SentAtUtc`. Match each message to the immutable `NotificationRecipientsJson`, subject, rendered body/link, and route. Do not derive or replace recipients from public request JSON.

| Route | Expected message |
|---|---|
| `SqmsOpportunityOpen` | One grouped open-to-bidding message for the configured quarter/occurrence |
| `SqmsOpportunityReminder` | One grouped reminder message for the same occurrence window |
| `SqmsOpportunityPendingSellerApproval` | One seller-approval message for Opportunity 10000 |
| `SqmsBidAccepted` | One accepted disposition message for Bid 10000 |
| `SqmsBidPartiallyAccepted` | One partially accepted disposition message for Bid 10001 |
| `SqmsBidNotAccepted` | One not-accepted disposition message for Bid 10002 |

Bid 10003 ends `NOT ALLOCATED` during scheduled classification and sends no disposition email.

## Retained 2026-08-22 evidence

The preserved live acceptance produced exactly these six messages through smtp4dev:

- `SqmsOpportunityOpen`: `Q1 2030 Secondary Sale Opportunities - Open to Bidding: Response Required by 01/16/2030`
- `SqmsOpportunityReminder`: `REMINDER Q1 2030 Secondary Sale Opportunities - Open to Bidding: Response Required by 01/16/2030`
- `SqmsOpportunityPendingSellerApproval`: `Q1 2030 Secondary Sale Bids - SQMS Live Vehicle A/SQMS-PROJECTION-OWNERSHIP-LIVE-20260822-A - SQMS Live Acceptance Opportunity A: Response Required by 01/22/2030`
- `SqmsBidAccepted`: subject contains `...-BID-01 - ... Bid 01`
- `SqmsBidPartiallyAccepted`: subject contains `...-BID-02 - ... Bid 02`
- `SqmsBidNotAccepted`: subject contains `...-BID-03 - ... Bid 03`

The compiled retained verifier matched each Sent outbox row to exactly one smtp4dev message, including recipients and rendered links. This is historical evidence; do not replay or clear the retained mailbox. New target integration acceptance must use its own isolated database and mailbox.
