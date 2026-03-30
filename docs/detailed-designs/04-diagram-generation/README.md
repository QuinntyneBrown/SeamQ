# Detailed Design: Diagram Generation Module

**Module:** `SeamQ.Renderer`
**Target Framework:** .NET 8
**Status:** Design
**Total Diagram Types:** 39 (12 class, 15 sequence, 2 state, 10+ C4)

---

## 1. Overview

The Diagram Generation module is responsible for producing visual representations of every seam that SeamQ detects. For each seam, the module generates up to 39 distinct diagram files in PlantUML (`.puml`) syntax and optionally renders them to SVG or PNG images.

The module is organized into four diagram families:

| Family | Count | Purpose |
|---|---|---|
| Class diagrams | 12 | Show static structure -- interfaces, DTOs, controllers, schemas |
| Sequence diagrams | 15 | Show runtime interactions -- request flows, lifecycle events, data pipelines |
| State diagrams | 2 | Show state machines -- datastore transitions, subscription lifecycle |
| C4 diagrams | 10+ | Show architecture at system-context, container, and component levels |

Each diagram generator implements a common interface (`IPlantUmlDiagram`) and is conditionally invoked based on the content of the analyzed seam. The orchestrator (`DiagramRenderer`) selects applicable generators, invokes them in parallel where possible, and writes the resulting `.puml` files. An optional second pass uses `ImageRenderer` to convert `.puml` files to raster or vector images via a local PlantUML JAR, Docker container, or remote PlantUML server.

![Renderer class hierarchy](diagrams/class-renderer.png)

![ImageRenderer and encoding classes](diagrams/class-image-renderer.png)

---

## 2. Core Components

### 2.1 IDiagramRenderer / DiagramRenderer

`IDiagramRenderer` is the top-level entry point consumed by `SeamQ.Generator` (the ICD generator). It accepts a `SeamModel` and `DiagramOptions` and returns a collection of `DiagramResult` objects.

```
IDiagramRenderer
  + GenerateAsync(seam, options, ct): Task<IReadOnlyList<DiagramResult>>
```

`DiagramRenderer` is the concrete implementation. Its responsibilities are:

1. **Discovery** -- iterate the injected `IReadOnlyList<IPlantUmlDiagram>` and call `IsApplicable(seam)` on each.
2. **Generation** -- invoke `GenerateAsync` on every applicable generator, collecting `.puml` content.
3. **Encoding** -- pass each raw PlantUML string through `PlantUmlEncoder` to validate and normalize syntax.
4. **File output** -- write `.puml` files to the configured output directory.
5. **Image rendering (optional)** -- if `DiagramOptions.RenderImages` is true, delegate to `ImageRenderer`.

The generator list is registered via dependency injection so new diagram types can be added without modifying the orchestrator.

### 2.2 IPlantUmlDiagram

Every diagram generator implements this interface:

```
IPlantUmlDiagram
  + DiagramType: DiagramFamily {readOnly}   // Class, Sequence, State, C4
  + Name: string {readOnly}                 // Unique diagram key
  + IsApplicable(seam: SeamModel): bool
  + GenerateAsync(seam: SeamModel, ctx: RenderContext, ct: CancellationToken): Task<DiagramContent>
```

`DiagramContent` contains the raw PlantUML text, a suggested file name, and metadata (title, description, cross-references to ICD sections).

### 2.3 PlantUmlEncoder

A stateless utility class that:

- Wraps content in `@startuml` / `@enduml` markers if absent.
- Validates bracket matching and directive syntax.
- Applies skinparam defaults from `DiagramOptions.Theme`.
- Encodes the text for PlantUML Server URLs (deflate + base64 per the PlantUML specification).

### 2.4 ImageRenderer

Converts `.puml` files to SVG or PNG. It supports three rendering backends detected at startup in priority order:

1. **Local JAR** -- invokes `java -jar plantuml.jar` directly.
2. **Docker** -- runs the `plantuml/plantuml-server` image.
3. **Remote server** -- POSTs encoded text to a configurable PlantUML server URL.

`ImageRenderer` manages concurrency (configurable parallelism), timeout, and retry for each rendering call.

![Diagram generation sequence](diagrams/seq-generate-diagrams.png)

![Image rendering sequence](diagrams/seq-render-image.png)

![Rendering state machine](diagrams/state-rendering.png)

---

## 3. Class Diagram Generators

All class diagram generators live under `PlantUml/ClassDiagrams/` and produce diagrams that depict the static structure visible at a seam boundary.

| # | Class | Diagram Content |
|---|---|---|
| 1 | `ApiSurfaceClassDiagram` | Public API types exposed by the seam -- REST DTOs, GraphQL types, gRPC messages. |
| 2 | `BackendContractsClassDiagram` | Interfaces and abstract classes that backend consumers must implement. |
| 3 | `BackendControllersClassDiagram` | Controller classes, action methods, route attributes, and parameter types. |
| 4 | `DatastoreSchemaClassDiagram` | Entity models, table mappings, relationships, and key constraints. |
| 5 | `DomainDataObjectsClassDiagram` | Domain value objects, aggregates, and their invariants. |
| 6 | `FrontendServicesClassDiagram` | Angular/React service classes or hooks injected across the seam. |
| 7 | `MessageInterfacesClassDiagram` | Message bus contracts -- commands, events, envelopes, headers. |
| 8 | `RealtimeCommunicationClassDiagram` | SignalR hubs, WebSocket channels, and their message types. |
| 9 | `TelemetryModelsClassDiagram` | Telemetry event payloads, metric definitions, and dimension enums. |
| 10 | `TelemetryServiceClassDiagram` | Telemetry service interfaces and their concrete providers. |
| 11 | `RegistrationSystemClassDiagram` | Plugin registration interfaces, manifest types, and capability descriptors. |
| 12 | *(reserved)* | Extensibility slot for project-specific class diagrams. |

Each generator queries the `SeamModel` for the relevant code elements (e.g., `seam.Controllers`, `seam.DataEntities`) and returns an empty result when none are found, making the generator effectively non-applicable.

---

## 4. Sequence Diagram Generators

Sequence diagram generators live under `PlantUml/SequenceDiagrams/` and depict runtime interactions across the seam boundary.

| # | Class | Scenario Depicted |
|---|---|---|
| 1 | `AppStartupSequence` | Application bootstrap -- host builder, DI registration, middleware pipeline, seam initialization. |
| 2 | `PluginLifecycleSequence` | Plugin load, initialize, activate, deactivate, and unload stages. |
| 3 | `DataConsumptionSequence` | End-to-end flow of data from external source through ingestion, transformation, and storage. |
| 4 | `TileAddSubscribeSequence` | UI tile creation, subscription to a data channel, and initial data push. |
| 5 | `TileRemoveUnsubscribeSequence` | UI tile removal, unsubscription, and resource cleanup. |
| 6 | `RequestFlowSequence` | HTTP request from client through middleware, controller, service, and repository layers. |
| 7 | `QueryFlowSequence` | CQRS query dispatch through handler, repository, caching, and response mapping. |
| 8 | `CommandFlowSequence` | CQRS command dispatch through validation, handler, domain logic, and persistence. |
| 9 | `EventPublishSequence` | Domain event raised, dispatched to bus, delivered to subscribers. |
| 10 | `EventConsumeSequence` | Message bus consumer receives event, deserializes, processes, and acknowledges. |
| 11 | `AuthenticationSequence` | Token acquisition, validation, claims extraction, and principal creation. |
| 12 | `AuthorizationSequence` | Policy evaluation, role check, resource-based authorization decision. |
| 13 | `ErrorHandlingSequence` | Exception thrown, caught by middleware, mapped to problem-details response. |
| 14 | `CacheLookupSequence` | Cache check, miss, backend fetch, cache population, and return. |
| 15 | `HealthCheckSequence` | Health endpoint invoked, dependency checks executed, aggregate status returned. |

Sequence generators inspect `seam.Interactions` and `seam.Protocols` to determine participant ordering and message labels.

---

## 5. State Diagram Generators

State diagram generators live under `PlantUml/StateDiagrams/`.

| # | Class | States Modeled |
|---|---|---|
| 1 | `DatastoreStateDiagram` | `Disconnected -> Connecting -> Connected -> Migrating -> Ready -> Error` with reconnect transitions. |
| 2 | `SubscriptionLifecycleStateDiagram` | `Idle -> Subscribing -> Active -> Paused -> Unsubscribing -> Terminated` with error recovery. |

State diagrams are generated when `seam.StateMachines` contains recognized state-transition metadata.

---

## 6. C4 Diagram Generators

C4 diagram generators live under `C4/` and use the [C4-PlantUML](https://github.com/plantuml-stdlib/C4-PlantUML) standard library.

| # | Class | C4 Level | Content |
|---|---|---|---|
| 1 | `C4SystemContext` | Level 1 | The system under analysis, external actors, and neighboring systems. |
| 2 | `C4Container` | Level 2 | Containers within the system -- web app, API, database, message bus. |
| 3 | `C4ComponentServices` | Level 3 | Service-layer components within the API container. |
| 4 | `C4ComponentBackend` | Level 3 | Backend infrastructure components -- repositories, caches, gateways. |
| 5 | `C4PluginApiLayers` | Level 3 | Plugin API layers -- host facade, plugin sandbox, extension points. |
| 6 | `C4PluginArchitecture` | Level 3 | Plugin internal architecture -- manifest, capabilities, lifecycle manager. |
| 7 | `C4DataFlow` | Level 3 | Data movement across containers -- ingestion, transformation, storage, query. |
| 8 | `C4DeploymentDev` | Level 4 | Development deployment topology -- local services, containers, ports. |
| 9 | `C4DeploymentProd` | Level 4 | Production deployment topology -- cloud resources, scaling groups, CDN. |
| 10 | `C4DynamicRequest` | Dynamic | A single request traced through all containers and components. |

C4 generators derive their content from `seam.Architecture` and `seam.Deployment` metadata. When deployment metadata is absent, the Level 4 deployment diagrams are skipped.

![C4 Component view of the Renderer module](diagrams/c4-component-renderer.png)

---

## 7. Generation Pipeline

The following sequence describes the end-to-end generation pipeline:

1. The ICD Generator calls `IDiagramRenderer.GenerateAsync(seam, options, ct)`.
2. `DiagramRenderer` filters the injected diagram list via `IsApplicable`.
3. Applicable generators run concurrently (bounded by `options.MaxParallelism`, default 4).
4. Each generator returns `DiagramContent` containing raw PlantUML text.
5. `PlantUmlEncoder` validates and normalizes each `.puml` output.
6. `.puml` files are written to `{outputDir}/diagrams/`.
7. If `options.RenderImages` is true, `ImageRenderer` converts `.puml` to SVG/PNG.
8. `DiagramResult` objects (file paths, metadata) are returned to the caller.

### Error Handling

- Individual diagram failures are caught and logged; they do not abort the batch.
- `ImageRenderer` failures (e.g., PlantUML server unreachable) produce a warning and leave the `.puml` file in place without a rendered image.
- A `DiagramResult.Status` enum (`Success`, `PartialFailure`, `Skipped`, `Error`) communicates per-diagram outcomes.

### Performance Considerations

- Diagram generators are stateless and safe for concurrent execution.
- `ImageRenderer` parallelism is capped to avoid overwhelming the PlantUML process or server.
- Large seams (50+ types) trigger automatic diagram splitting to keep output within letter-size page bounds.

---

## 8. Configuration

Diagram generation is configured through `DiagramOptions`, populated from the CLI or `seamq.json`:

| Property | Type | Default | Description |
|---|---|---|---|
| `RenderImages` | `bool` | `false` | Whether to render `.puml` to images. |
| `ImageFormat` | `ImageFormat` | `Svg` | Output format: `Svg`, `Png`, or `Both`. |
| `Theme` | `string` | `"plain"` | PlantUML theme name. |
| `MaxParallelism` | `int` | `4` | Max concurrent diagram generators. |
| `RenderParallelism` | `int` | `2` | Max concurrent image renders. |
| `PlantUmlServerUrl` | `string?` | `null` | Remote PlantUML server URL (overrides local). |
| `PlantUmlJarPath` | `string?` | `null` | Path to `plantuml.jar` (auto-detected if null). |
| `DockerImage` | `string` | `"plantuml/plantuml-server:latest"` | Docker image for rendering. |
| `OutputSubdirectory` | `string` | `"diagrams"` | Subdirectory within the ICD output folder. |
| `PageSize` | `PageSize` | `Letter` | Target page size for layout constraints. |
| `EnabledFamilies` | `DiagramFamily[]` | All | Which diagram families to generate. |

---

## 9. Dependency Injection Registration

All diagram generators are registered via a single extension method:

```csharp
services.AddSeamQDiagramGeneration(options =>
{
    options.RenderImages = true;
    options.ImageFormat = ImageFormat.Svg;
});
```

This registers:

- `IDiagramRenderer` as `DiagramRenderer` (scoped).
- All `IPlantUmlDiagram` implementations via assembly scanning (transient).
- `PlantUmlEncoder` (singleton).
- `ImageRenderer` (singleton).

---

## 10. Extensibility

Third-party diagram types can be added by:

1. Implementing `IPlantUmlDiagram`.
2. Registering the implementation in the DI container.
3. The orchestrator discovers it automatically on the next generation run.

No changes to `DiagramRenderer` or configuration are required.

---

## 11. Diagram Reference Index

All design diagrams for this module:

| Diagram | File | Description |
|---|---|---|
| Renderer class hierarchy | [class-renderer.puml](diagrams/class-renderer.puml) | Core classes and the diagram generator interface hierarchy. |
| Image renderer classes | [class-image-renderer.puml](diagrams/class-image-renderer.puml) | ImageRenderer, PlantUmlEncoder, and rendering option types. |
| Generation sequence | [seq-generate-diagrams.puml](diagrams/seq-generate-diagrams.puml) | End-to-end flow from orchestrator through generators to file output. |
| Image rendering sequence | [seq-render-image.puml](diagrams/seq-render-image.puml) | ImageRenderer detecting a backend and invoking PlantUML. |
| Rendering state machine | [state-rendering.puml](diagrams/state-rendering.puml) | States of the rendering pipeline from idle to complete. |
| C4 component view | [c4-component-renderer.puml](diagrams/c4-component-renderer.puml) | C4 Level 3 component view of the Renderer module internals. |
