# Workflow API Remaining Tasks

- [x] Confirm Swagger route set after endpoint changes.
      Files involved:
      `workflow-api/src/WorkflowEngine.WorkflowApi/Controllers/WorkflowContextController.cs`, `workflow-api/src/WorkflowEngine.WorkflowApi/Controllers/WorkflowActionController.cs`, `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Startup/WorkflowApiProjectStructureTests.cs`
      Why it matters:
      Confirms the old duplicate context route is gone and the new button-click endpoint is exposed.
      Completed:
      Local Swagger smoke check on `http://localhost:5068/swagger/v1/swagger.json` returned only `/v1/WorkflowAction` and `/v1/WorkflowContext`.
      Risk level: Low

- [x] Review and normalize `launchSettings.json`.
      Files involved:
      `workflow-api/src/WorkflowEngine.WorkflowApi/Properties/launchSettings.json`
      Why it matters:
      It appears modified by Visual Studio and may contain local tooling noise.
      Completed:
      Kept project `http` and `https` launch profiles for local migration/testing, preserved existing launch URLs, moved `$schema` to the top-level conventional position, and removed IIS Express-specific local tooling settings.
      Risk level: Low

- [x] Decide whether to keep the console development connection string.
      Files involved:
      `src/WorkflowEngine.Console/appsettings.Development.json`
      Why it matters:
      It makes prep commands target the API database but includes a local database name.
      Completed:
      User confirmed local API and console testing should use `cdt_workflow_api_20260527`; console development config already points at that database.
      Risk level: Medium

- [ ] Review Visual Studio `launchSettings.json` SMTP environment variables before commit.
      Files involved:
      `workflow-api/src/WorkflowEngine.WorkflowApi/Properties/launchSettings.json`
      Why it matters:
      The `http` profile now contains local SMTP testing values, including `Smtp__Port = 3025`; verify this against the current smtp4dev listener and whether these local values should be committed.
      Suggested next step:
      Confirm the current smtp4dev port, then either keep the profile values intentionally, move them to user-local settings, or document the manual setup instead.
      Risk level: Medium

- [x] Implement `POST /v1/WorkflowAction`.
      Files involved:
      API controller/service; `WorkflowActionRequest`; existing `IWorkflowExecutionService`, `WorkflowActionEnvelopeNormalizer`, `CanonicalRequestHasher`.
      Why it matters:
      This is the workflow-only button-click execution endpoint.
      Completed:
      Added a thin controller and API service that accept only `workflowInstanceId` and `actionKey`, resolve actor identity from trusted headers, generate internal IDs, execute without section-data persistence, and return `{ result: { actionResult, workflowContext } }`.
      Risk level: High

- [x] Add clean `POST /v1/WorkflowStart`.
      Files involved:
      `WorkflowStartController`, `WorkflowStartService`, `WorkflowStartRequest`, existing `IStartWorkflowService`.
      Why it matters:
      The form/business API needs a clean workflow-only endpoint it can call after creating its business object.
      Completed:
      Added a workflow-only start endpoint accepting `workflowKey`, `businessObjectType`, and `businessObjectId`; starting user comes from trusted headers; public `reset` is not exposed. Response shape is `{ result: { actionResult, workflowContext } }`.
      Risk level: Medium

- [x] Support optional initial action on `POST /v1/WorkflowStart`.
      Files involved:
      `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowStartRequest.cs`,
      `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowStartService.cs`,
      `src/WorkflowEngine.Application/WorkflowStart/IStartWorkflowService.cs`,
      `src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs`,
      `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs`
      Why it matters:
      Maker can submit a brand-new form into workflow in one API call without first saving a draft.
      Completed:
      Added optional `initialActionKey` to `WorkflowStartRequest`. When present, workflow start creates the instance in `Draft` and immediately executes the metadata-valid first action, returning the initial action result and refreshed context. Invalid initial actions roll back the newly-created workflow instance. Action-created notifications are delivered through the same targeted dispatcher used by `POST /v1/WorkflowAction`.
      Risk level: Medium

- [x] Deliver action-created notifications before successful `POST /v1/WorkflowAction` responses.
      Files involved:
      `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs`,
      `workflow-api/src/WorkflowEngine.WorkflowApi/Services/IWorkflowActionNotificationDispatcher.cs`,
      `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionNotificationDispatcher.cs`
      Why it matters:
      In real SMTP environments, workflow action POST success must mean metadata-created notification rows for that action were delivered.
      Completed:
      Added a targeted dispatcher that selects only pending notification rows for the successful action's workflow instance ID and correlation ID, sends them through the configured `INotificationSender`, marks them `Sent` on success, and converts delivery failure into `NotificationDeliveryFailed` so the controller returns HTTP 500.
      Boundary:
      `POST /v1/WorkflowStart` still sends no email unless start later creates a metadata-backed notification row.
      Risk level: Medium

- [ ] Support dynamic Trading Desk task assignment.
      Files involved:
      `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowActionRequest.cs`,
      `src/WorkflowEngine.Application/Contracts/WorkflowActionEnvelope.cs`,
      `src/WorkflowEngine.Infrastructure/Persistence/Entities/WorkflowTaskAssignmentRoleDefinition.cs`,
      `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs`,
      `samples/workflow-actions/05-checker-send-to-trading-desk.json`
      Why it matters:
      `CheckerSendToTradingDesk` must create the Trading Desk review task for the upstream-selected desk group while keeping workflow authorization metadata-driven.
      Suggested next step:
      Run final verification, then mark this item complete.
      Risk level: Medium

- [ ] Decide whether audit/history, tasks, and notifications need read APIs.
      Files involved:
      Workflow inspection/read services, new API controllers/services.
      Why it matters:
      Useful for history tabs/support/admin views, but not always needed for first migration.
      Suggested next step:
      Confirm UI and support requirements.
      Risk level: Low

- [ ] Plan SLA/notification processing as background/internal work.
      Files involved:
      `IRunSlaService`, `IProcessNotificationOutboxService`, potential hosted service/admin endpoints.
      Why it matters:
      These are operational jobs, not normal public user endpoints.
      Suggested next step:
      Choose hosted service, scheduled job, or protected admin endpoint.
      Risk level: Medium

- [ ] Replace trusted-header actor resolver with target auth/claims resolver.
      Files involved:
      `IWorkflowActorResolver`, `TrustedHeaderWorkflowActorResolver`, target auth code.
      Why it matters:
      Trusted headers are local/test integration only unless secured by upstream infrastructure.
      Suggested next step:
      Map target claims/roles/profile data to `WorkflowActorSnapshot`.
      Risk level: High

- [ ] Integrate workflow EF migrations into target deployment.
      Files involved:
      `WorkflowEngine.Infrastructure/Migrations`, `WorkflowDbContext`, target deployment pipeline.
      Why it matters:
      `init-db` is not the production deployment model.
      Suggested next step:
      Decide whether target API or deployment pipeline applies migrations.
      Risk level: High
