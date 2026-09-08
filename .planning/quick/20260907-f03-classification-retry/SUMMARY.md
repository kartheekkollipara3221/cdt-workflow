# F03 correction — completed locally, 2026-09-07

Changed: SQL-backed SQMS classification includes submitted bids and bids with a successful classification audit, even after they advance to seller review. Only submitted bids receive actions.

Why: successful earlier transitions must not remove fair-value/premium amounts from the total when a later bid fails and is retried.

Tests run:
- RED: two new retry regression cases failed against the original scheduler (after correcting a test-only state constant typo).
- GREEN: all 14 scheduler cases passed.
- Real SQL regression passed: injected error 51000 on discount projection, earlier commits retained, failed action rolled back, retry in fresh DI scope, earlier bid advanced to seller review, draft excluded, no duplicate actions and no work on final replay.
- `dotnet build src/WorkflowEngine.sln --no-restore -warnaserror --verbosity quiet`: zero warnings/errors.
- `dotnet build workflow-api/WorkflowEngine.WorkflowApi.slnx --no-restore -warnaserror --verbosity quiet`: zero warnings/errors.
- Core suite `--no-build --no-restore --filter 'Category!=Smtp'`: 477 passed, zero failed/skipped, including FreshClone.
- API suite `--no-build --no-restore`: 68 passed, zero failed/skipped.
- Results: results/f03-core.trx and results/f03-api.trx.
- `git diff --check`: passed (line-ending conversion notices only).

Seed verification run: passed through Section21QualityGateTests and FreshCloneWalkthroughTests in the private audit LocalDB instance.

Sample JSON executed: supported Checker/Trading return branches and demo through regression gates. Test migrations/initialization touched only test databases in `(localdb)\WorkflowAudit_20260906`; no existing DOLLY application databases or production initialization were touched.

Files updated:
- src/WorkflowEngine.Sqms/Persistence/ISqmsDataStore.cs
- src/WorkflowEngine.Sqms/Persistence/SqlSqmsDataStore.cs
- src/WorkflowEngine.Sqms/Scheduling/SqmsScheduledService.cs
- tests/WorkflowEngine.Tests/SqmsScheduledServiceTests.cs
- tests/WorkflowEngine.Tests/SqmsEndToEndTests.cs
- Audit presentation/checkpoint and session/quick-task records.

Open issues and limits:
- Custom ISqmsDataStore implementations retain their submitted-only default behavior until they override GetBidClassificationBasisAsync. The registered built-in SQL implementation is corrected; existing GetSubmittedBidsAsync semantics and public constructors/HTTP contracts remain unchanged.
- The basis uses current nondeleted target rows and durable success audit history. External financial edits, row moves/deletions or history removal can change the total; this change does not introduce an immutable financial snapshot.
- Previously misclassified production bids are not automatically repaired. No production deployment, schema change or data repair was performed.
- SMTP tests were excluded because no SMTP/notification behavior changed. SQL-backed verification used the private SQL Server 2025 LocalDB instance, not a rerun of the whole suite on SQL Server 2022.
- The first SQL test attempt was blocked by sandbox LocalDB registry access; rerun outside the sandbox passed. No application test failure remains.
- Full source audit stays deferred. F01 is set aside for the reported empty-database deployment; F02 authentication/identity is target-owned. Next: discuss F04 after user finishes F03.
