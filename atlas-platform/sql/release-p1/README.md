# sql/release-p1

Run order for release P1 (spec 12, section 4). Each script is additive, idempotent and ends with the script log block.
Run with SSMS or `sqlcmd -I -b` (the scripts also set `QUOTED_IDENTIFIER ON`, finding B12; `-b` stops at the first
error). Each script writes its `CP_Atlas_ScriptLog` row inside its main transaction, so a failed script has no log row
also when the tool goes on to the next batch (review DA-07). `050` stops with error 55007 and changes nothing if a user
saved in TTD during the run: run it again.

| # | Script | Placeholders |
|---|---|---|
| 010 | Engine idempotent migrations (H9): made by the target team with `dotnet ef migrations script --idempotent` from the engine. Not in this folder. | — |
| 020 | `020-script-log.sql` | — |
| 030 | `030-active-user-roles-view.sql` | — |
| 040 | `040-ttdv2-app.sql` | `<RUN_BY>` |
| 050 | `050-ttdv2-copy-from-ttd.sql` (change-over day only) | `<SERVER_TIME_ZONE>`, `<RUN_BY>` |
| 090 | `090-ttdv2-rollback.sql` (rollback only, not in the run order) | `<RUN_BY>`, `<COPY_RUN_BY>` |

This folder is the only copy of these scripts (FABLE review 4.3): the specs link here and the tests run these files. Change them here.

Rollback (review 3 RV-10): set `AppSettings:Modules:TTDV2:Enabled = false`, then run `090`. It gives back the TTD roles
that `050` revoked and soft-deletes the TTDV2 app row and roles, so the menu has no TTDV2 row also after a code revert.
It keeps the TTDV2 tables. A second run changes nothing. `040` stops (error 55010) while the deleted TTDV2 row is there:
to install again, set `IsDeleted = 0` on the app row and its roles first.
