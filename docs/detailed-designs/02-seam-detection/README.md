# Detailed Design -- 02 Seam Detection

**Module:** `SeamQ.Detector`
**Traces to:** L1-2 (Interface Seam Detection), L2-2.1 through L2-2.16
**Target Framework:** .NET 8

---

## Table of Contents

1. [Overview](#1-overview)
2. [Architecture](#2-architecture)
3. [Components](#3-components)
   - 3.1 [ISeamDetectionStrategy](#31-iseamdetectionstrategy)
   - 3.2 [SeamDetector](#32-seamdetector)
   - 3.3 [PluginContractStrategy](#33-plugincontractstrategy)
   - 3.4 [SharedLibraryStrategy](#34-sharedlibrarystrategy)
   - 3.5 [MessageBusStrategy](#35-messagebusstrategy)
   - 3.6 [RouteContractStrategy](#36-routecontractstrategy)
   - 3.7 [StateContractStrategy](#37-statecontractstrategy)
   - 3.8 [HttpApiContractStrategy](#38-httpapicontractstrategy)
   - 3.9 [CustomDecoratorStrategy](#39-customdecoratorstrategy)
   - 3.10 [ConfidenceScorer](#310-confidencescorer)
   - 3.11 [SeamRegistry](#311-seamregistry)
4. [Data Model](#4-data-model)
5. [Detection Pipeline](#5-detection-pipeline)
6. [Confidence Scoring Algorithm](#6-confidence-scoring-algorithm)
7. [Deduplication Logic](#7-deduplication-logic)
8. [Error Handling](#8-error-handling)
9. [Configuration](#9-configuration)
10. [Diagrams](#10-diagrams)
11. [Traceability](#11-traceability)

---

## 1. Overview

The Seam Detection module (`SeamQ.Detector`) is responsible for analyzing parsed Angular workspaces to discover interface boundaries -- the "seams" -- where types, services, tokens, bindings, events, routes, state, or messages cross workspace boundaries. Each detected seam is classified by type, assigned a confidence score, and registered in a queryable registry.

The module uses a **strategy pattern**: a central orchestrator (`SeamDetector`) delegates detection to a set of pluggable `ISeamDetectionStrategy` implementations, each responsible for one category of seam. After all strategies run, the orchestrator deduplicates overlapping results, scores each candidate via a `ConfidenceScorer`, and populates a `SeamRegistry` that downstream modules (ICD generation, diagram generation, CLI commands) consume.

### Design Goals

- **Extensibility** -- New seam categories can be added by implementing `ISeamDetectionStrategy` and registering it in DI.
- **Independence** -- Each strategy operates independently; a failure in one does not block others.
- **Determinism** -- Given identical parsed workspace inputs, the detection output is identical.
- **Configurability** -- Confidence thresholds, included/excluded seam types, and custom decorator patterns are all configurable.

---

## 2. Architecture

![C4 Component Diagram -- Detector Internals](diagrams/c4-component-detector.png)

The detector sits between the parser (which provides `ParsedWorkspace` models) and the downstream consumers (ICD generator, diagram generator, CLI commands). Its internal architecture is:

```
CLI / ScanCommand
       |
       v
  SeamDetector  ──>  ISeamDetectionStrategy[]
       |                   |
       |                   ├── PluginContractStrategy
       |                   ├── SharedLibraryStrategy
       |                   ├── MessageBusStrategy
       |                   ├── RouteContractStrategy
       |                   ├── StateContractStrategy
       |                   ├── HttpApiContractStrategy
       |                   └── CustomDecoratorStrategy
       |
       ├──>  ConfidenceScorer
       |
       v
  SeamRegistry
```

All components are registered in the Microsoft.Extensions.DependencyInjection container. Strategies are registered as `IEnumerable<ISeamDetectionStrategy>` so the orchestrator receives all of them automatically.

---

## 3. Components

### 3.1 ISeamDetectionStrategy

![Class Diagram -- SeamDetector and Strategies](diagrams/class-detector.png)

**Namespace:** `SeamQ.Detector.Strategies`

The strategy interface defines the contract for all seam detection algorithms.

```csharp
public interface ISeamDetectionStrategy
{
    /// <summary>
    /// Human-readable name for this strategy (e.g., "PluginContract").
    /// Used in logging and in SeamCandidate.DetectedBy.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Analyze the provided workspaces and return zero or more seam candidates.
    /// </summary>
    IReadOnlyList<SeamCandidate> Detect(IReadOnlyList<ParsedWorkspace> workspaces);
}
```

**Design decisions:**

- The method accepts the full list of workspaces because cross-workspace analysis requires visibility into both provider and consumer sides.
- Returns `IReadOnlyList<SeamCandidate>` rather than `IEnumerable` to ensure all detection work completes before the orchestrator proceeds to deduplication.
- Each strategy sets `SeamCandidate.RawConfidence` based on its own heuristics. The `ConfidenceScorer` later refines this into `FinalConfidence`.

---

### 3.2 SeamDetector

**Namespace:** `SeamQ.Detector`

The central orchestrator. It receives all registered strategies via constructor injection, iterates them, collects candidates, deduplicates, scores, and populates the registry.

```csharp
public class SeamDetector
{
    public SeamDetector(
        IEnumerable<ISeamDetectionStrategy> strategies,
        ConfidenceScorer scorer,
        SeamRegistry registry,
        ILogger<SeamDetector> logger);

    /// <summary>
    /// Run all strategies against the workspaces, deduplicate, score, and
    /// return the populated SeamRegistry.
    /// </summary>
    public SeamRegistry DetectAll(IReadOnlyList<ParsedWorkspace> workspaces);
}
```

**Orchestration steps:**

1. Validate the workspace list (at least one workspace required).
2. Iterate each `ISeamDetectionStrategy` and call `Detect(workspaces)`.
3. Accumulate all returned `SeamCandidate` objects into a combined list.
4. Deduplicate candidates that refer to the same provider symbol and consumer set (see Section 7).
5. Score each surviving candidate via `ConfidenceScorer.Score()`.
6. Register each scored candidate in `SeamRegistry`.
7. Call `SeamRegistry.Build()` to finalize lookup indexes.
8. Return the registry.

If a strategy throws, the orchestrator catches the exception, logs it, and continues with remaining strategies. This satisfies the graceful degradation requirement (L2-10.3).

---

### 3.3 PluginContractStrategy

**Namespace:** `SeamQ.Detector.Strategies`
**Traces to:** L2-2.1, L2-2.2, L2-2.9, L2-2.11

![Sequence -- Plugin Contract Detection](diagrams/seq-plugin-detection.png)

Detects plugin contract seams by finding abstract classes, interfaces consumed via `@Injectable`, `InjectionToken` declarations, and `forRoot()`/`forChild()` module configuration patterns that cross workspace boundaries.

**Detection phases:**

1. **Symbol discovery** -- For each workspace, scan the public API surface for:
   - Abstract classes exported from barrel files.
   - Interfaces that have at least one `@Injectable()` class implementing them in a different workspace.
   - `new InjectionToken<T>()` declarations.
   - Static `forRoot(config)` and `forChild(config)` methods on modules or standalone provider functions.

2. **Cross-workspace matching** -- For each discovered symbol, search all other workspaces for:
   - Classes extending the abstract class.
   - Classes implementing the interface.
   - `@Inject(token)` or `inject(token)` references to the injection token.
   - `Module.forRoot(...)` or `Module.forChild(...)` call sites.

3. **Candidate construction** -- For each matched pair, create a `SeamCandidate` with:
   - `Type = SeamType.PluginContract`
   - `ProviderWorkspace` = workspace exporting the symbol.
   - `ConsumerWorkspaces` = workspaces using the symbol.
   - `RawConfidence` = 0.9 for explicit interface implementation, 0.85 for injection tokens, 0.8 for `forRoot`/`forChild`.

**Raw confidence heuristics:**

| Evidence | RawConfidence |
|---|---|
| Class implements cross-workspace interface | 0.90 |
| Class extends cross-workspace abstract class | 0.90 |
| InjectionToken provided/injected across boundary | 0.85 |
| forRoot()/forChild() with configuration interface | 0.80 |

---

### 3.4 SharedLibraryStrategy

**Namespace:** `SeamQ.Detector.Strategies`
**Traces to:** L2-2.1, L2-2.10, L2-2.12

Detects shared library seams by identifying public API barrel exports (`public-api.ts`, `index.ts`) consumed by two or more other workspaces.

**Detection logic:**

1. For each workspace marked as `role: library` (or any workspace with barrel exports), enumerate all publicly exported symbols.
2. For each exported symbol, scan other workspaces for import statements referencing the library's TypeScript path alias.
3. If a symbol is imported by at least one other workspace, create a `SeamCandidate`.

**Raw confidence heuristics:**

| Evidence | RawConfidence |
|---|---|
| Symbol imported by 3+ workspaces | 0.85 |
| Symbol imported by 2 workspaces | 0.75 |
| Symbol imported by 1 workspace | 0.60 |
| Barrel re-export only (no direct usage found) | 0.40 |

---

### 3.5 MessageBusStrategy

**Namespace:** `SeamQ.Detector.Strategies`
**Traces to:** L2-2.5, L2-2.8

Detects message bus seams where event-driven or reactive communication crosses workspace boundaries.

**What it detects:**

- **RxJS Subject/BehaviorSubject exports** -- Public properties typed as `Subject<T>`, `BehaviorSubject<T>`, or `ReplaySubject<T>` in services exported from one workspace and subscribed to in another.
- **Event bus services** -- Services following the event bus pattern (methods named `emit`, `on`, `publish`, `subscribe` with generic event type parameters) that are exported and consumed cross-workspace.
- **SignalR hub methods** -- Services wrapping SignalR `HubConnection` with `.invoke()` and `.on()` calls, where the hub service is exported and consumed across workspaces. Extracts method names and payload types.

**Raw confidence heuristics:**

| Evidence | RawConfidence |
|---|---|
| BehaviorSubject/ReplaySubject exported and subscribed cross-workspace | 0.90 |
| Subject exported and subscribed cross-workspace | 0.85 |
| Event bus service pattern cross-workspace | 0.75 |
| SignalR hub wrapper cross-workspace | 0.80 |

---

### 3.6 RouteContractStrategy

**Namespace:** `SeamQ.Detector.Strategies`
**Traces to:** L2-2.6

Detects route contract seams where lazy-loaded routes, route data interfaces, or route guards create contracts across workspace boundaries.

**What it detects:**

- **loadChildren / loadComponent** -- Route configurations that lazy-load a module or component from another workspace via dynamic import path.
- **Route data interfaces** -- Interfaces used as the type for `route.data` or `ActivatedRoute.data` that are defined in one workspace and consumed in another.
- **Guard contracts** -- `CanActivate`, `CanDeactivate`, `CanMatch`, and `Resolve` guards defined in a shared workspace and referenced in route configurations of other workspaces.

**Detection approach:**

1. Parse route configuration arrays (`Routes`, `Route[]`) in each workspace.
2. Resolve `loadChildren` and `loadComponent` dynamic import paths to target workspaces.
3. For each route referencing a cross-workspace target, create a `SeamCandidate`.

**Raw confidence heuristics:**

| Evidence | RawConfidence |
|---|---|
| loadChildren pointing to another workspace | 0.90 |
| loadComponent pointing to another workspace | 0.90 |
| Shared route guard consumed cross-workspace | 0.80 |
| Shared route data interface consumed cross-workspace | 0.70 |

---

### 3.7 StateContractStrategy

**Namespace:** `SeamQ.Detector.Strategies`
**Traces to:** L2-2.7, L2-2.13

Detects state contract seams where state management artifacts cross workspace boundaries.

**What it detects:**

- **NgRx actions** -- `createAction()` calls exported from one workspace and dispatched or listened to (via `ofType()`) in another workspace.
- **NgRx selectors** -- `createSelector()` or `createFeatureSelector()` calls exported from one workspace and used via `store.select()` in another.
- **Shared service state** -- `@Injectable()` services with public state properties (signals, BehaviorSubjects, plain properties) exported and read/written from another workspace.
- **Signal-based state** -- `signal()`, `computed()`, and `effect()` patterns in services exported across boundaries.

**Raw confidence heuristics:**

| Evidence | RawConfidence |
|---|---|
| NgRx action dispatched cross-workspace | 0.90 |
| NgRx selector used cross-workspace | 0.85 |
| Signal/computed state exported and read cross-workspace | 0.80 |
| Service property state read cross-workspace | 0.70 |

---

### 3.8 HttpApiContractStrategy

**Namespace:** `SeamQ.Detector.Strategies`
**Traces to:** L2-2.10, L2-2.12

Detects HTTP API contract seams where backend communication interfaces are shared across workspaces.

**What it detects:**

- **HttpClient wrapper services** -- Services that inject `HttpClient` and expose typed methods (`get<T>()`, `post<T>()`, etc.) that are exported and consumed cross-workspace.
- **DTO exports** -- Interfaces and classes used as request/response body types in HTTP calls that are defined in one workspace and used in another.
- **Interceptors** -- `HttpInterceptor` implementations or `HttpInterceptorFn` functions exported from a shared workspace and registered in other workspaces.

**Detection approach:**

1. Identify services that inject `HttpClient` in their constructor or via `inject(HttpClient)`.
2. Extract the generic type parameters from HTTP method calls to discover DTO types.
3. Check if those services or their DTO types are exported and consumed cross-workspace.

**Raw confidence heuristics:**

| Evidence | RawConfidence |
|---|---|
| HttpClient wrapper service consumed cross-workspace | 0.85 |
| DTO interface used in HTTP calls across workspaces | 0.80 |
| HttpInterceptor exported and registered cross-workspace | 0.75 |

---

### 3.9 CustomDecoratorStrategy

**Namespace:** `SeamQ.Detector.Strategies`
**Traces to:** L2-2.1 (extensibility)

Detects seam candidates based on user-configured custom decorators. This enables teams with project-specific conventions to teach SeamQ about their own contract markers.

**Configuration:**

```json
{
  "analysis": {
    "customDecorators": [
      { "name": "@PluginExport", "seamType": "PluginContract" },
      { "name": "@SharedApi", "seamType": "SharedLibrary" }
    ]
  }
}
```

**Detection logic:**

1. Read the `customDecorators` configuration array.
2. For each configured decorator, scan all workspaces for classes/functions annotated with that decorator.
3. Check if the decorated symbol is exported in one workspace and imported in another.
4. Create a `SeamCandidate` with the configured `seamType`.

**Raw confidence:** 0.70 for all custom decorator matches (lower than built-in strategies because the match is purely naming-convention based).

---

### 3.10 ConfidenceScorer

![Class Diagram -- SeamRegistry and Scoring](diagrams/class-seam-registry.png)

**Namespace:** `SeamQ.Detector.Scoring`
**Traces to:** L2-2.16

The `ConfidenceScorer` computes a final confidence value between 0.0 and 1.0 for each `SeamCandidate`. The score determines whether a seam is reported, how prominently it appears in output, and whether it triggers a low-confidence warning.

```csharp
public class ConfidenceScorer
{
    public ConfidenceScorer(ConfidenceWeights weights);

    /// <summary>
    /// Calculate the final confidence score for a candidate.
    /// </summary>
    public double Score(SeamCandidate candidate);
}
```

The scoring algorithm is described in detail in Section 6.

---

### 3.11 SeamRegistry

**Namespace:** `SeamQ.Detector.Registry`

The `SeamRegistry` is the queryable store of all detected seams. After `SeamDetector` populates it, downstream modules use it to retrieve seams for ICD generation, diagram generation, and CLI display.

```csharp
public class SeamRegistry
{
    public int Count { get; }

    public void Register(SeamCandidate candidate);
    public void Build();  // finalize indexes

    public SeamCandidate? GetById(string id);
    public IReadOnlyList<SeamCandidate> GetByType(SeamType type);
    public IReadOnlyList<SeamCandidate> GetByProvider(string workspace);
    public IReadOnlyList<SeamCandidate> GetByConsumer(string workspace);
    public IReadOnlyList<SeamCandidate> GetByMinConfidence(double threshold);
    public IReadOnlyList<SeamCandidate> GetAll();
    public void Clear();
}
```

**Internal indexing:**

- Primary store: `Dictionary<string, SeamCandidate>` keyed by `SeamCandidate.Id`.
- Secondary indexes built on `Build()`: `ILookup<SeamType, SeamCandidate>` by type, `ILookup<string, SeamCandidate>` by provider workspace.
- Consumer queries iterate the primary store (consumer lists are small enough that a linear scan is acceptable for the expected dataset sizes).

**Thread safety:** The registry is populated single-threaded during the detection pipeline. After `Build()` is called, the registry is effectively immutable and safe for concurrent reads.

---

## 4. Data Model

The core data types used throughout the detection module:

```csharp
public enum SeamType
{
    PluginContract,
    SharedLibrary,
    MessageBus,
    RouteContract,
    StateContract,
    HttpApiContract,
    CustomDecorator
}

public enum SymbolKind
{
    Interface, AbstractClass, InjectionToken, Service, Component,
    Directive, Observable, Signal, Action, Selector, RouteGuard,
    Dto, Interceptor, Decorator
}

public class SeamCandidate
{
    public string Id { get; set; }                    // deterministic hash
    public string Name { get; set; }                  // human-readable name
    public SeamType Type { get; set; }
    public string ProviderWorkspace { get; set; }
    public IReadOnlyList<string> ConsumerWorkspaces { get; set; }
    public IReadOnlyList<ContractSymbol> ContractSymbols { get; set; }
    public string SourceFile { get; set; }
    public int SourceLine { get; set; }
    public double RawConfidence { get; set; }         // set by strategy
    public double FinalConfidence { get; set; }       // set by scorer
    public string DetectedBy { get; set; }            // strategy name
    public Dictionary<string, object> Metadata { get; set; }
}

public class ContractSymbol
{
    public string Name { get; set; }
    public SymbolKind Kind { get; set; }
    public string TypeSignature { get; set; }
    public string FilePath { get; set; }
    public int LineNumber { get; set; }
    public string? Documentation { get; set; }
}
```

**ID generation:** `SeamCandidate.Id` is a deterministic hash of `(Type, ProviderWorkspace, Name, sorted ConsumerWorkspaces)`. This ensures the same seam detected on repeated runs always gets the same ID, supporting baseline diffing (L1-7).

---

## 5. Detection Pipeline

![Sequence -- Seam Detection Pipeline](diagrams/seq-detect-seams.png)

![State -- Detection Pipeline](diagrams/state-detection-pipeline.png)

The detection pipeline proceeds through the following states:

| State | Description |
|---|---|
| **Idle** | No detection in progress. Awaiting `DetectAll()` call. |
| **Loading** | Validating workspace list. Verifying parsed data is present. |
| **Detecting** | Iterating registered strategies. Each strategy analyzes all workspaces and returns candidates. |
| **Scoring** | `ConfidenceScorer` evaluates each candidate and assigns `FinalConfidence`. |
| **Deduplicating** | Merging overlapping candidates. Keeping highest-confidence duplicates. Combining consumer lists. |
| **Complete** | `SeamRegistry` populated with indexed data. Ready for downstream queries. |
| **Failed** | Fatal error (e.g., no valid workspaces). Partial results may exist in the registry if some strategies completed. |

**Performance considerations:**

- Strategies run sequentially to keep memory usage predictable. Parallelism could be added later if profiling shows a bottleneck.
- Each strategy receives the full workspace list by reference (no copying).
- The pipeline targets less than 30 seconds for 500 files and less than 2 minutes for 2000 files (L2-10.1).

---

## 6. Confidence Scoring Algorithm

The `ConfidenceScorer` computes `FinalConfidence` as a weighted sum of four factors:

```
FinalConfidence = clamp(
    w_e * EvidenceStrength +
    w_c * CrossBoundary +
    w_s * SymbolCount +
    w_d * Documentation
, 0.0, 1.0)
```

**Default weights (`ConfidenceWeights`):**

| Factor | Weight | Description |
|---|---|---|
| `EvidenceStrength` (w_e) | 0.40 | How strong the detection evidence is. Based on `RawConfidence` set by the strategy. |
| `CrossBoundary` (w_c) | 0.30 | How many distinct consumer workspaces reference the symbol. More consumers = higher confidence. |
| `SymbolCount` (w_s) | 0.15 | How many contract symbols are associated with the seam. More symbols = richer contract surface. |
| `Documentation` (w_d) | 0.15 | Whether the provider symbols have TSDoc/JSDoc documentation. Documented APIs are more likely to be intentional contracts. |

**Factor calculations:**

- `EvidenceStrength`: Directly uses `SeamCandidate.RawConfidence` (already 0.0-1.0).
- `CrossBoundary`: `min(consumerCount / 3.0, 1.0)` -- saturates at 3 consumers.
- `SymbolCount`: `min(symbolCount / 5.0, 1.0)` -- saturates at 5 symbols.
- `Documentation`: `documentedSymbolCount / totalSymbolCount` -- fraction of symbols with documentation.

**Threshold behavior:**

- Candidates with `FinalConfidence` below `analysis.confidenceThreshold` (default 0.5) are excluded from the registry.
- Candidates between the threshold and 0.7 are included but flagged with a low-confidence warning in CLI output.

---

## 7. Deduplication Logic

Multiple strategies may detect the same boundary symbol. For example, a shared service with BehaviorSubject properties might be detected by both `SharedLibraryStrategy` and `MessageBusStrategy`.

**Deduplication rules:**

1. **Key:** Two candidates are duplicates if they share the same `(ProviderWorkspace, primary symbol name)` pair.
2. **Merge:** When duplicates are found:
   - Keep the candidate with the higher `RawConfidence`.
   - Union the `ConsumerWorkspaces` lists.
   - Union the `ContractSymbols` lists (deduplicated by `FilePath + LineNumber`).
   - Preserve the `DetectedBy` of the winning candidate but add the other strategy name to `Metadata["AlsoDetectedBy"]`.
3. **Type precedence:** If two strategies detect the same symbol but classify it as different `SeamType` values, the more specific type wins. Precedence (highest to lowest): `PluginContract` > `RouteContract` > `StateContract` > `MessageBus` > `HttpApiContract` > `SharedLibrary` > `CustomDecorator`.

---

## 8. Error Handling

**Strategy-level failures:**

- If a strategy throws during `Detect()`, the `SeamDetector` catches the exception, logs a warning with the strategy name and exception details, and continues with remaining strategies.
- The partial results from completed strategies are still scored and registered.
- The overall `DetectAll()` call succeeds (with partial data) unless zero strategies succeed, in which case it throws an `AggregateException`.

**Workspace-level failures:**

- If a `ParsedWorkspace` contains invalid or incomplete data, individual strategies handle this by skipping that workspace and logging a warning.
- This supports graceful degradation per L2-10.3.

**Scoring failures:**

- If scoring fails for a candidate (e.g., division by zero in a factor), the candidate is assigned `FinalConfidence = 0.0` and logged as a warning.

---

## 9. Configuration

The detection module reads these configuration sections from `seamq.config.json`:

```json
{
  "analysis": {
    "confidenceThreshold": 0.5,
    "includeInternalSeams": false,
    "seamTypes": {
      "include": ["PluginContract", "SharedLibrary", "MessageBus",
                   "RouteContract", "StateContract", "HttpApiContract"],
      "exclude": []
    },
    "customDecorators": [
      { "name": "@PluginExport", "seamType": "PluginContract" }
    ]
  }
}
```

| Setting | Type | Default | Description |
|---|---|---|---|
| `confidenceThreshold` | `double` | `0.5` | Minimum confidence to include a seam in the registry. |
| `includeInternalSeams` | `bool` | `false` | Whether to detect seams within a single workspace. |
| `seamTypes.include` | `string[]` | all types | Which seam types to detect. |
| `seamTypes.exclude` | `string[]` | `[]` | Which seam types to skip. Overrides include. |
| `customDecorators` | `array` | `[]` | Custom decorator patterns for `CustomDecoratorStrategy`. |

When `seamTypes.exclude` contains a type, the corresponding strategy is skipped entirely (not invoked).

---

## 10. Diagrams

### Class Diagrams

| Diagram | Description | Source |
|---|---|---|
| ![Class Diagram -- SeamDetector and Strategies](diagrams/class-detector.png) | SeamDetector, ISeamDetectionStrategy interface, and all seven strategy implementations. | [`class-detector.puml`](diagrams/class-detector.puml) |
| ![Class Diagram -- SeamRegistry and Scoring](diagrams/class-seam-registry.png) | SeamRegistry, SeamCandidate, ContractSymbol, ConfidenceScorer, and ConfidenceWeights. | [`class-seam-registry.puml`](diagrams/class-seam-registry.puml) |

### Sequence Diagrams

| Diagram | Description | Source |
|---|---|---|
| ![Sequence -- Seam Detection Pipeline](diagrams/seq-detect-seams.png) | End-to-end flow: SeamDetector iterates strategies, candidates are scored, registry is built. | [`seq-detect-seams.puml`](diagrams/seq-detect-seams.puml) |
| ![Sequence -- Plugin Contract Detection](diagrams/seq-plugin-detection.png) | PluginContractStrategy analyzing cross-workspace interface, token, and forRoot usage. | [`seq-plugin-detection.puml`](diagrams/seq-plugin-detection.puml) |

### State Diagrams

| Diagram | Description | Source |
|---|---|---|
| ![State -- Detection Pipeline](diagrams/state-detection-pipeline.png) | Pipeline states: Idle, Loading, Detecting (per strategy), Scoring, Deduplicating, Complete, Failed. | [`state-detection-pipeline.puml`](diagrams/state-detection-pipeline.puml) |

### C4 Diagrams

| Diagram | Description | Source |
|---|---|---|
| ![C4 Component -- Detector Internals](diagrams/c4-component-detector.png) | C4 Component diagram showing SeamDetector, all strategies, ConfidenceScorer, and SeamRegistry with their relationships. | [`c4-component-detector.puml`](diagrams/c4-component-detector.puml) |

---

## 11. Traceability

| Component | L2 Requirements |
|---|---|
| `ISeamDetectionStrategy` | L2-2.1 through L2-2.15 (defines the contract each strategy implements) |
| `SeamDetector` | L2-2.1 (orchestration), L2-10.3 (graceful degradation), L2-10.4 (deterministic output) |
| `PluginContractStrategy` | L2-2.1 (cross-workspace interfaces), L2-2.2 (injection tokens), L2-2.9 (forRoot/forChild), L2-2.11 (factory/provider functions) |
| `SharedLibraryStrategy` | L2-2.1 (cross-workspace interfaces), L2-2.10 (service API extraction), L2-2.12 (data object extraction) |
| `MessageBusStrategy` | L2-2.5 (Observable/Subject detection), L2-2.8 (SignalR hub detection) |
| `RouteContractStrategy` | L2-2.6 (route contract detection) |
| `StateContractStrategy` | L2-2.7 (NgRx integration), L2-2.13 (state machine detection) |
| `HttpApiContractStrategy` | L2-2.10 (service API extraction), L2-2.12 (data object extraction) |
| `CustomDecoratorStrategy` | L2-2.1 (extensibility for user-defined contracts) |
| `ConfidenceScorer` | L2-2.16 (confidence scoring) |
| `SeamRegistry` | L2-2.1 through L2-2.15 (stores results from all strategies) |
