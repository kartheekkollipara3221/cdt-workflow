# Run TTDV2 on this computer

Status: C6 (database and API). C9 adds the browser steps.

Needs: .NET SDK 10, Node 24 and Angular CLI 21, SQL Server on `localhost` with Windows authentication, `sqlcmd` on the PATH.

## 1. Database

```powershell
powershell -ExecutionPolicy Bypass -File atlas-platform/local/setup-local-db.ps1            # add -Recreate to start again
```

It makes `CPAtlasPlatformDev`: the local copy of the `dbo.CP_Atlas_*` tables (`local/sql/000`), the release scripts
`sql/release-p1/020`-`040`, and `local/sql/900-local-seed.sql` (test users, roles, `dbo.Get_Application`, 5 sample time sheets).
Your Windows login gets the TTDV2 role `Owner`.

## 2. API

```powershell
dotnet run --project atlas-platform/src/BBH.CP.Atlas.App --launch-profile StandIn
```

- http://localhost:5080/swagger : the P1 endpoints (`v1/Application`, `v1/SectionPage`, `v1/ManagePage`, `v1/RemovePage`, `v1/AuditLog`, `v1/GetAuditLog`, `v1/authorize`).
- Authentication: `StandIn:Authentication = Fixed` (default): each request is the Windows login of the API process. `Negotiate`: Windows authentication in the browser.
- Act as a test user (Development only): send the header `X-Atlas-Act-As: ttd.read` (or `ttd.prep`, `ttd.prep2`, `ttd.owner`, `ttd.none`). The UI has a user switch (C9). `v1/standin/users` lists the users.
- `AccessMode` is `LogOnly` in `appsettings.Development.json`: a denied check writes `Atlas.AccessDenied` in the console and continues. Set `AppSettings:Modules:TTDV2:AccessMode = Enforce` to get HTTP 403.

| Test user | TTDV2 role |
|---|---|
| your Windows login | Owner |
| `ttd.owner` Olive Owner | Owner |
| `ttd.prep` Pat Preparer | Prepare |
| `ttd.prep2` Quinn Preparer | Prepare |
| `ttd.read` Riley Reader | Read |
| `ttd.none` Nora Norole | none |

## 3. UI

(C9) `npm install` and `npm start` in `atlas-platform/WebUi`; open http://localhost:4200.

Stand-ins: PB accounts = 5 sample accounts; Active Directory = the Windows login + `StandIn:Users` settings; TPAM = the local connection string.
