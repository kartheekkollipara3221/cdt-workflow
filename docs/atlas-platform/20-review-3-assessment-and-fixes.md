# Review 3: second opinion and fixes (fix wave 4)

Status: Done 2026-10-31. Of 43 findings: 30 fixed (5 of them in part), 2 documented, 6 deferred with a reason, 3 not agreed, 2 for the target team. One new finding (fixed for P1). 294 .NET tests and 73 Angular tests pass. Nothing committed yet.

## 1. Purpose and method

Module 19 is an independent review of the complete solution before the move to the target. This module gives a second opinion on each of its 43 findings, and the fixes for the findings that this review agrees with.

Method:

- Each finding was opened at the cited line and examined against the code and the read-only target copies. The verdict is one of: **Agree** (fixed), **Agree in part** (a narrower or a different fix, with the reason), **Agree, deferred** (real, but the fix needs a decision or belongs to a later wave), **Document** (no code change; INTEGRATION.md tells the integrator), **Disagree** (with the reason), **Target team** (their data or their code).
- Each fix has a test that fails without the fix, except where section 6 says otherwise.
- Backward compatibility (AGENTS.md): no change to a public endpoint, a request or response field, a stable key or a current app. The current apps (SQMSO, SQMSB, TTD, AIN, CDT) run no platform code path that changed. TTDV2 is not live.
- Target code is not ours: this module does not copy a full target file into the repository. The hand edits are given as exact lines in INTEGRATION.md.

## 2. Verdict

Module 19 is a good review. Its three High findings are real and are now fixed. The `/v1/v1/` defect (RV-01) alone would have failed the first smoke test at the target. Its evidence was correct at each line that was examined.

This review does not agree with all of it:

- RV-16 (dates) names the wrong part. The dashboard display is correct for a timestamp; only the date filter was wrong.
- RV-17 would break the LogOnly design if it were fixed as written. The fix makes `CanEdit` tell what the save will accept.
- RV-04 as written (Create always enforced) would also block the load of a new form in LogOnly, which a test documents as intended. The fix enforces Create on the save only.
- 3 Low findings are not defects (section 5), and 2 belong to the target team.

One new finding came from the work on RV-18: the platform dashboard runs no `Procedure` tab, but the start-up check accepted one (section 4, RV-N1).

**The move can start.** (Answered 2026-10-31: the target is upgraded to .NET 10.) The item was: Confirm the target framework of `BBH.CP.Atlas.App` and `BBH.CP.Atlas.Service` (RV-13). The copy shows `net9.0` for both and `net10.0` for `BBH.CP.Atlas.Data`. If the target is `net9.0`, stop and ask: the platform needs `net10.0`, and the target team owns that choice. Then commit, rebuild the target package, and integrate.

## 3. Verdict for each High and Medium finding

| ID | Verdict | What was done | Test |
|---|---|---|---|
| RV-01 `/v1/v1/` URLs | Agree | The platform URLs have no `v1/` prefix (`Application`, `SectionPage`, ...), as the target service. The stand-in base URL is `http://localhost:5080/v1/`. The stand-in shell calls `authorize` and `standin/users`. | All component specs now match `SectionPage`, `ManagePage`, `RemovePage`, `GetAuditLog` with no prefix; `sends the Version ... on a URL with no v1/ prefix` |
| RV-02 rule-hidden values | Agree | Load: a field that the JSON shows and a rule hides gets no `Value`, no `Options` and an empty grid cell. Save: no `Outputs` entry for it. History: `***` for its old and new value, for that reader only (`AtlasAuditMask`). A field that the JSON hides keeps its value, because an effect can show it. | `A_field_that_a_rule_hides_has_no_value_in_the_load_the_save_result_or_the_History` |
| RV-03 one bad table, all apps 500 | Agree | `OnModelCreating` leaves out a table that cannot be mapped (`AtlasPlatformContext.IsMappable`); the start-up check already names it and disables its app. The model check runs also when another app has errors. | `A_table_that_cannot_be_mapped_is_left_out_and_the_other_tables_still_work` |
| RV-04 LogOnly Create | Agree in part | The save of a new record is always refused (403) when Create is denied, as a Delete. The load of a new form stays LogOnly (test `LogOnly_writes_AccessDenied_and_continues` keeps passing). | `LogOnly_does_not_let_a_user_without_the_Create_permission_save_a_new_record` (Read user and no-role user) |
| RV-05 missing Permissions key | Agree | A start-up check error for each missing `Create`, `Edit` or `Delete` key. No run-time change (a key that is there works as before). `View` and `History` keep their fallback (History follows View). | `A_Permissions_block_with_no_Create_Edit_or_Delete_key_is_an_error` |
| RV-06 per-app fault disables all | Agree | A table name with no schema is a check error of its app (`AtlasTable.IsValidFullName`); the metadata load skips it. Each app check runs in its own `try`, so an exception (for example a module constructor) is an error of that app. `--atlas-check` reports any failure as `ERROR *:` and exit 1, not a stack trace. | `A_table_name_with_no_schema_is_an_error_of_its_app_not_a_crash` |
| RV-07 start-up guard and recovery | Agree in part | Fixed: `DisableApps` no longer throws when the config store fails again; it disables all apps and the host runs. Deferred: the automatic retry after a transient SQL failure. It needs a re-enable path in `AtlasDisabledApps` and a gated re-run; decide the retry interval with the target operations team. Until then: recycle the app pool; `Atlas.AppDisabled` (41023) is logged at Critical. | Existing `A_start_up_failure_that_is_not_a_check_error_does_not_stop_the_host_by_default` |
| RV-08 NULL role name | Agree | The view `030` returns no row with a NULL `LanID`, app `Code` or role `Name`. The role query in `AtlasUserContext` filters NULL too (an older view at the target cannot throw). | `The_role_view_gives_no_row_for_a_role_with_no_name` |
| RV-09 050 role names, 040 deleted row | Agree | `050` part 1 lists each active TTD role name with no TTDV2 role of the same name; the script then stops before any change (error 55006). `040` stops (55010, 55011) on a deleted or a second `TTDV2` app row, and attaches roles only to the active row. | `A_TTD_role_with_no_TTDV2_role_of_the_same_name_stops_the_copy_and_revokes_nothing`; the rollback test runs `040` on the deleted row |
| RV-10 no rollback SQL | Agree | New `090-ttdv2-rollback.sql` (module copy `sql/release-p1/090-ttdv2-rollback.sql`): gives back the TTD roles that `050` revoked (by `@CopyRunBy` and the time of the `050` log row), soft-deletes the TTDV2 user roles, roles and app row, keeps the TTDV2 tables. Safe to run again. | `The_rollback_gives_back_the_TTD_roles_and_removes_TTDV2_from_the_menu_and_is_safe_to_run_again`; `The_rollback_before_the_copy_changes_no_TTD_role`; release copy test |
| RV-11 hand edits under-specified | Agree, other fix | No full copy of the target `CPAtlasService.cs` or dashboard in this repository (target code is not ours). Instead: (1) INTEGRATION.md gives the two signature changes and the exact replacement line for each method; (2) a new shipped helper `AtlasAppCode.ToLegacy` replaces `Enum.Parse` in the current chains; (3) a new shipped service `AtlasDashboardNavigation` makes the dashboard edit two guarded lines, with no template change. Not agreed: `editRow` with `applicationCode` from `setGridData` is correct (it is the app of the row). | `ToLegacy_gives_404_...`; `opens the platform form from the target dashboard ...` |
| RV-12 disabled app 500 | Agree | `AtlasAppCode.ToLegacy`: 404 for a platform app that is not available, the same enum as before for a current code or number, the `Enum.Parse` error as before for any other code. | `ToLegacy_gives_404_for_a_platform_app_that_is_not_available_and_the_enum_for_a_current_app` |
| RV-13 framework and path | Agree; answered | Path: `src/app/platform/` in the package script, INTEGRATION.md, report 18 and all COPY headers (the target `angular.json` has `sourceRoot: "src"`). Framework: a check row in INTEGRATION.md section 5 with a stop-and-ask rule. This cannot be settled from the copy. | Package path is in `make-target-package.py` |
| RV-14 late load response | Agree | A new load cancels the one before; load, save and remove stop when the form is destroyed. | `cancels the load when the form is closed before the answer` |
| RV-15 effect cycle | Agree | Client: a control whose effects are running does not run them again. Start-up check: an error for a Set, Clear or SetFromOption cycle. | `stops an effect cycle after one pass`; `A_Set_or_Clear_effect_cycle_is_an_error` |
| RV-16 dashboard dates | Agree in part | Not a defect: a timestamp shows the local day of the user (correct); TTDV2 `WeekEnding` is a `date` and has no time. The defect: the date filter compared the UTC day, so a row shown as 17-Oct did not match a 17-Oct filter. The filter now uses the shown day (`cellDay`). | `filters on the same day that the cell shows` |
| RV-17 CanEdit and CanCreate | Agree in part | Fixed as written, Save would disappear in LogOnly, but there the server only logs a denied Edit. So `CanEdit` now tells what the save accepts (true in LogOnly). The client hides Save and makes the form read-only when the save would be refused. A server with no `UserPermissions` keeps Save. | `has no Save button and read-only fields when the server would refuse the save` |
| RV-18 test gaps | Agree | New tests: section 3 rows above, plus `Two_saves_with_the_same_service_and_contexts_both_commit` and the warning tests. The `Procedure` gap led to RV-N1. Still with no local test (target side only): the edited target `CPAtlasService` chains and dashboard, and the `--atlas-check` switch in the target `program.cs`. The smoke test in INTEGRATION.md covers them. | Section 7 |

## 4. New finding

### RV-N1 The platform dashboard runs no Procedure tab, but the start-up check accepted one (Medium, dashboard)

Evidence: `AtlasDashboardService.cs:65` loads rows only when `tab.Source` is `Table`; no code runs `tab.Procedure`. `AtlasStartupCheck.cs` accepted "Procedure xor Source: Table". Spec 03, 4.7 says "A tab with `Procedure` works as today".

Why it matters: app #2 with a special tab (a join, the SQMS "Pending Seller Approval" list) would show an empty tab with no error. TTDV2 has only `Source: Table` tabs, so P1 is not affected.

Fix (done): a start-up check error for a `Procedure` tab on a platform dashboard, with the text "Use Source: Table". Open (before app #2): run a tab procedure with the visibility join and the paging parameters of spec 03, 4.2.1. Test: `A_dashboard_tab_with_a_Procedure_is_an_error_until_the_platform_runs_procedures`.

## 5. Verdict for each Low finding

| ID | Verdict | What was done, or the reason |
|---|---|---|
| RV-L01 shared transaction kept | Agree | `UseTransaction(null)` in the `finally` of the save. Test: two saves with one service. |
| RV-L02 Computed over text | Agree | Start-up error when a Computed source or target is not a number column. Test. |
| RV-L03 `user` control not validated | Agree, deferred | No enabled `user` control exists (TTDV2 `ModifiedBy` is a disabled system column). Decide the rule with the first app that has one: `CP_Atlas_User` or the provider list. |
| RV-L04 AuditTime and SystemTime | Agree in part | A start-up warning when `SystemTime` is Local and `AuditTime` is Utc. The default does not change (no app has `SystemTime` Local; a change of default is a behaviour change). Test. |
| RV-L05 config folder error | Agree | A folder that cannot be listed keeps the last good snapshot and logs `Atlas.ConfigInvalid`. |
| RV-L06 option check `SameValue` | Agree | The option check uses `AtlasValueConverter.SameValue` with the column (DA-06 complete); the private ordinal copy is removed. |
| RV-L07 display-name cache | Agree | A miss is kept 2 minutes, a name 30 minutes; each entry has `Size = 1`. |
| RV-L08 echoed default refused | Disagree | No caller posts a disabled default to a platform app: the platform client leaves out disabled fields, and the current client does not open platform apps. Refusing a value that the user cannot change is the SV-01 rule. Look again if a script caller appears. |
| RV-L09 duplicate Code | Agree | A second manifest with the same Code is a config error (the first file still wins). Test. |
| RV-L10 log names | Disagree | By design: the release scripts are the module scripts, byte for byte (a test compares them), so the log names the module file. `sql/release-p1/README.md` gives the map. |
| RV-L11 UTC in shared tables | Target team | The evidence (`CPTTDService.cs.txt:134`) writes `TTD.General`, not the shared `dbo.CP_Atlas_*` tables. Who writes those rows today, and in which time, is not in the copy. Ask the target team; until then the scripts keep `GETUTCDATE()`. |
| RV-L12 code in grid JSON | Agree | A start-up warning when `cellRendererParams.code` is not the app code. Test. |
| RV-L13 LanID index | Target team | Ask for a filtered index on `CP_Atlas_User (LanID)`. Not a regression. |
| RV-L14 second connection | Agree, deferred | Works at the target. Passing the context connection needs care not to dispose it; do it with the next change in that method. |
| RV-L15 comment | Agree | `AddAtlasPlatform(configuration)` in the comment. |
| RV-L16 copy differences | Document | INTEGRATION.md section 5. |
| RV-L17 target TypeScript flags | Agree | New `npm run check:target-flags` (`tsconfig.target-flags.json`). It found one real error in this wave's new code (an implicit `any[]` under `strictNullChecks: false`), now fixed. |
| RV-L18 dashboard `Code` = 0 | Document | INTEGRATION.md section 5: clients use `AppCode`. |
| RV-L19 History local time | Agree | `localTime()` reads `yyyy-MM-dd HH:mm:ss` into a local `Date`. Test. |
| RV-L20 reference dashboard | Agree, deferred | The file is a reference and is not copied to the target; the target keeps its dashboard. |
| RV-L21 errors on hidden fields | Agree | Save marks only the shown fields as touched. |
| RV-L22 radio group name | Agree, deferred | Backlog (accessibility wave). |
| RV-L23 a11y directive per cycle | Agree, deferred | Backlog (no measured cost at 40 fields). |
| RV-L24 form route in 3 places | Disagree for now | No host mounts the platform under a prefix. Add a token when one does. |
| RV-L25 zone test passes locally | Agree, deferred | The spec must run on Jasmine (target) and Vitest (local) with no runner API; the test runs at the target under Karma with zone.js. |

## 6. Compatibility notes

- **Current apps:** no changed code path. `ToLegacy` gives the same enum as `Enum.Parse` for each current code and number (test).
- **View `030`:** a role row with a NULL `LanID`, app code or role name is no longer in the view. Before you run `030` at the target, count such rows; a user whose only role has a NULL name loses it in the view (it gave no usable role before either):

```sql
SELECT COUNT(*) AS RowsLeftOut
FROM dbo.CP_Atlas_User AS u
JOIN dbo.CP_Atlas_UserEntitlement AS ue ON ue.CP_Atlas_UserID = u.ID AND ISNULL(ue.IsDeleted, 0) = 0
JOIN dbo.CP_Atlas_Entitlement AS e ON e.ID = ue.CP_Atlas_EntitlementID AND ISNULL(e.IsDeleted, 0) = 0
JOIN dbo.CP_Atlas_Application AS a ON a.ID = e.CP_Atlas_ApplicationID AND ISNULL(a.IsDeleted, 0) = 0
WHERE ISNULL(u.IsDeleted, 0) = 0 AND (u.LanID IS NULL OR a.Code IS NULL OR e.Name IS NULL);
```

- **`040` re-run after the rollback** stops with 55010 by design. To install again, set `IsDeleted = 0` on the TTDV2 app row and its roles first.
- **LogOnly:** the save of a new record by a user with no Create permission is now 403 (before: a row of defaults). The load of the form is not changed.
- **New start-up errors** (only for platform apps): a missing Permissions key, a Procedure tab, a Computed field over a non-number column, an effect cycle, a table name with no schema, a duplicate manifest Code. TTDV2 passes: `--atlas-check` gives 0 errors and 0 warnings.

## 7. Regression

| Check | Result |
|---|---|
| `dotnet build` (warnings as errors) | 0 warnings, 0 errors |
| `dotnet test` | 294 passed (277 before; 17 new) |
| `--atlas-check` on the TTDV2 config | 0 errors, 0 warnings, exit 0 |
| `ng build` | Complete, no warnings |
| `ng test` (Vitest) | 73 passed (66 before; 7 new) |
| `npm run check:target-flags` | No errors |
| SQL scripts on SQL Server | `CopyScriptTests`, `ReleaseScriptTests`, `Ttdv2AppScriptTests`: 17 passed |

Not run: the target package build. `make-target-package.py` refuses uncommitted changes, so it runs after the commit.

## 8. Files changed

| Area | Files |
|---|---|
| Service | `Application/ConfigSectionService.cs` (RV-02, RV-04, RV-L01, RV-L06); `Platform/Audit/AtlasAuditMask.cs` (new, RV-02); `Platform/Registry/AtlasAppManifest.cs` (`ToLegacy`, RV-12); `Platform/Registry/AtlasConfigStore.cs` (RV-L05, RV-L09); `Platform/Security/AtlasAccessService.cs` (RV-17); `Platform/Security/AtlasUserContext.cs` (RV-08, RV-L07); `StandIn/CPAtlasService.cs` (reference lines, RV-11, RV-12) |
| Data | `Platform/AtlasPlatformContext.cs` (RV-03); `Platform/AtlasTable.cs` (RV-06) |
| App | `Platform/AtlasStartupCheck.cs` (RV-03, RV-05, RV-06, RV-N1, RV-L02, RV-L04, RV-L12); `Platform/AtlasConfigLint.cs` (RV-15); `Platform/AtlasPlatformStartup.cs` (RV-06, RV-07); `Platform/AtlasPlatformServiceCollectionExtensions.cs` (RV-L15) |
| WebUi | `platform/atlas-api.service.ts` (RV-01); `platform/atlas-navigation.ts` (`AtlasDashboardNavigation`, RV-11); `platform/form/atlas-form.component.ts` (RV-14, RV-15, RV-17, RV-L21); `platform/dashboard/atlas-dashboard-columns.ts` (RV-16); `platform/atlas-history-dialog.ts` (RV-L19); COPY headers of all `platform/` files (RV-13); `environments/environment.ts`, `app.ts` (stand-in, RV-01); `tsconfig.target-flags.json` (new), `package.json` (RV-L17) |
| SQL | `030`, `040`, `050` (RV-08, RV-09); `090-ttdv2-rollback.sql` (new, RV-10); module copies in `docs/atlas-platform/sql/`; `sql/release-p1/README.md` |
| Docs | `INTEGRATION.md` (RV-10, RV-11, RV-12, RV-13, RV-L16, RV-L17, RV-L18); `local/make-target-package.py` (RV-13); report 18 path (RV-13) |
| Tests | `AtlasCheckTests`, `AtlasPlatformContextTests`, `CopyScriptTests`, `ReleaseScriptTests`, `RegistryTests`, `Ttdv2SectionTests`, `platform.spec.ts`, `platform.component.spec.ts` |

## 9. Next steps

1. Done 2026-10-31: the target is upgraded to .NET 10 (RV-13 answered).
2. Commit this wave; rebuild the target package (`python atlas-platform/local/make-target-package.py <out.ps1>`).
3. At the target, before `030`: run the count query in section 6.
4. One-time integration in the dev region (INTEGRATION.md), with the smoke test of the current apps before and after.
5. Before app #2: RV-N1 (tab procedures), RV-07 retry, RV-L03.
