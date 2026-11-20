# sql/release-p2

Run order for release P2, the platform app ClientInflows (`CFPINFLOW`). Design: `docs/atlas-platform/25-client-inflows-p2-design.md`,
section 4. Each script is additive, idempotent and writes its `CP_Atlas_ScriptLog` row (release `P2`) inside its main
transaction. Run with SSMS or `sqlcmd -I -b`. Run the P1 scripts `020` and `030` first (script log and active roles
view).

| # | Script | Database | Placeholders |
|---|---|---|---|
| 100 | `100-cfp-inflow-columns.sql`: new columns of `CFP.Inflow`; `Description` and `Comment` to `nvarchar(max)` | CP Atlas | — |
| 110 | `110-cfp-inflow-attachment-columns.sql`: new columns and index of `CFP.InflowAttachment`; `FileName` to `nvarchar(1000)` | CP Atlas | — |
| 120 | `120-cfp-audit-log.sql`: new table `CFP.AuditLog` | CP Atlas | — |
| 130 | `130-cfpinflow-app.sql`: app row `CFPINFLOW` and the roles `Read` and `Write` | CP Atlas | `<RUN_BY>` |
| 140 | `140-ain-account-inventory-rmcc.sql`: new column `AIN.AccountInventory.RMCC`. The AIN team runs it or copies it. | CP Atlas | — |
| 150 | `150-cfpinflow-workflow.sql`: engine metadata of the workflow `ClientInflow` (states, actions, role `Write`, routes E1 to E3 with their texts, request-input recipient rules; no address) | Engine database (`ConnectionStrings:DefaultConnection`) | — |

`CFP.Inflow` and `CFP.InflowAttachment` are target tables. The scripts change them in place and stop (error 55100 or
55110) when a table is not there. No script removes or renames a column.

The role grants use the template `docs/atlas-platform/sql/06-grant-revoke-role-template.sql`. No grant is in a release
script.

Rollback: set `AppSettings:Modules:CFPINFLOW:Enabled = false` and restart. The tables and the columns stay. Each
script header tells how to undo its change by hand.

This folder is the only copy of these scripts: the tests run these files. Change them here.
