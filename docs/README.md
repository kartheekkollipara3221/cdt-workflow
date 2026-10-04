# Documentation

Current documentation kept in this repo:

- [How one workflow action works](how-one-workflow-action-works.html): developer onboarding. Trace submit-for-checker through code, SQL records, failures, replay, and notification processing. Includes a debugging exercise and linked test evidence.
- `beginners-api-console-handoff.html`: single-file beginner handoff for API, console, and target API integration.
- `api-json-bodies-and-console-real-usage.html`: complete reference for current workflow API JSON bodies, Portfolio Trade sample flow, and console commands that apply to real operations.
- `frontend-workflow-api-handoff.html`: frontend-focused reference API contract, request/response parameters, and full Portfolio Trade happy path demo.
- `target-api-notification-changes-handoff.html`: precise target API file checklist for per-record SMTP recipients, copied recipients, and HTML notification bodies.
- `superpowers/specs/`: active design specifications for recent feature work.
- `superpowers/plans/`: active implementation plans for recent feature work.

Stale generated/historical API conversion and workflow visualization docs were removed when the legacy `api/` host was removed. Current API work should be documented against `workflow-api/WorkflowEngine.WorkflowApi.slnx`.
