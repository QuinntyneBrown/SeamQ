using SeamQ.Cli.Rendering;
using SeamQ.Core.Models;

namespace SeamQ.Cli;

/// <summary>
/// Generates annotated code files and LLM prompt files for the --prompt (-p) option.
/// When prompt mode is active, commands delegate to this service instead of running
/// their normal logic.
/// </summary>
public sealed class PromptFileGenerator
{
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "dist", "build", ".git", ".angular", ".nx", "coverage", "tmp", ".cache"
    };

    private static readonly HashSet<string> IncludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ts", ".json", ".yaml", ".yml"
    };

    private static readonly HashSet<string> ConfigFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "angular.json", "project.json", "nx.json", "workspace.json",
        "tsconfig.json", "tsconfig.base.json", "tsconfig.app.json", "tsconfig.lib.json",
        "package.json"
    };

    private readonly IConsoleRenderer _renderer;

    public PromptFileGenerator(IConsoleRenderer renderer)
    {
        _renderer = renderer;
    }

    /// <summary>
    /// Generates the annotated code file and prompt file for a workspace and command.
    /// Returns the paths of the two generated files.
    /// </summary>
    public async Task<(string codeFile, string promptFile)> GenerateAsync(
        Workspace workspace,
        string commandName,
        string outputDir,
        string? extraContext = null)
    {
        Directory.CreateDirectory(outputDir);

        var alias = SanitizeAlias(workspace.Alias);
        var codeFilePath = Path.Combine(outputDir, $"seamq-code-{alias}.txt");
        var promptFilePath = Path.Combine(outputDir, $"seamq-prompt-{commandName}-{alias}.md");

        // Generate annotated code file
        var fileCount = await WriteCodeFileAsync(workspace, codeFilePath);

        // Generate prompt file
        var codeFileName = Path.GetFileName(codeFilePath);
        await WritePromptFileAsync(commandName, workspace, codeFileName, promptFilePath, extraContext);

        _renderer.WriteSuccess($"generated code file ({fileCount} files): {Path.GetRelativePath(Directory.GetCurrentDirectory(), codeFilePath)}");
        _renderer.WriteSuccess($"generated prompt file: {Path.GetRelativePath(Directory.GetCurrentDirectory(), promptFilePath)}");

        return (codeFilePath, promptFilePath);
    }

    /// <summary>
    /// Short-circuit helper: prints a warning for commands that don't support prompt mode.
    /// </summary>
    public void WriteUnsupportedWarning(string commandName)
    {
        _renderer.WriteWarning($"The '{commandName}' command is interactive and does not support --prompt mode.");
    }

    private async Task<int> WriteCodeFileAsync(Workspace workspace, string outputPath)
    {
        var files = DiscoverFiles(workspace.Path);
        await using var writer = new StreamWriter(outputPath, false, System.Text.Encoding.UTF8);

        // Header
        await writer.WriteLineAsync($"// SeamQ Annotated Code Export");
        await writer.WriteLineAsync($"// Workspace: {workspace.Alias} ({workspace.Type})");
        await writer.WriteLineAsync($"// Path: {workspace.Path}");
        await writer.WriteLineAsync($"// Projects: {workspace.Projects.Count}");
        await writer.WriteLineAsync($"// Files: {files.Count}");
        await writer.WriteLineAsync($"// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        await writer.WriteLineAsync();

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(workspace.Path, file).Replace('\\', '/');
            await writer.WriteLineAsync($"// File: {relativePath}");
            await writer.WriteLineAsync(await File.ReadAllTextAsync(file));
            await writer.WriteLineAsync();
        }

        return files.Count;
    }

    private static List<string> DiscoverFiles(string workspacePath)
    {
        var files = new List<string>();
        CollectFiles(workspacePath, workspacePath, files);
        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    private static void CollectFiles(string rootPath, string currentDir, List<string> files)
    {
        foreach (var file in Directory.GetFiles(currentDir))
        {
            var fileName = Path.GetFileName(file);
            var ext = Path.GetExtension(file);

            // Always include config files at any level
            if (ConfigFiles.Contains(fileName))
            {
                files.Add(file);
                continue;
            }

            // Include files with matching extensions
            if (IncludedExtensions.Contains(ext))
            {
                files.Add(file);
            }
        }

        foreach (var dir in Directory.GetDirectories(currentDir))
        {
            var dirName = Path.GetFileName(dir);
            if (!ExcludedDirectories.Contains(dirName))
            {
                CollectFiles(rootPath, dir, files);
            }
        }
    }

    private static async Task WritePromptFileAsync(
        string commandName,
        Workspace workspace,
        string codeFileName,
        string outputPath,
        string? extraContext)
    {
        var prompt = commandName.ToLowerInvariant() switch
        {
            "scan" => BuildScanPrompt(workspace, codeFileName),
            "list" => BuildListPrompt(workspace, codeFileName),
            "generate" => BuildGeneratePrompt(workspace, codeFileName, extraContext),
            "diagram" => BuildDiagramPrompt(workspace, codeFileName, extraContext),
            "inspect" => BuildInspectPrompt(workspace, codeFileName, extraContext),
            "validate" => BuildValidatePrompt(workspace, codeFileName),
            "diff" => BuildDiffPrompt(workspace, codeFileName),
            "export" => BuildExportPrompt(workspace, codeFileName),
            "doc" => BuildDocPrompt(workspace, codeFileName),
            "public-api" => BuildPublicApiPrompt(workspace, codeFileName),
            "public-icd" => BuildPublicIcdPrompt(workspace, codeFileName),
            "init" => BuildInitPrompt(workspace, codeFileName),
            _ => BuildGenericPrompt(commandName, workspace, codeFileName)
        };

        await File.WriteAllTextAsync(outputPath, prompt, System.Text.Encoding.UTF8);
    }

    private static string BuildScanPrompt(Workspace workspace, string codeFileName)
    {
        return $"""
            # Role

            You are an Expert Angular Architect specializing in static analysis of Angular and Nx workspaces.

            # Task

            Analyze the uploaded code file (`{codeFileName}`) which contains the complete source of the **{workspace.Alias}** workspace ({workspace.Type}).

            Produce a comprehensive **Workspace Scan Report** in Markdown identifying:

            ## 1. Workspace Structure
            - Workspace type (Angular CLI, Nx Monorepo, or Standalone)
            - List every project/library with its name, type (Application or Library), and source root path
            - Path aliases from tsconfig

            ## 2. Exports & Public API
            - For each project, list all publicly exported symbols (types, classes, interfaces, enums, functions, constants)
            - Group by project and by kind (Component, Service, Directive, Pipe, Guard, Interceptor, Module, Interface, Type, Enum, Function, Constant)
            - Include the file path and line number for each symbol

            ## 3. Imports & Dependencies
            - Map which projects import from which other projects
            - Identify cross-workspace dependencies
            - Flag circular dependencies if any

            ## 4. Angular Metadata
            - Services with @Injectable() and their providedIn scope
            - Components with their selectors, inputs, and outputs
            - Modules and their imports/exports/declarations/providers
            - Route configurations

            ## 5. Injection Tokens & Contracts
            - InjectionToken definitions
            - Abstract classes used as DI tokens
            - Factory providers

            # Output Format

            Use Markdown with clear headings, tables, and code blocks. Organize by project.

            # Rules

            - Do NOT invent or fabricate any symbols, files, or dependencies not present in the code
            - Do NOT omit any publicly exported type
            - Do NOT guess at Angular metadata — only report what is explicitly in the code
            - If a symbol's kind is ambiguous, state what you observe and why
            - Include file paths relative to the workspace root
            """;
    }

    private static string BuildListPrompt(Workspace workspace, string codeFileName)
    {
        return $"""
            # Role

            You are an Expert Software Architect specializing in interface boundary detection in Angular workspaces.

            # Task

            Analyze the uploaded code file (`{codeFileName}`) which contains the complete source of the **{workspace.Alias}** workspace ({workspace.Type}).

            Produce a **Seam Detection Report** — a table listing every detected interface boundary (seam) where types, services, tokens, bindings, events, routes, state, or messages cross project boundaries.

            ## Seam Table Columns

            | ID | Name | Type | Provider | Consumer(s) | Confidence | Description |
            |----|------|------|----------|-------------|------------|-------------|

            ## Seam Types to Detect

            1. **Plugin Contract** — Abstract classes, interfaces, or InjectionTokens that define extension points
            2. **Shared Library** — Libraries exporting types consumed by multiple projects
            3. **Message Bus** — Event emitters, subjects, or message-passing patterns crossing boundaries
            4. **Route Contract** — Route configurations that load remote modules or define shared route params
            5. **State Contract** — Shared state (NgRx store slices, services with shared observables)
            6. **HTTP/API Contract** — HTTP client calls to known API endpoints with typed request/response

            ## Confidence Scoring

            Rate each seam 0.0–1.0 based on:
            - Explicitness of the contract (explicit interface = higher)
            - Number of consumers
            - Presence of documentation or JSDoc

            # Rules

            - Do NOT invent seams that don't exist in the code
            - Do NOT miss seams where one project imports from another
            - Every seam must cite the specific file(s) and symbol(s) involved
            - Sort the table by confidence descending
            """;
    }

    private static string BuildGeneratePrompt(Workspace workspace, string codeFileName, string? extraContext)
    {
        var formatNote = extraContext ?? "Markdown";
        return $"""
            # Role

            You are an Expert Systems Engineer specializing in Interface Control Documents (ICDs) for complex software systems.

            # Task

            Analyze the uploaded code file (`{codeFileName}`) which contains the complete source of the **{workspace.Alias}** workspace ({workspace.Type}).

            Generate a complete **Interface Control Document (ICD)** in {formatNote} format, equivalent in depth and structure to an aerospace-grade ICD.

            ## Required ICD Structure

            ### 1. Introduction
            - Purpose, scope, and intended audience
            - Applicable documents and references

            ### 2. Interface Overview
            - System context and participating components
            - Interface topology diagram description

            ### 3. Interface Definitions
            For each detected interface boundary:
            - Interface ID, name, and classification
            - Provider and consumer identification
            - Data objects exchanged (with full type definitions)
            - Method signatures and parameters
            - Input/Output bindings
            - Lifecycle and initialization sequence
            - Error handling and failure modes

            ### 4. Data Dictionary
            - Every type, interface, enum, and constant crossing a boundary
            - Field-level descriptions, types, constraints, and defaults

            ### 5. Protocols & Patterns
            - Dependency injection patterns
            - Event/message protocols
            - State synchronization patterns

            ### 6. Appendices
            - Glossary of terms
            - Traceability matrix (requirement → interface → implementation)
            - TBD items

            # Rules

            - Do NOT invent interfaces, types, or contracts not present in the code
            - Do NOT omit any interface that crosses a project boundary
            - Do NOT use placeholder text like "TBD" for information available in the code
            - Every data object must include all fields with their TypeScript types
            - Use code blocks for type definitions and method signatures
            """;
    }

    private static string BuildDiagramPrompt(Workspace workspace, string codeFileName, string? extraContext)
    {
        var diagramType = extraContext ?? "all applicable types";
        return $$"""
            # Role

            You are an Expert Software Architect specializing in PlantUML diagram generation for Angular systems.

            # Task

            Analyze the uploaded code file (`{{codeFileName}}`) which contains the complete source of the **{{workspace.Alias}}** workspace ({{workspace.Type}}).

            Generate **PlantUML diagrams** ({{diagramType}}) for each detected interface boundary (seam).

            ## Diagram Types to Generate

            ### Class Diagrams
            For each seam, generate a class diagram showing:
            - Provider interfaces/classes with all public members (properties, methods)
            - Consumer classes that implement or depend on the provider
            - Relationships: implements, extends, depends-on, uses
            - Stereotypes: <<interface>>, <<abstract>>, <<service>>, <<component>>

            ```plantuml
            @startuml ClassName
            skinparam classAttributeIconSize 0
            ' Diagram content here
            @enduml
            ```

            ### Sequence Diagrams
            For key interaction flows:
            - Service injection and initialization
            - Data flow through the interface
            - Event/message passing sequences

            ### C4 Diagrams
            - **Context**: System boundary and external actors
            - **Container**: Angular apps and libraries as containers
            - **Component**: Key components within each container

            ## PlantUML Syntax Rules

            - Each diagram must start with `@startuml <DiagramName>` and end with `@enduml`
            - Use proper PlantUML class syntax: `class Name { +method(): ReturnType }`
            - Use `interface` keyword for TypeScript interfaces
            - Use proper arrow notation: `-->` (dependency), `..|>` (implements), `--|>` (extends)
            - Group related elements with `package` or `namespace`

            # Rules

            - Do NOT invent classes, methods, or relationships not in the code
            - Do NOT omit public members from class diagrams
            - Do NOT use generic names — use exact class/interface names from the code
            - Every diagram must be valid PlantUML syntax
            - Include access modifiers: `+` public, `#` protected, `-` private
            - Use proper TypeScript types, not Java types
            """;
    }

    private static string BuildInspectPrompt(Workspace workspace, string codeFileName, string? extraContext)
    {
        var seamContext = extraContext != null ? $"Focus specifically on the seam: **{extraContext}**." : "Analyze all detected seams.";
        return $"""
            # Role

            You are an Expert Software Architect specializing in contract surface analysis for Angular systems.

            # Task

            Analyze the uploaded code file (`{codeFileName}`) which contains the complete source of the **{workspace.Alias}** workspace ({workspace.Type}).

            {seamContext}

            Produce a **Detailed Contract Surface Analysis** for each seam, including:

            ## For Each Seam

            ### Identity
            - Seam ID, name, type, confidence score
            - Provider project and consumer project(s)

            ### Contract Elements (grouped by kind)
            - **Components**: selector, inputs (name, type, required), outputs (name, event type)
            - **Services**: injectable scope, public methods (signature, return type), public properties
            - **Directives**: selector, inputs, outputs, host bindings
            - **Pipes**: name, transform signature
            - **Guards/Interceptors**: implemented interface, method signatures
            - **Interfaces/Types**: full type definition with all members
            - **Enums**: all values with descriptions
            - **InjectionTokens**: token name, provided type, default value
            - **Route Contracts**: path patterns, route data, lazy-loaded modules

            ### Dependencies
            - What this seam depends on (imports from other seams)
            - What depends on this seam (downstream consumers)

            # Rules

            - Do NOT invent contract elements not present in the code
            - Do NOT omit any publicly exported member of a contract element
            - Include exact TypeScript type signatures
            - For each element, cite the file path and line number
            """;
    }

    private static string BuildValidatePrompt(Workspace workspace, string codeFileName)
    {
        return $"""
            # Role

            You are an Expert Software Quality Engineer specializing in contract compliance verification for Angular systems.

            # Task

            Analyze the uploaded code file (`{codeFileName}`) which contains the complete source of the **{workspace.Alias}** workspace ({workspace.Type}).

            Produce a **Contract Compliance Report** checking that every consumer correctly implements provider contracts.

            ## Checks to Perform

            ### Interface Implementation
            - Verify classes that claim to implement an interface actually implement all members
            - Check method signatures match (parameter types, return types)
            - Check property types match

            ### Injection Token Compliance
            - Verify every InjectionToken referenced by a consumer has a corresponding provider
            - Check that provided values match the expected type

            ### Input/Output Binding Compliance
            - Verify component/directive consumers pass all required inputs
            - Check input types match the expected types
            - Verify output event handlers match the expected event types

            ### Service Contract Compliance
            - Verify injected services are used according to their public API
            - Flag usage of private or non-exported members

            ## Report Format

            For each consumer project, produce:

            | Severity | Seam | Element | Issue | File:Line |
            |----------|------|---------|-------|-----------|

            Severity levels: ERROR (contract violation), WARNING (potential issue), INFO (observation)

            # Rules

            - Do NOT report false positives — only flag genuine violations
            - Do NOT flag issues with internal (non-exported) code
            - Every issue must cite the exact file and line
            - Distinguish between "not implemented" and "incorrectly implemented"
            """;
    }

    private static string BuildDiffPrompt(Workspace workspace, string codeFileName)
    {
        return $"""
            # Role

            You are an Expert Software Architect specializing in change impact analysis for Angular systems.

            # Task

            Analyze the uploaded code file (`{codeFileName}`) which contains the complete source of the **{workspace.Alias}** workspace ({workspace.Type}).

            Since you do not have a previous baseline to compare against, produce a **Baseline Snapshot** that documents the current state of all interface boundaries. This snapshot can be compared against future versions.

            ## Snapshot Contents

            For each detected seam:

            ### Contract Fingerprint
            - Seam ID, name, type
            - Provider and consumer(s)
            - Hash/summary of the contract surface

            ### Exported Symbols
            - List every exported symbol with: name, kind, file path, type signature
            - For classes/interfaces: list all public members with signatures

            ### Dependency Map
            - Cross-project import graph

            ## Output Format

            Produce the snapshot as structured Markdown with consistent formatting so that a future diff can be performed systematically.

            # Rules

            - Do NOT invent or omit any exported symbol
            - Be exhaustive — this is a baseline for future comparison
            - Include exact type signatures, not summaries
            """;
    }

    private static string BuildExportPrompt(Workspace workspace, string codeFileName)
    {
        return $$"""
            # Role

            You are an Expert Software Architect specializing in structured data extraction from Angular workspaces.

            # Task

            Analyze the uploaded code file (`{{codeFileName}}`) which contains the complete source of the **{{workspace.Alias}}** workspace ({{workspace.Type}}).

            Produce a **structured JSON export** of all detected interface boundaries (seams).

            ## JSON Schema

            ```json
            {
              "workspace": {
                "alias": "{{workspace.Alias}}",
                "type": "{{workspace.Type}}",
                "path": "{{workspace.Path}}"
              },
              "seams": [
                {
                  "id": "string",
                  "name": "string",
                  "type": "PluginContract|SharedLibrary|MessageBus|RouteContract|StateContract|HttpApiContract",
                  "confidence": 0.0,
                  "provider": {
                    "project": "string",
                    "sourceRoot": "string"
                  },
                  "consumers": [
                    {
                      "project": "string",
                      "sourceRoot": "string"
                    }
                  ],
                  "contractSurface": {
                    "elements": [
                      {
                        "name": "string",
                        "kind": "Component|Service|Interface|Type|Enum|InjectionToken|...",
                        "filePath": "string",
                        "lineNumber": 0,
                        "members": [
                          {
                            "name": "string",
                            "kind": "method|property|input|output",
                            "typeSignature": "string"
                          }
                        ]
                      }
                    ]
                  }
                }
              ]
            }
            ```

            # Rules

            - Do NOT invent seams, symbols, or relationships not in the code
            - The output must be valid JSON
            - Include ALL detected seams, not a subset
            - Use exact type signatures from the TypeScript code
            """;
    }

    private static string BuildDocPrompt(Workspace workspace, string codeFileName)
    {
        return $$"""
            # Role

            You are an Expert Software Architect specializing in API documentation generation for Angular 17+ libraries and applications.

            # Task

            Analyze the uploaded code file (`{{codeFileName}}`) which contains the complete source of the **{{workspace.Alias}}** workspace ({{workspace.Type}}).

            Generate comprehensive **API Reference Documentation** in Markdown for each project in the workspace.

            ## Per-Project Documentation

            For each project/library, generate a `README.md` containing:

            ### 1. Overview
            - Project name, type (Application/Library), and purpose (inferred from exports)

            ### 2. PlantUML Class Diagrams
            For each publicly exported class, interface, or type:
            ```plantuml
            @startuml ClassName
            skinparam classAttributeIconSize 0
            class ClassName {
              +property: Type
              +method(param: Type): ReturnType
            }
            @enduml
            ```

            ### 3. API Reference
            For each publicly exported symbol, organized by kind:

            #### Components
            - Selector, inputs (with types and defaults), outputs (with event types)
            - Lifecycle hooks implemented
            - Usage example

            #### Services
            - Injectable scope (root, module, component)
            - Public methods with full signatures
            - Public properties/observables

            #### Interfaces & Types
            - Full type definition with all members
            - JSDoc descriptions (from source or inferred)

            #### Enums
            - All values with descriptions

            #### Functions & Constants
            - Signature, parameters, return type

            ### 4. Dependency Graph
            - What this project imports from other projects

            # Rules

            - Do NOT include private or internal (non-exported) members
            - Do NOT invent methods, properties, or types not in the code
            - Do NOT omit any publicly exported type
            - Do NOT generate documentation for node_modules dependencies
            - Use exact TypeScript types, not simplified versions
            - Every PlantUML block must be valid syntax with @startuml/@enduml
            - Use `+` for public, `#` for protected, `-` for private in PlantUML
            - Include the file path for each documented symbol
            """;
    }

    private static string BuildPublicApiPrompt(Workspace workspace, string codeFileName)
    {
        return $$"""
            # Role

            You are an Expert Angular Architect specializing in public API surface documentation.

            # Task

            Analyze the uploaded code file (`{{codeFileName}}`) which contains the complete source of the **{{workspace.Alias}}** workspace ({{workspace.Type}}).

            Generate **Public API Documentation** for each Angular project/library, focusing exclusively on the publicly exported surface.

            ## Per-Project Output

            For each project, generate a Markdown document with:

            ### Public API Surface
            List every symbol exported via barrel files (`index.ts`, `public-api.ts`):

            #### Classes
            ```typescript
            export class ClassName {
              // public members only
            }
            ```

            #### Interfaces
            ```typescript
            export interface InterfaceName {
              // all members
            }
            ```

            #### Types, Enums, Functions, Constants
            - Full type definitions / signatures

            ### Re-exports
            - Symbols re-exported from other packages

            ### Breaking Change Surface
            - Which exports, if changed, would break consumers
            - Identify widely-consumed vs narrowly-consumed APIs

            # Rules

            - ONLY document symbols reachable through barrel exports (index.ts, public-api.ts)
            - Do NOT document internal implementation details
            - Do NOT invent exports not present in the code
            - Include exact TypeScript signatures
            """;
    }

    private static string BuildPublicIcdPrompt(Workspace workspace, string codeFileName)
    {
        return $$"""
            # Role

            You are an Expert Systems Architect specializing in Interface Control Documents for Angular applications and libraries.

            # Task

            Analyze the uploaded code file (`{{codeFileName}}`) which contains the complete source of the **{{workspace.Alias}}** workspace ({{workspace.Type}}).

            Generate a comprehensive **Public Interface Control Document (ICD)** in Markdown for each project in the workspace. If the workspace has only one project, generate a single ICD for the entire application.

            ## Required ICD Structure

            ### 1. Overview
            - Project name, type (Application/Library), workspace context
            - Summary table of public surface: count of Components, Services, Interfaces, Classes, Enums, Types, Injection Tokens
            - Brief auto-generated description of the project's purpose based on its exports

            ### 2. Type Descriptions
            For EVERY publicly exported type, organized by category (Components, Services, Classes, Interfaces, Enumerations, Type Aliases, Injection Tokens):

            Each type entry must include:
            - Name, kind, source file path
            - Extends/implements relationships
            - JSDoc description or auto-generated description
            - **Members table**: name, kind (Property/Input/Output/Signal/Observable), type signature, description
            - **Methods table**: full signature, return type, description

            ### 3. Sequence Diagrams (PlantUML)
            Generate PlantUML sequence diagrams for ALL observable behaviors:

            - **Service Interaction**: show consumer calling each service's public methods with return types
            - **Component Lifecycle**: show Angular framework instantiating components, binding inputs, triggering lifecycle hooks, emitting outputs
            - **Data Flow**: show observable subscription patterns, stream emissions
            - **HTTP/API Interaction**: show HTTP service call chains (consumer → service → backend → response)

            Each diagram must use valid PlantUML syntax:
            ```plantuml
            @startuml
            title Diagram Title
            participant "Name" as alias
            "from" -> "to" : message
            activate "to"
            "to" --> "from" : response
            deactivate "to"
            @enduml
            ```

            ### 4. Class Diagrams (PlantUML)
            Generate PlantUML class diagrams:

            - **Master Class Diagram**: all exported types grouped by category in `package` blocks
            - **Per-Category Diagrams**: separate diagram for each category with 2+ types

            Each diagram must show:
            - Class/interface/enum definitions with all public members
            - Access modifiers: `+` public
            - Relationships: `--|>` extends, `..|>` implements, `-->` depends
            - Stereotypes for special members: `<<input>>`, `<<output>>`, `<<signal>>`

            ### 5. C4 Architecture Diagrams (PlantUML)
            Generate all four C4 levels:

            - **System Context**: the project as main system, other workspace projects as external systems
            - **Container**: all projects in the workspace as containers within a system boundary
            - **Component**: type categories (Services, Components, etc.) as components within the project
            - **Code**: detailed class diagram equivalent

            Use PlantUML rectangles with stereotypes and color coding:
            ```plantuml
            @startuml
            skinparam rectangle {
              BackgroundColor<<system>> #438DD5
              FontColor<<system>> white
            }
            rectangle "Name\n[Type]" as alias <<system>>
            @enduml
            ```

            # Rules

            - Do NOT invent types, methods, properties, or relationships not present in the code
            - Do NOT omit any publicly exported type — the ICD must be exhaustive
            - Do NOT include private or internal (non-exported) members
            - Do NOT generate documentation for node_modules dependencies
            - Use exact TypeScript type signatures, not simplified versions
            - Every PlantUML diagram must be valid syntax with @startuml/@enduml
            - Barrel re-exports (index.ts, public-api.ts) should be traversed, not documented as types
            - For each type, include the source file path and line number
            - Escape pipe characters in Markdown tables
            """;
    }

    private static string BuildInitPrompt(Workspace workspace, string codeFileName)
    {
        return $$"""
            # Role

            You are an Expert Angular DevOps Engineer specializing in workspace configuration.

            # Task

            Analyze the uploaded code file (`{{codeFileName}}`) which contains the complete source of the **{{workspace.Alias}}** workspace ({{workspace.Type}}).

            Generate a recommended **`seamq.config.json`** configuration file based on the workspace structure.

            ## Config Schema

            ```json
            {
              "workspaces": [
                {
                  "path": "./relative/path",
                  "alias": "HumanReadableName",
                  "role": "framework|plugin|library|application"
                }
              ],
              "output": {
                "directory": "./seamq-output",
                "formats": ["md", "html"]
              },
              "analysis": {
                "maxDepth": 10,
                "followNodeModules": false,
                "confidenceThreshold": 0.5
              }
            }
            ```

            ## Instructions

            - Detect all workspaces/projects and assign appropriate roles
            - Choose sensible defaults for output and analysis settings
            - Explain each configuration choice with inline comments (as a separate explanation section)

            # Rules

            - Do NOT invent projects not present in the workspace
            - The output must be valid JSON
            - Use relative paths from the workspace root
            """;
    }

    private static string BuildGenericPrompt(string commandName, Workspace workspace, string codeFileName)
    {
        return $"""
            # Role

            You are an Expert Software Architect.

            # Task

            Analyze the uploaded code file (`{codeFileName}`) which contains the complete source of the **{workspace.Alias}** workspace ({workspace.Type}).

            Perform the analysis that the SeamQ `{commandName}` command would perform and produce equivalent output in Markdown format.

            # Rules

            - Do NOT invent or fabricate any code elements not present in the uploaded file
            - Be thorough and exhaustive in your analysis
            - Use exact names and types from the code
            """;
    }

    /// <summary>
    /// Resolves workspace paths from config, falling back to the registry when config has no workspaces.
    /// </summary>
    public static string[] ResolveWorkspacePaths(
        SeamQ.Core.Configuration.SeamQConfig config,
        SeamQ.Detector.SeamRegistry registry)
    {
        var configPaths = config.Workspaces.Select(w => w.Path).ToArray();
        if (configPaths.Length > 0)
            return configPaths;

        // Fall back to unique workspace paths from the registry
        return registry.GetAll()
            .Select(s => s.Provider.Path)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string SanitizeAlias(string alias)
    {
        var sanitized = alias.Replace(' ', '-').Replace('/', '-').Replace('\\', '-');
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(c, '_');
        }
        return sanitized.ToLowerInvariant();
    }
}
