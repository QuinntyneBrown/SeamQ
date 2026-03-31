# SeamQ

A .NET CLI tool that statically analyzes Angular workspaces to detect interface boundaries (seams), generate aerospace-grade Interface Control Documents (ICDs), and produce PlantUML architecture diagrams.

## What It Does

SeamQ scans Angular and Nx monorepo workspaces, identifies where types, services, tokens, bindings, events, routes, state, or messages cross workspace boundaries, and produces comprehensive documentation for each detected seam.

### Key Capabilities

- **Workspace Scanning** -- Parses Angular CLI, Nx monorepo, and standalone project structures including TypeScript sources, Angular metadata, barrel exports, services, components, and state management patterns.
- **Seam Detection** -- Classifies interface boundaries by type (Plugin Contract, Shared Library, Message Bus, Route Contract, State Contract, HTTP/API Contract) with confidence scoring.
- **ICD Generation** -- Produces complete Interface Control Documents in Markdown, HTML, PDF, and DOCX with registration contracts, component input contracts, service APIs, data objects, lifecycle/state management, protocols, traceability matrices, and more.
- **Diagram Generation** -- Generates PlantUML diagrams: 12 class diagram types, 15 sequence diagram types, 2 state diagram types, and 12 C4 architecture diagram types.
- **API Documentation** -- Generates public API reference docs and per-seam API documentation with embedded PlantUML diagrams.
- **Contract Validation** -- Validates that consumer workspaces correctly implement provider contracts, checking interface implementations, injection tokens, and input/output bindings.
- **Baseline Diffing** -- Saves scan results as baselines and compares subsequent scans to identify additions, modifications, and removals in the contract surface.

## Prerequisites

| Dependency | Version | Required | Purpose |
|------------|---------|----------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0+ | Yes | Runtime for SeamQ |
| [Java JRE/JDK](https://adoptium.net/) | 11+ | For diagram rendering | Required by PlantUML to render `.puml` files to PNG/SVG |
| [PlantUML](https://plantuml.com/download) | Latest | For diagram rendering | Renders `.puml` diagram files to PNG/SVG images |
| [Graphviz](https://graphviz.org/download/) | 2.38+ | For diagram rendering | Required by PlantUML for class and C4 diagrams |

> **Note:** Java, PlantUML, and Graphviz are only needed if you want to render `.puml` files into images. SeamQ generates valid `.puml` source files without them. You can also preview `.puml` files in VS Code with the PlantUML extension.

## Installation

### Install from NuGet

```bash
dotnet tool install --global SeamQ
```

### Update to latest version

```bash
dotnet tool update --global SeamQ
```

### Verify installation

```bash
seamq --version
```

### Install PlantUML (optional, for diagram rendering)

PlantUML requires Java and optionally Graphviz to render diagrams.

**Windows (winget):**

```bash
winget install EclipseAdoptium.Temurin.21.JDK
winget install Graphviz.Graphviz
```

Then download `plantuml.jar` from [plantuml.com/download](https://plantuml.com/download) and place it on your PATH or in a known location.

**Windows (Chocolatey):**

```bash
choco install temurin21
choco install graphviz
choco install plantuml
```

**macOS (Homebrew):**

```bash
brew install --cask temurin
brew install graphviz
brew install plantuml
```

**Linux (apt):**

```bash
sudo apt install default-jre graphviz
# Download plantuml.jar from https://plantuml.com/download
```

**Docker (no local install needed):**

```bash
docker run --rm -v $(pwd):/data plantuml/plantuml -tpng /data/*.puml
```

## Quick Start

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

# Generate API docs with rendered diagrams
seamq doc --all

# Validate contracts
seamq validate --all
```

## CLI Commands

| Command      | Description                                              |
|--------------|----------------------------------------------------------|
| `scan`       | Scan workspaces and build the seam registry              |
| `list`       | Display all detected seams in a table                    |
| `generate`   | Generate ICD documents (Markdown, HTML, PDF, DOCX)       |
| `diagram`    | Generate PlantUML diagrams                               |
| `inspect`    | Show detailed contract surface for a single seam         |
| `validate`   | Check consumer contract compliance                       |
| `diff`       | Compare current scan against a saved baseline            |
| `init`       | Generate a `seamq.config.json` interactively             |
| `export`     | Export raw seam data as JSON                             |
| `doc`        | Generate API reference docs with PlantUML diagrams       |
| `public-api` | Generate public API documentation for Angular projects   |
| `serve`      | Launch a local web server to browse generated ICDs       |

### Command Details

#### `scan <paths...>`

Scan one or more Angular/Nx workspaces and populate the seam registry.

```bash
seamq scan ./my-app                              # Single workspace
seamq scan ./shell ./shared ./plugin-a           # Multiple workspaces
seamq scan ./my-app --save-baseline baseline.json # Save baseline for diffing
seamq scan ./my-app --no-cache                    # Force fresh scan
seamq scan ./my-app --exclude "**/*.spec.ts"      # Exclude patterns
```

#### `list`

Display detected seams in a formatted table.

```bash
seamq list                          # All seams
seamq list --type plugin-contract   # Filter by type
seamq list --confidence 0.8         # Minimum confidence
seamq list --provider Dashboard.Web # Filter by provider
```

#### `generate [seam-id]`

Generate ICD documents in the specified format.

```bash
seamq generate --all                          # All seams, default format
seamq generate --all --format md html         # Markdown and HTML
seamq generate --all --format pdf             # PDF output
seamq generate --all --format docx            # Word document
seamq generate 68e7766a4dd5f012 --format html # Specific seam
```

#### `diagram [seam-id]`

Generate PlantUML `.puml` diagram source files.

```bash
seamq diagram --all                        # All diagram types
seamq diagram --all --type class           # Class diagrams only
seamq diagram --all --type sequence        # Sequence diagrams only
seamq diagram --all --type c4-component    # C4 component diagrams
seamq diagram abc123 --type sequence       # Specific seam
```

#### `doc [seam-id]`

Generate API reference documentation with embedded PlantUML diagrams. Automatically renders diagrams to PNG if PlantUML is available.

```bash
seamq doc --all                    # All seams
seamq doc --all --format md        # Markdown format
seamq doc 68e7766a4dd5f012         # Specific seam
```

#### `public-api`

Generate public API documentation for Angular projects.

```bash
seamq public-api                   # Generate public API docs
```

#### `inspect <seam-id>`

Print the full contract surface for a specific seam.

```bash
seamq inspect 68e7766a4dd5f012
```

#### `validate [seam-id]`

Check that consumer workspaces correctly implement provider contracts.

```bash
seamq validate --all               # Validate all seams
seamq validate 68e7766a4dd5f012    # Validate specific seam
```

#### `diff <baseline-path>`

Compare the current scan against a previously saved baseline.

```bash
seamq diff baseline.json
```

#### `export [seam-id]`

Export raw seam data as JSON files.

```bash
seamq export --all                 # All seams
seamq export --all --output-dir ./data
```

#### `serve`

Launch a local web server to browse generated ICDs.

```bash
seamq serve                        # Default port 5050
seamq serve --port 8080            # Custom port
```

#### `init`

Generate a `seamq.config.json` configuration file interactively.

```bash
seamq init
```

### Global Flags

| Flag | Description |
|------|-------------|
| `--verbose` | Enable detailed logging output |
| `--quiet` | Suppress all output except errors |
| `--no-color` | Disable ANSI color codes |
| `--output-dir <path>` | Override the configured output directory |
| `--config <path>` | Specify a custom config file path |
| `--version` | Show version |
| `--help` | Show help |

## Rendering PlantUML Diagrams

SeamQ outputs `.puml` source files. To render them as images, you need PlantUML installed (see [Prerequisites](#prerequisites)).

### Render a single diagram

```bash
java -jar plantuml.jar -tpng my-diagram.puml
```

### Render all diagrams in the output directory

```bash
java -jar plantuml.jar -tpng ./seamq-output/*/diagrams/*.puml
```

### Render as SVG

```bash
java -jar plantuml.jar -tsvg ./seamq-output/*/diagrams/*.puml
```

### Using Docker (no Java required locally)

```bash
docker run --rm -v $(pwd):/data plantuml/plantuml -tpng /data/seamq-output/*/diagrams/*.puml
```

### Using VS Code

Install the [PlantUML extension](https://marketplace.visualstudio.com/items?itemName=jebbs.plantuml) by jebbs. Open any `.puml` file and press `Alt+D` to preview.

## Diagram Types

| `--type` Value | Diagrams Generated |
|----------------|-------------------|
| `class` | ApiSurface, FrontendServices, DomainDataObjects, RegistrationSystem, MessageInterfaces, RealtimeCommunication |
| `sequence` | AppStartup, PluginLifecycle, RequestFlow, QueryFlow, CommandFlow, DataConsumption, ErrorHandling |
| `c4-context` | C4 System Context |
| `c4-container` | C4 Container, C4 Data Flow |
| `c4-component` | C4 Component Services, C4 Plugin API Layers |
| `c4-code` | C4 Dynamic |
| *(omitted)* | All applicable diagrams |

## Seam Types Detected

| Type | What It Detects |
|--------------------|------------------------------------------------------------------------|
| Plugin Contract | Interfaces, abstract classes, injection tokens crossing boundaries |
| Shared Library | Barrel exports consumed across workspaces |
| Message Bus | RxJS Subject/Observable streams shared across boundaries |
| Route Contract | `loadChildren`, `loadComponent`, route guards, route data interfaces |
| State Contract | Signal state, NgRx actions/selectors, computed state shared across workspaces |
| HTTP/API Contract | SignalR hub methods, backend service interfaces |

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

## Solution Structure

```
SeamQ/
├── src/
│   ├── SeamQ.Cli/              # CLI entry point (dotnet tool, System.CommandLine)
│   ├── SeamQ.Core/             # Core domain models and abstractions
│   ├── SeamQ.Scanner/          # Workspace discovery and TypeScript parsing
│   ├── SeamQ.Detector/         # Seam detection engine (strategy pattern)
│   ├── SeamQ.Generator/        # ICD document generation (Markdown, HTML, PDF, DOCX)
│   ├── SeamQ.Renderer/         # PlantUML and C4 diagram generation
│   ├── SeamQ.Differ/           # Baseline comparison
│   └── SeamQ.Validator/        # Contract compliance checking
├── test/
│   ├── SeamQ.Tests.Unit/       # Unit tests (xUnit, FluentAssertions, NSubstitute)
│   ├── SeamQ.Tests.Integration/# Pipeline integration tests
│   └── SeamQ.Tests.E2E/        # End-to-end CLI tests
├── templates/                  # Handlebars ICD templates (Markdown, HTML, MIL-STD-498)
├── themes/                     # PlantUML themes (default, dark)
└── docs/
    ├── guide/                  # User guide
    ├── specs/                  # L1/L2 requirement specifications
    └── detailed-designs/       # Feature-level designs with PlantUML diagrams
```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `System.CommandLine` | 2.0.0-beta4 | CLI parsing and command routing |
| `Microsoft.Extensions.DependencyInjection` | 10.0.5 | Dependency injection |
| `Microsoft.Extensions.Hosting` | 10.0.5 | Host builder and configuration |
| `Microsoft.Extensions.Logging` | 8.0.2 | Structured logging |

## Tech Stack

- **.NET 8** with C# latest, nullable reference types, warnings as errors
- **System.CommandLine** for CLI parsing (file-per-command pattern)
- **Microsoft.Extensions** for DI, logging, and configuration
- **xUnit** + **FluentAssertions** + **NSubstitute** for testing

## Supported Environments

- **.NET:** 8.0+
- **Angular:** 15 -- 19
- **Nx:** 16 -- 19
- **TypeScript:** 5.0+
- **Platforms:** Windows, macOS, Linux

## Building

```bash
dotnet build
dotnet test
```

## Security

- **Read-only** -- SeamQ never writes to scanned workspace directories.
- **No telemetry** -- No network calls unless PlantUML server rendering is explicitly configured.
- **Data locality** -- Source code is processed entirely in-memory and never leaves the local machine.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup and guidelines.

## License

This project is licensed under the [MIT License](LICENSE).
