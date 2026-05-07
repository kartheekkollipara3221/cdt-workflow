# WorkflowEngine.Tests

Tier 1 is the default suite:

```bash
dotnet test
```

Tier 1 tests use in-process fakes such as `RecordingNotificationSender`. They do not perform file IO, SMTP delivery, or network calls.

Tier 2 SMTP tests are opt-in and must be marked with `Category=Smtp`:

```bash
docker run --rm -p 3025:25 -p 5000:80 rnwood/smtp4dev
set RUN_SMTP_TESTS=1
dotnet test --filter Category=Smtp
```

Captured smtp4dev messages can be inspected in the web UI at `http://localhost:5000` or through its local API. Tier 2 tests must only target a fake local SMTP server such as smtp4dev or Papercut; never configure them to point at a real SMTP relay.
