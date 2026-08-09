# Align Workflow Database Configuration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every normal local Console and Workflow API launch resolve `ConnectionStrings:DefaultConnection` to the same `workflowengine` SQL Server database without an environment override.

**Architecture:** Keep standard .NET configuration precedence and place the approved integrated-security connection string in both hosts' base and Development JSON. Pin both API launch profiles to the same value, align current operational documentation, and retain isolated test, CI, EF design-time, and historical database references.

**Tech Stack:** .NET 10 configuration, ASP.NET Core launch profiles, JSON, PowerShell, SQL Server

---

### Task 1: Align active Console and Workflow API configuration

**Files:**
- Modify: `src/WorkflowEngine.Console/appsettings.json`
- Modify: `src/WorkflowEngine.Console/appsettings.Development.json`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/appsettings.json`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/appsettings.Development.json`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Properties/launchSettings.json`

- [ ] **Step 1: Add the shared connection string to Console base configuration**

Make `src/WorkflowEngine.Console/appsettings.json` begin with:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False"
  },
  "WorkflowEngine": {},
  "Notifications": {
    "Sender": "Console"
  }
}
```

- [ ] **Step 2: Align Console Development configuration**

Set `ConnectionStrings:DefaultConnection` in `src/WorkflowEngine.Console/appsettings.Development.json` to:

```json
"Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False"
```

- [ ] **Step 3: Add the shared connection string to API base configuration**

Add this object at the top of `workflow-api/src/WorkflowEngine.WorkflowApi/appsettings.json`, preserving all existing CORS, notification, actor-header, logging, and host settings:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False"
}
```

- [ ] **Step 4: Align API Development configuration**

Set `ConnectionStrings:DefaultConnection` in `workflow-api/src/WorkflowEngine.WorkflowApi/appsettings.Development.json` to:

```json
"Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False"
```

- [ ] **Step 5: Pin both API launch profiles to the shared database**

Add this environment variable to the `http` and `https` profiles in `workflow-api/src/WorkflowEngine.WorkflowApi/Properties/launchSettings.json`:

```json
"ConnectionStrings__DefaultConnection": "Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False"
```

Preserve the existing environment, SMTP, URL, and command settings.

- [ ] **Step 6: Validate all changed JSON and active runtime references**

Run:

```powershell
$configPaths = @(
  'src\WorkflowEngine.Console\appsettings.json',
  'src\WorkflowEngine.Console\appsettings.Development.json',
  'workflow-api\src\WorkflowEngine.WorkflowApi\appsettings.json',
  'workflow-api\src\WorkflowEngine.WorkflowApi\appsettings.Development.json',
  'workflow-api\src\WorkflowEngine.WorkflowApi\Properties\launchSettings.json'
)
foreach ($configPath in $configPaths) {
  Get-Content -Raw $configPath | ConvertFrom-Json | Out-Null
}
git grep -n 'cdt_workflow_api_20260527' -- src workflow-api
```

Expected: JSON parsing succeeds; the search returns no matches.

- [ ] **Step 7: Commit active runtime configuration**

```powershell
git add -- src\WorkflowEngine.Console\appsettings.json src\WorkflowEngine.Console\appsettings.Development.json workflow-api\src\WorkflowEngine.WorkflowApi\appsettings.json workflow-api\src\WorkflowEngine.WorkflowApi\appsettings.Development.json workflow-api\src\WorkflowEngine.WorkflowApi\Properties\launchSettings.json
git commit -m "chore: align workflow database configuration"
```

### Task 2: Align current operational documentation

**Files:**
- Modify: `PROJECT.md`
- Modify: `README.md`

- [ ] **Step 1: Update the authoritative local SQL configuration**

Replace both current connection-string examples in `PROJECT.md` with:

```text
Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False
```

Do not alter test-isolation, CI, EF design-time, or historical planning references.

- [ ] **Step 2: Update current README setup and configuration examples**

Replace the current local verification string and the JSON configuration example in `README.md` with:

```text
Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False
```

- [ ] **Step 3: Verify current operational surfaces contain no obsolete database name**

Run:

```powershell
git grep -n 'cdt_workflow_api_20260527' -- PROJECT.md README.md src workflow-api
```

Expected: no matches.

- [ ] **Step 4: Commit current documentation alignment**

```powershell
git add -- PROJECT.md README.md
git commit -m "docs: align workflow database guidance"
```

### Task 3: Verify default resolution without creating scenario data

**Files:**
- No source-file changes

- [ ] **Step 1: Build both solutions with warnings as errors**

Run sequentially:

```powershell
dotnet build src\WorkflowEngine.sln -m:1 -warnaserror /p:UseAppHost=false --no-restore
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 -warnaserror /p:UseAppHost=false --no-restore
```

Expected: both builds succeed with 0 warnings and 0 errors. Stop only the verified Workflow API process if it locks API output files, then retry.

- [ ] **Step 2: Verify Console default configuration**

In a shell with no `ConnectionStrings__DefaultConnection`, run:

```powershell
Remove-Item Env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
dotnet run --project src\WorkflowEngine.Console --no-restore -- verify-seed
```

Expected: `Seed verification passed.` This proves the Console base configuration reaches the seeded `workflowengine` database without an override.

- [ ] **Step 3: Restart API through its normal launch profile**

Stop only the process currently listening on local port 5058 after verifying it is `WorkflowEngine.WorkflowApi.dll`. Start:

```powershell
Remove-Item Env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
dotnet run --project workflow-api\src\WorkflowEngine.WorkflowApi --no-build --launch-profile http
```

Keep the process running for the user-directed live scenario.

- [ ] **Step 4: Verify API health and resolved SQL database**

Confirm `http://localhost:5058/` and `http://localhost:5058/swagger/v1/swagger.json` return HTTP 200. Make a read-only request for a nonexistent workflow context if needed to open the SQL connection, then inspect SQL Server sessions for the API process and confirm `DB_NAME(database_id) = workflowengine`.

- [ ] **Step 5: Confirm the live test remains clean**

Run read-only SQL counts for `WorkflowInstance`, `WorkflowParticipant`, `WorkflowTask`, `WorkflowTaskSlaSchedule`, `NotificationOutbox`, `WorkflowAudit`, and `IdempotencyRecord` in `workflowengine`.

Expected: all counts are zero.

Query `http://localhost:5000/api/Messages`.

Expected: smtp4dev message count is zero.

- [ ] **Step 6: Run final repository checks**

```powershell
git diff --check
git status --short --branch
git log -3 --oneline
```

Expected: no whitespace errors; only pre-existing local `CODEX_HANDOFF.md` and untracked directories remain outside the committed change.

