# CDT PortfolioTrade v2 release guide

Release of PortfolioTrade **version 2**: the CDT notification recipients and SLA timing agreed on 2026-10-18.
Plan: `.planning/quick/261018-cdt-portfolio-trade-v2-notification-routes/261018-cdt-PLAN.md`.

Version 1 stays unchanged. Trades that are open on version 1 finish on version 1 with the old recipients. Only new trades use version 2, after activation.

## What changes for users (version 2)

| Email | To | Cc |
|---|---|---|
| Directed Trade Pending Checker Approval (+ Overdue reminders, every 60 minutes) | ICG RAs, Risk Team | Additional people the Maker added (`selectedCcUsers`) |
| Updates Required to Directed Trade Request | Maker | none |
| Directed Trade Request Pending Execution | ICG RAs, Risk Team, selected desk users | `selectedCcUsers` (includes the RM) |
| Due Today: ...Pending Execution (14:00 New York on the target date) | Selected desk users | none |
| Overdue: ...Pending Execution (08:00 New York daily, weekends included) | Selected desk users | none |
| Trade Execution Pending Confirmation | Maker, Risk Team | `selectedCcUsers` (includes the RM) |
| Overdue: Trade Execution Pending Confirmation (09:00 New York from the next day) | Maker | none |
| Confirmation that Trade settled in account | Maker, Risk Team | none |

A same-day trade that reaches the Trading Desk after 14:00 gets no Due Today reminder. Its first reminder is Overdue at 08:00 the next day.
The reminder recipients of a task are fixed when the task is created. A later membership change applies to new tasks.

## Requirement for the target team (before activation)

1. Stop sending `selectedCcUsers` on `CheckerReturnToMaker`, `TradingReturnToMaker` and `BdrmTradeFulfilled` (including the request that the target maps to `BdrmTradeFulfilled`). Version 2 rejects these actions with `ValidationFailed` if the input is sent. Version 1 accepts requests without it, so this change is safe for open version 1 trades.
2. No other request change. RM stays inside `selectedCcUsers`. `assignedRoleKey` and `slaTargetDate` are unchanged.
3. Do not install an engine build older than this release while any version 2 trade is open.

## Scripts (`sql/upgrades/`)

| Order | Script | Type | Effect |
|---|---|---|---|
| 1 | `20261019_01_cdt_v2_schema_ddl.sql` | DDL | Adds 6 columns. Safe to run again. Use this or the EF migration `AddCdtV2SlaReminderControls`, not both. |
| 2 | `20261019_02_portfolio_trade_v2_create_inactive_dml.sql` | DML | Creates version 2 inactive with placeholder addresses. Stops if version 2 exists. |
| 3 | `20261019_03_recipient_group_membership_dml_template.sql` | DML template | Adds or deactivates one person in one group on every route of that group. |
| 4 | `20261019_04_portfolio_trade_v2_verify.sql` | Read-only | Lists the version 2 metadata and runs the release checks. Fails if a check fails. |
| 5 | `20261019_05_portfolio_trade_v2_activate_dml.sql` | DML | Runs the checks, then makes version 2 active for new trades. |
| 6 | `20261019_06_portfolio_trade_v1_reactivate_dml.sql` | DML | Rollback for new trades only: version 1 active again. |

Every DML script runs in one transaction and changes nothing when it stops with an error.

## Release sequence

1. Target team: requirement 1 above.
2. Run script 01. Check that it completes without an error.
3. Deploy the engine build to every host that runs the engine: the target API, the SLA runner and the outbox processor. Version 1 stays active, so behavior is unchanged.
4. Run script 02. Version 2 exists but no new trade uses it.
5. Run script 03 once per person with `@VersionNumber = 2`: all ICG RAs, all Risk Team members and the users of each desk. **Add the real people first, then deactivate the placeholders** (`icg-ra-1/2`, `risk-team-1/2`, `taxable-trader-1`, `tax-exempt-trader-1`, `equity-trader-1` at `@example.test`). The template refuses to remove the last user of a desk.
6. Run script 04. Every check must show PASS.
7. Run script 05.
8. Start one test trade per desk. Check the task and outbox recipient JSON and the first reminder.

Never run `init-db` or the seed against a target database.

## Recipient groups and the membership template

| Group | SortOrder block | Routes |
|---|---|---|
| `ICG_RA` | 10-49 | Checker submit, Checker resubmit, Trading Desk assigned |
| `RISK` | 50-89 | the three above + BDRM assigned, Workflow completed |
| `DESK_TAXABLE` | 100-199 | Trading Desk assigned, Due Today, Overdue (`TradingDeskTaxable`) |
| `DESK_TAX_EXEMPT` | 200-299 | same three (`TradingDeskTaxExempt`) |
| `DESK_EQUITY` | 300-399 | same three (`TradingDeskEquity`) |

- Edit `@VersionNumber`, `@Group`, `@Address` and `@Action` (`ADD` or `DEACTIVATE`) at the top of the template.
- One address per run, without `;` or `,`. Each person is one rule row; the engine does not split delimited values.
- A repeated run changes nothing. An add reactivates an inactive row with the same address in that block.
- A person in both ICG RAs and Risk Team has one row in each block. Removing the person from one group keeps the other.
- A full block stops the template with error 52306.
- **Never delete rule rows.** The engine treats an SLA reminder route with any rule row as an own-rules route. Deleting all of its rows makes it fall back to the broad task recipients. Script 04 reports an own-rules route with no active row for a desk.

## Rollback

- Run script 06. New trades use version 1 again. Open version 2 trades continue on version 2.
- Keep this engine build installed while version 2 trades are open. An older build ignores the saved reminder audiences and the new SLA fields: version 2 reminders would go to the task recipients at the old reminder times.
- Keep the 6 columns. The EF `Down` migration removes saved reminder audiences and is not the normal rollback.
- No open workflow needs to be closed.

## Local development

- `init-db` keeps version 1 active (samples, demo and the existing tests use version 1).
- `init-db --portfolio-trade-version 2` seeds both versions and activates version 2; `--portfolio-trade-version 1` switches back.
- `verify-seed` checks both versions and that exactly one version is active. After the membership template changes version 2, `verify-seed` reports the differences from the seed; this is expected and target databases use script 04 instead.
