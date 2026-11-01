# Module 11: Configuration and deployment

Status: Draft 2, 2026-10-25. All open questions answered (section 9). Design only. No target code is changed.

## 1. Purpose

The platform moves app behaviour into configuration (JSON, settings) and SQL scripts. Thus the configuration and the scripts must be deployed as carefully as the code. This module defines the configuration layers for each environment, the JSON load rules, the check before a release, the order of the SQL scripts, and the release plan for all modules.

Scope: `appsettings` files, the configuration folder, the JSON parse options, the start-up check in a pipeline, the engine migrations at the target (issue H9), the platform SQL scripts, the feature switches (`AccessMode`, `AuditMode`, `Enabled`), the logs, and the release order. Related items: the registry (module 7), the start-up check (module 7, 4.8), all SQL scripts of modules 1, 4, 5, 6 and 8.

## 2. Current behaviour

- `appsettings.json` holds the settings of one developer environment: test SQL Server and Oracle hosts, `localhost` URLs, absolute JSON paths on one computer, and an empty JWT key. `appsettings.Development.json` holds only the log levels. The copy has no QC or production file. Evidence: `Atlasapp/Atlasapp/appsettings.json.txt:1-60`; `appsettings.Development.json.txt`.
- The database password is not in the file. `TPAMAPIAccess.GetConnectionString` gets the connection (TPAM credential service). Evidence: `program.cs.txt:25-26`; `appsettings.json.txt:22-25`.
- The engine e-mail map `Smtp:RoleEmailMap:*` lists the addresses of two named persons for all CDT roles, in the base file. Evidence: `appsettings.json.txt:46-53`.
- The JSON files have `//` comments and trailing commas. `Newtonsoft.Json` accepts them. Evidence: `Config/CDT.json.txt:26`; `Config/SQMSO.json.txt:115`.
- log4net writes to fixed paths: `C:\Logs\CPAtlas\AppLog.log` and `D:\CPAtlasLogs\CPAtlas_Debug.log`. Evidence: `log4net.config.txt:3, 22`.
- The target database script (2026-10-06) does not have the two latest engine migrations: `20261014222530_AddOutboxRetryScheduling` and `20261018214009_AddCdtV2SlaReminderControls` (issue H9). Evidence: `CP_Sharepoint.txt`; `src/WorkflowEngine.Infrastructure/Migrations`.
- The client has `environment.ts` (production: `apiBaseUrl` = `/v1/`, same server) and `environment.development.ts` (`https://localhost:44399/v1/`). Evidence: `WebUi/WebUi/Source/Environments/`.

## 3. Findings

### CD-01 Environment values are in the base settings file (Medium)

The base `appsettings.json` has the test hosts, `localhost` URLs and developer paths. Any environment that does not override each key uses the test values. Nothing stops a production server from connecting to the test database if one key is missing.

### CD-02 Named persons receive all CDT role e-mails (Medium)

`Smtp:RoleEmailMap` sends the e-mails of all CDT roles (Checker, Trading Desk, Maker, ...) to two named persons, in the base file. If a QC or production file does not override each key, these persons get production e-mails. The map belongs to the engine configuration; the target team owns the values.

### CD-03 The engine migrations at the target are not current (High, issue H9)

The outbox retry (`NextAttemptAtUtc`) and the CDT v2 SLA reminder controls are not in the target database. Module 10 needs the outbox retry (question 2 of module 10). There is no defined step that applies the engine migrations at the target.

### CD-04 No order for the SQL scripts (Medium)

The modules deliver many additive scripts (roles view, LAN ID columns and backfill, rowversion, visibility template, grant/revoke, new-app template). Nothing records which script ran in which database, or in which order the scripts must run.

### CD-05 The JSON parse rules must stay the same (Medium)

The platform will load JSON with its own loader (module 7, 4.7). If the loader uses strict `System.Text.Json` rules, the current files with comments and trailing commas fail to load. The parse rules must stay as tolerant as `Newtonsoft.Json`.

### CD-06 Fixed log paths (Low)

The log paths are fixed in `log4net.config`. A server with no `D:` drive loses the debug log.

## 4. Reusable design

### 4.1 Configuration layers

| Layer | Holds | Owner |
|---|---|---|
| `appsettings.json` | Values that are the same in all environments: log levels, defaults, feature defaults. No host, no URL, no e-mail address. | Target team (code review) |
| `appsettings.{Development,QC,Production}.json` | Hosts, URLs, CORS origin, e-mail maps, `AppSettings:Modules:<Code>:*` overrides (`Enabled`, `AccessMode`, `AuditMode`) | Target team (code review); production values approved by the business owner |
| TPAM | Database and API credentials (no change) | Security |
| `Config/` folder | App manifests, form JSON, dashboard JSON (module 7) | Target team (code review) |

- The IIS application pool sets `ASPNETCORE_ENVIRONMENT` (`QC`, `Production`).
- A required environment value that is missing stops the start-up with a clear message (`AppSettings:HostedURL is not set for Production`). There is no fall-back to a test value. This removes CD-01.
- The `Smtp:RoleEmailMap` values move to the environment files. The base file keeps no address. This removes CD-02. (Engine configuration keys: the keys do not change, only the file that holds them.)

### 4.1.1 Team guide: environment files

Decision 2026-10-25 (question 1): today the values are changed on the server after each deployment. The target is the environment files of 4.1. The team does not know them yet, so this guide is part of the release P1 training.

How it works:

- ASP.NET Core reads `appsettings.json` first, then `appsettings.<Environment>.json`. A key in the environment file replaces the same key of the base file. A key that is not in the environment file keeps the base value.
- `<Environment>` is the value of `ASPNETCORE_ENVIRONMENT` on the server. On IIS, set it one time for each site, in `web.config`:

```xml
<aspNetCore processPath="dotnet" arguments=".\BBH.CP.Atlas.App.dll" hostingModel="inprocess">
  <environmentVariables>
    <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="QC" />
  </environmentVariables>
</aspNetCore>
```

- All files are in the release package. The same package goes to each server; only `ASPNETCORE_ENVIRONMENT` is different. Nobody changes a file on the server after the deployment.
- A change of a value is a change of the file in the repository, with a code review, and a new deployment.
- Secrets never go in these files. The database credentials stay in TPAM.

Example `appsettings.QC.json`:

```json
{
  "AppSettings": {
    "HostedURL": "https://qc-atlas.example.internal",
    "HostedClientURL": "https://qc-atlas.example.internal",
    "Modules": {
      "PBSP": { "Enabled": true, "AccessMode": "LogOnly" }
    }
  },
  "Smtp:RoleEmailMap:Checker": "qc-checker-group@example.internal"
}
```

Checklist for each release:

- The new key is in the base file (default) or in each environment file that needs a different value.
- No host, URL, e-mail address or secret is in the base file.
- `--atlas-check --environment <Env>` (4.3) passes for QC and Production.
- After the deployment, the start-up report in the log shows the environment name and the switch values (4.5).

Change-over from the current way (one time, in release P1):

1. Copy the values that are changed on each server today into `appsettings.QC.json` and `appsettings.Production.json` in the repository.
2. Add `ASPNETCORE_ENVIRONMENT` to `web.config` of each site.
3. Deploy the package with no manual change. Compare the start-up report with the old values.
4. From then on, no manual change on the server.

### 4.2 JSON parse rules

The platform loader uses `System.Text.Json` with `ReadCommentHandling = Skip`, `AllowTrailingCommas = true`, `PropertyNameCaseInsensitive = true`, and string enums by name. The current files load with no change. A test loads each current JSON file with the old and the new loader and compares the result. This removes CD-05.

### 4.3 Check before a release

- The start-up check (module 7, 4.8) can also run as a command: `BBH.CP.Atlas.App.exe --atlas-check --environment QC`. It loads the settings, the manifests, the JSON files and the database metadata, runs all checks, writes the report, and exits with code 0 (no errors) or 1 (errors). It does not start the web server.
- The release pipeline runs it for the target environment before the files are copied. A release with errors stops.

### 4.4 SQL scripts

- One release folder for each release: `sql/release-<n>/`. Each script has a number: `010-engine-migrations.sql`, `020-active-user-roles-view.sql`, `030-lanid-columns.sql`, ... The DBA runs them in number order.
- Each script is additive and idempotent (as all module scripts are today).
- Engine migrations: the target team makes an idempotent script from the engine migrations (`dotnet ef migrations script --idempotent`) and puts it first in the release folder. The API does not run migrations at start-up. Decision 2026-10-25 (question 2): the DBA runs the idempotent engine script as the first script of the release; the API login keeps no DDL right in QC and production (module 5, question 4). This removes CD-03.
- A script tracking table (decision 2026-10-25, question 3): `dbo.CP_Atlas_ScriptLog` (`ScriptName`, `Release`, `AppliedAtUtc`, `AppliedBy`). Script: [sql/release-p1/020-script-log.sql](../../atlas-platform/sql/release-p1/020-script-log.sql), run once in release P1. Each later platform script ends with the short "log this script" block from that file and writes one row. The API login has `SELECT` only. The start-up check warns if a script of the current release is not in the table. This removes CD-04.

### 4.5 Feature switches

| Switch | Values | Default | Module |
|---|---|---|---|
| `AccessMode` | `LogOnly`, `Enforce` | `LogOnly` | 6 |
| `AuditMode` | `Client`, `Server` | `Client` (existing), `Server` (new) | 9 |
| `RuleMode` | `FirstMatch`, `MostPermissive` | `FirstMatch` (existing), `MostPermissive` (TTD, new) | 4 |
| `RuleDefault` | `Keep`, `ReadOnly`, `Hidden` | `Keep` (existing), `ReadOnly` (new) | 4 |
| `Enabled` | `true`, `false` | `true` | 7 |
| `Load` (dashboard) | `Eager`, `Lazy` | `Eager` | 3 |

- Each switch is in the manifest and can be overridden for each environment (4.1).
- A rollback of a behaviour change is a change of a switch and a restart. No new build.
- The start-up report lists the value of each switch for each app.

### 4.6 Logs

- The log paths come from the settings (`Logging:File:Path`), not from fixed paths. This removes CD-06.
- The platform writes these log events with a fixed event name, so that support can search them: `Atlas.AccessDenied` (module 6), `Atlas.StartupCheck` (module 7), `Atlas.WorkflowSync` and `Atlas.WorkflowRefused` (module 10), `Atlas.AttachmentRejected` (module 8), `Atlas.AuditWriteFailed` (module 9).

### 4.7 Release plan

Decision 2026-10-25 (question 4): the pilot comes first. The platform is built **beside** the current code (a new generic form component and a generic section service), not inside it. The current Section Master component and the current services stay frozen until each app moves. A manifest setting selects the path for each app, so the rollback of an app is a change of its registration line and manifest.

All releases are backward compatible: the apps that stay on the current path do not change.

| Release | Content | Modules |
|---|---|---|
| P1 TTD pilot | Vertical slice for TTD only: registry and one registration line, generic section service, new form component, rules engine (`MostPermissive`, `Permissions`, `OwnerFields`), user context, active roles view, access check in `LogOnly`, shared-type data mapping, server audit with `Old`, TTD dashboard (rules before load), environment files, JSON loader, start-up check, script log. Comparison tests against the current `CPTTDService`. Not in P1: attachments, workflow adapter, relations, lazy load and server paging. | 7, 1, 4, 6, 5, 9, 3, 11 |
| P2 First new app | The first of the 13 apps on the same path: SQL template, manifest, JSON, one registration line. Count the steps and the time. | all |
| P3 Security fixes | Download from the database (H1), safe upload names (H2), no rows for hidden tabs in the current dashboard code (DB-01). | 8, 3 |
| P4 Migrate | SQMSO, SQMSB and AIN to the new path; engine result check (H8) and engine call last (H6) on the way; CDT fixes for H6 and H8. `Enforce` for each app after its log review. Then remove the old component. | 1, 10, 6 |
| P5 Later | Attachment service for new apps, workflow adapter for new workflow apps, `Lazy` load and server paging when the rows grow, module 2 (Step Master). | 8, 10, 3, 2 |

Accepted risk (decision 2026-10-25): H1 and H2 stay in production until P3, after P1 and P2. The decision owner accepted this order. Mitigation until P3: the support team checks the API log for `v1/Download` requests with `..`, `/` or `\` in `id` or `name`.

## 5. Backward compatibility

- The setting keys do not change; values move between files only.
- The JSON loader reads the current files with no change (4.2).
- The script log table is new; no existing table changes.
- The engine migrations are the engine's own additive migrations.
- Each release keeps the current behaviour until a switch changes it.

## 6. Migration steps

| Step | Change | Risk |
|---|---|---|
| 1 | Change-over to environment files (4.1.1): copy the server values into `appsettings.QC.json` and `appsettings.Production.json`; move the hosts, URLs and e-mail maps out of the base file; set `ASPNETCORE_ENVIRONMENT` in `web.config`; stop manual changes on the server. | Medium. Test each environment once; compare the start-up report. |
| 2 | Apply the missing engine migrations at the target with an idempotent script (DBA). | Medium. Back up first. |
| 3 | Add the script log table and the release folder rule. | Low |
| 4 | Add the `--atlas-check` command to the release pipeline. | Low |
| 5 | Move the log paths to the settings. | Low |

## 7. Acceptance

- A production start with no `AppSettings:HostedURL` stops with a clear message and does not use a test value.
- The base `appsettings.json` has no host name, no URL and no e-mail address.
- All current JSON files load with the new loader, and the result is the same as with `Newtonsoft.Json`.
- `--atlas-check` returns 1 for a manifest with no registration line, and 0 for the current apps.
- `dbo.CP_Atlas_ScriptLog` shows each script of a release after the DBA runs the folder.
- The target `__EFMigrationsHistory` lists all engine migrations of the engine version in use.

## 8. Notes

- The client has no per-app configuration: it gets all app data from the API (modules 3, 7). Its `environment.ts` files do not change.
- The engine configuration (`Smtp:*`, `Notifications:*`) keeps its keys; only the files change.

## 9. Open questions

1. Answered (2026-10-25): today the values are changed on the server after each deployment. The team must change to environment files (4.1); the team guide and the change-over steps are in 4.1.1.
2. Answered (2026-10-25): an idempotent SQL script from the engine migrations, run by the DBA as the first script of the release (4.4).
3. Answered (2026-10-25): yes. `dbo.CP_Atlas_ScriptLog`, script [sql/release-p1/020-script-log.sql](../../atlas-platform/sql/release-p1/020-script-log.sql) (4.4).
4. Answered (2026-10-25): changed order. TTD pilot first (P1), then the first new app (P2), then the security fixes H1, H2, DB-01 (P3), then the migration of the current apps (P4). The platform is built beside the current code (4.7).
