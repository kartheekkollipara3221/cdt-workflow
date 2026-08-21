# SQMS Email Template Correction Design

**Date:** 2026-08-21

**Status:** Approved design awaiting written-spec review

**Scope:** Correct the six SQMS notification templates to match the current SharePoint business messages.

## Problem

The implemented SQMS notification routes use inferred subjects and condensed body text. The current SharePoint messages supplied by the user are the authoritative business examples. The implementation differs in subject structure, body wording, field layout, hyperlink labels, capitalization, and date formatting.

This correction changes future rendered SQMS messages only. It does not change workflow transitions, recipient ownership, public requests or responses, target-table persistence, scheduling eligibility, or previously queued notification snapshots.

## Chosen approach

Update the templates on the existing six metadata-driven routes through the rerunnable SQMS DML package:

- `SqmsOpportunityOpen`
- `SqmsOpportunityReminder`
- `SqmsOpportunityPendingSellerApproval`
- `SqmsBidAccepted`
- `SqmsBidPartiallyAccepted`
- `SqmsBidNotAccepted`

The existing route keys and notification types remain stable. A second route version is unnecessary because the current wording is incorrect rather than an independently supported variant. SQMS-specific email generation must not move into application code; the generic notification renderer and outbox remain authoritative.

Rerunning the DML updates the route metadata in place. Already-created outbox rows remain immutable and are not rewritten. Only notifications queued after the metadata update use the corrected content.

## Shared rendering rules

- Correct obvious spelling, grammar, capitalization, and duplicate-label errors from the legacy examples without changing their business meaning.
- Placeholder quotation marks in the supplied examples are notation only and do not appear in delivered subjects or bodies.
- All displayed dates use zero-padded `MM/dd/yyyy` formatting.
- Quarter and year are derived from `Opportunity.EffectiveDateForSale`.
- Monetary strings come from the approved target columns and are not recalculated or reformatted by the workflow layer.
- Plain-text bodies preserve the documented paragraph breaks and field-per-line layout.
- HTML bodies contain equivalent paragraphs and field labels, HTML-encode target-derived values through the existing renderer, and make only the documented link label clickable.
- Existing configured URLs and identifier query parameters remain authoritative.
- Missing required target values or invalid configured URLs continue to fail atomically under the existing validation behavior.

## Opportunity open notification

### Recipients

- To: configured static Open-to-Bidding To list.
- Cc: configured static Open-to-Bidding Cc list.

Recipient metadata is unchanged.

### Subject

```text
{{quarter}} {{year}} Secondary Sale Opportunities - Open to Bidding: Response Required by {{responseDate}}
```

### Body

```text
Please click the following link(s) to review secondary sales opportunities for this quarter.

View all Opportunities open for Bidding
```

`View all Opportunities open for Bidding` is the hyperlink label for the configured static Open-to-Bidding dashboard URL. No date or Opportunity identifier is appended to that URL.

## Opportunity reminder notification

### Recipients

The reminder uses the same static To and Cc lists as the open notification.

### Subject

```text
REMINDER {{quarter}} {{year}} Secondary Sale Opportunities - Open to Bidding: Response Required by {{responseDate}}
```

### Body

The reminder body and hyperlink are identical to the Opportunity open body.

## Pending Seller Approval notification

### Recipients

- To, in order: target-API-resolved `Opportunity.RelationshipManager`, then `Opportunity.SellerRMTeam`.
- Cc: configured static Seller-approval Cc list.

The target API continues resolving the two identifiers to email addresses before invoking the workflow layer.

### Subject

```text
{{quarter}} {{year}} Secondary Sale Bids - {{vehicle}}/{{accountNumber}} - {{accountName}}: Response Required by {{sellerResponseDate}}
```

### Body

```text
Please click the following link to review secondary sale bids for the following opportunity.

Account Number: {{accountNumber}}
Account Name: {{accountName}}
Name of Fund: {{fund}}
Name of Vehicle: {{vehicle}}
Total Amount Requested to Be Sold: {{saleAmount}}

Link to approve or reject bids received: Click Here
```

`Click Here` links to the configured Seller-approval Bid dashboard with the generated `opportunityId` query parameter. Existing query parameters on the configured base URL remain intact.

`sellerResponseDate` remains the local action date plus the configured `SellerResponseDays`; only its display format changes to `MM/dd/yyyy`.

## Accepted Bid notification

### Recipients

- To, in order: target-API-resolved `Bid.RelationshipManager`, then `Bid.BuyerRmteam`.
- Cc: configured static Bid-disposition Cc list.

The same recipient rule applies to all three Bid outcomes.

### Subject

```text
{{quarter}} {{year}} Secondary Sale Bids - {{vehicle}}/{{buyerAccountNumber}} - {{buyerAccountName}}
```

### Body

```text
The bid placed for the following opportunity has been accepted by the Seller for an effective date of {{effectiveDate}}.

Please confirm your buyer will proceed with the purchase of the stated private fund interest below at your earliest convenience.

Once confirmed, you will receive further communication shortly from the Private Funds team regarding the documentation required to effect this sale.

Buyer Account Number: {{buyerAccountNumber}}
Buyer Account Name: {{buyerAccountName}}
Name of Fund: {{fund}}
Name of Vehicle: {{vehicle}}
Total Amount Requested to Be Purchased: {{requestedAmount}}

Link to View: Click Here
```

`Click Here` links to the configured Bid form with the generated `bidId` query parameter. Existing query parameters on the configured base URL remain intact.

## Partially Accepted Bid notification

The recipients, subject, target fields, paragraphs, and hyperlink behavior match the Accepted Bid notification. The opening sentence is:

```text
The bid placed for the following opportunity has been partially accepted by the Seller for an effective date of {{effectiveDate}}.
```

The buyer-confirmation and Private Funds documentation paragraphs remain present.

## Not Accepted Bid notification

The recipients, subject, target fields, and hyperlink behavior match the Accepted Bid notification. The opening sentence is:

```text
The bid placed for the following opportunity has not been accepted by the Seller for an effective date of {{effectiveDate}}.
```

The buyer-confirmation and Private Funds documentation paragraphs are omitted. The remaining body is:

```text
Buyer Account Number: {{buyerAccountNumber}}
Buyer Account Name: {{buyerAccountName}}
Name of Fund: {{fund}}
Name of Vehicle: {{vehicle}}
Total Amount Requested to Be Purchased: {{requestedAmount}}

Link to View: Click Here
```

## Data mapping

| Template token | Authoritative source |
|---|---|
| `quarter` | Quarter containing `Opportunity.EffectiveDateForSale`, formatted `Q1`-`Q4` |
| `year` | Year of `Opportunity.EffectiveDateForSale` |
| `responseDate` | Opportunity opened local date plus configured `BiddingDays` |
| `sellerResponseDate` | Seller-approval action local date plus configured `SellerResponseDays` |
| `effectiveDate` | `Opportunity.EffectiveDateForSale`, formatted `MM/dd/yyyy` |
| `accountNumber` | `Opportunity.AccountNumber` |
| `accountName` | `Opportunity.AccountName` |
| `fund` | `Opportunity.FundConsideredSecondarySale` |
| `vehicle` | `Opportunity.Vehicle` |
| `saleAmount` | `Opportunity.CommitmentAmountSale` |
| `buyerAccountNumber` | `Bid.AccountNumber` |
| `buyerAccountName` | `Bid.AccountName` |
| `requestedAmount` | `Bid.CommitmentAmountRequested` |
| `dashboardUrl` | Configured static Open-to-Bidding dashboard URL |
| `opportunityUrl` | Configured Seller dashboard URL plus `opportunityId` |
| `bidUrl` | Configured Bid form URL plus `bidId` |

## Scheduling boundary

No new Effective Date rule is added.

IPG manually determines which Opportunities to move to `OPEN TO BIDDING` and when. The scheduled process continues to rely on the recorded opened date:

- Open notification occurrence: opened local date plus one day.
- Reminder occurrence: opened local date plus `BiddingDays - 1`.
- Response date shown in both subjects: opened local date plus `BiddingDays`.

`EffectiveDateForSale` is used only to group the notification by quarter/year and to populate message content. The workflow does not enforce that IPG opens an Opportunity by a particular calendar date.

## Backward compatibility and deployment

- No route, state, action, role, workflow, or notification-type key changes.
- No public HTTP, in-process service, Console, DTO, or target-table contract changes.
- No DDL or EF migration is required.
- The existing production DML file remains the DBA-delivered source of template data and stays rerunnable.
- Applying the DML intentionally changes future email subject/body semantics for these six stable routes, as explicitly approved.
- Previously queued or sent outbox rows retain their original subject, body, HTML body, recipients, and links.
- Existing recipient configuration variables and ordering remain unchanged.

## Verification design

Implementation must use test-driven development and prove:

1. The SQL package contains the exact six subject, plain-text, and HTML template contracts.
2. Rerunning the DML updates existing route rows without duplicating routes or recipient rules.
3. Date rendering is exactly `MM/dd/yyyy` for response, seller-response, and effective dates.
4. Open and reminder messages have the exact static link label and no injected Opportunity/date query parameters.
5. Seller approval renders all five Opportunity fields and an `opportunityId` link.
6. Each Bid outcome renders the correct opening sentence, five Bid/Opportunity fields, and a `bidId` link.
7. Accepted and Partially Accepted include the two follow-up paragraphs; Not Accepted omits them.
8. Dynamic and static recipient order remains unchanged.
9. Target-derived HTML values remain encoded and template-owned hyperlinks remain clickable.
10. Existing Portfolio Trade, public API, Console samples, SQMS state behavior, scheduling behavior, recipient rules, and retained calculated-column ownership regressions continue to pass.

The live acceptance runner and independent SQL verifier should be updated to assert the corrected messages. A new retained live run must use a new isolated database and smtp4dev data directory; it must not overwrite the retained `workflowengine_sqms_live_acceptance` evidence from the prior run.

## Out of scope

- Enforcing an IPG deadline for opening Opportunities.
- Selecting Opportunities from `EffectiveDateForSale` independently of workflow status.
- Changing recipient lists or identifier-resolution ownership.
- Changing scheduler cadence or Windows Task Scheduler configuration.
- Reformatting or recalculating target amount strings.
- Rewriting previously queued or sent emails.
- Changing dashboard or form URL ownership.
