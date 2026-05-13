# WorkflowEngine.Tests

Tier 1 is the default suite:

```bash
dotnet test
```

Tier 1 tests use in-process fakes such as `RecordingNotificationSender` for external senders, but most workflow persistence tests are SQL-backed. They use `WORKFLOW_ENGINE_TEST_CONNECTION_STRING`, `ConnectionStrings__DefaultConnection`, or the approved local DOLLY fallback and initialize schema through the database-first SQL artifacts.

Tier 2 SMTP tests are opt-in and must be marked with `Category=Smtp`:

```powershell
docker run --rm -p 3025:25 -p 5000:80 rnwood/smtp4dev
$env:RUN_SMTP_TESTS = "1"
dotnet test src\WorkflowEngine.sln --filter Category=Smtp
```

Manual prerequisite:

- smtp4dev SMTP endpoint: `localhost:3025`
- smtp4dev API/UI endpoint: `http://localhost:5000`
- Clear the smtp4dev mailbox from the web UI before a manual verification run if you want a clean inbox.
- The tests use unique subjects, so stale messages should not produce false passes.

Captured smtp4dev messages can be inspected in the web UI at `http://localhost:5000` or through its local API. Tier 2 tests must only target a fake local SMTP server such as smtp4dev or Papercut; never configure them to point at a real SMTP relay.
