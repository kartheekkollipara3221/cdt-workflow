# C13: bounded notification processing and retry scheduling

Active workflows do not need to be closed. The upgrade adds a nullable outbox timestamp and an index. It does not reset workflows, tasks, recipient snapshots or queued messages.

## Enable the controls

Merge this section into each notification consumer's configuration after the schema and binaries are upgraded:

```json
{
  "Notifications": {
    "Outbox": {
      "BatchSize": 100,
      "RetryInitialDelaySeconds": 60,
      "RetryMaxDelaySeconds": 3600
    }
  }
}
```

`AddWorkflowInfrastructure(configuration)` validates and registers these settings. Existing three-argument constructors still work with legacy defaults. Integrations constructing services themselves must use the new overload with `OutboxProcessingOptions` to enable controls. Restart consumers after configuration changes; options are loaded at registration.

| Setting | Omitted default | Meaning |
|---|---|---|
| BatchSize | Unlimited | Maximum eligible candidates per backlog call; must be positive when supplied |
| RetryInitialDelaySeconds | 0 | Zero retains immediate retries; a positive integer enables exponential delay |
| RetryMaxDelaySeconds | 3600 | Caps delay; must be non-negative and at least the initial delay |

With the recommended settings, successive failures wait 1, 2, 4, 8, 16, 32, then 60 minutes between attempts. Later failures also wait 60 minutes. Existing `RetryCount` contributes to the delay; an old row with many failures starts at the cap on its next failure. The delay starts when claim release is attempted after a failure, using `TimeProvider`.

New and existing null `NextAttemptAtUtc` values are immediately eligible. A non-null timestamp is always honored by upgraded consumers, even if their retry-delay configuration is disabled. This prevents a legacy-configured upgraded worker from bypassing a delay written by another worker. Successful delivery clears the timestamp. No maximum attempts, dead-letter status, deletion or automatic recipient repair is introduced.

Backlog runs select oldest eligible rows by `CreatedAtUtc`, then `Id`. Deferred rows do not consume batch slots. Selection loads only candidate identifiers and summary fields; bodies and recipients are loaded after ownership is claimed. Active-claim diagnostic warnings are also capped by BatchSize. `PendingCount` is the number of candidates selected for that call, not the total backlog. A concurrent claim can reduce actual sends below this count. Poll repeatedly through the existing scheduler/command; this change does not add a scheduler or drain loop.

The API's synchronous dispatcher remains scoped to the action's workflow and correlation ID. BatchSize does not truncate that action's delivery or change its success contract. It respects the same durable retry dates and writes the same delay after failure; a deferred pending notification returns the existing failure result. Both consumers recheck eligibility in their atomic claim updates.

## Deployment sequence

1. Inventory every notification consumer, including target API dispatchers, scheduled processors and console workers. Upgrade integrations that copy the outbox model/dispatcher as well as those referencing the library. The read-only target snapshot in this repository is not a deployed implementation.
2. Pause notification consumers and allow in-flight sends to finish. If inline API sending cannot be paused independently, briefly pause those requests. Keep active workflow records intact. A queued backlog can remain.
3. Back up the database and apply the additive schema upgrade to the intended database. Target databases managed with manual SQL use [20261014_outbox_retry_scheduling.sql](../sql/upgrades/20261014_outbox_retry_scheduling.sql). It is transactional and rerunnable, checks the column/index shape, and does not write EF migration history. Index creation can take locks; allow a maintenance window appropriate to table size.
4. EF-managed databases instead apply migration `20261014222530_AddOutboxRetryScheduling` using their normal migration process. Choose one schema-management path. Do not subsequently replay the EF migration over a manually applied column/index without reconciling migration history through the owning deployment process. Do not run `init-db` on production.
5. Deploy all compatible consumers, apply the recommended configuration, and resume processing. Check `Pending`, `Processing`, `RetryCount`, `LastError` and `NextAttemptAtUtc`. Confirm healthy mail continues while failed mail waits for its next attempt. Old binaries ignore retry timestamps; do not run old and new delivery consumers together once delays are enabled.

No production migration or deployment is performed by the code change or its test suite.

## Rollback and delivery limits

Keep the additive column and index during an application rollback. Disabling retry delays changes future failures to immediate retry but does not erase already scheduled timestamps. Old binaries ignore those timestamps and restore the previous immediate-retry behavior. Coordinate rollback across consumers and watch delivery load. Do not use the EF Down migration as the normal production rollback; it removes persisted retry timing.

SMTP delivery remains at least once. If SMTP accepts a message but the sender sees a connection failure, or persisting `Sent` fails, a later attempt may send it again. The existing 15-minute processing lease is unchanged; an operation exceeding that lease may also be reclaimed. This change does not provide exactly-once delivery or a lease heartbeat. Database failures while releasing a claim can still abort a processing run; a later run can recover expired claims. Failed recipients remain queued until repaired through an approved operational process.

## Verification scope

SQL-backed tests cover batch boundaries/order, deferred-row fairness, exponential delay/cap, new-context persistence, retry-at-boundary recovery, an eligibility change between selection and claim, concurrent ownership, bounded active-claim warnings, lease expiry, API/background cooperation, and data-preserving EF/manual upgrades (manual SQL applied twice). A sender test double simulates acceptance followed by an error to document possible duplicate delivery; this is not a real SMTP crash/acknowledgement experiment. Existing local SMTP tests verify message delivery and recipient/body behavior.
