# Workflow Map

This folder contains a standalone workflow documentation tool for the CDT Workflow Engine repository.

The page visualizes major packages/components and highlights how selected workflows move requests, DTOs, metadata, database writes, notifications, emails, build artifacts, and other handoff data through the system.

## Open It

Open `docs/workflow-map/index.html` in a browser.

The page loads `workflows.json` at runtime. It first uses `fetch("workflows.json")` and then tries a same-folder iframe fallback for direct `file://` opens. If your browser still blocks local JSON loading, run a tiny static server from this folder and browse to the shown URL:

```powershell
cd docs/workflow-map
python -m http.server 8080
```

Then open `http://localhost:8080/`.

## Files

- `index.html`: vanilla HTML/CSS/JavaScript visualization.
- `workflows.json`: component catalog and workflow definitions.
- `README.md`: maintenance notes and schema reference.

No build step or external CDN dependency is required.

## Add A Component

Edit `workflows.json` and add an object to `components`:

```json
{
  "id": "my-component",
  "name": "My Component",
  "type": "service",
  "lane": "application",
  "description": "What this component owns.",
  "sourceHints": [
    "src/MyProject/MyFolder"
  ],
  "layout": { "x": 10, "y": 20, "w": 18, "h": 14 }
}
```

`layout` uses percentages inside the map canvas. Keep `id` stable because workflow steps refer to it.

## Add A Workflow

Add an object to `workflows`:

```json
{
  "id": "my-workflow",
  "name": "My workflow",
  "category": "API flows",
  "status": "Implemented",
  "confidence": "high",
  "description": "What this flow documents.",
  "entryPoint": "POST /example",
  "notes": [
    "Optional assumptions or verification notes."
  ],
  "steps": [
    {
      "order": 1,
      "from": "api-host",
      "to": "application-services",
      "label": "Request DTO",
      "details": "Explain what crosses this boundary.",
      "artifacts": [
        "ExampleRequest"
      ],
      "codeHints": [
        "api/src/ExampleController.cs"
      ],
      "confidence": "high"
    }
  ]
}
```

Each step draws one directional edge. Reusing the same `from` and `to` pair is allowed; the UI offsets repeated edges.

## JSON Schema

Top-level fields:

- `schemaVersion`: current schema number.
- `title`, `description`: displayed context.
- `generatedFrom`: files inspected to seed the map.
- `notes`: repository-level assumptions or warnings.
- `components`: package/component catalog.
- `baseConnections`: default background architecture edges.
- `workflows`: selectable flows.

Component fields:

- `id`: unique key used by edges and steps.
- `name`: display label.
- `type`: visual category. Existing values include `entrypoint`, `service`, `engine`, `database`, `background`, `external`, and `tooling`.
- `lane`: broad map row.
- `description`: component responsibility.
- `sourceHints`: paths, tables, or files to inspect.
- `layout`: percentage-based `{ "x", "y", "w", "h" }`.

Workflow fields:

- `id`: unique key.
- `name`, `category`, `description`: displayed in the workflow list.
- `status`: implementation status.
- `confidence`: `high`, `medium`, or `low`.
- `entryPoint`: command, endpoint, or trigger.
- `notes`: workflow-specific assumptions.
- `steps`: ordered edges.

Step fields:

- `order`: human-readable order.
- `from`, `to`: component ids.
- `label`: edge label.
- `details`: explanation shown in the side panel.
- `artifacts`: DTOs, commands, entities, rows, emails, files, or other payloads crossing the boundary.
- `codeHints`: relevant source paths or identifiers.
- `confidence`: step-level confidence.

## Inspection Assumptions

- The reusable workflow engine is the implemented core. Portfolio Trade is metadata and sample data, not hardcoded engine behavior.
- Invite new user was requested as a selectable workflow, but no invitation implementation was found. It is included as a low-confidence gap/inferred extension flow.
- ToDesktop build was requested as a selectable workflow, but no ToDesktop-specific configuration was found. It is included as a low-confidence inferred packaging flow based on CI, solution build, and source bundle scripts.
- Current notification delivery is command-driven through `process-notification-outbox`; no always-on hosted SLA/outbox worker is implemented in the source inspected.
- SQL Server and EF Core remain the persistence points for workflow metadata, runtime state, tasks, SLA, notification outbox, audit, mock section data, and idempotency.
