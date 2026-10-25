# Run TTDV2 on this computer

Status: complete (C10). Database, API and UI.

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

```powershell
cd atlas-platform/WebUi
npm install        # first time only
npm start          # ng serve on http://localhost:4200
```

Open http://localhost:4200. The header has the app menu and the **Act as** switch (test users; Development only).

What to try:

1. Dashboard `Tax Time Database`: 3 tabs from the table (no procedure); the All Report has a totals row; the row menus come from `Permissions`.
2. **Add New** opens `form/TTDV2/0`: defaults (this tax year, last Sunday, you as preparer); choose an account and the account number and name fill in; the weekly total changes while you type; `1441` gives a field error.
3. Save, open the record again, change a value, save, then **History**: `Create` and `Update` with old and new values.
4. Act as `Riley Reader` (Read): no Add button, the form is read-only. Act as `Pat Preparer`: edit own records only. Act as `Nora Norole`: the menu is empty.
5. Open the same record in two tabs, save in both: the second save gets the conflict message.

## 4. Tests and the release check

```powershell
dotnet test atlas-platform/AtlasPlatform.slnx                 # all .NET tests; the SQL tests make and drop their own database
cd atlas-platform/WebUi; npx ng test --watch=false             # 13 tests
dotnet run --project atlas-platform/src/BBH.CP.Atlas.App --launch-profile StandIn -- --atlas-check   # exit code 0
```

Stand-ins: PB accounts = 5 sample accounts; Active Directory = the Windows login + `StandIn:Users` settings; TPAM = the local connection string.
