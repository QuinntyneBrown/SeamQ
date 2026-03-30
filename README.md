# SeamQ

A .NET CLI tool that statically analyzes Angular workspaces to detect interface boundaries (seams), generate aerospace-grade Interface Control Documents (ICDs), and produce PlantUML architecture diagrams.

## What It Does

SeamQ scans Angular and Nx monorepo workspaces, identifies where types, services, tokens, bindings, events, routes, state, or messages cross workspace boundaries, and produces comprehensive documentation for each detected seam.

### Key Capabilities

- **Workspace Scanning** -- Parses Angular CLI, Nx monorepo, and standalone project structures including TypeScript sources, Angular metadata, barrel exports, services, components, and state management patterns.
- **Seam Detection** -- Classifies interface boundaries by type (Plugin Contract, Shared Library, Message Bus, Route Contract, State Contract, HTTP/API Contract) with confidence scoring.
- **ICD Generation** -- Produces complete Interface Control Documents in Markdown and HTML with registration contracts, component input contracts, service APIs, data objects, lifecycle/state management, protocols, traceability matrices, and more.
- **Diagram Generation** -- Generates PlantUML diagrams: 12 class diagram types, 15 sequence diagram types, 2 state diagram types, and 12 C4 architecture diagram types.
- **Contract Validation** -- Validates that consumer workspaces correctly implement provider contracts, checking interface implementations, injection tokens, and input/output bindings.
- **Baseline Diffing** -- Saves scan results as baselines and compares subsequent scans to identify additions, modifications, and removals in the contract surface.

## Installation

```bash
dotnet tool install --global SeamQ
```

Requires .NET 8 or later.

## CLI Commands

| Command      | Description                                              |
|--------------|----------------------------------------------------------|
| `scan`       | Scan workspaces and build the seam registry              |
| `list`       | Display all detected seams in a table                    |
| `generate`   | Generate ICD documents (Markdown, HTML)                  |
| `diagram`    | Generate PlantUML diagrams                               |
| `inspect`    | Show detailed contract surface for a single seam         |
| `validate`   | Check consumer contract compliance                       |
| `diff`       | Compare current scan against a saved baseline            |
| `init`       | Generate a `seamq.config.json` interactively             |
| `export`     | Export raw seam data as JSON                             |
| `serve`      | Launch a local web server to browse generated ICDs       |

### Quick Start

```bash
# Initialize configuration
seamq init

# Scan workspaces
seamq scan ./workspace-a ./workspace-b

# List detected seams
seamq list

# Generate ICDs for all seams
seamq generate --all

# Generate diagrams
seamq diagram --all

# Validate contracts
seamq validate --all
```

### Global Flags

```
--verbose       Detailed logging output
--quiet         Suppress all output except errors
--no-color      Disable ANSI color codes
--output-dir    Override the configured output directory
--config        Specify a custom config file path
```

## Configuration

SeamQ is configured via a `seamq.config.json` file:

```json
{
  "workspaces": [
    { "path": "./apps/shell", "alias": "Shell", "role": "framework" },
    { "path": "./libs/shared", "alias": "Shared", "role": "library" },
    { "path": "./apps/plugin-a", "alias": "PluginA", "role": "plugin" }
  ],
  "output": {
    "directory": "./seamq-output",
    "formats": ["md", "html"],
    "diagrams": {
      "renderFormat": "svg",
      "plantumlServer": "local",
      "theme": "plain"
    }
  },
  "analysis": {
    "maxDepth": 10,
    "followNodeModules": false,
    "confidenceThreshold": 0.5
  },
  "icd": {
    "title": "Interface Control Document",
    "documentNumber": "ICD-001",
    "revision": "1.0"
  }
}
```

## Supported Environments

- **.NET:** 8.0+
- **Angular:** 15 -- 19
- **Nx:** 16 -- 19
- **TypeScript:** 5.0+
- **Platforms:** Windows, macOS, Linux

## Seam Types Detected

| Type               | What It Detects                                                        |
|--------------------|------------------------------------------------------------------------|
| Plugin Contract    | Interfaces, abstract classes, injection tokens crossing boundaries     |
| Shared Library     | Barrel exports consumed across workspaces                              |
| Message Bus        | RxJS Subject/Observable streams shared across boundaries               |
| Route Contract     | `loadChildren`, `loadComponent`, route guards, route data interfaces   |
| State Contract     | Signal state, NgRx actions/selectors, computed state shared across workspaces |
| HTTP/API Contract  | SignalR hub methods, backend service interfaces                        |

## Security

- **Read-only** -- SeamQ never writes to scanned workspace directories.
- **No telemetry** -- No network calls unless PlantUML server rendering is explicitly configured.
- **Data locality** -- Source code is processed entirely in-memory and never leaves the local machine.

## License

See [LICENSE](LICENSE) for details.
