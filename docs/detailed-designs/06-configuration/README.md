# Detailed Design: Configuration System

**Module:** `SeamQ.Configuration`
**Target Framework:** .NET 8
**Status:** Draft
**Last Updated:** 2026-03-30

---

## 1. Overview

The Configuration System is responsible for loading, validating, and providing SeamQ's runtime configuration to all other modules. Configuration is assembled from three sources, applied in precedence order (highest wins):

1. **CLI flags** -- per-invocation overrides (`--config`, `--output-dir`, `--verbose`, etc.).
2. **Config file** -- a `seamq.config.json` file discovered in the current directory or specified via `--config <path>`.
3. **Built-in defaults** -- sensible defaults so that SeamQ works out-of-the-box with zero configuration.

The module uses `Microsoft.Extensions.Configuration` to bind the JSON file into strongly-typed C# models, then merges CLI overrides on top. A validation pass ensures all values are legal before the configuration is handed to the DI container.

Additionally, the `seamq init` command provides an interactive wizard that generates a starter `seamq.config.json` by prompting the user for workspace paths, aliases, roles, and output preferences.

### Configuration Precedence

```
CLI Flags  (highest priority)
    |
    v
seamq.config.json
    |
    v
Built-in Defaults  (lowest priority)
```

### Example `seamq.config.json`

```json
{
  "workspaces": [
    {
      "path": "../framework",
      "alias": "Framework",
      "role": "framework"
    },
    {
      "path": "../plugin-a",
      "alias": "Plugin A",
      "role": "plugin"
    }
  ],
  "seams": {
    "include": ["PluginContract", "SharedLibrary"],
    "exclude": [],
    "customDecorators": ["@SeamBoundary", "@ContractPoint"]
  },
  "output": {
    "directory": "./seamq-output",
    "formats": ["html", "markdown"],
    "diagrams": {
      "renderFormat": "svg",
      "plantumlServer": "local",
      "theme": "plain",
      "skinparams": {
        "linetype": "ortho",
        "classFontSize": "12"
      }
    }
  },
  "analysis": {
    "maxDepth": 10,
    "followNodeModules": false,
    "includeInternalSeams": false,
    "confidenceThreshold": 0.5
  },
  "icd": {
    "title": "Interface Control Document",
    "documentNumber": "ICD-001",
    "revision": "A",
    "classification": "UNCLASSIFIED",
    "template": null
  }
}
```

---

## 2. Component Diagrams

### 2.1 Configuration Model Class Diagram

![Configuration Model Class Diagram](diagrams/class-configuration.png)

### 2.2 Configuration Services Class Diagram

![Configuration Services Class Diagram](diagrams/class-config-loader.png)

### 2.3 C4 Component Diagram

![C4 Component Diagram - Configuration Flow](diagrams/c4-component-config.png)

---

## 3. Sequence Diagrams

### 3.1 Config Loading Sequence

![Config Loading Sequence](diagrams/seq-load-config.png)

### 3.2 Init Wizard Sequence

![Init Wizard Sequence](diagrams/seq-init-wizard.png)

---

## 4. State Diagram

### 4.1 Config Loading States

![Config Loading State Diagram](diagrams/state-config-loading.png)

---

## 5. Component Descriptions

### 5.1 SeamQConfig

**Responsibility:** Root configuration model that aggregates all configuration sections.

`SeamQConfig` is an immutable record that serves as the single top-level configuration object consumed by every module in the SeamQ pipeline. It is registered as a singleton in the DI container.

```csharp
public record SeamQConfig
{
    public IReadOnlyList<WorkspaceConfig> Workspaces { get; init; } = [];
    public SeamFilterConfig Seams { get; init; } = new();
    public OutputConfig Output { get; init; } = new();
    public AnalysisConfig Analysis { get; init; } = new();
    public IcdMetadataConfig Icd { get; init; } = new();
}
```

Each nested section is independently defaulted so that a completely empty config file (or no config file at all) produces a valid `SeamQConfig` with all defaults applied.

### 5.2 WorkspaceConfig

**Responsibility:** Defines a single workspace to be scanned.

```csharp
public record WorkspaceConfig
{
    public required string Path { get; init; }
    public string? Alias { get; init; }
    public WorkspaceRole Role { get; init; } = WorkspaceRole.Application;
}

public enum WorkspaceRole
{
    Framework,
    Plugin,
    Library,
    Application
}
```

| Property | Type | Default | Description |
|---|---|---|---|
| `Path` | `string` | (required) | Absolute or relative path to workspace root. Relative paths are resolved against the config file's directory. |
| `Alias` | `string?` | Derived from directory name | Human-readable label used in generated output. |
| `Role` | `WorkspaceRole` | `Application` | Semantic role that influences seam detection priority and ICD framing. |

Path resolution rules:
- Absolute paths are used as-is.
- Relative paths are resolved relative to the directory containing `seamq.config.json`.
- When workspaces come from CLI arguments (no config file), relative paths are resolved from the current working directory.

### 5.3 SeamFilterConfig

**Responsibility:** Controls which seam types are detected and reported.

```csharp
public record SeamFilterConfig
{
    public IReadOnlyList<string> Include { get; init; } = [];
    public IReadOnlyList<string> Exclude { get; init; } = [];
    public IReadOnlyList<string> CustomDecorators { get; init; } = [];
}
```

| Property | Type | Default | Description |
|---|---|---|---|
| `Include` | `string[]` | `[]` (all types) | Seam type names to include. Empty means include all. Valid values: `PluginContract`, `SharedLibrary`, `MessageBus`, `RouteContract`, `StateContract`, `HttpApiContract`, `CustomDecorator`. |
| `Exclude` | `string[]` | `[]` | Seam type names to exclude. Applied after include. |
| `CustomDecorators` | `string[]` | `[]` | Decorator names (e.g., `@SeamBoundary`) that mark custom seam boundaries. Consumed by the `CustomDecoratorStrategy` in the detection module. |

Filter evaluation order:
1. If `Include` is non-empty, only those seam types are considered.
2. If `Include` is empty, all seam types are considered.
3. Any seam type in `Exclude` is removed from the set.
4. `CustomDecorators` are always passed through to `CustomDecoratorStrategy` regardless of include/exclude filters.

### 5.4 OutputConfig

**Responsibility:** Controls output directory, file formats, and diagram rendering settings.

```csharp
public record OutputConfig
{
    public string Directory { get; init; } = "./seamq-output";
    public IReadOnlyList<string> Formats { get; init; } = ["html", "markdown"];
    public DiagramOutputConfig Diagrams { get; init; } = new();
}

public record DiagramOutputConfig
{
    public string RenderFormat { get; init; } = "svg";
    public string PlantumlServer { get; init; } = "local";
    public string Theme { get; init; } = "plain";
    public IReadOnlyDictionary<string, string> Skinparams { get; init; }
        = new Dictionary<string, string>();
}
```

| Property | Type | Default | Description |
|---|---|---|---|
| `Directory` | `string` | `./seamq-output` | Path for all generated output. Resolved like workspace paths (relative to config file). |
| `Formats` | `string[]` | `["html", "markdown"]` | Output format(s) for ICD documents. Valid values: `html`, `markdown`, `json`. |
| `Diagrams.RenderFormat` | `string` | `svg` | Rendered image format. Valid values: `svg`, `png`. |
| `Diagrams.PlantumlServer` | `string` | `local` | PlantUML rendering target. `local` = local JAR, `docker` = Docker container, or a URL for a remote server. |
| `Diagrams.Theme` | `string` | `plain` | PlantUML theme name. |
| `Diagrams.Skinparams` | `Dictionary<string,string>` | `{}` | Additional PlantUML `skinparam` key-value pairs injected into every diagram. |

### 5.5 AnalysisConfig

**Responsibility:** Controls the depth and scope of static analysis.

```csharp
public record AnalysisConfig
{
    public int MaxDepth { get; init; } = 10;
    public bool FollowNodeModules { get; init; } = false;
    public bool IncludeInternalSeams { get; init; } = false;
    public double ConfidenceThreshold { get; init; } = 0.5;
}
```

| Property | Type | Default | Description |
|---|---|---|---|
| `MaxDepth` | `int` | `10` | Maximum import-chain traversal depth. Prevents runaway analysis on deeply nested imports. |
| `FollowNodeModules` | `bool` | `false` | Whether to follow imports into `node_modules`. When `false`, `node_modules` imports are recorded but not traversed. |
| `IncludeInternalSeams` | `bool` | `false` | Whether to report seams within a single workspace. When `false`, only cross-workspace seams are reported. |
| `ConfidenceThreshold` | `double` | `0.5` | Minimum confidence score (0.0--1.0) for a seam to be included in output. |

### 5.6 IcdMetadataConfig

**Responsibility:** Provides metadata fields injected into the header and front-matter of generated ICD documents.

```csharp
public record IcdMetadataConfig
{
    public string Title { get; init; } = "Interface Control Document";
    public string? DocumentNumber { get; init; }
    public string? Revision { get; init; }
    public string? Classification { get; init; }
    public string? Template { get; init; }
}
```

| Property | Type | Default | Description |
|---|---|---|---|
| `Title` | `string` | `"Interface Control Document"` | Document title for the ICD header. |
| `DocumentNumber` | `string?` | `null` | Document identifier (e.g., `ICD-001`). |
| `Revision` | `string?` | `null` | Revision letter or number (e.g., `A`, `1.0`). |
| `Classification` | `string?` | `null` | Classification marking (e.g., `UNCLASSIFIED`). |
| `Template` | `string?` | `null` | Path to a custom ICD template file. When `null`, the built-in template is used. |

### 5.7 ConfigLoader

**Responsibility:** Discovers and loads the configuration file, deserializes it into `SeamQConfig`, and merges CLI overrides.

```csharp
public class ConfigLoader
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ConfigLoader> _logger;

    public ConfigLoader(IFileSystem fileSystem, ILogger<ConfigLoader> logger);

    public Task<SeamQConfig> LoadAsync(ConfigLoadContext context, CancellationToken ct);
}

public record ConfigLoadContext
{
    public string? ConfigFilePath { get; init; }
    public string WorkingDirectory { get; init; } = Directory.GetCurrentDirectory();
    public IReadOnlyDictionary<string, string> CliOverrides { get; init; }
        = new Dictionary<string, string>();
}
```

Loading pipeline:

1. **Discover** -- If `ConfigFilePath` is specified, use it. Otherwise, search for `seamq.config.json` in `WorkingDirectory`, then walk up parent directories (up to the filesystem root or a Git repository root).
2. **Read & Parse** -- Read the file and deserialize with `System.Text.Json`. Use `JsonSerializerOptions` with `PropertyNameCaseInsensitive = true` and `ReadCommentHandling = JsonCommentHandling.Skip` so that users can add comments.
3. **Resolve Paths** -- Convert all relative paths in `WorkspaceConfig.Path` and `OutputConfig.Directory` to absolute paths using the config file's directory as the base.
4. **Validate** -- Delegate to `ConfigValidator` to check value constraints.
5. **Merge CLI Overrides** -- Apply CLI flag values on top of the deserialized config. For example, `--output-dir` overrides `Output.Directory`.
6. **Return** -- Return the final `SeamQConfig` instance.

If no config file is found, the loader returns a `SeamQConfig` with all defaults applied (step 2 is skipped but steps 3-6 still run).

Error handling:
- Malformed JSON throws `ConfigurationException` with the file path and the `System.Text.Json` error message (including line/column).
- File not found at an explicitly specified `--config` path throws `ConfigurationException`.
- File not found during auto-discovery is not an error; defaults are used silently.

### 5.8 ConfigValidator

**Responsibility:** Validates the deserialized `SeamQConfig` and returns a list of validation errors.

```csharp
public class ConfigValidator
{
    public ConfigValidationResult Validate(SeamQConfig config);
}

public record ConfigValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<ConfigValidationError> Errors { get; init; } = [];
    public IReadOnlyList<ConfigValidationWarning> Warnings { get; init; } = [];
}

public record ConfigValidationError(string Path, string Message);
public record ConfigValidationWarning(string Path, string Message);
```

Validation rules:

| Rule | Severity | Description |
|---|---|---|
| Workspace path exists | Error | Each `WorkspaceConfig.Path` must resolve to an existing directory. |
| Workspace path contains marker file | Warning | Each workspace should contain `angular.json` or `nx.json`. |
| No duplicate workspace aliases | Error | Aliases must be unique across all workspaces. |
| Valid workspace role | Error | `Role` must be one of `framework`, `plugin`, `library`, `application`. |
| Valid seam type names | Error | `Seams.Include` and `Seams.Exclude` entries must match known seam type names. |
| Valid output formats | Error | `Output.Formats` entries must be one of `html`, `markdown`, `json`. |
| Valid render format | Error | `Output.Diagrams.RenderFormat` must be `svg` or `png`. |
| MaxDepth > 0 | Error | `Analysis.MaxDepth` must be a positive integer. |
| ConfidenceThreshold in range | Error | `Analysis.ConfidenceThreshold` must be between 0.0 and 1.0 inclusive. |
| Template file exists | Warning | If `Icd.Template` is set, the file should exist. |
| Output directory writable | Warning | The output directory (or its parent) should be writable. |

The validator returns all errors and warnings in a single pass rather than failing on the first error, so the user can fix multiple issues at once.

### 5.9 ConfigInitializer

**Responsibility:** Implements the interactive `seamq init` wizard that generates a starter `seamq.config.json`.

```csharp
public class ConfigInitializer
{
    private readonly IConsolePrompter _prompter;
    private readonly ConfigValidator _validator;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ConfigInitializer> _logger;

    public ConfigInitializer(
        IConsolePrompter prompter,
        ConfigValidator validator,
        IFileSystem fileSystem,
        ILogger<ConfigInitializer> logger);

    public Task<int> RunAsync(string outputDirectory, CancellationToken ct);
}
```

The wizard follows this flow:

1. **Check for existing config** -- If `seamq.config.json` already exists in the target directory, prompt the user to confirm overwrite.
2. **Prompt for workspaces** -- In a loop, ask for:
   - Workspace path (required, validated to exist).
   - Alias (optional, defaults to directory name).
   - Role (selection from `framework` / `plugin` / `library` / `application`).
   - "Add another workspace?" (yes/no).
3. **Prompt for output settings** -- Ask for:
   - Output directory (default: `./seamq-output`).
   - Output formats (multi-select from `html`, `markdown`, `json`).
4. **Prompt for diagram settings** -- Ask for:
   - Render format (select from `svg`, `png`).
   - PlantUML server (default: `local`).
5. **Build and validate** -- Assemble a `SeamQConfig` from the answers and run `ConfigValidator`. If errors are found, display them and allow the user to correct.
6. **Serialize and write** -- Serialize to indented JSON and write `seamq.config.json`.
7. **Confirm** -- Display a success message with the file path.

The `IConsolePrompter` abstraction allows the wizard to be tested without real console I/O. In production, it wraps `System.Console` with styled prompts using ANSI color codes.

---

## 6. CLI Override Mapping

The following CLI flags map to configuration properties. When a flag is present, it takes precedence over the config file value.

| CLI Flag | Config Property | Notes |
|---|---|---|
| `--config <path>` | (controls which config file to load) | Does not map to a property; controls the loader. |
| `--output-dir <path>` | `Output.Directory` | |
| `--verbose` | (logging level) | Sets `ILogger` minimum level to `Debug`. |
| `--quiet` | (logging level) | Sets `ILogger` minimum level to `Error`. |
| `--no-color` | (console formatting) | Disables ANSI color codes. |
| `--no-cache` | (scanner option) | Disables AST caching. |
| `--format <fmt>` | `Output.Formats` | Single format override. |
| `--confidence <val>` | `Analysis.ConfidenceThreshold` | |
| `--max-depth <val>` | `Analysis.MaxDepth` | |
| `--include-internal` | `Analysis.IncludeInternalSeams` | Sets to `true`. |

The `ConfigLoader` receives CLI overrides as a flat `Dictionary<string, string>` and applies them after JSON deserialization but before validation. This ensures that CLI values are also validated.

---

## 7. Integration with Microsoft.Extensions.Configuration

The configuration system integrates with the standard .NET configuration and DI infrastructure:

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSeamQConfiguration(
        this IServiceCollection services,
        ConfigLoadContext context)
    {
        services.AddSingleton<ConfigLoader>();
        services.AddSingleton<ConfigValidator>();
        services.AddSingleton<ConfigInitializer>();

        services.AddSingleton(sp =>
        {
            var loader = sp.GetRequiredService<ConfigLoader>();
            return loader.LoadAsync(context, CancellationToken.None)
                .GetAwaiter().GetResult();
        });

        // Register individual sections for targeted injection
        services.AddSingleton(sp => sp.GetRequiredService<SeamQConfig>().Output);
        services.AddSingleton(sp => sp.GetRequiredService<SeamQConfig>().Analysis);
        services.AddSingleton(sp => sp.GetRequiredService<SeamQConfig>().Icd);
        services.AddSingleton(sp => sp.GetRequiredService<SeamQConfig>().Seams);

        return services;
    }
}
```

This allows modules to inject only the configuration section they need. For example, the ICD generator can depend on `IcdMetadataConfig` directly rather than pulling the entire `SeamQConfig`.

---

## 8. Error Handling Strategy

The configuration system uses a **fail-fast** approach (unlike the scanner's fail-soft approach):

- **Missing config file (auto-discovery):** Not an error. Defaults are used. A debug-level log message is emitted.
- **Missing config file (explicit `--config`):** Fatal. Throws `ConfigurationException` with a message indicating the path does not exist.
- **Malformed JSON:** Fatal. Throws `ConfigurationException` with the JSON parser error including file path, line number, and column.
- **Unknown properties in JSON:** Silently ignored. This allows forward compatibility when older SeamQ versions encounter config files with newer properties.
- **Validation errors:** Fatal. All validation errors are collected and reported together, then a `ConfigValidationException` is thrown with the full list.
- **Validation warnings:** Non-fatal. Warnings are logged at warning level but do not prevent startup.
- **Init wizard errors:** The wizard catches validation errors and loops, giving the user a chance to correct their input rather than aborting.

---

## 9. File Discovery Strategy

The `ConfigLoader` searches for `seamq.config.json` using the following strategy:

1. If `--config <path>` is provided, use that exact path. If it does not exist, fail with an error.
2. Otherwise, check the current working directory for `seamq.config.json`.
3. If not found, walk up parent directories until one of these conditions is met:
   - `seamq.config.json` is found (use it).
   - A `.git` directory is found (stop searching; use defaults).
   - The filesystem root is reached (stop searching; use defaults).

This walk-up strategy allows running SeamQ from subdirectories within a project while still picking up the project-level config file, similar to how `.eslintrc` or `tsconfig.json` discovery works.

---

## 10. Dependencies

| Dependency | Purpose |
|---|---|
| `Microsoft.Extensions.Configuration` | Configuration infrastructure and binding. |
| `Microsoft.Extensions.Configuration.Json` | JSON configuration file provider. |
| `Microsoft.Extensions.DependencyInjection` | Service registration for config objects. |
| `Microsoft.Extensions.Logging` | Structured logging throughout the loader pipeline. |
| `System.Text.Json` | JSON serialization/deserialization for config file I/O. |
| `System.IO.Abstractions` | Filesystem abstraction for testability. |
