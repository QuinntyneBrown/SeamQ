# Detailed Design: Core Domain Models

**Module:** `SeamQ.Core.Models`
**Document:** DD-10
**Traces to:** L1-1, L1-2, L1-3, L1-4, L1-6, L1-7, L1-8, L1-9

---

## 1. Overview

The Core Models module defines the domain objects that every other SeamQ module depends on. These types form the shared vocabulary of the system: they carry data between the scanner, detector, generator, renderer, differ, and validator without coupling those modules to each other.

All types in this module are plain data objects (records or simple classes) with no behaviour beyond basic construction and validation. They are immutable or effectively immutable once populated by the scanner and detector stages.

The models fall into four groups:

| Group | Purpose | Key Types |
|-------|---------|-----------|
| Workspace graph | Represent the scanned Angular workspace structure | `Workspace`, `Project` |
| Seam registry | Represent detected interface boundaries and their contracts | `Seam`, `SeamType`, `ContractSurface`, `ContractElement`, `ContractElementKind` |
| Traceability and data dictionary | Support ICD generation with source tracing and data documentation | `DataDictionaryEntry`, `TraceabilityEntry`, `DiagramSpec` |
| Configuration | Deserialise `seamq.config.json` into strongly-typed objects | `SeamQConfig` and nested settings types |

### Architectural Context

The following C4 System Context diagram shows where SeamQ sits relative to external actors and systems.

![C4 System Context](diagrams/c4-context.png)

The following C4 Container diagram shows the internal module decomposition of SeamQ and where the Core Models module fits.

![C4 Container](diagrams/c4-container.png)

---

## 2. Workspace Graph

### 2.1 Workspace

`Workspace` represents a single scanned Angular workspace root. A workspace may be an Nx monorepo, an Angular CLI workspace, or a standalone project.

| Property | Type | Description |
|----------|------|-------------|
| `Path` | `string` | Absolute file-system path to the workspace root directory. |
| `Alias` | `string` | Short display name from configuration (e.g., "Shell", "PluginA"). |
| `Role` | `WorkspaceRole` | The workspace's architectural role: `Framework`, `Plugin`, `Library`, or `Application`. |
| `Type` | `WorkspaceType` | Auto-detected type: `NxMonorepo`, `AngularCli`, or `Standalone`. |
| `Projects` | `IReadOnlyList<Project>` | All Angular projects discovered within this workspace. |
| `TsConfigPaths` | `IReadOnlyDictionary<string, string>` | Resolved TypeScript path aliases from `tsconfig.json` / `tsconfig.base.json`. |
| `BootstrapFile` | `string?` | Path to the detected `main.ts` or bootstrap file, if any. |

**Invariants:**

- `Path` must be a rooted, normalised path.
- `Projects` must contain at least one project for a valid workspace.
- `Alias` defaults to the directory name of `Path` when not provided via configuration.

### 2.2 WorkspaceRole (enum)

```
Framework | Plugin | Library | Application
```

Assigned via configuration (`seamq.config.json` `workspaces[].role`). Controls provider-vs-consumer classification during seam detection.

### 2.3 WorkspaceType (enum)

```
NxMonorepo | AngularCli | Standalone
```

Auto-detected by the scanner based on the presence of `nx.json`, `angular.json`, and project count.

### 2.4 Project

`Project` represents a single Angular project (application or library) inside a workspace.

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Project name from `angular.json` or `project.json`. |
| `ProjectType` | `ProjectType` | `Application` or `Library`. |
| `SourceRoot` | `string` | Relative path to the project source root (e.g., `src`). |
| `Exports` | `IReadOnlyList<ExportedSymbol>` | Symbols exported via barrel files (`public-api.ts`, `index.ts`). |
| `BarrelFiles` | `IReadOnlyList<string>` | Paths to discovered barrel files. |

### 2.5 ProjectType (enum)

```
Application | Library
```

### 2.6 ExportedSymbol

Represents a single publicly exported symbol from a barrel file.

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Symbol name as exported. |
| `Kind` | `ContractElementKind` | The kind of symbol (Interface, Class, Enum, etc.). |
| `SourceFile` | `string` | Original source file defining the symbol. |
| `IsReExport` | `bool` | `true` if this symbol is re-exported through intermediate barrels. |

### Class Diagram

![Core Models Class Diagram](diagrams/class-core-models.png)

---

## 3. Seam Registry

### 3.1 Seam

`Seam` is the central domain object. It represents a detected interface boundary where types, services, tokens, or messages cross from one workspace to another.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `string` | Stable identifier derived from provider workspace, consumer workspace(s), and seam type. Format: `seam-{provider}-{consumer}-{type}`. |
| `Name` | `string` | Human-readable seam name generated from the involved workspaces and contract nature (e.g., "Shell -- PluginA Plugin Contract"). |
| `Type` | `SeamType` | Classification of the interface boundary. |
| `ProviderWorkspace` | `Workspace` | The workspace that defines/exports the contract. |
| `ConsumerWorkspaces` | `IReadOnlyList<Workspace>` | Workspaces that consume/implement the contract. |
| `Confidence` | `double` | Confidence score from 0.0 to 1.0 indicating detection heuristic strength. |
| `ContractSurface` | `ContractSurface` | The complete set of contract elements at this boundary. |

**Invariants:**

- `Confidence` is clamped to `[0.0, 1.0]`.
- `ConsumerWorkspaces` must contain at least one workspace.
- `ProviderWorkspace` must not appear in `ConsumerWorkspaces`.

### 3.2 SeamType (enum)

Classifies the nature of a detected interface boundary.

| Value | Description | Detection Heuristic |
|-------|-------------|---------------------|
| `PluginContract` | Interfaces, abstract classes, and injection tokens exported for plugin implementation. | Exported abstract class or interface implemented across workspace boundary with injection token. |
| `SharedLibrary` | Barrel exports consumed by multiple workspaces via TypeScript path aliases. | Symbols from a library workspace imported in 2+ other workspaces. |
| `MessageBus` | RxJS Subject/Observable streams shared across boundaries. | `Subject<T>`, `BehaviorSubject<T>`, or `Observable<T>` property exposed in public API and subscribed cross-workspace. |
| `RouteContract` | Lazy-loaded routes, route guards, route data interfaces crossing boundaries. | `loadChildren` or `loadComponent` referencing a different workspace; shared route guard interfaces. |
| `StateContract` | Signal state, NgRx actions/selectors, computed state shared across workspaces. | `signal()`, `computed()`, `createAction()`, or `createSelector()` exports consumed cross-workspace. |
| `HttpApiContract` | SignalR hub methods, backend service interfaces crossing boundaries. | SignalR `.invoke()` / `.on()` calls; HTTP service interfaces shared across workspaces. |

### 3.3 ContractSurface

`ContractSurface` aggregates all contract elements that exist at a single seam boundary. It is the main payload for ICD generation.

| Property | Type | Description |
|----------|------|-------------|
| `Elements` | `IReadOnlyList<ContractElement>` | All contract elements at this seam. |
| `DataDictionary` | `IReadOnlyList<DataDictionaryEntry>` | All data types crossing this boundary. |
| `Protocols` | `IReadOnlyList<ProtocolClassification>` | Protocol classifications for contract groups. |

**Convenience accessors** (read-only, computed from `Elements`):

- `Interfaces` -- elements where `Kind == Interface`
- `AbstractClasses` -- elements where `Kind == AbstractClass`
- `InjectionTokens` -- elements where `Kind == InjectionToken`
- `InputBindings` -- elements where `Kind == InputBinding`
- `OutputBindings` -- elements where `Kind == OutputBinding`
- `SignalInputs` -- elements where `Kind == SignalInput`
- `Methods` -- elements where `Kind == Method`
- `Properties` -- elements where `Kind == Property`
- `Observables` -- elements where `Kind == Observable`
- `Types` -- elements where `Kind == Type`
- `Enums` -- elements where `Kind == Enum`
- `Routes` -- elements where `Kind == Route`
- `Actions` -- elements where `Kind == Action`
- `Selectors` -- elements where `Kind == Selector`

### 3.4 ContractElement

`ContractElement` is the base type for every individual element that forms part of a contract surface.

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Fully qualified symbol name. |
| `Kind` | `ContractElementKind` | Discriminator for the element category. |
| `SourceFile` | `string` | Relative path to the file defining this element. |
| `LineNumber` | `int` | 1-based line number of the declaration. |
| `Workspace` | `string` | Alias of the workspace that owns this element. |
| `TypeSignature` | `string?` | Full TypeScript type signature (e.g., method signature, property type). |
| `Documentation` | `string?` | TSDoc/JSDoc documentation extracted from the source. |
| `IsRequired` | `bool` | Whether this element must be implemented by consumers. |
| `GenericTypeParameters` | `IReadOnlyList<string>?` | Generic type parameters if applicable (e.g., `["T", "K"]`). |

### 3.5 ContractElementKind (enum)

Discriminates the category of a contract element.

| Value | Description |
|-------|-------------|
| `Interface` | TypeScript `interface` declaration. |
| `AbstractClass` | TypeScript `abstract class` declaration. |
| `InjectionToken` | Angular `InjectionToken<T>` instance. |
| `InputBinding` | `@Input()` decorated property on an exported component. |
| `OutputBinding` | `@Output()` decorated property with `EventEmitter<T>`. |
| `SignalInput` | Angular signal input via `input()`, `input.required()`, or `model()`. |
| `Method` | Public method on a service or abstract class crossing the boundary. |
| `Property` | Public property on a service or abstract class. |
| `Observable` | `Observable<T>`, `Subject<T>`, `BehaviorSubject<T>` typed member. |
| `Type` | Type alias or class that crosses the boundary. |
| `Enum` | TypeScript `enum` declaration. |
| `Route` | Route definition (`loadChildren`, `loadComponent`, guard interface). |
| `Action` | NgRx `createAction()` export. |
| `Selector` | NgRx `createSelector()` export. |

### Contract Elements Class Diagram

![Contract Elements Class Diagram](diagrams/class-contract-elements.png)

---

## 4. Traceability, Data Dictionary, and Diagrams

### 4.1 DataDictionaryEntry

Describes a type that crosses a seam boundary. Used by the ICD generator to produce the "Data Objects at the Boundary" section.

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Type name (e.g., `TelemetryReading`, `PluginDescriptor`). |
| `Kind` | `DataDictionaryEntryKind` | `Interface`, `TypeAlias`, `Enum`, or `Class`. |
| `Fields` | `IReadOnlyList<FieldDefinition>` | All fields/members with name, type, optionality, default, and documentation. |
| `Documentation` | `string?` | TSDoc block comment for the type. |
| `SourceFile` | `string` | Relative file path. |
| `LineNumber` | `int` | 1-based line number. |
| `GenericTypeParameters` | `IReadOnlyList<string>?` | Generic parameters (e.g., `["T"]`). |

### 4.2 FieldDefinition

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Field name. |
| `Type` | `string` | TypeScript type as a string. |
| `IsOptional` | `bool` | Whether the field is marked optional (`?`). |
| `DefaultValue` | `string?` | Default value expression, if any. |
| `Documentation` | `string?` | TSDoc for this field. |

### 4.3 DataDictionaryEntryKind (enum)

```
Interface | TypeAlias | Enum | Class
```

### 4.4 TraceabilityEntry

Maps a contract element back to its source location and optionally to external requirement identifiers. Used by the ICD generator to produce the Requirements Traceability Matrix.

| Property | Type | Description |
|----------|------|-------------|
| `ElementName` | `string` | Name of the contract element being traced. |
| `ElementKind` | `ContractElementKind` | Kind discriminator. |
| `SourceFile` | `string` | Relative path to the source file. |
| `LineNumber` | `int` | 1-based line number. |
| `Workspace` | `string` | Workspace alias. |
| `ExternalRequirementIds` | `IReadOnlyList<string>?` | External requirement identifiers from configuration (e.g., `["REQ-042", "REQ-043"]`). |

### 4.5 ProtocolClassification

Classifies a group of contract elements into a protocol category.

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Protocol name (e.g., "DI Multi-Provider Registration"). |
| `Category` | `ProtocolCategory` | `DiMultiProvider`, `ComponentRendering`, `ObservablePushStream`, `SignalRead`, `RequestReply`, or `FireAndForget`. |
| `IsAsync` | `bool` | Whether the protocol is asynchronous. |
| `Lifetime` | `ProtocolLifetime` | `OneTime`, `LongLived`, `SessionScoped`, or `ShortLived`. |
| `InvolvedElements` | `IReadOnlyList<string>` | Names of contract elements participating in this protocol. |

### 4.6 DiagramSpec

Specification for a diagram to be generated by the renderer. The detector and generator stages produce `DiagramSpec` instances; the renderer consumes them.

| Property | Type | Description |
|----------|------|-------------|
| `Type` | `DiagramType` | The kind of diagram (see below). |
| `Title` | `string` | Human-readable diagram title for the caption. |
| `OutputFileName` | `string` | File name for the `.puml` output (without directory). |
| `Participants` | `IReadOnlyList<string>` | Ordered list of actors, classes, or components shown in the diagram. |
| `Relationships` | `IReadOnlyList<DiagramRelationship>` | Connections between participants. |
| `SeamId` | `string` | The seam this diagram documents. |

### 4.7 DiagramType (enum)

```
ClassApiSurface | ClassDomainDataObjects | ClassFrontendServices |
ClassRegistrationSystem | ClassBackendContracts | ClassBackendControllers |
ClassDatastoreSchema | ClassMessageInterfaces | ClassRealtimeCommunication |
ClassTelemetryModels | ClassTelemetryService | ClassFileStorage |
SequenceAppStartup | SequencePluginLifecycle | SequenceDataConsumption |
SequenceTileAddSubscribe | SequenceTileRemoveUnsubscribe | SequenceRequestFlow |
SequenceQueryFlow | SequenceCommandFlow | SequenceCommandResponseUi |
SequenceConfigCrud | SequenceAdvisoryMessage | SequenceTelemetrySubscribe |
SequenceErrorHandling | SequenceMessageBusRouting | SequenceReviewTelemetry |
StateDatastore | StateSubscriptionLifecycle |
C4Context | C4ContextArchitecture | C4Container | C4ComponentFrontend |
C4ComponentBackend | C4PluginApiLayers | C4PluginArchitecture |
C4DataFlow | C4SubscriptionChannelMap | C4ProtocolStack | C4Dynamic |
C4Deployment
```

### 4.8 DiagramRelationship

| Property | Type | Description |
|----------|------|-------------|
| `From` | `string` | Source participant name. |
| `To` | `string` | Target participant name. |
| `Label` | `string?` | Relationship label (e.g., "implements", "subscribes to"). |
| `Style` | `RelationshipStyle` | `Solid`, `Dashed`, `Dotted`, or `Bold`. |

---

## 5. Configuration

### 5.1 SeamQConfig

Top-level configuration object deserialized from `seamq.config.json`. All properties have sensible defaults so the config file is optional.

| Property | Type | Description |
|----------|------|-------------|
| `Workspaces` | `List<WorkspaceConfig>` | Workspace definitions. |
| `Seams` | `SeamFilterConfig?` | Optional seam type include/exclude filters. |
| `Output` | `OutputConfig` | Output directory, formats, and diagram settings. |
| `Analysis` | `AnalysisConfig` | Scanner/detector tuning parameters. |
| `Icd` | `IcdConfig` | ICD document metadata. |

### 5.2 WorkspaceConfig

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Path` | `string` | (required) | Workspace root path, resolved relative to the config file. |
| `Alias` | `string?` | directory name | Display alias. |
| `Role` | `string` | `"application"` | `"framework"`, `"plugin"`, `"library"`, or `"application"`. |

### 5.3 SeamFilterConfig

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IncludeTypes` | `List<string>?` | all types | Seam types to include (e.g., `["PluginContract", "SharedLibrary"]`). |
| `ExcludeTypes` | `List<string>?` | none | Seam types to exclude. |
| `CustomDecorators` | `List<string>?` | none | Extra decorator names to treat as seam markers. |

### 5.4 OutputConfig

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Directory` | `string` | `"./seamq-output"` | Output root directory. |
| `Formats` | `List<string>` | `["md"]` | Default output formats: `"md"`, `"html"`. |
| `Diagrams` | `DiagramOutputConfig` | (defaults) | Diagram rendering settings. |

### 5.5 DiagramOutputConfig

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RenderFormat` | `string` | `"svg"` | `"svg"`, `"png"`, or `"both"`. |
| `PlantumlServer` | `string` | `"local"` | `"local"`, `"docker"`, or a URL. |
| `Theme` | `string` | `"plain"` | PlantUML theme name. |
| `Skinparams` | `Dictionary<string, string>?` | none | Extra PlantUML skinparam overrides. |

### 5.6 AnalysisConfig

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MaxDepth` | `int` | `10` | Maximum import chain traversal depth. |
| `FollowNodeModules` | `bool` | `false` | Whether to scan into `node_modules`. |
| `IncludeInternalSeams` | `bool` | `false` | Detect seams within the same workspace. |
| `ConfidenceThreshold` | `double` | `0.5` | Minimum confidence to report a seam. |
| `Exclude` | `List<string>?` | none | Glob patterns to exclude from scanning. |

### 5.7 IcdConfig

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Title` | `string` | `"Interface Control Document"` | Document title. |
| `DocumentNumber` | `string?` | none | Document number (e.g., `"ICD-001"`). |
| `Revision` | `string?` | none | Revision identifier. |
| `Classification` | `string?` | none | Classification marking. |
| `Template` | `string?` | none | Path to a custom ICD template file. |
| `Standards` | `List<string>?` | none | Applicable standards listed in the ICD introduction. |

### Configuration Class Diagram

![Configuration Class Diagram](diagrams/class-configuration.png)

---

## 6. Serialization and Persistence

All model types are designed for `System.Text.Json` serialization. The following conventions apply:

- Properties use `camelCase` in JSON (via `JsonSerializerDefaults.Web` or `[JsonPropertyName]`).
- Enum values are serialized as strings (via `JsonStringEnumConverter`).
- `IReadOnlyList<T>` properties deserialize from JSON arrays.
- `IReadOnlyDictionary<K,V>` properties deserialize from JSON objects.
- Nullable properties are omitted from JSON output when `null` (via `DefaultIgnoreCondition = WhenWritingNull`).

The `SeamQConfig` type is the root deserialization target for `seamq.config.json`. All other model types are serialized when saving baselines (`seamq scan --save-baseline`) and exporting data (`seamq export`).

---

## 7. Design Decisions

| Decision | Rationale |
|----------|-----------|
| Records over classes for data models | Immutability, value equality, and `with` expression support reduce bugs in multi-stage pipelines. |
| `IReadOnlyList<T>` / `IReadOnlyDictionary<K,V>` for collections | Enforces immutability at the type level; callers cannot accidentally mutate shared model instances. |
| `ContractElementKind` enum rather than a class hierarchy | All contract elements share the same property set; a discriminator enum avoids a deep inheritance tree and simplifies serialization. |
| Stable `Seam.Id` format | Deterministic IDs enable baseline diffing, cross-run identity, and stable file names. |
| Separate `SeamQConfig` from domain models | Configuration shape mirrors the JSON file; domain models are richer. Mapping happens once at startup. |
| No behaviour in models | Keeps the Core Models assembly dependency-free and testable. Behaviour lives in Scanner, Detector, Generator, etc. |

---

## 8. Dependencies

The `SeamQ.Core` assembly containing these models has **zero external package dependencies**. It targets `net8.0` and uses only BCL types (`System.Collections.Generic`, `System.Text.Json.Serialization` attributes).

All other SeamQ assemblies reference `SeamQ.Core`:

```
SeamQ.Cli --> SeamQ.Core
SeamQ.Scanner --> SeamQ.Core
SeamQ.Detector --> SeamQ.Core
SeamQ.Generator --> SeamQ.Core
SeamQ.Renderer --> SeamQ.Core
SeamQ.Differ --> SeamQ.Core
SeamQ.Validator --> SeamQ.Core
```

---

## 9. Diagram Index

| Diagram | File | Section |
|---------|------|---------|
| C4 System Context | `diagrams/c4-context.puml` | 1. Overview |
| C4 Container | `diagrams/c4-container.puml` | 1. Overview |
| Core Models Class Diagram | `diagrams/class-core-models.puml` | 2. Workspace Graph |
| Contract Elements Class Diagram | `diagrams/class-contract-elements.puml` | 3. Seam Registry |
| Configuration Class Diagram | `diagrams/class-configuration.puml` | 5. Configuration |
