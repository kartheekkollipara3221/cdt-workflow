# atlas-platform

Local build of the CP Atlas reusable platform, release P1 (TTDV2 pilot). Spec: [docs/atlas-platform/12-ttd-pilot-build.md](../docs/atlas-platform/12-ttd-pilot-build.md), section 11.

This is not part of `WorkflowEngine.sln`. The workflow engine does not change.

## Layout

The projects have the same names and folders as the target. In each project:

| Folder | Copy to the target? |
|---|---|
| `Platform/` | Yes, to the same path |
| `Application/` new files (`ConfigSectionService.cs`, `CPTTDV2Service.cs`, `TTDV2/`) | Yes |
| `Config/TTDV2.*.json` | Yes |
| `StandIn/` | No. Local copies of target parts that are not in this repository. Each file starts with `// STAND-IN: do not copy to the target.` |
| `BBH.CP.Atlas.Infra` (whole project) | No (stand-in) |
| `*.csproj`, `appsettings*.json`, `tests/` | No |

[INTEGRATION.md](INTEGRATION.md) lists every step at the target. [local/README.md](local/README.md) tells you how to run TTDV2 on this computer.

## Build and test

```powershell
dotnet build atlas-platform/AtlasPlatform.slnx
dotnet test atlas-platform/AtlasPlatform.slnx
```

Rules: .NET 10, warnings are errors, `TimeProvider` (no `DateTime.Now`), no new third-party packages.

## Phases

| Phase | Status |
|---|---|
| C0 Skeleton and stand-ins | Done |
| C1 Data | Not started |
| C2 Config and registry | Not started |
| C3 Security | Not started |
| C4 Rules engine | Not started |
| C5 Section service and save pipeline | Not started |
| C6 Local database, demo host, API | Not started |
| C7 Dashboard | Not started |
| C8 Start-up check | Not started |
| C9 WebUi and `@pbdev` stand-in | Not started |
| C10 Release package and acceptance | Not started |
