# AGENTS.md

## Source of truth
This repo is governed by `PROJECT.md`.

Before planning or implementing, read `PROJECT.md`.
Do not summarize it from memory.
Do not replace its milestones with a new roadmap.

`PROJECT.md` section 0 describes the product, CP Atlas, and its two components in this repo: the workflow engine (`src/`, `workflow-api/`) and the Atlas platform (`atlas-platform/`). They are one product, not two; never propose a repository split. Sections 1 to 28 of `PROJECT.md` govern the engine. The platform rules are in the section "Atlas platform" below.

## GSD workflow
This mapping applies to the workflow engine component. Use GSD end-to-end, but map GSD phases directly to `PROJECT.md` section 24:

- Phase 1 = Milestone 1: Solution skeleton
- Phase 2 = Milestone 2: SQL schema and seed data
- Phase 3 = Milestone 3: WorkflowActionEnvelope execution path
- Phase 4 = Milestone 4: Workflow engine
- Phase 5 = Milestone 5: SLA monitor and notification senders
- Phase 6 = Milestone 6: Samples, tests, and demo

Each phase must stop after its milestone is complete.
Do not start the next phase until the current phase passes its quality gate.

## Required architecture rules
- .NET 10.
- SQL Server.
- EF Core.
- Use `TimeProvider`; do not call `DateTime.UtcNow` outside the composition root.
- Keep workflow logic generic and metadata-driven.
- Portfolio Trade is seed/sample metadata only.
- Do not hardcode Portfolio Trade behavior inside the reusable engine.
- Do not let request JSON specify `toState`.
- Do not let `workflow.options` override metadata.
- Do not add CQRS, MediatR, event sourcing, BPMN, message brokers, microservices, or plugin frameworks.
- Do not add third-party packages except MailKit, which `PROJECT.md` pre-approves for SMTP.
- Do not create a DemoUser table or SQL user-to-role mapping.
- Treat warnings as errors.

## Backward compatibility

All future changes must be backward compatible by default.

- Existing public HTTP endpoints, trusted actor headers, request JSON, response JSON, status-code behavior, and field semantics must continue to work for existing callers.
- Public contract changes must be additive. New request fields must be optional unless introduced through a new explicitly versioned endpoint or contract.
- Existing console commands, command arguments, `WorkflowActionEnvelope` fixtures, sample JSON, and restore-bundle usage must remain executable.
- Database migrations must support in-place upgrades of existing databases and preserve existing data. Prefer additive nullable columns and new tables; do not rename, drop, or reinterpret persisted columns without an explicitly approved migration strategy.
- Existing workflow, state, action, task, role, SLA-policy, notification-route, and template keys are stable integration identifiers. Do not rename, remove, or change their meaning in place.
- Existing notification recipient snapshots, SLA behavior, email subject/body semantics, and target-application integration entry points must remain valid unless the user explicitly approves a versioned behavior change.
- Preserve existing dependency-injection registration methods and public .NET integration surfaces used by the target API. Add overloads or optional parameters instead of breaking existing call sites.
- Before completing a change, run regression coverage proving existing public samples and supported workflows still pass in addition to tests for the new behavior.
- If a requirement cannot be delivered compatibly, stop and ask before implementation. Document the breaking impact, affected callers/data, versioning or migration plan, rollback path, and required integration-team action.

## Stop-and-ask rules
Follow all stop-and-ask triggers in `PROJECT.md` section 22.

## Quality gates
Before marking an engine phase done, run the relevant commands from `PROJECT.md` section 21. For platform work, run the platform gates in the section "Atlas platform" below.

Always include this completion summary:

Changed:
Why:
Tests run:
Seed verification run:
Sample JSON executed:
Files updated:
Open issues:

## Atlas platform (`atlas-platform/`)

The platform is the generic, metadata-driven app layer of CP Atlas, so that the next 16+ apps are JSON + SQL + one registration line. Pilot app: TTDV2 (release P1). The rules above (architecture, backward compatibility, stop-and-ask) also apply; these rules are in addition.

### Scope and ownership
- Specs: `docs/atlas-platform/` (modules 01 to 24; `README.md` is the index). Integration guide: `atlas-platform/INTEGRATION.md`. Hand-off documents for the target team: `docs/atlas-platform/ATLAS_CODE_MAP.html` and `ATLAS_TEAM_NOTES.html`.
- Platform files are owned in this repo. Never edit them at the target. A fix is: change here, run the gates, rebuild the package, and the user copies the changed files to the target.
- The target code (CP Atlas: `program.cs`, `CPAtlasService`, the controllers, `cp-atlas-dashboard.ts`, ...) belongs to the target team. State what the target must change as integration steps in `INTEGRATION.md`; the user does the copy and the wiring. `target-implementation-read-only/` is a read-only, git-ignored copy of target code: never edit it, and never copy its license keys.
- Keep the platform generic. A new app must not need new C# or TypeScript. Never copy target hardcoding into the platform (field-name `if` chains, fixed field lists, redirects). TTDV2 is pilot metadata, not a special case in the code.
- Look and control usage follow the target (layout, `@pbdev` controls, ag-grid, toastr, messages). Behaviour stays metadata-driven (rules, effects, computed values, registries).
- A section form with a workflow has one submit button, not one button for each section. The server selects the workflow step from the state of the record. The form metadata lists the steps by state. Each save of such a form runs the step for the current state; no save goes past the workflow.
- Target stack: .NET 10 / C# 14, EF Core 10, SQL Server, Angular 21 (`atlas-platform/WebUi`). No new packages: use only what the target already has (`ag-grid` 33, `@ng-select/ng-select` 21, `ngx-toastr` 20, `@pbdev/pbdev-core`).

### Backward compatibility (platform)
- The current CP Atlas apps (TTD, CDT, SQMSO, SQMSB, AIN) must not change. A platform line in a shared target file runs first and falls through to the current `if` chain.
- Changes to the `v1/...` requests and responses are additive. New fields are optional.
- App codes, role names, table and column names, JSON keys, control names and log event IDs (41001 to 41024) are stable identifiers. Do not rename them.
- Database changes: SQL scripts only, in `atlas-platform/sql/release-p1/` (additive, idempotent, with a `CP_Atlas_ScriptLog` row). Never EF migrations, `EnsureCreated` or automatic seeding for platform tables. Never run `050` in the shared development database; `050` runs only on the change-over day, and `090` only to roll back.
- Config files and table metadata are read one time at start-up. A change needs an app pool restart; this is documented, not a defect.

### Platform quality gates
Run before marking a step or fix done. Stop the local API first; a running API locks the App dll.

```bash
dotnet build atlas-platform/AtlasPlatform.slnx -warnaserror
dotnet test atlas-platform/AtlasPlatform.slnx
dotnet run --project atlas-platform/src/BBH.CP.Atlas.App --launch-profile StandIn -- --atlas-check
cd atlas-platform/WebUi && npx ng build && npx ng test --watch=false
cd atlas-platform/WebUi && npm run check:target-flags
```

Expected: 0 warnings; all .NET tests pass (the SQL tests make and drop their own databases); `--atlas-check` prints `0 error(s), 0 warning(s)` and exits 0; `ng build` 0 warnings; all specs pass; the target-flags type check is clean.

Rebuild the target package when a copied file or `INTEGRATION.md` changes (the script refuses uncommitted changes):
`python atlas-platform/local/make-target-package.py atlas-platform/local/package/atlas-target-package.ps1`

Completion summary for platform work: the same format as above. Write `not applicable (platform)` for "Seed verification run" and "Sample JSON executed", and add a line `Package rebuilt:` (yes with the commit, or no).

### Stop-and-ask (platform)
- A change to a persisted column, a stable identifier, or a `v1/...` contract.
- A new package, or a new target file that must change.
- A fix that needs app-specific code in the platform.
- Any change to `050` or `090` (migration and rollback), including the review findings R01 and R02.
- The `PROJECT.md` section 22 triggers that apply (for example the complexity list, `DateTime.UtcNow`, engine adapter work).

### Code comments (platform)
Comments follow the Google style guides (TypeScript, C++ and the engineering practices), written in ASD-STE100.
- A doc comment tells the contract: what the member does, what the inputs and the result mean, what can go wrong.
  Not how the code does it.
- The first sentence is a summary that starts with a verb ("Applies...", "Returns...", "Removes...").
- An inline comment tells why, when the code cannot show it. Do not repeat what the code says.
- No history and no references in comments: no review, defect, decision or question IDs (RV-.., SV-.., DA-.., B12,
  Q07, D2, ...), no spec, module or section numbers, no document names (INTEGRATION.md, 01-....md), no "fix for",
  "was", "now". Put the link from code to spec, review and decision in `docs/atlas-platform/CODE_TRACE.md`
  (file and member, no line numbers). The commit message holds the history.
- References to other code stay ("see atlas-effects.ts", `<see cref="..."/>`).
- C#: `/// <summary>` on each public type and member; `<param>`, `<returns>`, `<exception>` only when they add
  information; `<remarks>` for the reason or a limit. TypeScript: JSDoc `/** ... */` with no types (TypeScript has
  them); `@param` and `@returns` only when they add information.
- Short sentences (20 words or fewer), active voice, one statement in each sentence.
- The test `CodeCommentRulesTests` (platform tests) fails when a comment has a reference ID, or when a row of
  `CODE_TRACE.md` names a file or member that does not exist. Test files and `050`/`090` are not checked.

```csharp
/// <summary>
/// Removes the value and the options of each field that an access rule hides from the current user.
/// </summary>
/// <remarks>
/// The server clears the values, so a hidden value is never in the response.
/// </remarks>
```

```ts
/**
 * Runs the effects of a control for a new value.
 *
 * Does nothing when the effects of this control already run.
 */
```

### Working style
- One question or step at a time; short, plain answers; record answers, do not rewrite specs.
- Explanations and documents for the user and the target team: ASD-STE100 (about 80%).
- Commit for each step or fix; push only when the user asks.

## Long-running Codex work

For multi-step work, maintain `CODEX_HANDOFF.md`. Its top has two sections: A (Atlas platform) and B (workflow engine), each with state, pending items and next steps. Dated history entries follow.

Update it before ending a session or when context is getting low. Include:

- Current goal
- Completed changes
- Changed files
- Build/test status
- Remaining tasks
- Known risks
- Next-session prompt

Do not rely only on chat history for project state.
