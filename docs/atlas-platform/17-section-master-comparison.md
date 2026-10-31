# Section Master: target and new platform compared

Status: Report, 2026-10-29. Compares the target section master (copy as of 2026-10-27) with the new platform form (`atlas-platform/`, release P1, TTDV2). No code is changed by this report.

## 1. Purpose

This report gives answers to three questions:

1. How is the new section master better than the target section master?
2. How is the new solution (form, server, data, operations) different from the target, and what does it improve?
3. What is the benefit of moving the new solution to the target?

Evidence:

- Target: `target-implementation-read-only/section-master-asof10272026/cp-atlas-sectionmaster-files/` (`.ts` 501 lines, `.html` 164 lines) and the target `Service/`, `Atlasapp/Config/` copies.
- New: `atlas-platform/WebUi/src/app/platform/form/` and `atlas-platform/src/`.
- Earlier specs and reviews: [01-section-master](01-section-master.html) (findings SM-01 to SM-19), [12-ttd-pilot-build](12-ttd-pilot-build.html), [13-architecture-review](13-architecture-review.html), [16-architecture-review-2](16-architecture-review-2.html).

## 2. Short answer

| Question | Answer |
|---|---|
| Better section master? | **Yes.** The target component has app names and field names in code. The new form has no app name and no field name. All app behaviour comes from JSON. The users see the same layout. |
| Different solution? | **Yes, at the back.** The target trusts the browser. The new server checks each save again: rules, allow list, validators, options, version, audit. Data is typed and has a version column. |
| Benefit at the target? | **A new app is configuration, not code.** App #17 does not change shared code, so it cannot break apps #1 to #16. Save errors, lost updates and false audit entries stop. One switch turns an app off. |
| Same as the target? | **The look and the contracts.** Same pbdev controls, same layout and CSS classes, same `div_{Name}` and `btn_{Name}` IDs, same endpoints. The current apps (SQMSO, SQMSB, TTD, AIN, CDT) do not change. |

## 3. How each one works

### 3.1 Target section master (today)

1. The route `sectionmaster/:code/:id` opens the component. It calls `GET v1/SectionPage`.
2. The server selects a module service with an `if` chain on the app code. The module service reads its own JSON file from disk and adds defaults in code.
3. The component builds the form from `Controls`. It has special code for some apps and some fields (section 4).
4. After 5 seconds, a timer shows or hides some SQMS fields with `document.getElementById`.
5. Save: the component sends all form values to `POST v1/ManagePage`. The module service maps each name to a column. It catches all errors.
6. The component always shows "Record saved successfully". An HTTP interceptor in the browser writes the audit entry.

### 3.2 New platform form (TTDV2)

1. The route `form/:code/:id` opens the form. The old route stays for the current apps.
2. The server finds the app in a registry (manifest + one registration line). One generic service (`ConfigSectionService`) loads the form JSON from a cache, binds the record, adds `Defaults` and `OptionsSource` lists, and applies the `Rules` for this user.
3. The form builds controls from the JSON. A renderer registry maps each `Type` to a component. `Effects` (show, hide, require, set) run at once when a value changes. `Computed` fields update while the user types.
4. Save: the form sends only the shown and enabled fields, with the `Version` token.
5. The server runs the 12-step save pipeline (section 5.2). Each error comes back to the user. The audit entry is written in the same transaction.
6. The form shows "saved" only when there is no error. Field errors go to the field.

## 4. The section master: item by item

### 4.1 App code and field names in the shared component

| Item in the target | Evidence (target `.ts`) | New platform |
|---|---|---|
| `Account` split on `" - "` into 4 fields | 258-269 | `Effects` → `SetFromOption`, reads `Option.Data` (no position split). Fallback to the split for old options. |
| `OpportunityId` split on `" $$ "` into 5 fields | 270-282 | Same `SetFromOption`, JSON only |
| `BidForFullCommitment`, `BidFairValue` set or clear a field | 283-296 | `Effects` → `Set` / `Clear` |
| `FundConsideredSecondarySale` → vehicle list in a component variable | 142-147, 297-301; `.html` 21-46 | No special row. Options are on the control. |
| `WeekEnding` → grid column headers | 302-317 | Not yet (section 7) |
| `ClientRelationshipLeavingBBH`, `IsValuationForPrivateFundVehicle` → show, hide, require | 197-238, 321-357 | `Effects` → `Show` / `Hide` / `Require` / `Unrequire`. Runs at once; no timer; no `document.getElementById`. |
| `WeeklyHour` grid: 8 column names in the save | 412-425 | Generic: each enabled cell of a `SingleRowFields` grid |
| `code == 'SQMSO' / 'SQMSB'`: a button sets `Status` | 385-388 | `Button.Action` in JSON (`Save`, `Close`, `Delete`) |
| Redirect by `SQMSO`, `SQMSB`, `AIN` and roles `mfo` / `ipg` | 448-475 | One dashboard link for each app (`ATLAS_DASHBOARD_LINK`) |
| `Bid` button → `addBid`, fixed target `SQMSB`, parent data in a memory service | 477-487, 108-111 | Not in P1 (parent/child design in spec 01, section 11) |

Count: the target component has **3 app codes, 2 role names and more than 12 field names in code**. The new form has **0** (`grep` of `WebUi/src/app/platform`, specs excluded). Each new app with dependent fields makes the target component larger. The new form does not change for a new app.

### 4.2 Correctness

| Problem in the target | Effect | New platform |
|---|---|---|
| 5-second `setTimeout` for conditional fields (237) | For 5 seconds the form shows wrong fields and has wrong validators. A user can save in that time. If the page loads slowly, the rule does not run. | Effects run when the form is built and at each value change. |
| Always shows "Record saved successfully" (439) | The server catches all errors and rolls back (SM-16). The user thinks the record is saved. | Message only when `Errors` is empty. Field errors on the field; other errors at the top. HTTP errors are shown. |
| Hidden fields stay in the form and are sent | A hidden field can still block the save or be saved. | Hidden or disabled fields are not validated and not sent (`isShown`, `buildOutputs`). |
| Unknown `Type` shows nothing | A typing error in the JSON hides a field with no message (SM-05). | Visible "unsupported" note; the control is disabled and not sent. `--atlas-check` finds it before deployment. |
| Validators only `Required`, `Email`, `MinLength`, `MaxLength`, `Pattern` | A value of only spaces passes `Required`. No number checks. | Same rules on client and server: spaces = empty, `Integer`, `Number`, `Min`, `Max`, own `Message`. Shared test cases. |
| Value types | Date and checkbox values go to the server in browser format. | Fixed formats: `yyyy-MM-dd`, `true`/`false`, list with `#$#` (the target separator is kept). |
| `addBid` parent data in a memory service | A page reload loses the parent data (SM-19). | Not used. |

### 4.3 Code quality (Angular)

| Item | Target | New |
|---|---|---|
| Template | One long `*ngIf` chain, one block for each type | One loop and a renderer registry (`@for`, `NgComponentOutlet`) |
| New control type | Edit the shared template | `provideAtlasRenderers({ rating: RatingRenderer })` in `app.config.ts` |
| Change detection | Default, `cdr.detectChanges()` calls | Signals, `OnPush` |
| Subscriptions | Manual `unsubscribe` | `takeUntilDestroyed` |
| Accessibility | Label not linked to the input | `for`/`id`, `aria-required`, `aria-invalid`, error text linked with `aria-describedby` |
| Tests | 1 spec file, 22 lines (create only) | 66 Angular tests for the platform |
| Form logic | In the component | Plain functions (`atlas-form-model.ts`, `atlas-effects.ts`) that tests can call with no component |

### 4.4 New features in the form

- **History** button: shows `Create`, `Update`, `Delete` with old and new values.
- **Delete** button with a confirm dialog (soft delete, only with the `CanDelete` permission).
- **Save conflict** message when another user saved the record first.
- **Load error** page with a "Back to the dashboard" button, not an empty page.
- **Computed** fields (`Sum`, `Count`, `Min`, `Max`), for example the weekly total.
- The save button is disabled during the save (no double save).

### 4.5 What stays the same (look and contracts)

- Layout and CSS classes are copied from the target 2026-10-27 template: `center-container`, `header-container`, `pbdev-section` (blue), `section-master`, `btnContainer`, `header-btn btn btn--primary`.
- The same pbdev controls: `pbdev-input`, `pbdev-select`, `pbdev-textarea`, `pbdev-date-picker`, `pbdev-checkbox`, `pbdev-dialog`, `pbdev-loader`; `ng-select`; ag-grid with the target cell editors (`TextEditor`, `SelectEditor`, `NumericEditor`).
- The same mandatory-field message and Ok dialog as `CPAtlasConfirmation`.
- The same element IDs (`div_{Name}`, `btn_{Name}`), the same `#$#` separator, the same toastr settings.
- The user confirmed the local look built from the real pbdev-core source: "spot on" (2026-10-28).

## 5. The whole solution: what is different

### 5.1 Server: one generic service, not one class for each app

| Item | Target | New |
|---|---|---|
| Select the app service | `if` chains in `CPAtlasService` (13 branches); `null` for a code with no branch → `NullReferenceException` (SM-06) | Registry: `AddAtlasModule<T>("CODE")`; a code with no module gives a clear 404 |
| App code | Enum value in `CPApplicationCode` | String code in a manifest (`<CODE>.app.json`); no enum change |
| Service for each app | `CPTTDService` 323 lines; `CPSQMSOService` 594; `CPSQMSBService` 738; `CPAINService` 147 | `ConfigSectionService` for all; TTDV2 adds one hook class (31 lines) and two option providers (38 lines) |
| JSON file | Read from disk on each request; one `AppSettings:<Code>ConfigPath` key for each app | One `ConfigFolder`; cached; reload when the file changes |
| Defaults | In C# (`"DRAFT"`, header text) | `Defaults` in JSON, with tokens (`{User.LanID}`, `{Today}`, `{Options[0]}`) |
| Option lists | In C# for each app | `OptionsSource` → named provider (platform or app) |

### 5.2 Save: the server checks again (security and data quality)

The target save trusts the browser (SM-14, issues H3, H4). The new save pipeline has these steps:

1. Load the configuration and the record.
2. Read the user roles for this app only (and its parent app). Deleted entitlements do not count (SM-08).
3. Apply `Rules` and the server part of `Effects` for this user and record.
4. Allow list: accept only fields that are shown and enabled for this user. Ignore other names and write `Atlas.FieldIgnored` (SM-14, H3).
5. Stop if the user may not save this record (H4). `AccessMode` `LogOnly` first, then `Enforce`.
6. Check the `Validator` rules and `Require` effects on the server.
7. Check that a changed option value is one of the options that the server gives to this user.
8. Check the version token. A conflict gives an error (SM-18).
9. Convert the text values to the column types and map them.
10. Set the system fields on the server: `CreatedBy`, `ModifiedBy`, dates from `TimeProvider`. Calculate `Computed` columns again.
11. Write the audit entry in the same transaction (SM-17).
12. Commit. On an error: roll back, log `Atlas.SaveFailed`, return the error to the user (SM-16).

Result: a direct API call cannot save a hidden field, a value that is not in the list, a system column, or a record of another owner.

### 5.3 Data

| Item | Target (`TTD.General`) | New (`TTDV2.General`) |
|---|---|---|
| Column types | Text for numbers and dates | Typed columns (`int`, `date`, `decimal`) |
| People | Display names | LAN IDs (joins and reports work; name changes do not break them) |
| Concurrency | Last write wins (SM-18) | `rowversion`; second save gets a conflict |
| Find one row | `ToList()` of the full table, then `Where` (SM-12) | Key lookup, no tracking for reads |
| Audit | Written by the browser after each `POST` (a failed save still gets an entry; a user can skip it) | Written by the server in the save transaction; no entry when nothing changed |
| Time | `DateTime.Now` (local) | UTC from `TimeProvider` (or local by app setting) |

### 5.4 Dashboard

| Item | Target | New |
|---|---|---|
| Tab data | A stored procedure for each tab | `Source: Table` with `Filter`, `MaxRows`, `Columns` in JSON (no procedure) |
| Open a record | `if` chain in `onClick` / `editRow` | `FormType` from the app row; one `formRoute()` call |
| Row actions | In code | `Permissions` from the server |
| Totals row | In code | `sumsRow()` from the column JSON |

### 5.5 Operations

| Item | Target | New |
|---|---|---|
| Check before deployment | None; users find errors | `--atlas-check` (names, tokens, patterns, tables, column types, registration); exit code 0 or 1 |
| Turn one app off | Code change and deployment | `AppSettings:Modules:<CODE>:Enabled = false` |
| A bad config | Can fail at run time for all users | `OnStartupError = DisableModule`: only that app is off; the current apps work (AR-01) |
| Logs | Errors are caught and lost | Named events 41001-41024 (`Atlas.AccessDenied`, `Atlas.FieldIgnored`, `Atlas.SaveFailed`, ...) |
| Database changes | Hand-written | Numbered scripts in `sql/release-p1/` with a script log |
| Tests | Few | 277 .NET tests (with SQL Server) and 66 Angular tests; 0 build warnings |

## 6. Benefit of moving to the target

### 6.1 The cost of one new app

| Step | Target today | With the platform |
|---|---|---|
| Code value | Enum value (C#) | Manifest `<CODE>.app.json` |
| Server wiring | `if` branches in service and controller (C#) | One registration line |
| Service class | New class, 150-740 lines (C#) | None, or one small hook class |
| Table | DDL, EF entity, `DbSet`, DTO | SQL template `05-new-app-template.sql` |
| Form | JSON + branches in the section master (TypeScript) | JSON only |
| Dashboard | JSON + branches in the dashboard + procedures | JSON only |
| Entitlements | SQL | SQL (same) |
| Test | Manual | `--atlas-check` + platform tests |
| Shared files changed | 5 to 7 | 0 (only the one registration line) |

13 apps are waiting (the unused codes in `CPApplicationCode`), and all 13 use Section Master. With the target design, each app adds branches to the same shared files, so each app can break the others. With the platform, these apps are JSON and SQL.

### 6.2 Benefit for each group

| Group | Benefit |
|---|---|
| Business | Faster delivery of the next 13+ apps. Lower cost for each app. One app can be turned off in minutes. |
| Users | Same look. Correct field rules at once (no 5-second gap). Real error messages. No lost work when two users save. History of each change. |
| Developers | No change in shared files for a new app. Fewer regressions. Plain JSON with a checker. Tests for the shared code. |
| Security and audit | The server enforces roles, fields and options. Roles only from the own app. Audit cannot be skipped or forged by the browser. |
| Operations | Release check with an exit code. Named log events. A config error disables one app, not the host. |
| Data | Typed columns and LAN IDs give correct reports and joins. UTC times. |

### 6.3 Why the move is safe

- All target changes are additive. The current route, endpoints, request and response shapes, control names and element IDs do not change.
- The current apps keep their code path. The `if` chains stay for the current codes; the platform runs only for codes with a manifest.
- TTDV2 has its own schema. TTD and its data do not change until the change-over script `050` runs.
- `Modules:TTDV2:Enabled = false` until the smoke test of the current apps passes.
- `AccessMode = LogOnly` first: a denied check is logged, not blocked, for one release.
- No new package at the target. The platform uses the packages that the target already has.

## 7. What is not better yet (limits)

The new form does not have every feature of the target section master. A new app that needs one of these features cannot be config-only yet (review 2, AR-09).

| Item | Status |
|---|---|
| File upload (`filepicker`) and attachments | Not in P1 (module 8 designed). The type shows "unsupported". |
| Multi-row grids (`Grid.Data` with many rows) | Not in P1. Only `SingleRowFields` grids. |
| Grid column headers from a date (target `WeekEnding` branch, new on 2026-10-27) | Not in P1. Headers are static. Can be added once as metadata. |
| Workflow engine calls (SQMS submit, status buttons) | Not in P1 (module 10 designed). |
| Parent and child forms (`Bid` from an opportunity) | Not in P1 (spec 01, section 11 designed). |
| Stepper forms (CDT) | Out of scope; Step Master stays. |
| `pbdev-select` and the exact `pbdev-core` version | Not proven at the target (AR-10). The first `ng build` at the target closes it. |
| Synchronous database calls | Same as the target. Measure during integration (AR-04). |
| Ownership | The platform is shared code. A team must own it, and app authors need a guide (AR-08). |
| Integration effort | About 8 hand edits in target files (AR-03). Most of the integration risk is here. |

## 8. Recommendation

Move the platform to the target development region as planned:

1. Run the target package and do the hand edits of INTEGRATION.md section 2.
2. Run a smoke test of each current app (menu, dashboard, load, save) before and after.
3. Turn TTDV2 on only when the smoke test passes.
4. Make a feature table of the next 13 apps. Add each missing feature once, as metadata (section 7), before the app that needs it.
