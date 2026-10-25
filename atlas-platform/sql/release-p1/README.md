# sql/release-p1

Run order for release P1 (spec 12, section 4). C10 puts the numbered copies here:

| # | Script | Source |
|---|---|---|
| 010 | Engine idempotent migrations (H9) | made by the target team (`dotnet ef migrations script --idempotent`) |
| 020 | Script log table | `docs/atlas-platform/sql/11-script-log.sql` |
| 030 | Active user roles view | `docs/atlas-platform/sql/06-active-user-roles-view.sql` |
| 040 | TTDV2 app | `docs/atlas-platform/sql/12-ttdv2-app.sql` |
| 050 | Copy TTD to TTDV2 (change-over day only) | `docs/atlas-platform/sql/12-ttdv2-copy-from-ttd.sql` |
