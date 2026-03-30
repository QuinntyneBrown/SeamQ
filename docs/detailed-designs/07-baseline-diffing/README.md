# Detailed Design: Baseline Diffing Module

**Module:** `SeamQ.Differ`
**Target Framework:** .NET 8
**Status:** Draft
**Last Updated:** 2026-03-30

---

## 1. Overview

The Baseline Diffing module enables developers to detect contract changes between successive scans of an Angular/Nx workspace. It operates by comparing a **current** `SeamRegistry` (produced by the Seam Detection module) against a previously saved **baseline** snapshot, then producing a structured `DiffReport` that describes what changed, where, and how.

The workflow follows three stages:

1. **Save Baseline** -- Serialize the current `SeamRegistry` to a stable JSON file on disk. Key ordering is deterministic so that identical registries always produce identical JSON, enabling reliable textual and structural comparison.
2. **Compare** -- Load both the baseline JSON and the current `SeamRegistry`, then perform a per-seam deep comparison of contract surfaces (inputs, outputs, methods, properties, types, metadata).
3. **Report** -- Classify each difference as ADDED, MODIFIED, or REMOVED, aggregate results into a `DiffReport` with per-seam change lists and summary statistics.

### Pipeline Flow

```
Baseline JSON file (saved earlier)
  |
  v
BaselineSerializer.Deserialize()
  |                               Current SeamRegistry (from SeamDetector)
  |                                       |
  v                                       v
SeamDiffer.DiffAsync(baseline, current)
  |--- per-seam key matching (union of IDs)
  |--- ContractSurfaceComparer.Compare(old, new)
  |--- ChangeClassifier.Classify(differences)
  v
DiffReport
  |--- per-seam SeamChange lists
  |--- summary statistics (added, modified, removed counts)
```

---

## 2. Component Diagrams

### 2.1 Differ Core Class Diagram

![Differ Core Class Diagram](diagrams/class-differ.png)

### 2.2 Baseline Serializer Class Diagram

![Baseline Serializer Class Diagram](diagrams/class-baseline.png)

### 2.3 C4 Component Diagram -- Differ Internals

![C4 Component Diagram - Differ Internals](diagrams/c4-component-differ.png)

---

## 3. Sequence Diagrams

### 3.1 Diff Against Baseline Sequence

![Diff Against Baseline Sequence](diagrams/seq-diff-baseline.png)

### 3.2 Save Baseline Sequence

![Save Baseline Sequence](diagrams/seq-save-baseline.png)

---

## 4. State Diagram

### 4.1 Diff Process States

![Diff Process State Diagram](diagrams/state-diff-process.png)

---

## 5. Component Descriptions

### 5.1 ISeamDiffer / SeamDiffer

**Responsibility:** Top-level orchestrator that compares a current `SeamRegistry` against a baseline `SeamRegistry` and produces a `DiffReport`.

`ISeamDiffer` defines the contract:

```csharp
public interface ISeamDiffer
{
    Task<DiffReport> DiffAsync(
        SeamRegistry baseline,
        SeamRegistry current,
        DiffOptions options,
        CancellationToken ct);

    Task<DiffReport> DiffFromFileAsync(
        string baselinePath,
        SeamRegistry current,
        DiffOptions options,
        CancellationToken ct);
}
```

`SeamDiffer` implements the interface and coordinates the comparison pipeline:

1. Loads the baseline `SeamRegistry` (either from an already-deserialized instance or from a JSON file via `BaselineSerializer`).
2. Computes the **union** of seam IDs from baseline and current registries.
3. For each seam ID, determines whether it exists only in baseline (REMOVED), only in current (ADDED), or in both (potentially MODIFIED).
4. For seams present in both, delegates to `ContractSurfaceComparer` to produce a list of field-level differences.
5. Passes the raw differences to `ChangeClassifier` to assign `ChangeType` values.
6. Aggregates all `SeamChange` instances into a `DiffReport` with summary statistics.

Error handling: if the baseline file is missing or malformed, `SeamDiffer` throws a `BaselineNotFoundException` or `BaselineCorruptException` respectively. The caller can catch these to prompt the user to re-save a baseline.

### 5.2 BaselineSerializer

**Responsibility:** Serializes and deserializes a `SeamRegistry` to and from JSON with stable, deterministic key ordering.

```csharp
public class BaselineSerializer
{
    public Task SaveAsync(SeamRegistry registry, string filePath, CancellationToken ct);
    public Task<SeamRegistry> LoadAsync(string filePath, CancellationToken ct);
    public string Serialize(SeamRegistry registry);
    public SeamRegistry Deserialize(string json);
}
```

Key design decisions:

- **Stable key ordering:** All dictionaries and collections are sorted by key (seam ID, symbol name) before serialization. This guarantees that two identical registries produce byte-for-byte identical JSON, which enables simple file-level diffing (e.g., `git diff`) in addition to structural comparison.
- **Indented JSON:** The output is human-readable (indented with 2 spaces) so that baseline files can be committed to version control and reviewed in pull requests.
- **Schema versioning:** The JSON envelope includes a `"$schemaVersion"` field (currently `1`). If the schema changes in a future release, the serializer can detect the version and apply migration logic or emit a clear error.
- **Serialization engine:** Uses `System.Text.Json` with a custom `JsonSerializerOptions` that includes `WriteIndented = true`, `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`, and custom converters for enum types.

Baseline JSON structure (abbreviated):

```json
{
  "$schemaVersion": 1,
  "$generatedAt": "2026-03-30T14:22:00Z",
  "seams": {
    "shared-auth-token": {
      "id": "shared-auth-token",
      "name": "AuthToken Service",
      "type": "SharedLibrary",
      "providerWorkspace": "libs/shared/auth",
      "consumerWorkspaces": ["apps/portal", "apps/admin"],
      "contractSurface": {
        "inputs": [...],
        "outputs": [...],
        "methods": [...],
        "properties": [...],
        "types": [...]
      },
      "confidence": 0.92,
      "metadata": {}
    }
  }
}
```

### 5.3 ChangeClassifier

**Responsibility:** Classifies raw comparison results into typed `SeamChange` instances with a `ChangeType` value.

```csharp
public class ChangeClassifier
{
    public IReadOnlyList<SeamChange> Classify(
        string seamId,
        IReadOnlyList<FieldDifference> differences);

    public ChangeType DetermineSeamLevelChange(
        bool inBaseline,
        bool inCurrent);
}
```

Classification rules:

| Condition | ChangeType |
|---|---|
| Seam exists only in current registry | `Added` |
| Seam exists only in baseline registry | `Removed` |
| Seam exists in both, but contract surface differs | `Modified` |
| Seam exists in both, contract surface identical | (no change -- omitted from report) |

For `Modified` seams, the classifier inspects each `FieldDifference` returned by the comparer and produces a `SeamChange` per field. A single seam can produce multiple `SeamChange` entries (e.g., one input added, one method signature changed).

The classifier also tags changes with a **severity hint**:

- `Breaking` -- a removal or type-incompatible modification of a public contract element.
- `NonBreaking` -- an addition or backward-compatible modification (e.g., new optional input).
- `Unknown` -- cannot determine compatibility automatically; requires human review.

### 5.4 DiffReport

**Responsibility:** Immutable result object containing the full diff output.

```csharp
public record DiffReport
{
    public DateTimeOffset GeneratedAt { get; init; }
    public string BaselinePath { get; init; }
    public int BaselineSeamCount { get; init; }
    public int CurrentSeamCount { get; init; }
    public IReadOnlyList<SeamDiff> SeamDiffs { get; init; }
    public DiffSummary Summary { get; init; }
}

public record SeamDiff
{
    public string SeamId { get; init; }
    public string SeamName { get; init; }
    public ChangeType OverallChange { get; init; }
    public IReadOnlyList<SeamChange> Changes { get; init; }
}

public record DiffSummary
{
    public int TotalSeamsCompared { get; init; }
    public int SeamsAdded { get; init; }
    public int SeamsModified { get; init; }
    public int SeamsRemoved { get; init; }
    public int SeamsUnchanged { get; init; }
    public int TotalFieldChanges { get; init; }
    public int BreakingChanges { get; init; }
    public int NonBreakingChanges { get; init; }
    public bool HasBreakingChanges => BreakingChanges > 0;
}
```

The report is designed to be consumed by:

- The CLI for console output (human-readable table or JSON).
- CI/CD pipelines for pass/fail gating (check `HasBreakingChanges`).
- The ICD Generator for annotating revision history sections.

### 5.5 SeamChange

**Responsibility:** Describes a single atomic change within a seam's contract surface.

```csharp
public record SeamChange
{
    public string SeamId { get; init; }
    public string Element { get; init; }
    public string ElementPath { get; init; }
    public ChangeType ChangeType { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public ChangeSeverity Severity { get; init; }
    public string Description { get; init; }
}
```

| Field | Description |
|---|---|
| `SeamId` | The unique identifier of the affected seam. |
| `Element` | Human-readable label for the changed element (e.g., `"Input: userId"`, `"Method: getToken"`). |
| `ElementPath` | Dot-separated path within the contract surface (e.g., `"inputs.userId.type"`). |
| `ChangeType` | One of `Added`, `Modified`, `Removed`. |
| `OldValue` | The baseline value (null for additions). Serialized as a string representation. |
| `NewValue` | The current value (null for removals). Serialized as a string representation. |
| `Severity` | `Breaking`, `NonBreaking`, or `Unknown`. |
| `Description` | Auto-generated human-readable sentence describing the change. |

### 5.6 ChangeType Enum

```csharp
public enum ChangeType
{
    Added,
    Modified,
    Removed
}

public enum ChangeSeverity
{
    Breaking,
    NonBreaking,
    Unknown
}
```

- `Added` -- the element exists in the current scan but not in the baseline.
- `Modified` -- the element exists in both but its value, type, or structure has changed.
- `Removed` -- the element exists in the baseline but not in the current scan.

### 5.7 ContractSurfaceComparer

**Responsibility:** Performs deep structural comparison of two `ContractSurface` instances to produce a list of `FieldDifference` entries.

```csharp
public class ContractSurfaceComparer
{
    public IReadOnlyList<FieldDifference> Compare(
        ContractSurface baseline,
        ContractSurface current);
}

public record FieldDifference
{
    public string Path { get; init; }
    public string? BaselineValue { get; init; }
    public string? CurrentValue { get; init; }
    public FieldDifferenceKind Kind { get; init; }
}

public enum FieldDifferenceKind
{
    Added,
    Removed,
    ValueChanged,
    TypeChanged,
    OrderChanged
}
```

The comparer walks the contract surface tree and compares the following elements:

| Contract Element | Comparison Strategy |
|---|---|
| **Inputs** | Match by name. Compare type, required flag, default value, alias. |
| **Outputs** | Match by name. Compare event payload type, alias. |
| **Methods** | Match by name. Compare parameter list (name, type, optionality), return type. |
| **Properties** | Match by name. Compare type, readonly flag, optionality. |
| **Types** (interfaces, enums, type aliases) | Match by name. Recursively compare members. |
| **Metadata** (decorators, annotations) | Match by decorator name. Compare argument values. |
| **Consumer list** | Set comparison of consumer workspace identifiers. |
| **Confidence score** | Numeric comparison with configurable epsilon (default 0.01). |

The comparer uses a **key-based matching** strategy: elements are matched by their name/identifier, not by position. This means reordering elements without changing their names does not produce false-positive changes. Genuine order-sensitive changes (e.g., method parameter order) are detected and flagged as `OrderChanged`.

### 5.8 DiffOptions

Configuration for the diff operation:

```csharp
public class DiffOptions
{
    public double ConfidenceEpsilon { get; set; } = 0.01;
    public bool IgnoreConfidenceChanges { get; set; } = false;
    public bool IgnoreMetadataChanges { get; set; } = false;
    public bool IgnoreConsumerListChanges { get; set; } = false;
    public IReadOnlyList<string>? SeamIdFilter { get; set; }
    public double? MinConfidenceThreshold { get; set; }
}
```

| Setting | Description |
|---|---|
| `ConfidenceEpsilon` | Minimum confidence score delta to consider as a change. |
| `IgnoreConfidenceChanges` | Skip confidence score differences entirely. |
| `IgnoreMetadataChanges` | Skip decorator/metadata differences. |
| `IgnoreConsumerListChanges` | Skip consumer workspace list differences. |
| `SeamIdFilter` | If set, only diff the listed seam IDs. |
| `MinConfidenceThreshold` | If set, exclude seams below this confidence from the report. |

---

## 6. Baseline JSON Schema Summary

The baseline file uses a versioned JSON envelope:

| Field | Type | Description |
|---|---|---|
| `$schemaVersion` | `int` | Schema version for forward compatibility. Current: `1`. |
| `$generatedAt` | `string` (ISO 8601) | Timestamp when the baseline was saved. |
| `seams` | `object` | Map of seam ID to seam snapshot (sorted by ID). |
| `seams[id].id` | `string` | Unique seam identifier. |
| `seams[id].name` | `string` | Human-readable seam name. |
| `seams[id].type` | `string` | Seam type enum value. |
| `seams[id].providerWorkspace` | `string` | Provider workspace path. |
| `seams[id].consumerWorkspaces` | `string[]` | Sorted list of consumer workspace paths. |
| `seams[id].contractSurface` | `object` | Full contract surface snapshot. |
| `seams[id].confidence` | `number` | Final confidence score at baseline time. |
| `seams[id].metadata` | `object` | Additional key-value metadata (sorted by key). |

---

## 7. Error Handling Strategy

The differ uses a **fail-fast** approach for infrastructure errors and a **collect-and-continue** approach for per-seam comparison errors:

- **`BaselineNotFoundException`** -- thrown when the specified baseline file does not exist. The caller should prompt the user to save a baseline first.
- **`BaselineCorruptException`** -- thrown when the baseline JSON cannot be deserialized (malformed JSON, unknown schema version, missing required fields). Includes the inner exception for diagnostics.
- **Per-seam comparison errors** -- if comparing a single seam throws (e.g., unexpected null in the contract surface), the error is recorded as a `SeamDiff` with `OverallChange = Modified` and a diagnostic `SeamChange` entry describing the comparison failure. The diff continues with remaining seams.
- **Schema version mismatch** -- if `$schemaVersion` is higher than the current supported version, `BaselineSerializer` throws `BaselineSchemaVersionException` advising the user to update SeamQ.

---

## 8. Concurrency

- `ContractSurfaceComparer` is stateless and thread-safe. When many seams need comparison, `SeamDiffer` can compare them in parallel using `Parallel.ForEachAsync` with a configurable `MaxDegreeOfParallelism`.
- `BaselineSerializer` file I/O uses async streams to avoid blocking threads.
- `DiffReport` and all model types are immutable records, so they are inherently thread-safe after construction.
- `CancellationToken` is threaded through all async methods to support user cancellation.

---

## 9. Integration Points

| Upstream Module | Data Consumed |
|---|---|
| `SeamQ.Detector` | `SeamRegistry`, `SeamCandidate`, `ContractSymbol` |

| Downstream Consumer | Data Produced |
|---|---|
| CLI (`seamq diff` command) | `DiffReport` (rendered as console table or JSON) |
| CI/CD gating | `DiffSummary.HasBreakingChanges` (exit code 1 if true) |
| `SeamQ.Generator` | `DiffReport` (injected into ICD revision history section) |

---

## 10. Dependencies

| Dependency | Purpose |
|---|---|
| `System.Text.Json` | JSON serialization/deserialization of baseline files. |
| `Microsoft.Extensions.Logging` | Structured logging throughout the diff pipeline. |
| `SeamQ.Detector` | Provides `SeamRegistry`, `SeamCandidate`, and related model types. |
