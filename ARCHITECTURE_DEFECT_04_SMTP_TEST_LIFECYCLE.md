# Defect 04: Tier 2 SMTP Tests Depend on Manual Fake Server Setup

Status: Fixed in working tree by formal manual prerequisite

Severity: Major

Source review: `ARCHITECTURE_REVIEW.md`

## Summary

Tier 2 SMTP integration tests are correctly excluded from default test runs with `Category=Smtp`, but they depend on a manually running fake SMTP server such as smtp4dev or Papercut.

The test suite does not fully own its verification environment. As a result, SMTP test failures can mean either "the code is broken" or "the local fake SMTP prerequisite was missing/misconfigured."

## Production-Like Verification Scenario

A release engineer wants to verify SMTP delivery before enabling:

```text
Notifications:Sender = Smtp
```

They run:

```bash
dotnet test src/WorkflowEngine.sln --filter Category=Smtp
```

The test expects a fake SMTP server on a local host/port, for example:

```text
localhost:3025
```

and may query a fake SMTP web/API endpoint to confirm the message arrived.

If the fake server is not already running, the test fails. That failure does not prove the SMTP sender code is broken; it only proves the manual environment was not prepared.

On another machine:

- smtp4dev may run on a different port.
- Papercut may expose a different API.
- Stale messages may be present from a previous run.
- A developer may have no fake SMTP tool installed.
- CI may not have the dependency at all.

The result is environment-dependent verification.

## Real-Time Impact

This weakens release confidence in the production notification path:

- CI cannot reliably run the SMTP tier unless the external dependency is manually provisioned.
- Release verification may be skipped because setup is annoying.
- Failures are ambiguous: code bug vs missing local fake server.
- Passing tests may be polluted by stale messages from previous runs.
- Different engineers can get different results.
- SMTP delivery issues may be discovered late during UI integration or production smoke testing.

For this workflow engine, SMTP validates the path from database outbox to real delivery:

```text
NotificationOutbox row
  -> ProcessNotificationOutboxService
  -> INotificationSender
  -> SmtpNotificationSender
  -> SMTP relay
  -> received message
```

That path matters for operational handoffs:

```text
Maker submits -> Checker email
Checker sends to Trading Desk -> Trading Desk email
Trading Desk executes -> BDRM Client Service email
BDRM rejects -> Trading Desk rework email
Workflow completes -> Maker email if configured
```

The Tier 2 tests should provide controlled confidence that this delivery path works.

## Code Evidence

Primary files:

```text
tests/WorkflowEngine.Tests/SmtpFactAttribute.cs
tests/WorkflowEngine.Tests/SmtpNotificationSenderIntegrationTests.cs
```

Observed issue:

```text
SMTP integration tests are marked Category=Smtp, which is correct.
The tests assume a local fake SMTP server exists before the test starts.
The tests do not start, isolate, clear, or stop that fake SMTP server themselves.
```

## Required Fix Options

There are two acceptable paths. Prefer Option A if feasible.

### Option A: Own Fake SMTP Lifecycle in Tests

Add a test fixture that owns the fake SMTP server lifecycle:

1. Start a fake SMTP server before SMTP integration tests run.
2. Configure test SMTP host/port to that server.
3. Use an isolated mailbox or clear mailbox before each test.
4. Send the notification.
5. Assert the received message subject/body/recipient.
6. Tear down the fake SMTP server after tests complete.

The fixture should make failures meaningful:

```text
If the test fails, it should usually indicate a code or contract problem, not missing manual setup.
```

### Option B: Formalize Manual Prerequisite

If owning the fake server lifecycle is too heavy for this POC, explicitly document the manual prerequisite and make the test failure message clear.

Documentation should include:

```text
Tool name: smtp4dev or Papercut
Required SMTP host
Required SMTP port
Required web/API endpoint if used
How to clear mailbox
Exact command to run Category=Smtp tests
Expected failure message when prerequisite is missing
```

This is weaker than Option A but acceptable if the project intentionally treats Tier 2 SMTP tests as manually provisioned.

## Important Constraints

- Do not make `Category=Smtp` tests run by default.
- Do not hit a real SMTP relay in tests.
- Do not require production SMTP credentials.
- Do not add SMTP username/password support.
- Do not add message brokers, background workers, or unrelated infrastructure.
- Do not change workflow execution semantics.
- Keep the `INotificationSender` abstraction.
- Keep default dev/test sender configuration as `Console` or `NoOp`, not real SMTP.

## Acceptance Criteria

For Option A:

- `dotnet test src/WorkflowEngine.sln --filter Category=Smtp` starts or provisions its fake SMTP dependency.
- The test config points `SmtpNotificationSender` at the owned fake server.
- Tests assert at least subject, body, and recipient for a sent notification.
- Tests isolate or clear mailbox state so stale messages cannot produce false passes.
- The fake SMTP server is stopped or cleaned up after tests.
- Default `dotnet test src/WorkflowEngine.sln` still excludes SMTP tests.

For Option B:

- README or test docs clearly state the manual fake SMTP prerequisite.
- The prerequisite includes exact host, port, API endpoint, and mailbox clearing instructions.
- The SMTP test fails with a clear skip/failure message when the fake server is unavailable.
- Default `dotnet test src/WorkflowEngine.sln` still excludes SMTP tests.

## Suggested Verification Commands

Default tests:

```bash
dotnet test src/WorkflowEngine.sln
```

Tier 2 SMTP tests:

```bash
dotnet test src/WorkflowEngine.sln --filter Category=Smtp
```

Do not change the default test command so that SMTP tests run automatically.
