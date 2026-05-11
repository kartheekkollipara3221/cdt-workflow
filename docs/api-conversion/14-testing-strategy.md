# Testing Strategy

Keep the existing workflow library tests and add API-specific tests around HTTP behavior, authenticated actor resolution, route/body validation, and hosted/background processing.

## Existing Test Mapping

| Existing Test / Test Area | Keep | Move | Rewrite | New API Test Needed | Priority |
|---|---|---|---|---|---|
| `WorkflowActionEnvelopeValidationTests` | Yes | No | No | Controller returns `400` for validation failed. | High |
| `WorkflowActionEnvelopeNormalizationTests` | Yes | No | No | API does not silently apply CLI overrides. | Medium |
| `WorkflowActionEnvelopeSerializationTests` | Yes | No | No | API JSON casing remains compatible. | Medium |
| `WorkflowActionResultBuilderTests` | Yes | No | No | HTTP mapper status-code tests. | High |
| `WorkflowActionResultHashTests` | Yes | No | No | Actor stamping occurs before hashing. | High |
| `WorkflowTransitionTests` | Yes | No | No | Execute action endpoint smoke tests. | High |
| `WorkflowExecutionValidationTests` | Yes | No | No | API status mapping for forbidden/conflict/not found. | High |
| `WorkflowOptionsConflictTests` | Yes | No | No | API logs warnings without letting options override metadata. | Medium |
| `WorkflowContextReaderTests` | Yes | No | No | Context endpoint uses claims roles. | High |
| `WorkflowContextSerializationTests` | Yes | No | No | Context endpoint response JSON shape. | Medium |
| `StartWorkflowServiceTests` | Yes | No | No | Start endpoint duplicate and reset-disallowed tests. | High |
| `StartWorkflowCommandTests` | Keep as console | No | No | API start controller tests. | Medium |
| `ExecuteJsonCommandTests` | Keep as console | No | No | API execute controller tests. | High |
| `ShowContextCommandTests` | Keep as console | No | No | API context controller tests. | High |
| `ShowInspectionCommandTests` | Keep as console | No | No | Admin inspection endpoint tests if exposed. | Medium |
| `SeedDataSmokeTests` | Yes | No | No | API deployment/seed verification test or admin endpoint test. | High |
| `SchemaConstraintTests` | Yes | No | No | API test DB uses real SQL Server. | High |
| `Phase5SlaTests` | Yes | No | No | Hosted service/admin endpoint trigger test. | Medium |
| `Phase5OutboxTests` | Yes | No | No | Hosted service scope and sender failure tests. | Medium |
| `NotificationSenderSelectionTests` | Yes | No | No | API startup config tests. | High |
| `SmtpNotificationSenderTests` | Yes | No | No | API config smoke for SMTP mode. | Medium |
| `SmtpNotificationSenderIntegrationTests` | Keep opt-in | No | No | Optional API end-to-end SMTP smoke against fake SMTP. | Low |
| `SampleExpectationTests` | Yes | No | No | Samples as API request fixtures without `testExpectations`. | High |
| `BranchScriptTests` | Yes | No | No | API branch smoke tests. | Medium |
| `DemoCommandTests` | Keep as console/demo | No | No | Do not expose demo API endpoint. | Low |
| `FreshCloneWalkthroughTests` | Keep opt-in | No | No | API fresh-clone/deployment pipeline test. | Medium |
| `WorkflowPerformanceTests` | Keep opt-in | No | No | API latency smoke if needed. | Low |

## Existing Tests That Should Remain Unchanged

Keep the current library/console tests because they verify reusable behavior:

- Contracts and serialization.
- Validation and normalization.
- Hashing and result building.
- Workflow execution and transitions.
- Seed data.
- EF schema constraints.
- SLA and outbox behavior.
- SMTP sender behavior.
- Samples and demo scripts.

## Console-Specific Tests

Keep but do not treat as API coverage:

- `CommandRegistryTests`
- `StartWorkflowCommandTests`
- `ExecuteJsonCommandTests`
- `ShowContextCommandTests`
- `ShowInspectionCommandTests`
- `DemoCommandTests`

They verify console adapters only.

## New API Controller Tests

Suggested test names:

- `StartWorkflow_ValidRequest_ReturnsCreatedOrOkWithSuccessEnvelope`
- `StartWorkflow_DuplicateBusinessObject_Returns409AlreadyExists`
- `StartWorkflow_ResetDisallowedForNormalUser`
- `WorkflowContext_ValidRequest_ReturnsContext`
- `WorkflowContext_RolesComeFromClaims`
- `ExecuteAction_ValidSubmitForChecker_ReturnsSuccessEnvelope`
- `ExecuteAction_ActorRolesFromClaimsOverrideBodyRoles`
- `ExecuteAction_StaleExpectedVersion_Returns409ConcurrencyConflict`
- `ExecuteAction_InvalidTransition_Returns409`
- `ExecuteAction_ForbiddenAction_Returns403`
- `ExecuteAction_IdempotentReplay_ReturnsStoredResult`
- `ExecuteAction_IdempotencyConflict_Returns409`
- `ExecuteAction_RouteBodyWorkflowInstanceIdMismatch_Returns400`
- `ExecuteAction_AdminOverrideDisallowedForNormalUser`
- `SeedVerification_AdminOnly_WhenExposed`

## New Actor Resolution Tests

- `ClaimsWorkflowActorResolver_MapsMakerClaim`
- `ClaimsWorkflowActorResolver_MapsCheckerClaim`
- `ClaimsWorkflowActorResolver_MapsTradingDeskClaim`
- `ClaimsWorkflowActorResolver_MapsBdrmClaim`
- `ClaimsWorkflowActorResolver_MultipleClaimsProduceMultipleWorkflowRoles`
- `ClaimsWorkflowActorResolver_MissingUserIdThrowsOrFailsAuth`
- `ClaimsWorkflowActorResolver_UnknownGroupsProduceNoWorkflowRoles`

## New HTTP Status Mapping Tests

- `WorkflowHttpResultMapper_ValidationFailed_Returns400`
- `WorkflowHttpResultMapper_WorkflowInstanceNotFound_Returns404`
- `WorkflowHttpResultMapper_Forbidden_Returns403`
- `WorkflowHttpResultMapper_InvalidTransition_Returns409`
- `WorkflowHttpResultMapper_ConcurrencyConflict_Returns409`
- `WorkflowHttpResultMapper_DefinitionVersionMismatch_Returns409`
- `WorkflowHttpResultMapper_IdempotencyConflict_Returns409`
- `WorkflowHttpResultMapper_AlreadyExists_Returns409`
- `WorkflowHttpResultMapper_SuccessReplay_Returns200`

## New Configuration And Startup Tests

- Missing `ConnectionStrings:DefaultConnection` fails startup.
- Unknown `Notifications:Sender` fails startup.
- SMTP mode without `Smtp:Host` fails startup.
- SMTP mode without `Smtp:From` fails startup.
- `NoOp` and `Console` modes do not require SMTP settings.

## SLA/Outbox Hosted Service Tests

Suggested names:

- `WorkflowSlaHostedService_UsesServiceScopeFactory`
- `WorkflowSlaHostedService_QueuesOverdueNotificationOnce`
- `NotificationOutboxHostedService_UsesScopedProcessor`
- `NotificationOutboxHostedService_LogsFailureAndContinuesNextInterval`
- `NotificationOutboxProcessor_UsesNoOpSenderInApiTests`

## Seed/Migration Verification Tests

- API test database applies workflow migrations.
- Seed verification passes after deployment setup.
- Schema constraints exist in API test database.
- Portfolio Trade transition matrix remains intact.

## Current Repo Verification Commands

```bash
dotnet restore src/WorkflowEngine.sln
dotnet build src/WorkflowEngine.sln -warnaserror
dotnet test src/WorkflowEngine.sln
```

## Placeholder API Verification Commands

```bash
dotnet restore <ApiSolutionPath>
dotnet build <ApiSolutionPath> -warnaserror
dotnet test <ApiSolutionPath>
```

## Test Data Strategy

Use `samples/workflow-actions` as fixtures for execute-action tests, but strip or ignore `testExpectations`. Stamp actor fields from test auth claims before sending or inside the test server pipeline.

Use SQL Server-backed integration tests for workflow persistence. Do not use EF Core InMemory to verify migrations, constraints, idempotency, SLA, or outbox behavior.

