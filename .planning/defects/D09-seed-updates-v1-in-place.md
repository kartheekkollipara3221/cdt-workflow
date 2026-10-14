# D09: Seed updates version 1 in place, without a transaction

| Field | Value |
|---|---|
| Defect ID | D09 (item 9 in `C:/projects/cdt-workflow-archive/20261014-cleanup/snapshot/WORKFLOW_ENGINE_REVIEW.md`, "Top 10 problems") |
| Tag | **MAJOR** |
| Original severity | Medium |
| Status | Open, deferred for a later fix |
| Date tagged | 2026-10-11 |
| Owner decision | The user tagged this defect as important during the defect review |
| Related defects | D08 (hardcoded `"Draft"` and `"SaveDraft"`), accepted as manageable |

## 1. Problem

The seed always writes to version 1 of the Portfolio Trade definition. It updates the metadata of that version in place. It uses four separate `SaveChanges()` calls without a transaction. It never sets a removed row to inactive. There is no procedure to publish a new version.

## 2. Locations

| File | Line | Item |
|---|---|---|
| `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs` | 7 | `public const int VersionNumber = 1;` |
| `src/WorkflowEngine.Infrastructure/Persistence/Seed/WorkflowSeedService.cs` | 7-49 | `SeedPortfolioTrade()`: four `SaveChanges()` calls (lines 19, 35, 45, 48), no transaction |
| `src/WorkflowEngine.Infrastructure/Persistence/Seed/WorkflowSeedService.cs` | 51-283 | `Upsert*` methods: add or update only, never deactivate |
| `src/WorkflowEngine.Infrastructure/Persistence/Seed/WorkflowSeedService.cs` | 34 | Sets the version to active, but does not deactivate other versions |
| `src/WorkflowEngine.Infrastructure/Persistence/Seed/WorkflowSeedService.cs` | 237 | `routes[seed.RouteKey]` can throw `KeyNotFoundException` after earlier saves |
| `src/WorkflowEngine.Infrastructure/Persistence/Seed/WorkflowDatabaseInitializer.cs` | 11-13 | `Migrate()`, then the seed, on each initialize command |
| `src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs` | `ResolveActiveDefinition` | `SingleOrDefault` on active versions; throws if two versions are active |

## 3. Runtime impact

1. **In-flight instances change rules.** A seed change to a transition, template, recipient rule, SLA policy, or task assignment applies immediately to all instances on version 1. No record shows the change.
2. **A failed seed leaves mixed metadata.** If a later `SaveChanges()` fails, the earlier saves stay. The API uses the mixed metadata immediately. Manual repair is necessary.
3. **Partial metadata during a successful seed.** Between the saves, requests can read new routes and tasks with old transitions and recipient rules.
4. **A removed row stays active.** A transition removed from the seed file stays `IsActive = true`. The old permission continues. A changed key adds a new row and keeps the old row.
5. **Version 2 stops all starts.** If `VersionNumber` changes to 2, versions 1 and 2 are both active. `ResolveActiveDefinition` throws. All starts fail with HTTP 500. Actions on current instances continue.
6. **Manual SQL changes are lost.** The next initialize command writes the seed values again without a warning.
7. **Two seeds at the same time can fail** or make duplicate rows.

No effect today, because the seed file did not change in a way that is unsafe for in-flight instances. The defect occurs at the first unsafe change on a database with live instances.

## 4. Fix plan (backward compatible)

1. Put all of `SeedPortfolioTrade()` in one transaction. If a step fails, roll back all steps.
2. Make the seed find all route keys, task keys, and state keys before the first write. Stop with a clear message if a key is missing.
3. Write a publish procedure for a new version:
   - a. Insert version N+1 with `IsActive = false`.
   - b. Insert all metadata rows for version N+1.
   - c. In one transaction, set version N to `IsActive = false` and version N+1 to `IsActive = true`.
   - d. Do not change the metadata rows of version N.
4. Keep version 1 instances on version 1. The engine already reads metadata by the version ID of the instance. Do not change this.
5. Decide how the seed handles a row that is not in the seed file (see section 5).
6. Document the procedure in the README: how to change metadata, how to publish version N+1, and what not to do.
7. Keep the current initialize command and its arguments. Keep `WorkflowSeedService.SeedPortfolioTrade()` as a public method.

## 5. Decisions required before implementation

1. **Version 1 with live instances.** Choose one option:
   - a. The seed continues to update version 1 in place (current behavior), but in one transaction.
   - b. The seed refuses to change a version that has active instances. It tells the operator to publish a new version. This changes the behavior of the initialize command and needs owner approval.
2. **Removed rows.** Choose one option:
   - a. Keep the current behavior: never deactivate.
   - b. Deactivate rows of the seeded version that are not in the seed file. This changes metadata on the current database and needs owner approval.
3. **Concurrent seeds.** Choose one option:
   - a. Document that only one initialize command can run at a time.
   - b. Take an application lock (`sp_getapplock`) during the seed.
4. **Publish tool.** Choose one option:
   - a. A new console command, for example `publish-workflow-version`.
   - b. A documented SQL script.

## 6. Backward compatibility check

| Item | Change |
|---|---|
| HTTP routes, headers, request and response JSON | No change |
| Status codes | No change |
| Console commands and arguments | No change (a new command is additive) |
| `WorkflowActionEnvelope` fixtures and sample JSON | No change |
| Workflow, state, action, task, role, SLA-policy, route, and template keys | No change |
| Version 1 metadata values | No change |
| Database schema | No change expected |
| Public .NET methods | No change; new methods are additive |

## 7. Quality gate for the fix

1. Build both solutions with warnings as errors:
   - `src/WorkflowEngine.sln`
   - `workflow-api/WorkflowEngine.WorkflowApi.slnx`
2. Run all current core tests and API tests. All must pass.
3. Add tests for the new behavior:
   - a. A seed that fails in a late step leaves the metadata unchanged.
   - b. A seed with a missing route key stops before the first write.
   - c. After version 2 becomes active, a version 1 instance keeps version 1 transitions, tasks, routes, and recipients.
   - d. After version 2 becomes active, a new start uses version 2.
   - e. Only one version is active after a publish.
   - f. Running the seed two times gives the same metadata (idempotent).
4. Run the seed verification and execute the current sample JSON (see `PROJECT.md` section 21).

## 8. Not in scope

- Moving `"Draft"` and `"SaveDraft"` to metadata (D08).
- Seed data for workflows other than Portfolio Trade.
- A user interface to edit metadata.
