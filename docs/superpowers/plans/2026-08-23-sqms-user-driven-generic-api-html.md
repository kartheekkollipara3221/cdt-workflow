# SQMS User-Driven Generic API HTML Handoff Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create one verified, self-contained HTML handoff showing the target team exactly how to invoke and interpret generic Workflow API requests for every user-driven SQMS Opportunity and Bid path.

**Architecture:** The artifact is documentation only. It derives the HTTP contract from the existing Workflow API controllers/DTOs, derives transitions and notification data from the SQMS SQL metadata, and keeps scheduled execution in a separate technical ownership overview rather than presenting it as a public endpoint.

**Tech Stack:** HTML5, embedded CSS, embedded vanilla JavaScript, ASP.NET Core controller contracts, JSON, PowerShell verification.

---

### Task 1: Build the self-contained target-team handoff

**Files:**
- Create: `docs/sqms/SQMS-USER-DRIVEN-GENERIC-API-HANDOFF.html`
- Reference: `workflow-api/src/WorkflowEngine.WorkflowApi/Controllers/WorkflowStartController.cs`
- Reference: `workflow-api/src/WorkflowEngine.WorkflowApi/Controllers/WorkflowContextController.cs`
- Reference: `workflow-api/src/WorkflowEngine.WorkflowApi/Controllers/WorkflowActionController.cs`
- Reference: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowStartRequest.cs`
- Reference: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowActionRequest.cs`
- Reference: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowNotificationRequest.cs`
- Reference: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowStartApiResponse.cs`
- Reference: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowActionApiResponse.cs`
- Reference: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowContextApiResponse.cs`
- Reference: `src/WorkflowEngine.Application/Contracts/WorkflowContextResponse.cs`
- Reference: `src/WorkflowEngine.Application/Contracts/WorkflowActionResult.cs`
- Reference: `sql/sqms/002_sqms_workflow_metadata_dml.sql`
- Reference: `src/WorkflowEngine.Sqms/Contracts/ISqmsScheduledService.cs`
- Reference: `src/WorkflowEngine.Sqms/DependencyInjection/SqmsServiceCollectionExtensions.cs`
- Reference: `docs/sqms/DBA-DEPLOYMENT.md`

- [ ] **Step 1: Create the HTML shell and navigation**

Create a valid HTML5 document with embedded styles, responsive sidebar navigation, accessible focus states, clear scope callouts, print styles, and embedded copy buttons. Use no external fonts, scripts, styles, images, packages, or network resources.

- [ ] **Step 2: Add common HTTP contracts and identity rules**

Document the three implemented routes verbatim:

```text
POST /v1/WorkflowStart
GET  /v1/WorkflowContext?workflowInstanceId={id}
POST /v1/WorkflowAction
```

Document the trusted headers verbatim:

```text
X-Workflow-UserId
X-Workflow-Roles
X-Workflow-DisplayName
```

State that the target server builds these headers from authenticated identity and that request JSON never supplies `toState`.

- [ ] **Step 3: Add response examples and interpretation**

Include representative Start, Context, Action, workflow-failure, and ProblemDetails JSON. Mark every JSON code block with `data-json="true"` so verification can parse it. Explain the exact paths for `success`, `resultCode`, `workflowInstanceId`, `oldState`, `newState`, `notification.queued`, `diagnostics.replay`, authoritative current state/version, actor access, and available actions.

- [ ] **Step 4: Add all user-driven Opportunity requests**

Cover Start-only, Start-and-Submit, Get Context, Submit, return from Submitted, mark Pending, return from Pending, open for bidding, the explicit scheduled-close boundary, request Seller approval with complete notification data, the three Seller-approval branches, and completion from both allocation states. Label terminal and mutually exclusive paths.

- [ ] **Step 5: Add all user-driven Bid requests**

Cover Start-only, Start-and-Submit, Get Context, Submit, return to Draft, the explicit scheduled-classification boundary, Send to Seller Review, and the Accepted, Partially Accepted, and Not Accepted branches with complete notification data. Explain that `NOT ALLOCATED` is scheduled and terminal.

- [ ] **Step 6: Add persistence and scheduled ownership sections**

Explain that the target API persists returned `Status`/`WorkflowState` after successful user-driven operations, does not update after failure/replay, and needs one in-process transaction for target and workflow persistence. Separately document `ISqmsScheduledService.RunAsync`, `AddSqmsWorkflowIntegration`, the console trigger, close/classify/re-read/notify ordering, four-column scheduled projection, separate outbox processing, and the three future ownership choices. Do not create a scheduled HTTP contract.

### Task 2: Verify the artifact against source and HTML/JSON safety gates

**Files:**
- Verify: `docs/sqms/SQMS-USER-DRIVEN-GENERIC-API-HANDOFF.html`
- Update: `CODEX_HANDOFF.md`

- [ ] **Step 1: Parse every documented JSON block**

Run a PowerShell check that extracts `<code data-json="true">...</code>`, HTML-decodes each block, and parses it with `System.Text.Json.JsonDocument.Parse`. Expected: every block parses and the count is nonzero.

- [ ] **Step 2: Assert required and forbidden contract text**

Assert that the HTML contains all three endpoints, all three trusted headers, both workflow keys, every user-driven action key, `ISqmsScheduledService.RunAsync`, `process-notification-outbox`, all response-reading paths, and scheduled-exclusion language. Assert that it contains no request property named `toState` and no public scheduled route.

- [ ] **Step 3: Scan for placeholders and secrets**

Search for `TBD`, `TODO`, incomplete markers, private-key headers, common API-token formats, production credentials, and accidental local connection strings. Expected: no matches. Obvious documentation variables such as `{{baseUrl}}`, `{Opportunity.ID}`, and `example.com` are intentional and must remain.

- [ ] **Step 4: Run repository formatting checks**

Run:

```powershell
git diff --check -- docs/sqms/SQMS-USER-DRIVEN-GENERIC-API-HANDOFF.html CODEX_HANDOFF.md
```

Expected: no output and exit code 0.

- [ ] **Step 5: Update handoff and commit only task-owned files**

Append the goal, completed documentation, verification evidence, remaining target-team decisions, and next-session prompt to `CODEX_HANDOFF.md`. Preserve its existing content. Commit only the HTML and plan-owned handoff change; do not include unrelated working-tree changes.
