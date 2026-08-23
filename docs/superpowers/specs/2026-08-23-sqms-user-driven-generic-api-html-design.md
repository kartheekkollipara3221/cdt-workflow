# SQMS User-Driven Generic API HTML Handoff Design

## Goal

Create one self-contained HTML file for the target API team that documents the existing generic Workflow API headers, request bodies, response bodies, and response interpretation for every user-driven SQMS Opportunity and Bid workflow path.

## Output

- Path: `docs/sqms/SQMS-USER-DRIVEN-GENERIC-API-HANDOFF.html`
- Format: standalone HTML with embedded CSS and small copy-to-clipboard JavaScript; no external assets or packages.
- Audience: target API developers integrating SQMS through generic Workflow Start, Context, and Action endpoints without using `WorkflowEngine.Sqms` for user-driven operations.

## Scope

The document will cover:

- Trusted actor headers and their server-owned security boundary.
- Opportunity and Bid business-object/workflow-instance ID conventions.
- `POST /v1/WorkflowStart`, `GET /v1/WorkflowContext`, and `POST /v1/WorkflowAction`.
- All user-driven Opportunity and Bid actions, including mutually exclusive branches.
- Complete seller-approval and Bid-disposition notification inputs required when the SQMS adapter is not used.
- Representative Start, Context, Action, workflow-failure, and ProblemDetails responses.
- Exact response paths for success, state, version, available actions, notification queueing, and replay.
- Target persistence rules after success and prohibitions after failure/replay.

The document will explicitly exclude:

- Opportunity open/reminder notification scheduling.
- Automatic Opportunity close.
- Scheduled Bid classification to `IPG REVIEW` or `NOT ALLOCATED`.
- Scheduler deployment or configuration.
- Any new HTTP contract, endpoint, or product behavior.

## Structure

1. Scope and architectural boundary.
2. Quick-start variables, ID rules, and trusted headers.
3. Endpoint and response-shape reference.
4. How to read Start, Context, Action, and failure responses.
5. Opportunity request catalog with branch map and scheduled boundary.
6. Bid request catalog with branch map and scheduled boundary.
7. Notification request data for manual notification-producing actions.
8. Target persistence, transaction, replay, and failure checklist.
9. Implementation checklist and source references.

## Presentation

- Responsive target-team handoff layout with a sticky table of contents on wider screens.
- Compact cards and tables for rules and response-field meanings.
- Dark, copy-ready request/response blocks with accessible copy buttons.
- Clear badges for `USER`, `MFO`, `IPG`, `SELLER`, `SCHEDULED EXCLUDED`, and terminal states.
- No environment-specific secrets, production addresses, or real URLs; all variable values remain obvious examples.

## Verification

Every endpoint, header, request property, response wrapper, workflow key, action key, role, state transition, notification source key, and template token will be checked against current controllers, DTOs, application contracts, SQMS metadata SQL, and target scenario documentation. Verification will include HTML parsing/basic structural checks, secret-pattern scanning, link/path inspection, and `git diff --check`.

No application build, seed execution, or sample API lifecycle is required because this task changes documentation only and introduces no runtime behavior.
