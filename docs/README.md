# Documentation

Current documentation kept in this repo:

- [Open issues and paused work](OPEN-ISSUES.md): the current index of unresolved findings and preserved owner decisions.
- [SQMS target integration](sqms/TARGET-API-INTEGRATION.md) and [DBA deployment](sqms/DBA-DEPLOYMENT.md): current integration and deployment guidance.
- [SQMS scenario packs](sqms/target-api-scenarios/README.md): executable examples verified by the regression suite. Keep these files.

- [How workflow tables support the engine](how-workflow-tables-support-the-engine.html): how the 20 core tables work together for rules, state, tasks, deadlines, messages, history, and replay. Includes one action trace.
- [Portfolio Trade: one successful workflow](portfolio-trade-happy-path.html): one direct path from Maker to completion. Shows each handoff, one complete Checker request, engine orchestration, email, and task deadlines.
- [How one workflow action works](how-one-workflow-action-works.html): developer onboarding. Trace submit-for-checker through code, SQL records, failures, replay, and notification processing. Includes a debugging exercise and linked test evidence.
- `beginners-api-console-handoff.html`: single-file beginner handoff for API, console, and target API integration.
- `api-json-bodies-and-console-real-usage.html`: complete reference for current workflow API JSON bodies, Portfolio Trade sample flow, and console commands that apply to real operations.
- `frontend-workflow-api-handoff.html`: frontend-focused reference API contract, request/response parameters, and full Portfolio Trade happy path demo.
- `target-api-notification-changes-handoff.html`: precise target API file checklist for per-record SMTP recipients, copied recipients, and HTML notification bodies.
- `superpowers/specs/`: active design specifications for recent feature work.
- Historical implementation plans are in `C:/projects/cdt-workflow-archive/20261014-cleanup/snapshot/docs/superpowers/plans/`.

Stale generated/historical API conversion and workflow visualization docs were removed when the legacy `api/` host was removed. Current API work should be documented against `workflow-api/WorkflowEngine.WorkflowApi.slnx`.
