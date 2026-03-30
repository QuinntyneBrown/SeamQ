# Detailed Design: Data Export Module

**Module:** `SeamQ.Export`
**Target Framework:** .NET 8
**Status:** Draft
**Last Updated:** 2026-03-30

---

## 1. Overview

The Data Export module provides the `seamq export` command, enabling users to export raw seam analysis data as structured JSON for consumption by external toolchains -- CI/CD pipelines, documentation generators, custom dashboards, and contract-testing frameworks.

The module operates on the `SeamRegistry` produced by the detection pipeline and serialises three complementary views of the data:

1. **Contract Surface** -- the complete contract surface for each seam, including interfaces, tokens, bindings, services, and Angular-specific metadata.
2. **Data Dictionary** -- every type that crosses a seam boundary, with field-level detail (name, type, optionality, TSDoc description).
3. **Traceability Matrix** -- source file paths, line numbers, and workspace membership for every symbol in every seam.

Each view is written to a dedicated JSON file with a documented schema. Output is deterministic: identical inputs produce byte-identical JSON (sorted keys, stable ordering).

### Export Pipeline

```
CLI ExportCommand
  |
  v
JsonDataExporter (orchestrator)
  |--- ContractSurfaceExporter  ->  contract-surface.json
  |--- DataDictionaryExporter   ->  data-dictionary.json
  |--- TraceabilityMatrixExporter -> traceability-matrix.json
  v
Output directory (--output-dir or cwd)
```

---

## 2. Component Diagrams

### 2.1 Exporter Class Diagram

![Exporter Class Diagram](diagrams/class-exporter.png)

### 2.2 Export Schema Class Diagram

![Export Schema Class Diagram](diagrams/class-export-schema.png)

### 2.3 C4 Component Diagram

![C4 Component Diagram - Export Internals](diagrams/c4-component-export.png)

---

## 3. Sequence Diagrams

### 3.1 Export Data Sequence

![Export Data Sequence](diagrams/seq-export-data.png)

---

## 4. State Diagrams

### 4.1 Export Pipeline States

![Export Pipeline State Diagram](diagrams/state-export.png)

---

## 5. Component Descriptions

### 5.1 IDataExporter / JsonDataExporter

**Responsibility:** Top-level orchestrator for the export pipeline.

`IDataExporter` defines the contract:

```csharp
public interface IDataExporter
{
    Task ExportAsync(ExportOptions options, SeamRegistry registry, CancellationToken ct);
}
```

`JsonDataExporter` implements the interface and coordinates the three sub-exporters:

1. Receives the `SeamRegistry` and `ExportOptions` from the CLI command.
2. Resolves and validates the output directory (creates it if missing).
3. Filters seams based on `ExportOptions` (single seam by ID, all seams, or confidence threshold).
4. Delegates to each sub-exporter to produce its in-memory schema model.
5. Serialises each model to JSON using `System.Text.Json` with deterministic settings (sorted properties, indented, no trailing commas).
6. Writes the three JSON files to the output directory atomically (write to `.tmp`, then rename).

If `--output stdout` is specified, the exporter writes a single combined JSON document to stdout instead of separate files.

### 5.2 ContractSurfaceExporter

**Responsibility:** Produces the `contract-surface.json` output containing the full contract surface for each detected seam.

For each seam in the registry, the exporter extracts:

| Data | Source |
|---|---|
| Seam ID, type, confidence | `SeamCandidate` |
| Provider workspace and project | `SeamCandidate.Provider` |
| Consumer workspaces and projects | `SeamCandidate.Consumers` |
| Exported interfaces with full property and method signatures | `ParsedInterface` |
| Injection tokens with type and providedIn scope | `ParsedService` |
| Component `@Input()` / `@Output()` bindings with types and defaults | `ParsedComponent` |
| Signal inputs (`input()`, `input.required()`) and model signals | `ParsedComponent` |
| NgRx actions, selectors, and feature state interfaces | Strategy-specific metadata |
| Route contracts (guards, resolvers, data interfaces) | Strategy-specific metadata |

The output is grouped by seam, with each seam containing a `contracts` array of typed contract entries.

### 5.3 DataDictionaryExporter

**Responsibility:** Produces the `data-dictionary.json` output containing every type that crosses a seam boundary.

This exporter walks all seams and collects the union of types referenced in contract surfaces:

- **Interfaces** -- every property with name, TypeScript type string, optionality, and TSDoc description.
- **Type aliases** -- the alias name and the resolved type expression.
- **Enums** -- every member with name and value (string or numeric).
- **Classes used as DTOs** -- properties only (methods excluded unless they are factory methods annotated with `@static`).

Each entry includes:

| Field | Description |
|---|---|
| `qualifiedName` | Fully qualified name including project path (e.g. `@mylib/models.UserDto`). |
| `kind` | `interface`, `typeAlias`, `enum`, or `class`. |
| `fields` | Array of field entries (name, type, optional, description). |
| `referencedBy` | Array of seam IDs that reference this type. |
| `sourceFile` | Relative path to the declaring source file. |
| `sourceLine` | Line number of the declaration. |

Types are deduplicated by qualified name. If the same type appears in multiple seams, the `referencedBy` array contains all seam IDs.

### 5.4 TraceabilityMatrixExporter

**Responsibility:** Produces the `traceability-matrix.json` output mapping every symbol in every seam back to its source location and workspace membership.

For each seam, the exporter emits a list of symbol entries:

| Field | Description |
|---|---|
| `seamId` | The parent seam identifier. |
| `symbolName` | The symbol's short name (e.g. `UserService`). |
| `qualifiedName` | Fully qualified name including project. |
| `kind` | Symbol kind (`interface`, `class`, `service`, `component`, `enum`, `typeAlias`, `token`, `action`, `selector`). |
| `role` | `provider` or `consumer`. |
| `workspace` | Workspace name. |
| `project` | Project name within the workspace. |
| `sourceFile` | Relative file path from the workspace root. |
| `sourceLine` | Line number of the declaration. |
| `barrelExportFile` | Path to the barrel file that re-exports this symbol (if applicable). |

The matrix enables downstream tools to navigate from any seam concept directly to the source code that declares or consumes it.

### 5.5 ExportSchema

**Responsibility:** Documents and enforces the JSON schema for each export format.

`ExportSchema` is a static utility class that provides:

```csharp
public static class ExportSchema
{
    public static JsonElement GetContractSurfaceSchema();
    public static JsonElement GetDataDictionarySchema();
    public static JsonElement GetTraceabilityMatrixSchema();
    public static bool Validate(JsonDocument doc, ExportFormat format, out List<string> errors);
}
```

- Schemas are defined as embedded JSON Schema (draft 2020-12) resources in the assembly.
- `Validate` is used in tests and optionally at runtime (with `--validate-output` flag) to assert that generated JSON conforms to the declared schema.
- Schemas are versioned. The `$schema` property in each output file references the schema version (e.g. `"$schema": "seamq://export/contract-surface/v1"`).

### 5.6 ExportOptions

Configuration record supplied by the CLI `ExportCommand`:

| Field | Type | Default | Description |
|---|---|---|---|
| `OutputDirectory` | `string?` | Current directory | Target directory for JSON files. |
| `SeamId` | `string?` | `null` | Export a single seam by ID. `null` = all seams. |
| `MinConfidence` | `double` | `0.0` | Exclude seams below this confidence threshold. |
| `Format` | `ExportFormat` | `Json` | Output format (currently only `Json`). |
| `WriteToStdout` | `bool` | `false` | Write combined JSON to stdout instead of files. |
| `ValidateOutput` | `bool` | `false` | Validate output against embedded JSON schema. |
| `PrettyPrint` | `bool` | `true` | Indent JSON for readability. |

---

## 6. Output Files

### 6.1 contract-surface.json

Top-level structure:

```json
{
  "$schema": "seamq://export/contract-surface/v1",
  "generatedAt": "2026-03-30T12:00:00Z",
  "seamqVersion": "1.0.0",
  "seams": [
    {
      "id": "seam-abc-123",
      "type": "SharedLibrary",
      "confidence": 0.92,
      "provider": {
        "workspace": "platform",
        "project": "shared-models"
      },
      "consumers": [
        { "workspace": "checkout", "project": "checkout-app" }
      ],
      "contracts": [
        {
          "kind": "interface",
          "name": "OrderDto",
          "properties": [
            { "name": "id", "type": "string", "optional": false, "description": "Unique order identifier." },
            { "name": "total", "type": "number", "optional": false, "description": "Order total in cents." }
          ]
        }
      ]
    }
  ]
}
```

### 6.2 data-dictionary.json

Top-level structure:

```json
{
  "$schema": "seamq://export/data-dictionary/v1",
  "generatedAt": "2026-03-30T12:00:00Z",
  "seamqVersion": "1.0.0",
  "types": [
    {
      "qualifiedName": "@platform/shared-models.OrderDto",
      "kind": "interface",
      "fields": [
        { "name": "id", "type": "string", "optional": false, "description": "Unique order identifier." },
        { "name": "total", "type": "number", "optional": false, "description": "Order total in cents." }
      ],
      "referencedBy": ["seam-abc-123", "seam-def-456"],
      "sourceFile": "libs/shared-models/src/lib/order.dto.ts",
      "sourceLine": 5
    }
  ]
}
```

### 6.3 traceability-matrix.json

Top-level structure:

```json
{
  "$schema": "seamq://export/traceability-matrix/v1",
  "generatedAt": "2026-03-30T12:00:00Z",
  "seamqVersion": "1.0.0",
  "entries": [
    {
      "seamId": "seam-abc-123",
      "symbolName": "OrderDto",
      "qualifiedName": "@platform/shared-models.OrderDto",
      "kind": "interface",
      "role": "provider",
      "workspace": "platform",
      "project": "shared-models",
      "sourceFile": "libs/shared-models/src/lib/order.dto.ts",
      "sourceLine": 5,
      "barrelExportFile": "libs/shared-models/src/public-api.ts"
    }
  ]
}
```

---

## 7. Error Handling Strategy

The export module uses a **fail-soft** approach consistent with the rest of SeamQ:

- If a single seam fails to serialise (e.g., a symbol reference cannot be resolved), the error is recorded in `ExportDiagnostics` and that seam is skipped. Remaining seams are still exported.
- If the output directory cannot be created or is not writable, the exporter throws `ExportException` (fatal).
- If `--validate-output` is enabled and validation fails, the exporter writes the file but returns exit code 1 with diagnostic messages listing the schema violations.
- Atomic file writes (write to `.tmp` then rename) prevent partial output on crash or cancellation.

---

## 8. Determinism

All output is deterministic to satisfy L2-10.4:

- JSON object keys are sorted alphabetically.
- Arrays of seams are sorted by seam ID.
- Arrays of types are sorted by qualified name.
- Arrays of traceability entries are sorted by `(seamId, qualifiedName, role)`.
- The `generatedAt` timestamp is excluded from determinism checks and can be suppressed with `--no-timestamp`.

---

## 9. Dependencies

| Dependency | Purpose |
|---|---|
| `System.Text.Json` | JSON serialisation with deterministic settings. |
| `Microsoft.Extensions.Logging` | Structured logging throughout the export pipeline. |
| `Json.Schema.Net` | JSON Schema validation (optional, for `--validate-output`). |
| `SeamQ.Detector` | Provides `SeamRegistry` as input. |
| `SeamQ.Scanner` | Provides `ParsedFile`, `ParsedSymbol`, and workspace models. |
