# 28. Disabled SQMS scheduler performs zero work

This pack invokes the implemented `ISqmsScheduledService`; it has no public HTTP route.

## Contract status

**Implemented:** `SqmsScheduledService.RunAsync` checks `Sqms.Enabled` before resolving the time zone or reading any target table. **Target implementation required:** deployment configuration and operator scheduling.

## Configuration

```text
Sqms__Enabled=false
```

## Expected result

The call returns successful zero work: zero Opportunities closed, Bids moved to IPG review, Bids not allocated, open notifications, reminder notifications, and already queued, with an empty failure list.

## Important boundary

The return occurs before `GetOpenOpportunitiesAsync` or any other target read. Therefore disabling SQMS neither probes nor mutates `[SSQMS].[Opportunity]` or `[SSQMS].[Bid]`, and it does not read or write workflow/outbox runtime data. The existing unit test `DisabledIntegrationReturnsSuccessfulZeroWorkWithoutReadingTargetTables` is the executable proof; SQL can only confirm that persisted counts did not change.

## Endpoint

There is no public scheduled route. Disabling is server-owned configuration, not browser input.
