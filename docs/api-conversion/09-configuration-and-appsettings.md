# Configuration and appsettings Migration

This document maps console configuration to the external API configuration system.

## Current Configuration Files

Current console files:

- `src/WorkflowEngine.Console/appsettings.json`
- `src/WorkflowEngine.Console/appsettings.Development.json`

Current committed `appsettings.json`:

```json
{
  "WorkflowEngine": {},
  "Notifications": {
    "Sender": "Console"
  }
}
```

Current committed `appsettings.Development.json`:

```json
{
  "WorkflowEngine": {}
}
```

The connection string is expected from configuration/environment, especially `ConnectionStrings:DefaultConnection` or `ConnectionStrings__DefaultConnection`.

## Configuration Table

| Current Configuration | Current Location | API Location | Required? | Notes |
|---|---|---|---|---|
| `ConnectionStrings:DefaultConnection` | Environment or local config | API `appsettings*.json`, secrets, environment | Yes | Required by `AddWorkflowInfrastructure`. |
| `Notifications:Sender` | Console `appsettings.json` | API config | Yes | Defaults to `Console` if missing, but configure deliberately. |
| `Smtp:Host` | Config/environment | API secrets/environment | Required for SMTP | Fails startup in SMTP mode if missing. |
| `Smtp:Port` | Config/environment | API config | Optional | Defaults to `25` if not parseable. |
| `Smtp:From` | Config/environment | API secrets/environment | Required for SMTP | Fails startup in SMTP mode if missing. |
| `Smtp:RecipientOverride` | Config/environment | API non-prod config | Optional | Redirects all mail to one address for smoke tests. |
| `Smtp:RoleEmailMap` | Config/environment | API production config/secrets | Required without override | Maps workflow roles to email addresses. |
| `WorkflowEngine` | Console config | API config | Optional currently | `WorkflowEngineOptions.SectionName` exists but has no properties yet. |
| `WORKFLOW_ENGINE_TEST_CONNECTION_STRING` | Test environment | API test environment | Test only | Used by current test helper. |
| `ConnectionStrings__DefaultConnection` | Environment | API environment | Yes for deployment | Standard .NET env var form. |
| `--reset` | CLI flag | Admin-only route/body if allowed | No normal user use | Demo-only in console. |
| `--admin-override` | CLI flag | Admin-only policy if allowed | No normal user use | Bypasses role auth only. |
| `--business-object` | CLI override | Route/body value | Avoid silent override | API should validate route/body consistency. |
| `--workflow-instance` | CLI override | Route/body value | Avoid silent override | API should validate route/body consistency. |
| `--now` | CLI SLA option | Admin request or hosted service option | Optional | Use `TimeProvider` normally. |

## Recommended API appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "<SQL Server workflow database connection string>"
  },
  "WorkflowEngine": {},
  "Notifications": {
    "Sender": "NoOp"
  },
  "Smtp": {
    "Host": "",
    "Port": 25,
    "From": "",
    "RecipientOverride": "",
    "RoleEmailMap": {
      "Maker": "",
      "Checker": "",
      "TradingDesk": "",
      "BDRMClientService": ""
    }
  }
}
```

Use `NoOp` or a fake sender for local automated API tests. Use `Console` only if console output in the web host is deliberately accepted for development diagnostics.

## Development Configuration

Development can use:

- Local SQL Server.
- `Notifications:Sender = NoOp` or `Console`.
- `Smtp:RecipientOverride` when testing SMTP.

Do not commit passwords or developer-specific secrets. Use user secrets or environment variables.

## Test Configuration

For current workflow test project:

- `WORKFLOW_ENGINE_TEST_CONNECTION_STRING`
- or `ConnectionStrings__DefaultConnection`

For external API tests:

- Use a dedicated test database.
- Use `NoOp` or fake notification sender.
- Avoid sending real email.
- Run SQL-backed tests against SQL Server, not EF Core InMemory.

## Production Configuration

Production must provide:

- `ConnectionStrings__DefaultConnection`
- `Notifications__Sender`
- If SMTP: `Smtp__Host`, `Smtp__From`, and `Smtp__RoleEmailMap__<Role>`

Example environment variables:

```text
ConnectionStrings__DefaultConnection=Server=...;Database=...;...
Notifications__Sender=Smtp
Smtp__Host=smtp-relay.example.local
Smtp__Port=25
Smtp__From=workflow@example.com
Smtp__RoleEmailMap__Maker=maker-team@example.com
Smtp__RoleEmailMap__Checker=checker-team@example.com
Smtp__RoleEmailMap__TradingDesk=trading-desk@example.com
Smtp__RoleEmailMap__BDRMClientService=bdrm-client-service@example.com
```

## Startup Failures

The API should fail at startup when:

- `ConnectionStrings:DefaultConnection` is missing.
- `Notifications:Sender` is unknown.
- `Notifications:Sender = Smtp` and `Smtp:Host` is missing.
- `Notifications:Sender = Smtp` and `Smtp:From` is missing.

The current `AddWorkflowInfrastructure` enforces these checks.

## Deployment Validation

Before deployment:

- Confirm SQL Server connectivity.
- Apply migrations.
- Run seed.
- Run seed verification.
- Confirm notification sender selection.
- Confirm SMTP mapping for every role that can receive notifications.
- Confirm no real SMTP is configured in test environments unless deliberately testing against a fake relay.

## Must Not Be Hardcoded

- SQL connection strings.
- SMTP host/from/recipients.
- User IDs.
- Workflow roles for specific users.
- Portfolio Trade transitions in controllers.
- Environment-specific database names.

## Settings That Belong In The External API Repo

- API `appsettings.json` workflow sections.
- API `appsettings.Development.json` safe defaults.
- API deployment environment variables/secrets.
- Hosted service intervals if you add API background processing.
- Auth claims-to-workflow-role mapping settings if you make them configurable.

