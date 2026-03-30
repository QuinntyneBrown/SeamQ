# Detailed Design: CLI Interface (SeamQ.Cli)

**Module:** `SeamQ.Cli`
**Traces to:** L1-5 (CLI Interface)
**Framework:** .NET 8, System.CommandLine, Microsoft.Extensions (DI, Logging, Configuration)
**Distribution:** `dotnet tool install seamq`

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Component Descriptions](#component-descriptions)
   - [Program.cs (Entry Point)](#programcs-entry-point)
   - [GlobalOptions](#globaloptions)
   - [Command Classes](#command-classes)
   - [IConsoleRenderer / ConsoleRenderer](#iconsolerenderer--consolerenderer)
   - [DI Registration](#di-registration)
   - [Logging Integration](#logging-integration)
   - [Configuration Binding](#configuration-binding)
4. [Command Details](#command-details)
   - [ScanCommand](#scancommand)
   - [ListCommand](#listcommand)
   - [GenerateCommand](#generatecommand)
   - [DiagramCommand](#diagramcommand)
   - [InspectCommand](#inspectcommand)
   - [ValidateCommand](#validatecommand)
   - [DiffCommand](#diffcommand)
   - [InitCommand](#initcommand)
   - [ExportCommand](#exportcommand)
   - [ServeCommand](#servecommand)
5. [Middleware Pipeline](#middleware-pipeline)
6. [Exit Codes](#exit-codes)
7. [Diagrams](#diagrams)
8. [Traceability](#traceability)

---

## Overview

The SeamQ CLI (`seamq`) is the primary user interface for workspace scanning, seam detection, ICD generation, diagram generation, contract validation, and data export. It is implemented as a .NET 8 global tool distributed via NuGet, built on the `System.CommandLine` library for argument parsing and the `Microsoft.Extensions.*` libraries for dependency injection, structured logging, and layered configuration.

The CLI follows a **file-per-command** pattern where each command (`scan`, `list`, `generate`, etc.) is defined in its own class file. All commands share a set of global options (`--verbose`, `--quiet`, `--no-color`, `--output-dir`, `--config`) and are composed into a `RootCommand` at startup. A middleware pipeline intercepts every invocation to configure logging verbosity, load configuration, wire up DI, and handle unhandled exceptions.

### Key Design Principles

- **Single Responsibility:** Each command file owns its argument definitions, validation, and handler logic.
- **Dependency Injection:** Command handlers never instantiate services directly; all dependencies are resolved from the DI container.
- **Testability:** The `IConsoleRenderer` abstraction enables unit-testing command output without a real terminal.
- **Cross-Platform:** The CLI runs identically on Windows, macOS, and Linux. ANSI color support is detected at runtime and can be disabled via `--no-color`.
- **Deterministic Output:** Given the same inputs and configuration, the CLI produces byte-identical output (L1-10).

---

## Architecture

The CLI follows a layered architecture where `System.CommandLine` handles parsing and routing, a middleware pipeline performs cross-cutting concerns, and command handlers delegate to core services resolved from the DI container.

![C4 Component Diagram - CLI Internals](diagrams/c4-component-cli.png)

**Execution flow summary:**

1. `Program.Main` builds the DI container and `RootCommand`.
2. `System.CommandLine` parses `argv` and identifies the target command.
3. Middleware runs: logging configuration, config file loading, DI scope creation.
4. The matched command handler is invoked with parsed options and DI-resolved services.
5. The handler delegates to core services (scanner, detector, generator, etc.).
6. Results are rendered to the console via `IConsoleRenderer`.
7. An appropriate exit code is returned.

![Sequence - Command Execution](diagrams/seq-command-execution.png)

---

## Component Descriptions

### Program.cs (Entry Point)

**File:** `src/SeamQ.Cli/Program.cs`
**Responsibility:** Application entry point. Configures the DI container, builds the root command tree, registers middleware, and invokes the command line parser.

```
Program.Main(args)
  -> BuildServiceProvider()
  -> BuildRootCommand(serviceProvider)
  -> rootCommand.InvokeAsync(args)
```

**Key behaviors:**

- Creates a `ServiceCollection` and registers all core services, command handlers, and the console renderer.
- Builds `IConfiguration` from `seamq.config.json` (auto-discovered or via `--config`), environment variables, and CLI arguments.
- Registers `ILogger<T>` via `Microsoft.Extensions.Logging` with console provider.
- Constructs the `RootCommand` and adds all sub-commands and global options.
- Registers a `CommandLineBuilder` middleware pipeline for cross-cutting concerns.
- Returns the integer exit code from `InvokeAsync`.

### GlobalOptions

**File:** `src/SeamQ.Cli/GlobalOptions.cs`
**Responsibility:** Defines the shared CLI options available on every command.
**Traces to:** L2-5.10

| Option | Type | Default | Description |
|---|---|---|---|
| `--verbose` | `bool` | `false` | Enables detailed logging output |
| `--quiet` | `bool` | `false` | Suppresses all output except errors |
| `--no-color` | `bool` | `false` | Disables ANSI color codes in output |
| `--output-dir` | `DirectoryInfo?` | config value | Overrides the configured output directory |
| `--config` | `FileInfo?` | `seamq.config.json` | Path to custom configuration file |

`GlobalOptions` is a static class that exposes `Option<T>` instances. These are added to the `RootCommand` so they propagate to all sub-commands. Middleware reads their parsed values from the `InvocationContext` to configure logging levels, color mode, and configuration source before the command handler executes.

### Command Classes

Each command is implemented as a class that inherits from or wraps a `System.CommandLine.Command`. The class defines its arguments, options, and a handler method. The handler receives parsed values and DI-resolved services.

![Class Diagram - CLI Commands](diagrams/class-cli-commands.png)

**File-per-command layout:**

```
src/SeamQ.Cli/
  Program.cs
  GlobalOptions.cs
  Commands/
    ScanCommand.cs
    ListCommand.cs
    GenerateCommand.cs
    DiagramCommand.cs
    InspectCommand.cs
    ValidateCommand.cs
    DiffCommand.cs
    InitCommand.cs
    ExportCommand.cs
    ServeCommand.cs
```

Each command class follows this pattern:

1. Constructor defines the command name, description, arguments, and options.
2. A `SetHandler` call wires the handler method, binding parsed symbols and DI services.
3. The handler method is `async Task<int>`, returning the exit code.

### IConsoleRenderer / ConsoleRenderer

**Files:** `src/SeamQ.Cli/Rendering/IConsoleRenderer.cs`, `src/SeamQ.Cli/Rendering/ConsoleRenderer.cs`
**Responsibility:** Abstracts all formatted console output so that command handlers never write directly to `Console`.

![Class Diagram - Console Renderer](diagrams/class-console-renderer.png)

**`IConsoleRenderer` methods:**

| Method | Description |
|---|---|
| `WriteTable(headers, rows)` | Renders a formatted table with column alignment |
| `WriteSuccess(message)` | Writes a green success message |
| `WriteWarning(message)` | Writes a yellow warning message |
| `WriteError(message)` | Writes a red error message |
| `WriteInfo(message)` | Writes an informational message |
| `WriteHeading(text)` | Writes a bold section heading |
| `WriteKeyValue(key, value)` | Writes a key-value pair with alignment |
| `WriteJson(obj)` | Writes syntax-highlighted JSON |
| `StartProgress(description)` | Starts a progress spinner/bar and returns `IProgress<int>` |
| `WriteGrouped(title, items)` | Writes a titled group of items with indentation |
| `WriteDiff(added, modified, removed)` | Writes a color-coded diff summary |
| `NewLine()` | Writes a blank line |

**`ConsoleRenderer` implementation:**

- Uses ANSI escape codes for colored output (or optionally Spectre.Console for richer formatting).
- Respects the `--no-color` flag by stripping escape codes when color is disabled.
- Respects the `--quiet` flag by suppressing all output except `WriteError`.
- Table rendering calculates column widths from data and pads with spaces for alignment.
- Progress indicators use carriage-return overwriting for spinner animation.
- Registered as `IConsoleRenderer` -> `ConsoleRenderer` singleton in DI.

### DI Registration

**File:** `src/SeamQ.Cli/ServiceCollectionExtensions.cs`
**Responsibility:** Extension methods on `IServiceCollection` to register all CLI and core services.

![Class Diagram - DI Registration](diagrams/class-di-registration.png)

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSeamQCli(this IServiceCollection services)
    {
        // Core services
        services.AddSingleton<IWorkspaceScanner, WorkspaceScanner>();
        services.AddSingleton<ISeamDetector, SeamDetector>();
        services.AddSingleton<IIcdGenerator, IcdGenerator>();
        services.AddSingleton<IDiagramGenerator, DiagramGenerator>();
        services.AddSingleton<IBaselineDiffer, BaselineDiffer>();
        services.AddSingleton<IContractValidator, ContractValidator>();
        services.AddSingleton<IDataExporter, DataExporter>();

        // CLI services
        services.AddSingleton<IConsoleRenderer, ConsoleRenderer>();
        services.AddSingleton<IConfigurationLoader, ConfigurationLoader>();

        return services;
    }
}
```

The DI container is built once in `Program.Main` and the `IServiceProvider` is made available to command handlers via the `System.CommandLine` binding mechanism. Each command handler declares its dependencies as parameters, and the middleware resolves them from the container before invocation.

### Logging Integration

**Dependency:** `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Logging.Console`

Logging is configured in `Program.cs` during service registration:

- Default log level: `Warning`.
- When `--verbose` is set: log level changes to `Debug`.
- When `--quiet` is set: log level changes to `Error`.
- Log output goes to `stderr` so that `stdout` remains clean for piped/redirected data (e.g., `seamq export --all | jq`).
- Each core service receives an `ILogger<T>` via constructor injection.

```csharp
services.AddLogging(builder =>
{
    builder.AddConsole(options =>
    {
        options.LogToStandardErrorThreshold = LogLevel.Trace;
    });
    builder.SetMinimumLevel(verbose ? LogLevel.Debug : quiet ? LogLevel.Error : LogLevel.Warning);
});
```

### Configuration Binding

**Dependency:** `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.Configuration.Json`

Configuration is loaded in layers with later sources overriding earlier ones:

1. **Defaults:** Hard-coded default values.
2. **Config file:** `seamq.config.json` from the current directory (auto-discovered) or the path specified by `--config`.
3. **Environment variables:** Prefixed with `SEAMQ_` (e.g., `SEAMQ_OUTPUT_DIR`).
4. **CLI arguments:** `--output-dir`, `--verbose`, etc. override everything.

The configuration is bound to a strongly-typed `SeamQOptions` class:

```csharp
services.Configure<SeamQOptions>(configuration.GetSection("seamq"));
```

Key configuration sections map directly to the L2-6 requirements: workspace definitions, seam filtering, output settings, analysis options, and ICD metadata.

---

## Command Details

### ScanCommand

**File:** `src/SeamQ.Cli/Commands/ScanCommand.cs`
**Traces to:** L2-5.1

**Syntax:**
```
seamq scan <path1> [path2] [pathN] [--save-baseline <path>]
```

**Arguments and options:**

| Symbol | Type | Required | Description |
|---|---|---|---|
| `paths` | `string[]` | No | Workspace root paths to scan. Falls back to config. |
| `--save-baseline` | `FileInfo?` | No | Save scan result as baseline JSON file. |

**Handler logic:**

1. Resolve `IWorkspaceScanner` and `ISeamDetector` from DI.
2. Determine workspace paths from arguments or configuration.
3. For each path, call `IWorkspaceScanner.ScanAsync(path)`.
4. Pass scanned workspaces to `ISeamDetector.DetectSeamsAsync(workspaces)`.
5. Render summary table via `IConsoleRenderer`: workspace name, project count, export count.
6. Render total seam count.
7. If `--save-baseline` specified, serialize the seam registry to JSON at the given path.
8. Return exit code 0 on success, 1 on partial failure, 2 on fatal error.

![Sequence - Scan Command](diagrams/seq-scan-command.png)

### ListCommand

**File:** `src/SeamQ.Cli/Commands/ListCommand.cs`
**Traces to:** L2-5.2

**Syntax:**
```
seamq list [--type <type>] [--provider <name>] [--confidence <threshold>]
```

**Options:**

| Option | Type | Default | Description |
|---|---|---|---|
| `--type` | `string?` | all | Filter by seam type (e.g., `PluginContract`, `SharedLibrary`) |
| `--provider` | `string?` | all | Filter by provider workspace name |
| `--confidence` | `double?` | 0.0 | Minimum confidence threshold (0.0 - 1.0) |

**Handler logic:**

1. Require a prior scan (load from cache or report error).
2. Apply filters to the seam registry.
3. Render a table with columns: ID, Seam Name, Type, Provider, Consumer(s), Confidence.
4. Return exit code 0.

### GenerateCommand

**File:** `src/SeamQ.Cli/Commands/GenerateCommand.cs`
**Traces to:** L2-5.3

**Syntax:**
```
seamq generate <seam-id> [--format md|html|pdf|docx] [--all]
```

**Arguments and options:**

| Symbol | Type | Required | Description |
|---|---|---|---|
| `seam-id` | `string?` | No* | Seam identifier to generate ICD for |
| `--format` | `string[]` | No | Output format(s). Default: `md`. Multiple allowed. |
| `--all` | `bool` | No | Generate for all detected seams |

*Either `seam-id` or `--all` is required.

**Handler logic:**

1. Resolve `IIcdGenerator` from DI.
2. Determine target seam(s) from argument or `--all`.
3. For each seam, call `IIcdGenerator.GenerateAsync(seam, formats)`.
4. Render list of generated file paths via `IConsoleRenderer`.
5. Return exit code 0.

### DiagramCommand

**File:** `src/SeamQ.Cli/Commands/DiagramCommand.cs`
**Traces to:** L2-5.4

**Syntax:**
```
seamq diagram <seam-id> [--type <type>] [--all]
```

**Options:**

| Option | Type | Default | Description |
|---|---|---|---|
| `seam-id` | `string?` | None | Target seam identifier |
| `--type` | `string?` | all types | Diagram type: `context`, `class`, `sequence`, `state`, `c4-context`, `c4-container`, `c4-component`, `c4-code` |
| `--all` | `bool` | `false` | Generate for all detected seams |

**Handler logic:**

1. Resolve `IDiagramGenerator` from DI.
2. Determine target seam(s) and diagram type(s).
3. Generate `.puml` files and optionally render to SVG/PNG via PlantUML server.
4. Render list of generated file paths.
5. Return exit code 0.

### InspectCommand

**File:** `src/SeamQ.Cli/Commands/InspectCommand.cs`
**Traces to:** L2-5.5

**Syntax:**
```
seamq inspect <seam-id>
```

**Handler logic:**

1. Locate the seam by ID in the registry.
2. Render seam metadata: Type, Provider, Consumers, Confidence.
3. Render contract surface elements grouped by category (Interfaces, Injection Tokens, Abstract Classes, Types/Enums, Input/Output Bindings, Methods).
4. Each element shows source file path and line number.
5. Return exit code 0.

### ValidateCommand

**File:** `src/SeamQ.Cli/Commands/ValidateCommand.cs`
**Traces to:** L2-5.6

**Syntax:**
```
seamq validate <seam-id> [--all]
```

**Handler logic:**

1. Resolve `IContractValidator` from DI.
2. Validate target seam(s) against consumer implementations.
3. Render per-consumer output with checkmarks/crosses for each contract element.
4. Render summary with error and warning counts.
5. Return exit code 0 if all pass, 1 if validation errors exist.

### DiffCommand

**File:** `src/SeamQ.Cli/Commands/DiffCommand.cs`
**Traces to:** L2-5.7

**Syntax:**
```
seamq diff <baseline-path>
```

**Handler logic:**

1. Resolve `IBaselineDiffer` from DI.
2. Load baseline JSON from the given path.
3. Compare against current scan results.
4. Render ADDED, MODIFIED, REMOVED contract elements per seam using `WriteDiff`.
5. Render summary with total change counts.
6. Return exit code 0 for no changes, 1 for changes detected.

### InitCommand

**File:** `src/SeamQ.Cli/Commands/InitCommand.cs`
**Traces to:** L2-5.8

**Syntax:**
```
seamq init
```

**Handler logic:**

1. Check if `seamq.config.json` already exists; warn if overwriting.
2. Prompt for workspace paths, aliases, and roles.
3. Prompt for output directory and format preferences.
4. Validate that workspace paths exist.
5. Write `seamq.config.json` to the current directory.
6. Return exit code 0.

### ExportCommand

**File:** `src/SeamQ.Cli/Commands/ExportCommand.cs`
**Traces to:** L2-5.9

**Syntax:**
```
seamq export <seam-id> [--all] [--format json]
```

**Handler logic:**

1. Resolve `IDataExporter` from DI.
2. Determine target seam(s).
3. Serialize seam data to JSON.
4. Write to `stdout` by default, or to a file if `--output-dir` is set.
5. Return exit code 0.

### ServeCommand

**File:** `src/SeamQ.Cli/Commands/ServeCommand.cs`
**Traces to:** L2-5.12

**Syntax:**
```
seamq serve [--port <number>]
```

**Options:**

| Option | Type | Default | Description |
|---|---|---|---|
| `--port` | `int` | `5050` | HTTP server port number |

**Handler logic:**

1. Locate generated HTML ICDs in the output directory.
2. Launch a Kestrel-based HTTP server on the specified port.
3. Serve an index page listing all available ICDs.
4. Serve individual ICD HTML files with navigation.
5. Display the URL in the console.
6. Block until Ctrl+C.
7. Return exit code 0.

---

## Middleware Pipeline

The `CommandLineBuilder` is configured with middleware that runs for every command invocation, in order:

1. **Exception Handling Middleware:** Catches unhandled exceptions, logs them, renders a user-friendly error via `IConsoleRenderer.WriteError`, and returns exit code 2.

2. **Configuration Middleware:** Reads the `--config` global option. Loads `seamq.config.json` from the specified path or auto-discovers it in the current directory. Binds to `SeamQOptions`.

3. **Logging Middleware:** Reads `--verbose` and `--quiet` global options. Reconfigures the minimum log level on the logging provider accordingly.

4. **Color Middleware:** Reads `--no-color` global option. If set, configures `ConsoleRenderer` to strip ANSI codes. Also detects `NO_COLOR` environment variable per the `no-color.org` convention.

5. **DI Scope Middleware:** Creates a new DI scope for the command invocation. Disposes the scope after the handler completes.

6. **Version Middleware:** Intercepts `--version` and prints the assembly informational version.

![State Diagram - Command Lifecycle](diagrams/state-command-lifecycle.png)

---

## Exit Codes

| Code | Meaning | Traces to |
|---|---|---|
| 0 | Success / No changes (diff) | L1-10 |
| 1 | Partial failure / Validation errors / Changes detected (diff) | L1-10 |
| 2 | Fatal error (unhandled exception, invalid arguments) | L1-10 |

---

## Diagrams

### Class Diagrams

![Class Diagram - CLI Commands](diagrams/class-cli-commands.png)

This diagram shows the `RootCommand` and all ten command classes with their arguments, options, and handler methods. `GlobalOptions` provides the shared options added to `RootCommand`.

![Class Diagram - DI Registration](diagrams/class-di-registration.png)

This diagram shows the `ServiceCollectionExtensions` class and the service registration pattern, illustrating which interfaces map to which implementations.

![Class Diagram - Console Renderer](diagrams/class-console-renderer.png)

This diagram shows the `IConsoleRenderer` interface, the `ConsoleRenderer` implementation, and related types for table rendering and progress indication.

### Sequence Diagrams

![Sequence Diagram - Command Execution](diagrams/seq-command-execution.png)

This diagram traces the full lifecycle from CLI invocation through argument parsing, middleware execution, DI resolution, handler execution, output rendering, and exit code return.

![Sequence Diagram - Scan Command](diagrams/seq-scan-command.png)

This diagram shows the `ScanCommand` handler resolving `IWorkspaceScanner` and `ISeamDetector` from DI, scanning each workspace path, detecting seams, and rendering results.

### State Diagram

![State Diagram - Command Lifecycle](diagrams/state-command-lifecycle.png)

This diagram models the command lifecycle states: Parsing, Validating, Resolving, Executing, Rendering, and the terminal ExitCode state.

### C4 Component Diagram

![C4 Component Diagram - CLI Internals](diagrams/c4-component-cli.png)

This C4 component diagram shows the internal structure of the `SeamQ.Cli` container, including the command classes, middleware pipeline, console renderer, DI container, and their relationships to core library components.

---

## Traceability

| Requirement | Component | Section |
|---|---|---|
| L2-5.1 | `ScanCommand` | [ScanCommand](#scancommand) |
| L2-5.2 | `ListCommand` | [ListCommand](#listcommand) |
| L2-5.3 | `GenerateCommand` | [GenerateCommand](#generatecommand) |
| L2-5.4 | `DiagramCommand` | [DiagramCommand](#diagramcommand) |
| L2-5.5 | `InspectCommand` | [InspectCommand](#inspectcommand) |
| L2-5.6 | `ValidateCommand` | [ValidateCommand](#validatecommand) |
| L2-5.7 | `DiffCommand` | [DiffCommand](#diffcommand) |
| L2-5.8 | `InitCommand` | [InitCommand](#initcommand) |
| L2-5.9 | `ExportCommand` | [ExportCommand](#exportcommand) |
| L2-5.10 | `GlobalOptions` | [GlobalOptions](#globaloptions) |
| L2-5.11 | `Program.cs`, Middleware | [Middleware Pipeline](#middleware-pipeline) |
| L2-5.12 | `ServeCommand` | [ServeCommand](#servecommand) |
