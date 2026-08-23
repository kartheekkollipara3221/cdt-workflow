# 30. Deliver SQMS outbox messages through SMTP

## Contract status

**Implemented:** immutable outbox snapshots, `process-notification-outbox`, sender selection, and SMTP delivery. **Target/operator implementation required:** approved configuration, job hosting, local test relay lifecycle, monitoring, and production relay approval. This pack does not start smtp4dev and does not claim a production relay is configured.

## Command

After messages have been queued, run the existing console command:

```powershell
dotnet run --project src/WorkflowEngine.Console -- process-notification-outbox
```

For local testing, configure `Notifications__Sender=Smtp`, `Smtp__Host`, `Smtp__Port`, and `Smtp__From` for an already-running local smtp4dev instance. Use `Smtp__RecipientOverride` when required by the test environment. Never point test configuration at a production relay.

## Immutable delivery contract

The processor reads `NotificationRecipientsJson`, `Subject`, `Body`, `HtmlBody`, and `PayloadJson` from the queued row. It does not re-resolve current metadata or target recipients at send time. To/Cc therefore remain the exact immutable queue-time snapshot.

## Six SQMS routes

- `SqmsOpportunityOpen`
- `SqmsOpportunityReminder`
- `SqmsOpportunityPendingSellerApproval`
- `SqmsBidAccepted`
- `SqmsBidPartiallyAccepted`
- `SqmsBidNotAccepted`

`NOT ALLOCATED` classification has no disposition email.

## Success and retry behavior

A first-attempt successful delivery ends with `Status = Sent`, `RetryCount = 0`, non-null `SentAtUtc`, null `LastError`, and cleared processing lease fields. If sending throws, the row returns to `Pending`, increments `RetryCount`, stores `LastError`, and the command stops after reporting the failure; this pack's success verifier expects retry zero.

## smtp4dev inspection

Open the UI belonging to the already-running isolated smtp4dev instance (the common test default is `http://localhost:5000`; retained acceptance profiles may use another documented port). Inspect one captured message per expected route and compare subject, To, Cc, plain body, HTML body, and links with the persisted snapshot. Database state alone proves outbox processing, not mailbox receipt.

## Production boundary

Production relay hostname, credentials, TLS policy, firewall access, sender authorization, retry operations, and monitoring require the deployment team's approval and are not established by this documentation.
