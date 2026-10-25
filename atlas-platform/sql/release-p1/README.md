# sql/release-p1

Run order for release P1 (spec 12, section 4). Each script is additive, idempotent and ends with the script log block.
Run with SSMS or `sqlcmd -I` (the scripts also set `QUOTED_IDENTIFIER ON`, finding B12).

| # | Script | Placeholders | Source |
|---|---|---|---|
| 010 | Engine idempotent migrations (H9) | — | Made by the target team: `dotnet ef migrations script --idempotent` from the engine. Not in this folder. |
| 020 | `020-script-log.sql` | — | `docs/atlas-platform/sql/11-script-log.sql` |
| 030 | `030-active-user-roles-view.sql` | — | `docs/atlas-platform/sql/06-active-user-roles-view.sql` |
| 040 | `040-ttdv2-app.sql` | `<RUN_BY>` | `docs/atlas-platform/sql/12-ttdv2-app.sql` |
| 050 | `050-ttdv2-copy-from-ttd.sql` (change-over day only) | `<SERVER_TIME_ZONE>`, `<RUN_BY>` | `docs/atlas-platform/sql/12-ttdv2-copy-from-ttd.sql` |

The copies are the same as the module scripts (a test compares them). Change the module script, then copy it here.
