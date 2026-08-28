# SQMS Bid Category Column Correction Design

## Problem

The scheduled SQMS classifier currently passes `Bid.BidExpressedAsFairValue` to the Bid-category parser. The target application stores numeric percentage text in that column, such as `100 %` and `5 %`, so every submitted Bid fails classification with `Bid category '<value>' is not recognized.`

The target schema already contains the separate authoritative category column `Bid.BidFairValue`. Its confirmed values are:

- `Yes, Fair Value`
- `No, Premium`
- `No, Discount`

This matches the legacy classification evidence. `BidExpressedAsFairValue` remains a percentage and must not be used to infer whether a Bid is Premium or Discount.

## Scope

Correct the scheduled Bid-classification input without changing workflow transitions, classification thresholds, public HTTP contracts, target data, or database schema.

## Design

### Persistence mapping

Extend `SqmsBidRow` additively with a `BidFairValue` property while preserving its existing positional constructor. Append `BidFairValue` to the SQL projection so all existing column ordinals and constructor arguments remain stable. Populate the new property when reading an `SSQMS.Bid` row.

This avoids a breaking constructor change and accurately represents both target columns:

- `BidFairValue`: authoritative category label.
- `BidExpressedAsFairValue`: percentage text retained for other target behavior and reporting.

### Category parsing

Continue accepting the existing canonical inputs `Fair Value`, `Premium`, and `Discount`. Add the confirmed legacy labels `Yes, Fair Value`, `No, Premium`, and `No, Discount`, case-insensitively with surrounding or embedded whitespace normalization consistent with the current parser.

The scheduler will parse `BidFairValue`. For compatibility with existing custom `ISqmsDataStore` implementations and tests that construct `SqmsBidRow` using the existing positional constructor, it will fall back to `BidExpressedAsFairValue` only when `BidFairValue` is null or blank. Numeric percentages remain invalid category values and will continue to produce a structured candidate failure rather than being guessed.

### Scheduled processing

No orchestration changes are required. A rerun will rediscover closed Opportunities with submitted Bids. Each corrected candidate will use the existing rules:

- Sum requested amounts for Fair Value and Premium Bids.
- When that sum is strictly greater than the Opportunity sale amount, move Discount Bids to `NotAllocated`.
- Move all other valid submitted Bids to `IpgReview`.
- Continue per Bid on failure and return a nonzero command result if any candidate still fails.

## Compatibility

- No SQL migration or data rewrite.
- No request or response contract change.
- No workflow/state/action key change.
- No change to target-owned request persistence or scheduled four-column projection.
- Existing canonical category labels remain accepted.
- Existing `SqmsBidRow` positional construction remains source-compatible.

## Error handling

Missing or unrecognized `BidFairValue` remains a structured per-candidate failure. The classifier will not infer category from `BidExpressedAsFairValue`, because a percentage alone cannot distinguish Premium from Discount reliably.

## Verification

Use test-driven development:

1. Add parser cases for all three confirmed legacy labels.
2. Add a scheduled-classification regression using `BidFairValue` labels together with `100 %` and `5 %` percentage values.
3. Add a SQL data-store assertion proving both fields are read from their correct columns.
4. Observe the focused tests fail before production changes.
5. Apply the minimal mapping/parser/scheduler correction.
6. Run focused tests, the complete non-SMTP regression suite, and warning-as-error builds required by `PROJECT.md`.

The retained one-shot SQMS live-acceptance environment must not be rerun or modified.
