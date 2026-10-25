# Run TTDV2 on this computer (outline)

Status: outline (C0). C6 adds the database and the API steps; C9 adds the browser steps.

Needs: .NET SDK 10, Node 24 and Angular CLI 21, SQL Server on `localhost` with Windows authentication.

Planned steps:

1. Make the local database: run `local/setup-local-db.sql` (C6). It makes `CPAtlasPlatformDev`, runs the P1 scripts and adds the test users.
2. Start the API: `dotnet run --project atlas-platform/src/BBH.CP.Atlas.App` (C6). Swagger shows the TTDV2 endpoints.
3. Start the UI: `npm install` and `ng serve` in `atlas-platform/WebUi` (C9). Open the TTDV2 dashboard.
4. Act as another test user: development-only user switch (C6).

Stand-ins: PB accounts = sample accounts; Active Directory = the Windows login + `StandIn:Users` settings; TPAM = the local connection string.
