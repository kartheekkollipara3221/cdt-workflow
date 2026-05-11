# Defect 03: SMTP Configuration Validation Is Lazy

Status: Fixed in working tree

Severity: Major

Source review: `ARCHITECTURE_REVIEW.md`

## Summary

SMTP configuration validation happens only when `INotificationSender` is first resolved. The console app can start and run commands without resolving the sender, even when `Notifications:Sender = Smtp` and SMTP settings are invalid.

Bad SMTP configuration should fail at startup in SMTP mode, not later when `process-notification-outbox` first attempts to resolve the SMTP sender.

## Production Scenario

Production is configured for SMTP delivery:

```json
{
  "Notifications": {
    "Sender": "Smtp"
  },
  "Smtp": {
    "Host": "",
    "From": "workflow@example.com",
    "RoleEmailMap": {
      "Maker": "maker-team@example.com",
      "Checker": "checker-team@example.com",
      "TradingDesk": "trading-team@example.com",
      "BDRMClientService": "bdrm-team@example.com"
    }
  }
}
```

`Smtp:Host` is missing or empty.

The app builds the service provider successfully because `INotificationSender` is registered through a singleton factory and is not constructed until first use. Operators run smoke commands such as:

```text
verify-seed
show-context
show
```

Those commands may pass because they do not require notification delivery.

Real workflow activity then starts:

```text
Maker submits to Checker
Checker sends to Trading Desk
BDRM routes work back to Trading Desk Rework
Workflow completes
```

Notification outbox rows are queued. Later, `process-notification-outbox` resolves `INotificationSender`, constructs `SmtpNotificationSender`, and finally throws because SMTP configuration is invalid.

## Real-Time Impact

The system discovers a deployment/configuration problem too late:

- Deployment appears healthy.
- Workflow actions already happened.
- Notification rows accumulate as `Pending`.
- Users do not receive expected handoff emails.
- Task assignment and SLA visibility can be delayed.
- Operators learn about the problem only when notification processing runs.
- A preventable startup/configuration issue becomes a production incident.

For this workflow, notifications are part of operational handoff visibility:

```text
Maker submits -> Checker notified
Checker sends to Trading Desk -> Trading Desk notified
Trading Desk executes -> BDRM Client Service notified
BDRM rejects execution -> Trading Desk notified
Workflow completes -> Maker notified if configured
```

If SMTP delivery is configured, required SMTP settings must be validated before the app accepts normal operation.

## Code Evidence

Primary files:

```text
src/WorkflowEngine.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs
src/WorkflowEngine.Console/Program.cs
src/WorkflowEngine.Infrastructure/Notifications/SmtpNotificationSender.cs
```

Relevant flow:

```text
InfrastructureServiceCollectionExtensions.cs:60-61
  Registers INotificationSender as a singleton factory.

InfrastructureServiceCollectionExtensions.cs:70-73
  Creates SmtpNotificationSender only when the factory is invoked.

Program.cs:18-39
  Builds the service provider but does not eagerly resolve INotificationSender.

SmtpNotificationSender constructor
  Performs validation, but only after construction.
```

Current timing:

```text
app startup
  -> service provider built
  -> no SMTP validation necessarily happens

first sender resolution
  -> SMTP validation happens
```

Required timing:

```text
app startup with Notifications:Sender = Smtp
  -> SMTP validation happens immediately
  -> invalid config fails startup
```

## Required Fix

Ensure SMTP configuration is validated during startup when:

```text
Notifications:Sender = Smtp
```

Acceptable simple approaches:

- Eagerly resolve `INotificationSender` from the root provider or startup scope after `BuildServiceProvider` when SMTP mode is enabled.
- Add explicit notification configuration validation during infrastructure registration.
- Add a focused helper such as `ValidateNotificationConfiguration(configuration)` and call it from the composition root.

The mechanism is less important than the guarantee:

```text
Bad SMTP config fails before command dispatch, not at first notification processing.
```

## Required Validation

At minimum, fail startup in SMTP mode when required values are missing or empty:

- `Smtp:Host`
- `Smtp:From`

Also validate role recipient configuration according to existing SMTP behavior:

- If using `Smtp:RecipientOverride`, role map gaps may be acceptable for smoke testing.
- Without `Smtp:RecipientOverride`, missing required `Smtp:RoleEmailMap` entries should fail before sending can silently stall or throw during processing.

Use existing project rules:

- Role recipients come from configuration only.
- Do not add SQL role-recipient lookup tables.
- Do not add SMTP credential support.
- Do not enable real SMTP in dev/test defaults.

## Important Constraints

- Do not hardcode SMTP delivery into workflow execution.
- Keep `INotificationSender` abstraction.
- Keep Console and NoOp sender modes working.
- Do not require SMTP settings when `Notifications:Sender` is `Console` or `NoOp`.
- Do not change notification outbox persistence semantics.
- Do not add third-party packages beyond those already approved by `PROJECT.md`.
- Do not introduce hosted services, background workers, message brokers, or plugin systems for this fix.

## Acceptance Criteria

Add or update tests proving:

- `Notifications:Sender = Smtp` with missing `Smtp:Host` fails during startup/service-provider initialization path, before command dispatch.
- `Notifications:Sender = Smtp` with missing `Smtp:From` fails during startup/service-provider initialization path.
- Valid SMTP configuration starts successfully.
- `Notifications:Sender = Console` does not require SMTP settings.
- `Notifications:Sender = NoOp` does not require SMTP settings.
- Unknown `Notifications:Sender` still fails clearly.
- Existing `process-notification-outbox` behavior still uses `INotificationSender`.

## Suggested Verification Commands

```bash
dotnet test src/WorkflowEngine.sln
```

If running SMTP Tier 2 tests separately:

```bash
dotnet test src/WorkflowEngine.sln --filter Category=Smtp
```

Do not make Tier 2 SMTP tests run by default.
