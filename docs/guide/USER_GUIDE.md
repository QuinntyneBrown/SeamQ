# SeamQ User Guide

SeamQ is a .NET CLI tool that statically analyzes Angular workspaces to detect interface boundaries (seams), generate aerospace-grade Interface Control Documents (ICDs), and produce PlantUML architecture diagrams.

---

## Table of Contents

- [Prerequisites](#prerequisites)
- [Installation](#installation)
  - [Install SeamQ](#install-seamq)
  - [Install PlantUML for Diagram Rendering](#install-plantuml-for-diagram-rendering)
- [Quick Start](#quick-start)
- [Scanning Workspaces](#scanning-workspaces)
- [Listing Detected Seams](#listing-detected-seams)
- [Generating ICD Documents](#generating-icd-documents)
- [Generating Diagrams](#generating-diagrams)
  - [Class Diagrams](#class-diagrams)
  - [Sequence Diagrams](#sequence-diagrams)
  - [C4 Architecture Diagrams](#c4-architecture-diagrams)
- [Generating API Documentation](#generating-api-documentation)
- [Inspecting Seams](#inspecting-seams)
- [Validating Contracts](#validating-contracts)
- [Baseline Diffing](#baseline-diffing)
- [Exporting Data](#exporting-data)
- [Browsing Results](#browsing-results)
- [Configuration](#configuration)
- [Rendering Diagrams to PNG/SVG](#rendering-diagrams-to-pngsvg)
  - [What You Need Installed](#what-you-need-installed)
  - [Installing Java](#installing-java)
  - [Installing Graphviz](#installing-graphviz)
  - [Installing PlantUML](#installing-plantuml)
  - [Rendering with PlantUML JAR](#rendering-with-plantuml-jar)
  - [Rendering with Docker](#rendering-with-docker)
  - [Rendering in VS Code](#rendering-in-vs-code)
  - [Rendering in IntelliJ / WebStorm](#rendering-in-intellij--webstorm)
  - [Batch Rendering Scripts](#batch-rendering-scripts)
  - [Troubleshooting Rendering](#troubleshooting-rendering)
- [CLI Reference](#cli-reference)
- [Scenarios](#scenarios)

---

## Prerequisites

Before installing SeamQ, ensure you have:

| Dependency | Version | Required | Purpose |
|------------|---------|----------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0+ | Yes | Runtime for SeamQ CLI |
| [Java JRE/JDK](https://adoptium.net/) | 11+ | For rendering | Required by PlantUML to convert `.puml` to PNG/SVG |
| [PlantUML](https://plantuml.com/download) | Latest | For rendering | Converts `.puml` diagram source to images |
| [Graphviz](https://graphviz.org/download/) | 2.38+ | For rendering | Layout engine used by PlantUML for class/C4 diagrams |

> **Note:** Java, PlantUML, and Graphviz are only needed if you want to render `.puml` files into PNG or SVG images. SeamQ generates valid PlantUML source files without any of these. You can also preview `.puml` files directly in VS Code or IntelliJ with the appropriate extension.

---

## Installation

### Install SeamQ

Install SeamQ as a global .NET tool from NuGet:

```bash
dotnet tool install --global SeamQ
```

Update to the latest version:

```bash
dotnet tool update --global SeamQ
```

Verify the installation:

```bash
seamq --version
```

Uninstall:

```bash
dotnet tool uninstall --global SeamQ
```

### Install PlantUML for Diagram Rendering

SeamQ generates `.puml` source files for all diagrams. To render these into PNG or SVG images, you need Java, Graphviz, and PlantUML installed.

#### Windows

**Option A: Using winget**

```bash
# Install Java (Eclipse Temurin JDK)
winget install EclipseAdoptium.Temurin.21.JDK

# Install Graphviz
winget install Graphviz.Graphviz

# Download plantuml.jar
# Visit https://plantuml.com/download and save plantuml.jar to a known location
# Example: C:\tools\plantuml.jar
```

**Option B: Using Chocolatey**

```bash
choco install temurin21
choco install graphviz
choco install plantuml
```

With Chocolatey, `plantuml` is available as a command directly:

```bash
plantuml -tpng my-diagram.puml
```

**Option C: Using Scoop**

```bash
scoop bucket add java
scoop install temurin21-jdk
scoop install graphviz
# Download plantuml.jar manually from https://plantuml.com/download
```

#### macOS

```bash
# Using Homebrew
brew install --cask temurin
brew install graphviz
brew install plantuml
```

After installation, `plantuml` is available as a command:

```bash
plantuml -tpng my-diagram.puml
```

#### Linux (Debian/Ubuntu)

```bash
# Install Java and Graphviz
sudo apt update
sudo apt install default-jre graphviz

# Download PlantUML JAR
wget https://github.com/plantuml/plantuml/releases/latest/download/plantuml.jar -O ~/plantuml.jar

# Create a convenience alias (add to ~/.bashrc or ~/.zshrc)
alias plantuml="java -jar ~/plantuml.jar"
```

#### Linux (Fedora/RHEL)

```bash
sudo dnf install java-21-openjdk graphviz
wget https://github.com/plantuml/plantuml/releases/latest/download/plantuml.jar -O ~/plantuml.jar
```

#### Docker (no local install needed)

If you prefer not to install Java or Graphviz locally, use the official PlantUML Docker image:

```bash
docker run --rm -v $(pwd):/data plantuml/plantuml -tpng /data/*.puml
```

#### Verify your PlantUML installation

```bash
# Check Java
java -version

# Check Graphviz
dot -V

# Check PlantUML
java -jar plantuml.jar -version
# or if installed via package manager:
plantuml -version
```

You should see version output from all three. If Graphviz is missing, PlantUML will warn you and some diagram types (class diagrams, C4 diagrams) will fail to render.

---

## Quick Start

Analyze an Angular workspace in three commands:

```bash
# 1. Scan the workspace
seamq scan ./my-angular-app

# 2. Generate ICD documents
seamq generate --all --format md html

# 3. Generate architecture diagrams
seamq diagram --all
```

Output lands in `./seamq-output/` by default, organized by seam ID.

### Full Pipeline Example

```bash
# Scan with a saved baseline
seamq scan ./src/Dashboard.Web --save-baseline baseline.json --verbose

# List what was found
seamq list

# Generate everything
seamq generate --all --format md html --output-dir ./docs-out
seamq diagram --all --output-dir ./docs-out
seamq doc --all --output-dir ./docs-out
seamq validate --all
seamq export --all --output-dir ./docs-out

# Render diagrams to PNG
java -jar plantuml.jar -tpng ./docs-out/*/diagrams/*.puml

# Browse the results
seamq serve
```

---

## Scanning Workspaces

The `scan` command analyzes Angular workspaces and builds the seam registry.

### Scan a single workspace

```bash
seamq scan C:/projects/Dashboard/src/Dashboard.Web
```

### Scan multiple workspaces

```bash
seamq scan ./apps/shell ./libs/shared ./plugins/reporting
```

### Scan with a baseline for later diffing

```bash
seamq scan ./my-app --save-baseline baseline.json
```

### Scan with exclusions

```bash
seamq scan ./my-app --exclude "**/*.spec.ts" "**/__mocks__/**"
```

### Disable caching for a fresh scan

```bash
seamq scan ./my-app --no-cache
```

### Typical output

```
[ok] scanned Dashboard.Web (6 projects, 228 exports)
found 4 seams across 1 workspaces.
```

---

## Listing Detected Seams

After scanning, list all detected seams:

```bash
seamq list
```

### Filter by type

```bash
seamq list --type plugin-contract
seamq list --type http-api-contract
```

### Filter by confidence

```bash
seamq list --confidence 0.8
```

### Filter by provider workspace

```bash
seamq list --provider Dashboard.Web
```

### Seam Types

| Type | Description |
|------|-------------|
| `plugin-contract` | Interfaces, tokens, abstract classes crossing workspace boundaries |
| `shared-library` | Barrel exports consumed across workspaces |
| `message-bus` | RxJS Subjects, NgRx actions/selectors |
| `route-contract` | loadChildren, loadComponent, route guards |
| `state-contract` | Angular Signal state, NgRx patterns |
| `http-api-contract` | SignalR hubs, backend service interfaces |

---

## Generating ICD Documents

Generate Interface Control Documents for your seams.

### Generate Markdown ICDs for all seams

```bash
seamq generate --all --format md
```

### Generate both Markdown and HTML

```bash
seamq generate --all --format md html
```

### Generate PDF or Word documents

```bash
seamq generate --all --format pdf
seamq generate --all --format docx
```

### Generate for a specific seam

```bash
seamq generate 68e7766a4dd5f012 --format html
```

### Specify output directory

```bash
seamq generate --all --format md html --output-dir ./docs/icds
```

ICD documents include:
- Introduction and scope
- Interface overview with architecture layers
- Registration contracts (tokens, providers)
- Component input/output contracts
- Injectable service APIs
- Data objects at the boundary
- Lifecycle and state management
- Protocols
- Traceability matrix
- Diagram index

---

## Generating Diagrams

SeamQ generates PlantUML diagrams across three categories: class, sequence, and C4 architecture. All diagrams are **standalone** `.puml` files that render without network access or external dependencies.

### Generate all diagrams for all seams

```bash
seamq diagram --all
```

### Generate only class diagrams

```bash
seamq diagram --all --type class
```

### Generate only sequence diagrams

```bash
seamq diagram --all --type sequence
```

### Generate only C4 diagrams

```bash
seamq diagram --all --type c4-component
seamq diagram --all --type c4-container
seamq diagram --all --type c4-context
```

### Generate diagrams for a specific seam

```bash
seamq diagram 68e7766a4dd5f012
seamq diagram 68e7766a4dd5f012 --type sequence
```

---

### Class Diagrams

Class diagrams show the static structure of the contract surface.

#### API Surface

Shows all interfaces, types, enums, and injection tokens at the seam boundary.

```bash
seamq diagram --all --type class
```

![API Surface Class Diagram](images/ClassApiSurface.png)

#### Frontend Services

Shows services with their message types and relationships. Services are connected to the messages they handle and responses they return.

![Frontend Services](images/ClassFrontendServices.png)

#### Message Interfaces

Shows the request/response message type pairs with `<<Message>>` and `<<Response>>` stereotypes and `produces` relationships.

![Message Interfaces](images/ClassMessageInterfaces.png)

#### Domain Data Objects

Shows data types, enums, and their field-level properties with composition relationships.

![Domain Data Objects](images/ClassDomainDataObjects.png)

---

### Sequence Diagrams

Sequence diagrams show runtime interaction flows.

#### Request Flow

Shows how requests flow from consumer through provider services to the backend, with proper message and response type pairing.

```bash
seamq diagram --all --type sequence
```

![Request Flow](images/SeqRequestFlow.png)

#### Command Flow

Shows the command dispatch pattern: consumer sends a command message, the command service validates, executes, emits events, and returns a response.

![Command Flow](images/SeqCommandFlow.png)

#### Query Flow

Shows the query execution pattern: consumer sends a query through the query service to the data store and receives results.

![Query Flow](images/SeqQueryFlow.png)

#### App Startup

Shows the application bootstrap sequence: host initialization, provider registration, consumer loading, and ready state.

![App Startup](images/SeqAppStartup.png)

#### Error Handling

Shows success and error paths side by side, demonstrating how the system handles failures.

![Error Handling](images/SeqErrorHandling.png)

#### Data Consumption

Shows observable subscription patterns and data push flows from providers to consumers.

![Data Consumption](images/SeqDataConsumption.png)

---

### C4 Architecture Diagrams

C4 diagrams show system architecture at different levels of abstraction.

#### Component Services

Shows all services, components, message types, and response types as C4 components within a system boundary, with `handles` and `returns` relationships.

```bash
seamq diagram --all --type c4-component
```

![C4 Component Services](images/C4ComponentServices.png)

#### Plugin API Layers

Shows the layered architecture: Registration Layer (services), Contract Layer (messages), Binding Layer (components, enums, responses), and Runtime Layer.

![C4 Plugin API Layers](images/C4PluginApiLayers.png)

#### Data Flow

Shows how data flows through the system: services accept messages and return responses, with database-style containers for data payloads.

```bash
seamq diagram --all --type c4-container
```

![C4 Data Flow](images/C4DataFlow.png)

#### Dynamic Diagram

Shows numbered interaction steps across system boundaries: registration, service invocations, and data returns.

```bash
seamq diagram --all --type c4-code
```

![C4 Dynamic](images/C4Dynamic.png)

---

## Generating API Documentation

### API Reference (`doc`)

Generate API reference documentation with embedded PlantUML diagrams for each seam. If PlantUML is installed, diagrams are automatically rendered to PNG.

```bash
seamq doc --all                           # All seams
seamq doc --all --format md               # Markdown format
seamq doc 68e7766a4dd5f012                # Specific seam
seamq doc --all --output-dir ./api-docs   # Custom output directory
```

### Public API (`public-api`)

Generate public API surface documentation for Angular projects, listing all exported components, services, interfaces, types, and methods.

```bash
seamq public-api
```

---

## Inspecting Seams

Get detailed contract surface information for a specific seam:

```bash
seamq inspect 68e7766a4dd5f012
```

Output includes:
- Seam metadata (type, provider, consumers, confidence score)
- All contract elements grouped by category: Components, Services, Directives, Pipes, Interfaces, Abstract Classes, Enumerations, Injection Tokens, Input Bindings, Output Bindings, Signal Inputs, Properties, Methods, Types
- Source file paths and line numbers for each element

### Example output

```
Dashboard.Web/http-api-contract
id          68e7766a4dd5f012
type        http-api-contract
provider    Dashboard.Web
consumers   (none)
confidence  40%
elements    19

  Services (3)
    CommandService
      projects/api/src/lib/services/command.service.ts:9
    QueryService
      projects/api/src/lib/services/query.service.ts:7
    RequestService
      projects/api/src/lib/services/request.service.ts:7

  Interfaces (2)
    CommandMessage
      projects/api/src/lib/models/command-message.ts:1
    CommandResponse
      projects/api/src/lib/models/command-message.ts:7
    ...
```

---

## Validating Contracts

Check that consumer workspaces correctly implement provider contracts:

### Validate all seams

```bash
seamq validate --all
```

### Validate a specific seam

```bash
seamq validate 68e7766a4dd5f012
```

### Example output

```
[ok] Dashboard.Web/http-api-contract
[ok] Dashboard.Web/shared-library
[ok] Dashboard.Web/plugin-contract
[ok] Dashboard.Web/state-contract

all 4 seam(s) valid.
```

Validation checks:
- Interface implementations
- Injection token provisioning
- Input/output binding compliance

Exit codes: `0` = all valid, `1` = errors found.

---

## Baseline Diffing

Track changes to your contract surface over time.

### Step 1: Save a baseline

```bash
seamq scan ./my-app --save-baseline baseline.json
```

### Step 2: Make changes to your code

### Step 3: Scan again and compare

```bash
seamq scan ./my-app
seamq diff baseline.json
```

The diff report shows:
- `+` Added contract elements
- `-` Removed contract elements
- `~` Modified contract elements

---

## Exporting Data

Export raw seam data as JSON for integration with external tools.

### Export all seams

```bash
seamq export --all
```

### Export a specific seam

```bash
seamq export 68e7766a4dd5f012
```

### Export to a specific directory

```bash
seamq export --all --output-dir ./data
```

Each seam exports three JSON files:
- `contract-surface.json` - All contract elements with metadata
- `data-dictionary.json` - Type definitions and field details
- `traceability-matrix.json` - Source file mappings

---

## Browsing Results

Launch a local web server to browse generated ICDs and diagrams:

```bash
seamq serve                    # Default: http://localhost:5050
seamq serve --port 8080        # Custom port
```

---

## Configuration

Create a `seamq.config.json` for persistent settings:

```bash
seamq init
```

### Example configuration

```json
{
  "workspaces": [
    {
      "path": "./src/Dashboard.Web",
      "alias": "Dashboard.Web",
      "role": "framework"
    }
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
    "title": "Dashboard Interface Control Document",
    "documentNumber": "ICD-DASH-001",
    "revision": "A",
    "classification": "INTERNAL"
  }
}
```

### Workspace roles

| Role | Description |
|------|-------------|
| `framework` | The host application that defines the contract surface |
| `plugin` | A workspace that implements/consumes the contract |
| `library` | A shared library workspace |
| `application` | A standalone application |

### Using a config file

```bash
# Auto-discovered from current directory
seamq scan

# Explicit config path
seamq scan --config ./configs/seamq.config.json
```

### CLI flags override config

```bash
# Config says output to ./seamq-output, but override:
seamq generate --all --output-dir ./custom-output
```

---

## Rendering Diagrams to PNG/SVG

SeamQ outputs `.puml` source files. This section explains everything you need to render them as images.

### What You Need Installed

To render `.puml` files to PNG or SVG images, you need three things:

1. **Java** (JRE 11+) -- the runtime PlantUML runs on
2. **Graphviz** (2.38+) -- the layout engine for class diagrams and C4 diagrams
3. **PlantUML** (the JAR file or a package manager install)

Sequence diagrams do not require Graphviz, but class diagrams and C4 diagrams do.

### Installing Java

PlantUML requires Java 11 or later. We recommend Eclipse Temurin (formerly AdoptOpenJDK).

| Platform | Command |
|----------|---------|
| Windows (winget) | `winget install EclipseAdoptium.Temurin.21.JDK` |
| Windows (Chocolatey) | `choco install temurin21` |
| Windows (Scoop) | `scoop bucket add java && scoop install temurin21-jdk` |
| macOS (Homebrew) | `brew install --cask temurin` |
| Ubuntu/Debian | `sudo apt install default-jre` |
| Fedora/RHEL | `sudo dnf install java-21-openjdk` |

Verify: `java -version`

### Installing Graphviz

Graphviz provides the `dot` layout engine that PlantUML uses for class diagrams and C4 diagrams.

| Platform | Command |
|----------|---------|
| Windows (winget) | `winget install Graphviz.Graphviz` |
| Windows (Chocolatey) | `choco install graphviz` |
| Windows (Scoop) | `scoop install graphviz` |
| macOS (Homebrew) | `brew install graphviz` |
| Ubuntu/Debian | `sudo apt install graphviz` |
| Fedora/RHEL | `sudo dnf install graphviz` |

Verify: `dot -V`

> **Important:** After installing Graphviz on Windows, you may need to restart your terminal or add the Graphviz `bin` directory to your PATH. PlantUML will print a warning if it cannot find `dot`.

### Installing PlantUML

**Option A: Download the JAR (all platforms)**

Download `plantuml.jar` from [plantuml.com/download](https://plantuml.com/download) or from the [GitHub releases](https://github.com/plantuml/plantuml/releases).

```bash
# Place it somewhere convenient
# Windows example:
mkdir C:\tools
# Save plantuml.jar to C:\tools\plantuml.jar

# Linux/macOS example:
wget https://github.com/plantuml/plantuml/releases/latest/download/plantuml.jar -O ~/plantuml.jar
```

Usage with the JAR:

```bash
java -jar plantuml.jar -tpng diagram.puml
java -jar C:\tools\plantuml.jar -tsvg diagram.puml
```

**Option B: Package manager (wraps the JAR)**

| Platform | Command | Usage |
|----------|---------|-------|
| Windows (Chocolatey) | `choco install plantuml` | `plantuml -tpng diagram.puml` |
| macOS (Homebrew) | `brew install plantuml` | `plantuml -tpng diagram.puml` |

Package managers install a wrapper script so you can use `plantuml` directly instead of `java -jar plantuml.jar`.

**Option C: Docker (no Java or Graphviz needed locally)**

```bash
docker run --rm -v $(pwd):/data plantuml/plantuml -tpng /data/diagram.puml
```

Verify your installation:

```bash
java -jar plantuml.jar -version
# or
plantuml -version
```

### Rendering with PlantUML JAR

#### Render a single diagram to PNG

```bash
java -jar plantuml.jar -tpng my-diagram.puml
```

This creates `my-diagram.png` in the same directory.

#### Render a single diagram to SVG

```bash
java -jar plantuml.jar -tsvg my-diagram.puml
```

#### Render all diagrams in the output directory

```bash
java -jar plantuml.jar -tpng ./seamq-output/*/diagrams/*.puml
```

#### Render with a specific output directory

```bash
java -jar plantuml.jar -tpng -o ./rendered ./seamq-output/*/diagrams/*.puml
```

#### Render with higher resolution

```bash
java -DPLANTUML_LIMIT_SIZE=16384 -jar plantuml.jar -tpng my-diagram.puml
```

### Rendering with Docker

No Java or Graphviz installation required.

#### Render all diagrams

```bash
docker run --rm -v $(pwd):/data plantuml/plantuml -tpng /data/seamq-output/*/diagrams/*.puml
```

#### Render as SVG

```bash
docker run --rm -v $(pwd):/data plantuml/plantuml -tsvg /data/seamq-output/*/diagrams/*.puml
```

#### Windows (PowerShell)

```powershell
docker run --rm -v ${PWD}:/data plantuml/plantuml -tpng /data/seamq-output/*/diagrams/*.puml
```

### Rendering in VS Code

1. Install the [PlantUML extension](https://marketplace.visualstudio.com/items?itemName=jebbs.plantuml) by jebbs
2. Open any `.puml` file
3. Press `Alt+D` to open the preview pane
4. Right-click in the preview to export as PNG, SVG, or other formats

The extension can use a local PlantUML JAR or an online server. For offline use, configure in VS Code settings:

```json
{
  "plantuml.render": "Local",
  "plantuml.jar": "C:\\tools\\plantuml.jar"
}
```

### Rendering in IntelliJ / WebStorm

1. Install the **PlantUML Integration** plugin from the JetBrains marketplace
2. Open any `.puml` file
3. The preview panel renders automatically on the right side
4. Requires Java and Graphviz to be installed locally

### Batch Rendering Scripts

#### Bash (Linux/macOS)

```bash
#!/bin/bash
# render-all.sh - Render all SeamQ diagrams to PNG
OUTPUT_DIR="./seamq-output"
PLANTUML_JAR="${PLANTUML_JAR:-plantuml.jar}"

for f in "$OUTPUT_DIR"/*/diagrams/*.puml; do
  echo "Rendering: $f"
  java -jar "$PLANTUML_JAR" -tpng "$f"
done

echo "Done. PNG files created alongside .puml files."
```

#### PowerShell (Windows)

```powershell
# render-all.ps1 - Render all SeamQ diagrams to PNG
$outputDir = ".\seamq-output"
$plantumlJar = $env:PLANTUML_JAR ?? "plantuml.jar"

Get-ChildItem -Path $outputDir -Filter "*.puml" -Recurse | ForEach-Object {
    Write-Host "Rendering: $($_.FullName)"
    java -jar $plantumlJar -tpng $_.FullName
}

Write-Host "Done."
```

### Troubleshooting Rendering

| Problem | Solution |
|---------|----------|
| `Error: No Java runtime present` | Install Java 11+ and ensure `java` is on your PATH |
| `Cannot find Graphviz` | Install Graphviz and ensure `dot` is on your PATH. Restart your terminal after installing. |
| `Error: File not found: plantuml.jar` | Provide the full path: `java -jar C:\tools\plantuml.jar -tpng ...` |
| Sequence diagrams render but class diagrams don't | Install Graphviz -- class/C4 diagrams require it, sequence diagrams do not |
| Diagram renders but looks cut off | Increase the size limit: `java -DPLANTUML_LIMIT_SIZE=16384 -jar plantuml.jar ...` |
| Docker: permission denied on output files | Add `--user $(id -u):$(id -g)` to the docker run command |
| VS Code preview shows nothing | Check that the PlantUML extension is configured to use a local JAR or that Java is on your PATH |

---

## CLI Reference

### Global Options

| Flag | Description |
|------|-------------|
| `--verbose` | Enable detailed logging |
| `--quiet` | Suppress all output except errors |
| `--no-color` | Disable ANSI color codes |
| `--output-dir <path>` | Override output directory |
| `--config <path>` | Specify config file path |
| `--version` | Show version |
| `--help` | Show help |

### Commands

| Command | Description |
|---------|-------------|
| `scan <paths...>` | Scan workspaces and build seam registry |
| `list` | List all detected seams |
| `generate [seam-id]` | Generate ICD documents (Markdown, HTML, PDF, DOCX) |
| `diagram [seam-id]` | Generate PlantUML diagrams |
| `inspect <seam-id>` | Print detailed contract surface |
| `validate [seam-id]` | Check consumer contract compliance |
| `diff <baseline-path>` | Compare scan against a baseline |
| `init` | Generate seamq.config.json interactively |
| `export [seam-id]` | Export raw seam data as JSON |
| `doc [seam-id]` | Generate API reference docs with PlantUML diagrams |
| `public-api` | Generate public API documentation |
| `serve` | Launch local web server to browse ICDs |

### Diagram Types

| `--type` Value | Diagrams Generated |
|----------------|-------------------|
| `class` | ApiSurface, FrontendServices, DomainDataObjects, RegistrationSystem, MessageInterfaces, RealtimeCommunication |
| `sequence` | AppStartup, PluginLifecycle, RequestFlow, QueryFlow, CommandFlow, DataConsumption, ErrorHandling |
| `c4-context` | C4 System Context |
| `c4-container` | C4 Container, C4 Data Flow |
| `c4-component` | C4 Component Services, C4 Plugin API Layers |
| `c4-code` | C4 Dynamic |
| *(omitted)* | All applicable diagrams |

### Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `1` | Partial failure (some errors/warnings) |
| `2` | Fatal error (cannot proceed) |

---

## Scenarios

### Scenario 1: First-time analysis of a new workspace

```bash
cd /path/to/angular-workspace
seamq init                              # Create config
seamq scan . --verbose                  # Analyze
seamq list                              # See what was found
seamq generate --all --format md html   # Generate ICDs
seamq diagram --all                     # Generate diagrams
seamq doc --all                         # Generate API docs

# Render diagrams to PNG
java -jar plantuml.jar -tpng ./seamq-output/*/diagrams/*.puml

# Browse results
seamq serve                             # Open http://localhost:5050
```

### Scenario 2: CI/CD contract change detection

```bash
# In CI pipeline:
seamq scan ./app --save-baseline current.json
seamq diff previous-baseline.json
# Exit code 1 if changes detected
```

### Scenario 3: Generate only sequence diagrams for one seam

```bash
seamq scan ./app
seamq list                                    # Note the seam ID
seamq diagram abc123 --type sequence          # Only sequence diagrams

# Render
java -jar plantuml.jar -tpng ./seamq-output/abc123/diagrams/*.puml
```

### Scenario 4: Full documentation export with rendered diagrams

```bash
seamq scan ./app --output-dir ./docs
seamq generate --all --format md html --output-dir ./docs
seamq diagram --all --output-dir ./docs
seamq doc --all --output-dir ./docs
seamq export --all --output-dir ./docs
seamq validate --all

# Render all diagrams to PNG
java -jar plantuml.jar -tpng ./docs/*/diagrams/*.puml

# Or use Docker if Java is not installed
docker run --rm -v $(pwd):/data plantuml/plantuml -tpng /data/docs/*/diagrams/*.puml
```

### Scenario 5: Monitor contract surface changes during development

```bash
# Start of sprint: save baseline
seamq scan ./app --save-baseline sprint-start.json

# During development: check for unintended changes
seamq scan ./app
seamq diff sprint-start.json

# Review: generate updated docs
seamq generate --all --format html --output-dir ./review-docs
seamq diagram --all --output-dir ./review-docs

# Render for review
java -jar plantuml.jar -tpng ./review-docs/*/diagrams/*.puml
```

### Scenario 6: Quick preview without installing Java

```bash
# Generate diagrams
seamq diagram --all

# Preview in VS Code (requires PlantUML extension)
code ./seamq-output/*/diagrams/*.puml
# Press Alt+D in any .puml file to preview
```
