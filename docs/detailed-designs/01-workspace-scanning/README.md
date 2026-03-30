# Detailed Design: Workspace Scanning Module

**Module:** `SeamQ.Scanner`
**Target Framework:** .NET 8
**Status:** Draft
**Last Updated:** 2026-03-30

---

## 1. Overview

The Workspace Scanning module is the entry point of the SeamQ analysis pipeline. It is responsible for discovering, parsing, and modelling the structure of Angular and Nx workspaces so that downstream modules (seam detection, ICD generation, diagram generation) can operate on a rich, typed AST representation.

The module follows a pipeline architecture:

1. **Detect** the workspace type (Angular CLI or Nx).
2. **Parse** the workspace configuration to discover projects.
3. **Resolve** TypeScript path aliases and project references.
4. **Parse barrel exports** to determine the public API surface of each library.
5. **Parse TypeScript source files** to build per-file AST models.
6. **Build** the aggregate `WorkspaceModel`.

Results are cached at the file level using SHA-256 content hashing so that incremental re-scans are fast.

### Pipeline Flow

```
Workspace Root
  |
  v
WorkspaceScanner (orchestrator)
  |--- detect workspace type
  |--- delegate to AngularWorkspaceParser or NxWorkspaceParser
  |--- TsConfigResolver (path aliases, references)
  |--- BarrelExportParser (public-api.ts / index.ts)
  |--- TypeScriptAstParser (per .ts file)
  |        |--- AngularMetadataExtractor
  |        |--- TsDocParser
  |--- AstCache (SHA-256 invalidation)
  v
WorkspaceModel
```

---

## 2. Component Diagrams

### 2.1 Scanner Class Diagram

![Scanner Class Diagram](diagrams/class-scanner.png)

### 2.2 AST Model Class Diagram

![AST Model Class Diagram](diagrams/class-ast-model.png)

### 2.3 C4 Component Diagram

![C4 Component Diagram - Scanner Internals](diagrams/c4-component-scanner.png)

---

## 3. Sequence Diagrams

### 3.1 Workspace Scan Sequence

![Workspace Scan Sequence](diagrams/seq-scan-workspace.png)

### 3.2 TypeScript File Parse Sequence

![TypeScript File Parse Sequence](diagrams/seq-parse-typescript.png)

---

## 4. State Diagram

### 4.1 Scan Progress States

![Scan Progress State Diagram](diagrams/state-scan-progress.png)

---

## 5. Component Descriptions

### 5.1 IWorkspaceScanner / WorkspaceScanner

**Responsibility:** Top-level orchestrator for scanning a single workspace root.

`IWorkspaceScanner` defines the contract:

```csharp
public interface IWorkspaceScanner
{
    Task<WorkspaceModel> ScanAsync(string workspaceRoot, ScanOptions options, CancellationToken ct);
}
```

`WorkspaceScanner` implements the interface and coordinates the full pipeline:

1. Probes the workspace root for `angular.json` or `nx.json` to select the appropriate `IWorkspaceParser`.
2. Calls the parser to obtain the list of discovered projects with their source roots.
3. Invokes `TsConfigResolver` to build the path-alias map.
4. For each library project, invokes `BarrelExportParser` on `public-api.ts` or `index.ts`.
5. For each discovered `.ts` file, invokes `TypeScriptAstParser` (checking `AstCache` first).
6. Assembles the final `WorkspaceModel`.

Error handling uses a `ScanDiagnostics` collector so partial results are still returned when individual files fail to parse.

### 5.2 IWorkspaceParser

**Responsibility:** Common interface for workspace-type-specific configuration parsers.

```csharp
public interface IWorkspaceParser
{
    bool CanHandle(string workspaceRoot);
    Task<WorkspaceConfig> ParseAsync(string workspaceRoot, CancellationToken ct);
}
```

Each implementation detects its marker file and returns a `WorkspaceConfig` containing the list of `ProjectInfo` entries (name, root, source root, project type, architect targets).

### 5.3 AngularWorkspaceParser

**Responsibility:** Parses `angular.json` (Angular CLI workspace).

- Reads and deserialises `angular.json` using `System.Text.Json`.
- Iterates the `projects` map to build `ProjectInfo` entries.
- Resolves `sourceRoot` relative to the workspace root.
- Identifies project types (`application`, `library`) from the `projectType` field.
- Extracts `architect` / `targets` metadata for downstream use (build configuration, entry points).

### 5.4 NxWorkspaceParser

**Responsibility:** Parses Nx workspaces (`nx.json`, per-project `project.json`).

- Detects Nx by the presence of `nx.json`.
- Reads `nx.json` for default settings and plugin configuration.
- Discovers projects by scanning for `project.json` files under `apps/` and `libs/` (or custom layout directories from `workspaceLayout`).
- Parses each `project.json` to build `ProjectInfo` entries.
- Supports both integrated and package-based Nx workspaces.

### 5.5 TsConfigResolver

**Responsibility:** Resolves TypeScript path aliases and project references.

- Reads `tsconfig.base.json` (or `tsconfig.json` at workspace root).
- Parses `compilerOptions.paths` to build an alias-to-physical-path map.
- Follows `references` entries to discover composite project boundaries.
- Handles `extends` chains by merging path maps from parent configs.
- The resolved map is used by `TypeScriptAstParser` to resolve import specifiers to physical file paths.

```csharp
public class TsConfigResolver
{
    public Task<PathAliasMap> ResolveAsync(string workspaceRoot, CancellationToken ct);
}
```

`PathAliasMap` stores entries like `@mylib/*` -> `libs/mylib/src/*` and provides a `Resolve(string importSpecifier)` method.

### 5.6 BarrelExportParser

**Responsibility:** Parses barrel files (`public-api.ts`, `index.ts`) to determine the exported symbols of a library.

- Locates the barrel file for a given project by checking `public-api.ts` then `index.ts` in the source root.
- Parses `export { Foo, Bar } from './...'` and `export * from './...'` statements.
- Recursively follows re-exports to build the full set of exported symbol names.
- The export list is attached to the `ProjectInfo` so that seam detection knows which symbols are part of the public API.

### 5.7 TypeScriptAstParser

**Responsibility:** Parses a single `.ts` file and returns a `ParsedFile` AST model.

This is the core parsing component. It:

1. Reads the file content and computes its SHA-256 hash (for cache lookup).
2. Tokenises and parses the TypeScript source. Parsing is performed using a lightweight recursive-descent parser targeting the TypeScript constructs SeamQ cares about (interfaces, classes, enums, type aliases, decorators, imports, exports).
3. Delegates to `AngularMetadataExtractor` for any classes with Angular decorators.
4. Delegates to `TsDocParser` to attach documentation from preceding JSDoc/TSDoc comment blocks.
5. Returns a `ParsedFile` containing all discovered symbols.

The parser does not need to handle every TypeScript construct -- it focuses on the declaration-level structures required for seam analysis and ICD generation.

### 5.8 AngularMetadataExtractor

**Responsibility:** Extracts Angular-specific metadata from decorated classes.

Handles the following decorator patterns:

| Decorator | Extracted Metadata |
|---|---|
| `@Component({...})` | selector, templateUrl, styleUrls, standalone flag, imports, changeDetection |
| `@Injectable({...})` | providedIn scope |
| `@Directive({...})` | selector, standalone flag |
| `@Pipe({...})` | name, standalone flag |
| `@NgModule({...})` | declarations, imports, exports, providers |
| `@Input()` | property name, alias, required flag |
| `@Output()` | property name, alias |

Also detects modern Angular patterns:

- `inject(ServiceName)` calls for constructor-less DI.
- `input()` / `input.required()` signal inputs.
- `output()` signal outputs.
- `model()` two-way binding signals.
- `viewChild()` / `viewChildren()` / `contentChild()` / `contentChildren()` signal queries.

### 5.9 TsDocParser

**Responsibility:** Extracts TSDoc and JSDoc comments and attaches them to parsed symbols.

- Scans for `/** ... */` comment blocks immediately preceding a declaration.
- Parses standard tags: `@param`, `@returns`, `@deprecated`, `@example`, `@see`.
- Preserves the summary (first paragraph) as a plain-text description.
- Attaches the parsed `TsDocComment` to the corresponding `ParsedSymbol`.

### 5.10 AstCache

**Responsibility:** Caches parsed AST results to avoid redundant work on incremental re-scans.

- Keyed by absolute file path.
- Each entry stores the `ParsedFile` result and the SHA-256 hash of the file content at parse time.
- On cache lookup, the current file hash is compared to the stored hash. On mismatch, the entry is evicted.
- Backed by an in-memory `ConcurrentDictionary`. An optional on-disk serialisation layer (JSON) can persist the cache between CLI invocations using a `.seamq-cache` file at the workspace root.
- Cache is workspace-scoped; each `WorkspaceScanner` instance gets its own `AstCache`.

```csharp
public class AstCache
{
    public bool TryGet(string filePath, string currentHash, out ParsedFile result);
    public void Set(string filePath, string hash, ParsedFile result);
    public void Invalidate(string filePath);
    public Task SaveToDiskAsync(string cacheFilePath, CancellationToken ct);
    public Task LoadFromDiskAsync(string cacheFilePath, CancellationToken ct);
}
```

---

## 6. AST Model Summary

The parsed AST model is a set of immutable record types:

| Type | Description |
|---|---|
| `WorkspaceModel` | Root object. Contains workspace type, root path, list of `ParsedProject`. |
| `ParsedProject` | A single Angular/Nx project. Contains name, root, type, list of `ParsedFile`, barrel exports. |
| `ParsedFile` | One `.ts` source file. Contains path, imports, exports, list of `ParsedSymbol`. |
| `ParsedSymbol` | Abstract base for any top-level declaration. |
| `ParsedInterface` | Interface declaration with properties and methods. |
| `ParsedClass` | Class declaration with properties, methods, decorators. |
| `ParsedService` | Specialisation of `ParsedClass` for `@Injectable` classes. Includes `providedIn`, injected dependencies. |
| `ParsedComponent` | Specialisation of `ParsedClass` for `@Component` classes. Includes selector, inputs, outputs, template info. |
| `ParsedDirective` | Specialisation of `ParsedClass` for `@Directive` classes. |
| `ParsedPipe` | Specialisation of `ParsedClass` for `@Pipe` classes. |
| `ParsedEnum` | Enum declaration with members. |
| `ParsedTypeAlias` | Type alias (`type Foo = ...`). |
| `ParsedImport` | An import statement with source module and imported names. |
| `ParsedExport` | An export statement. |
| `ParsedProperty` | A property on an interface or class. Includes name, type, decorators, optionality. |
| `ParsedMethod` | A method signature. Includes name, parameters, return type, decorators. |
| `ParsedDecorator` | A decorator invocation with name and argument map. |
| `TsDocComment` | Parsed documentation comment with summary, tags, params. |

---

## 7. Error Handling Strategy

The scanner uses a **fail-soft** approach:

- Individual file parse failures are recorded as `ScanDiagnostic` entries (severity, file path, message, exception) but do not abort the scan.
- Workspace configuration parse failures (e.g., malformed `angular.json`) are fatal and throw `WorkspaceScanException`.
- `TsConfigResolver` failures degrade gracefully: path aliases that cannot be resolved are logged as warnings, and imports remain unresolved in the model.
- `AstCache` corruption is handled by discarding the cache and re-parsing.

---

## 8. Concurrency

- `TypeScriptAstParser` is stateless and thread-safe. Files are parsed in parallel using `Parallel.ForEachAsync` with a configurable `MaxDegreeOfParallelism` (default: `Environment.ProcessorCount`).
- `AstCache` uses `ConcurrentDictionary` for thread-safe access.
- `ScanDiagnostics` uses a `ConcurrentBag<ScanDiagnostic>` for lock-free diagnostic collection.
- `CancellationToken` is threaded through all async methods to support user cancellation.

---

## 9. Configuration

Relevant settings in `ScanOptions`:

| Setting | Type | Default | Description |
|---|---|---|---|
| `MaxDegreeOfParallelism` | `int` | CPU count | Max concurrent file parses. |
| `EnableCache` | `bool` | `true` | Whether to use `AstCache`. |
| `CacheFilePath` | `string?` | `.seamq-cache` | Path for on-disk cache persistence. |
| `ExcludePatterns` | `string[]` | `["**/*.spec.ts", "**/*.stories.ts"]` | Glob patterns for files to skip. |
| `IncludePatterns` | `string[]` | `["**/*.ts"]` | Glob patterns for files to include. |
| `ParseTsDocs` | `bool` | `true` | Whether to extract TSDoc comments. |

---

## 10. Dependencies

| Dependency | Purpose |
|---|---|
| `System.Text.Json` | JSON deserialisation for workspace configs, tsconfig. |
| `Microsoft.Extensions.Logging` | Structured logging throughout the pipeline. |
| `Microsoft.Extensions.FileSystemGlobbing` | Glob pattern matching for include/exclude filters. |
| `System.IO.Hashing` | SHA-256 computation for cache invalidation. |
| `System.Threading.Tasks.Dataflow` | Optional: bounded parallelism via `ActionBlock`. |
