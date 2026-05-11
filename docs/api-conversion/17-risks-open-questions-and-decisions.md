# Risks, Open Questions, and Required Decisions

This document lists risks and decisions to resolve before or during manual migration.

## Risk Register

| Risk | Impact | Likelihood | Mitigation | Owner |
|---|---|---|---|---|
| API project unavailable to Codex. | Instructions may need path/namespace adjustment. | High | Use placeholders and inspect real API before applying. | API team |
| Unknown API conventions. | Controllers/DTOs may not match local style. | High | Follow existing API folders, naming, responses. | API team |
| Unknown authentication/authorization model. | Actor resolver can be wrong. | High | Identify claim/group source before execute endpoint. | API/security team |
| Unknown claims-to-workflow-role mapping. | Users may be over- or under-authorized. | High | Define explicit mapping to `Maker`, `Checker`, `TradingDesk`, `BDRMClientService`. | Business/security team |
| Trusting request body actor roles. | Privilege escalation. | High | Overwrite `Actor` from authenticated user before validation/hashing. | API team |
| Exposing reset publicly. | Deletes runtime workflow data. | Medium | Do not expose reset; admin/environment gate if needed. | API/ops team |
| Exposing admin override publicly. | Bypasses role authorization. | Medium | Do not expose; strict admin policy and audit if required. | Security team |
| Exposing EF entities directly. | Leaks schema and persistence details. | Medium | Use existing contracts/read models, not entities. | API team |
| Migration ownership conflict. | Broken deployments or duplicate migration history. | Medium | Decide migration assembly before applying DB changes. | API/DBA team |
| `MockSectionData` is POC storage. | Production form persistence may be inadequate. | High | Decide whether to keep temporarily or replace deliberately. | Product/API team |
| Synchronous workflow execution in web request pipeline. | Request latency under load. | Medium | Measure; keep transaction small; do not add batch work to user requests. | API team |
| DI lifetime mismatch. | Runtime exceptions or DbContext misuse. | Medium | Keep workflow services scoped; hosted services use scopes. | API team |
| Hosted service scope mistakes. | Background service startup/runtime failure. | Medium | Use `IServiceScopeFactory`. | API team |
| Hardcoded configuration. | Environment-specific failures or secret leakage. | Medium | Use API config/secrets/env variables. | Ops/API team |
| Logging sensitive payload. | Data leak. | Medium | Never log `dataSave.payload` by default. | API/ops team |
| Changing contract shapes. | UI/test incompatibility. | Medium | Reuse contracts unless API wrapper is clearly needed. | API team |
| Hardcoding Portfolio Trade behavior. | Breaks reusable engine principle. | Medium | Controllers call generic workflow services only. | API team |
| Running seed/migration on every request. | Performance/security/data mutation risk. | Low | Run in deployment/admin path only. | Ops team |
| Public admin operations. | Operational attack surface. | Medium | Admin-only policy or omit endpoints. | Security/API team |
| Notification sender misconfiguration. | Lost/delayed handoff emails. | Medium | Startup validation, seed verification, smoke tests. | Ops team |
| SLA/outbox concurrency assumptions. | Duplicate or missed notifications. | Medium | Use existing `RunSlaService` and `ProcessNotificationOutboxService`; do not bypass claim logic. | API team |
| Test gaps. | Regressions in auth/status mapping. | High | Add API tests listed in document 14. | QA/API team |

## Open Questions

| Question | Why It Matters | Who Should Answer | Blocking? | Suggested Default |
|---|---|---|---|---|
| What is the real API project path? | Required for project references. | API team | Yes | Replace `<ApiProjectPath>` before editing. |
| What is the real API namespace? | Required for controllers/services. | API team | Yes | Use existing namespace convention. |
| What are the controller folder conventions? | Keeps structure consistent. | API team | No | Use existing `<ControllerFolder>`. |
| What authentication system is used? | Determines actor resolver. | Security/API team | Yes | Use existing API auth. |
| What claims represent workflow roles? | Determines authorization correctness. | Security/business team | Yes | Map enterprise groups to workflow roles. |
| Should routes be generic workflow routes or domain-nested Portfolio Trade routes? | Affects client shape and route/body validation. | API/product team | Yes | Prefer generic unless existing API is domain-resource oriented. |
| Should `MockSectionData` remain or be replaced? | Affects production data persistence. | Product/API team | Yes for production | Keep only as POC/demo until real form persistence decision. |
| Who owns EF migrations? | Affects deployment and tooling. | API/DBA team | Yes | Keep in `WorkflowEngine.Infrastructure` by default. |
| Should SLA/outbox run in API hosted services or external jobs? | Affects operations and scaling. | Ops/API team | Yes before production | External job or hosted service; avoid user request path. |
| Are admin endpoints allowed? | Affects seed/inspection/SLA/outbox surface. | Security/ops team | No for core user flow | Omit unless approved. |
| What is the API's standard error response model? | Affects `ProblemDetails` and wrappers. | API team | Yes for implementation | Use existing API convention. |
| What is the logging/correlation standard? | Affects observability and support. | Ops/API team | Yes before cutover | Use existing API trace/correlation middleware. |
| What CI environment will run SQL-backed tests? | Required for reliable verification. | DevOps/API team | Yes before merge | SQL Server service/container with test connection env var. |

## Required Decisions

Record decisions before implementation:

- Project reference strategy.
- Migration assembly ownership.
- Route strategy: generic versus domain-nested.
- Actor claim mapping.
- Missing workflow role behavior.
- Admin endpoint policy.
- Reset/admin override policy.
- `MockSectionData` production stance.
- SLA/outbox processing strategy.
- Notification sender per environment.
- API error response convention.
- Payload logging/redaction policy.
- SQL test environment.

## Default Decisions If Not Otherwise Specified

- Reference Application, Infrastructure, and Domain projects.
- Do not reference Console project for API runtime.
- Keep migrations in `WorkflowEngine.Infrastructure`.
- Use generic workflow routes.
- Stamp actor from authenticated claims.
- Do not expose reset or admin override.
- Use `WorkflowActionResultEnvelope` for workflow outcomes.
- Use `ProblemDetails` for HTTP/model/system errors.
- Use `NoOp` sender in tests.
- Use hosted service or external job for SLA/outbox, not user endpoints.

