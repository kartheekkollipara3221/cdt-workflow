# Ready-to-paste prompt for next Codex session

You are working in `C:\projects\cdt-workflow` on branch `console-code-first-0518`.

Target API integration is complete. The current paused work is discovery for adding two workflow steps for the new `SQMS` module, described by the user as Secondary Sales Qualified Matching Service.

Before planning or editing:

1. Read `AGENTS.MD`.
2. Read `PROJECT.md`; it is the source of truth.
3. Read `context.md`.
4. Read `CODEX_HANDOFF.md`.
5. Inspect the current worktree:

```powershell
git status --short --untracked-files=normal
git diff --stat
git diff --name-only
```

6. Read all files currently under `SQMS/`.

Current known SQMS state:

- The first observed file is `SQMS/Opportunity status.txt`.
- More SQMS files are expected from the user before work resumes.
- No code implementation has started.
- The first file appears to be legacy SharePoint-style reference code around opportunity status changes.
- Visible concepts include `Pending Seller Approval`, open-to-bidding notifications/reminders, `CLOSED TO BIDDING`, bid status `NOT ALLOCATED`, and bid status `IPG REVIEW`.
- Some logic appears user-workflow-like; some logic appears scheduled/background-job-like.

Current repo shape:

- `src/WorkflowEngine.sln` is the reusable workflow engine and console/demo host.
- `workflow-api/WorkflowEngine.WorkflowApi.slnx` is the active ASP.NET Core workflow API reference host.
- `tests/WorkflowEngine.Tests` and `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests` are the active test suites.
- `samples/workflow-actions` contains Portfolio Trade sample action JSON.
- `SQMS/` is currently reference/source material for the new module and may be untracked.

Important boundaries:

- Do not make code changes until the SQMS workflow steps are understood and the user approves a design.
- Keep the engine generic and metadata-driven.
- Do not hardcode SQMS behavior inside the reusable engine.
- SQMS should be represented as workflow metadata, samples, and tests where possible.
- Separate true user workflow steps/actions from scheduled jobs such as reminders or automatic close-to-bidding processing.
- Do not add new public DTO fields unless the user explicitly approves the contract change.

First question to answer after reading the full `SQMS/` folder:

```text
Which exact two SQMS workflow steps should be added?
```

Initial unconfirmed candidates inferred from `Opportunity status.txt`:

1. `PendingSellerApproval`
2. `OpenToBidding`

Do not assume those are correct. Verify against the full SQMS source and the user's intent.

Useful verification commands once implementation eventually starts:

```powershell
dotnet build src\WorkflowEngine.sln -warnaserror -m:1 /p:UseAppHost=false
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -warnaserror -m:1 /p:UseAppHost=false
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 /p:UseAppHost=false
```
