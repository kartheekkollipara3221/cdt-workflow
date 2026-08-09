# Align Workflow Database Configuration Design

## Goal

Make every normal local launch path for the Workflow Console and Workflow API use the same SQL Server database, `workflowengine`, without requiring a shell-level connection-string override.

## Approved scope

Update the active local runtime configuration surfaces:

- Console `appsettings.json`
- Console `appsettings.Development.json`
- Workflow API `appsettings.json`
- Workflow API `appsettings.Development.json`
- Workflow API `http` and `https` launch profiles
- Current database guidance in `PROJECT.md`
- Current database guidance in `README.md`

The shared value is:

```text
Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False
```

## Deliberate exclusions

Do not redirect these paths to the live `workflowengine` database:

- Automated tests, which derive isolated per-suite databases and may create or delete them.
- GitHub Actions, which runs against its own SQL Server service and cannot access the local `DOLLY` instance.
- The EF Core design-time fallback database, which prevents design-time tooling from unintentionally targeting the live scenario database.
- Historical plans, handoffs, and archived planning artifacts, which record past execution rather than control current runtime configuration.

No application code, API contract, workflow metadata, database schema, seed data, or SMTP configuration changes are included.

## Configuration behavior

.NET configuration precedence remains unchanged: command-line values and environment variables can still override JSON configuration when an operator intentionally supplies them. Without an override:

1. Console uses `workflowengine` in both the default and Development environments.
2. Workflow API uses `workflowengine` in both the default and Development environments.
3. API IDE or `dotnet run` launches through either checked-in launch profile explicitly select `workflowengine`.

Keeping the same value at these active layers prevents ordinary local launch methods from drifting to different databases.

## Verification

Verification must not create a workflow instance or send an email.

1. Parse all changed JSON files to prove valid syntax.
2. Search active runtime configuration and current operational documentation to confirm the obsolete local database name is absent.
3. Stop the currently running API process, then start the API through its normal `http` launch profile without a connection-string environment override.
4. Confirm the API listens on port 5058 and returns HTTP 200 for its Swagger root/OpenAPI document.
5. Run Console `verify-seed` in the Development environment without a connection-string environment override.
6. Use SQL inspection to confirm `workflowengine` still has zero runtime workflow rows and the smtp4dev mailbox remains empty.
7. Run warnings-as-errors builds for the Console/shared solution and Workflow API solution. This is a configuration-only change, so full workflow scenario execution is intentionally deferred to the user-directed live test.

## Failure handling

- If either host resolves a different database, stop before running a live scenario and inspect configuration-provider precedence.
- If the API cannot restart, restore service availability before continuing.
- If verification creates workflow or notification data unexpectedly, report it immediately and do not delete it without explicit approval.

