# Detailed Design: ICD Generation Module

**Module:** `SeamQ.Generator`
**Target Framework:** .NET 8
**Namespace Root:** `SeamQ.Generator`
**Status:** Design
**Last Updated:** 2026-03-30

---

## Table of Contents

1. [Overview](#1-overview)
2. [Architecture](#2-architecture)
3. [Core Interfaces and Orchestrator](#3-core-interfaces-and-orchestrator)
4. [Section Generators](#4-section-generators)
5. [Output Formatters](#5-output-formatters)
6. [Template Engine](#6-template-engine)
7. [Generation Pipeline](#7-generation-pipeline)
8. [State Machine](#8-state-machine)
9. [Dependency Registration](#9-dependency-registration)
10. [Error Handling](#10-error-handling)
11. [Configuration](#11-configuration)
12. [Diagram Index](#12-diagram-index)

---

## 1. Overview

The ICD Generation module is the core output-producing component of SeamQ. It consumes a `SeamModel` (produced by the Seam Detection module) and generates a complete **Interface Control Document** -- a structured, multi-section document that fully describes every contract surface of a detected seam.

The module is designed around three principles:

- **Section modularity** -- Each ICD section is an independent generator implementing a common interface. Sections can be added, removed, or reordered without touching the orchestrator.
- **Format independence** -- The generation pipeline produces a format-neutral content model first, then delegates to pluggable formatters for Markdown, HTML, and PDF output.
- **Template customization** -- All output passes through a Handlebars-based template engine, allowing teams to supply their own templates without modifying code.

### C4 Component View

The following C4 component diagram shows how the Generator module relates to other SeamQ systems and its internal structure.

![C4 Component Diagram - Generator Internals](diagrams/c4-component-generator.png)

---

## 2. Architecture

### High-Level Flow

```
SeamModel ──> IcdGenerator ──> [Section Generators] ──> ContentModel ──> [Formatters] ──> .md / .html / .pdf
```

The `IcdGenerator` is the single entry point. It receives a `SeamModel` plus `GenerationOptions`, iterates over the ordered collection of `IIcdSection` implementations, assembles results into an `IcdDocument`, and then invokes the requested `IOutputFormatter` implementations to produce files.

### Class Structure - Generator Core

![Class Diagram - Generator Core](diagrams/class-generator.png)

### Class Structure - Formatters

![Class Diagram - Formatters](diagrams/class-formatters.png)

---

## 3. Core Interfaces and Orchestrator

### 3.1 `IIcdGenerator` Interface

```csharp
namespace SeamQ.Generator;

public interface IIcdGenerator
{
    Task<IcdDocument> GenerateAsync(
        SeamModel seam,
        GenerationOptions options,
        CancellationToken cancellationToken = default);
}
```

This is the public API consumed by the CLI and any programmatic callers. It accepts a fully-populated `SeamModel`, a set of `GenerationOptions` controlling output format and directory, and returns the assembled `IcdDocument` once all files have been written.

### 3.2 `IcdGenerator` Implementation

`IcdGenerator` is the concrete orchestrator registered as a singleton. It holds:

| Dependency | Purpose |
|---|---|
| `IReadOnlyList<IIcdSection>` | Ordered collection of section generators, injected via DI |
| `IReadOnlyList<IOutputFormatter>` | Available output formatters, filtered at runtime by requested formats |
| `TemplateEngine` | Handlebars template compilation and rendering |
| `ILogger<IcdGenerator>` | Structured logging throughout the pipeline |

**Orchestration steps:**

1. **Validate** the `SeamModel` and `GenerationOptions`. Throw `GenerationException` on invalid input.
2. **Iterate** sections in `Order` sequence. For each, call `IsApplicable(seam)` -- skip sections that do not apply to this seam type.
3. **Generate** by calling `GenerateAsync(seam, ctx, ct)` on applicable sections. Each returns an `IcdSectionResult`.
4. **Build content model** -- merge all `IcdSectionResult` instances into a unified `ContentModel` with cross-references resolved.
5. **Create `IcdDocument`** from the content model, capturing metadata and timestamps.
6. **Format** -- for each requested `OutputFormat`, locate the matching `IOutputFormatter` and call `FormatAsync`. Write the resulting stream to the output directory.
7. **Return** the `IcdDocument` to the caller.

### 3.3 `IIcdSection` Interface

```csharp
namespace SeamQ.Generator.Sections;

public interface IIcdSection
{
    int Order { get; }
    string Title { get; }
    bool IsApplicable(SeamModel seam);
    Task<IcdSectionResult> GenerateAsync(
        SeamModel seam,
        GenerationContext context,
        CancellationToken cancellationToken = default);
}
```

| Member | Description |
|---|---|
| `Order` | Integer controlling the section's position in the final document. Sections are sorted ascending before iteration. |
| `Title` | Human-readable section heading (e.g., "Introduction", "Protocols"). |
| `IsApplicable` | Returns `false` when the seam data does not contain relevant information for this section (e.g., `ProtocolsSection` returns `false` when no protocols are detected). Skipped sections do not appear in the output. |
| `GenerateAsync` | Produces an `IcdSectionResult` containing rendered content, diagram references, and cross-reference entries. |

### 3.4 `GenerationContext`

A shared context object passed to every section generator, providing:

- Access to previously generated section results (for cross-referencing).
- Shared diagram registry (to avoid duplicate diagram generation).
- The active `GenerationOptions`.
- A cancellation token relay.

---

## 4. Section Generators

Each section generator is a separate class implementing `IIcdSection`. They are registered in DI and automatically discovered by the orchestrator. The table below summarizes all 15 sections.

| Order | Class | ICD Section Title | Description |
|-------|-------|-------------------|-------------|
| 1 | `IntroductionSection` | Introduction | Purpose, scope, system overview, and context diagram |
| 2 | `ReferenceDocumentsSection` | Reference Documents | Auto-discovered source code references table |
| 3 | `InterfaceOverviewSection` | Interface Overview | API layers table and architecture diagrams |
| 4 | `ScopeOfResponsibilitySection` | Scope of Responsibility | Responsibility matrix (provider / consumer / shared) |
| 5 | `StandardsConventionsSection` | Standards & Conventions | Detected coding conventions and patterns |
| 6 | `RegistrationContractSection` | Registration Contract | Injection tokens, descriptors, factories |
| 7 | `ComponentInputContractSection` | Component Input Contract | All bindings with types and defaults |
| 8 | `InjectableServicesSection` | Injectable Services | Service API tables grouped by purpose |
| 9 | `UtilitiesSection` | Utilities | Exported utility functions |
| 10 | `DataObjectsSection` | Data Objects | Types organized by domain |
| 11 | `LifecycleStateManagementSection` | Lifecycle & State Management | State machines and step-by-step sequences |
| 12 | `ProtocolsSection` | Protocols | One subsection per detected protocol |
| 13 | `TraceabilityMatrixSection` | Traceability Matrix | Contract element to source location mapping |
| 14 | `DiagramIndexSection` | Diagram Index | All diagrams by type with cross-references |
| 15 | `RevisionHistorySection` | Revision History | Tracked changes between generation runs |

### 4.1 `IntroductionSection` (Order 1)

**Responsibility:** Generates the opening section of the ICD containing:

- **Purpose** -- a statement derived from the seam's detected role (e.g., "This ICD defines the interface contract for the UserAuthentication seam").
- **Scope** -- boundaries of what the ICD covers, derived from the seam's provider and consumer components.
- **System Overview** -- a prose summary of the seam's place in the overall architecture.
- **Context Diagram** -- requests a C4 Context diagram from the Diagram Generation module and embeds a reference.

**Applicability:** Always applicable. Every seam produces an introduction.

**Key logic:**
- Reads `SeamModel.Name`, `SeamModel.Description`, `SeamModel.Provider`, and `SeamModel.Consumer` to populate templates.
- Calls `IDiagramGenerator.GenerateContextDiagram(seam)` to produce the context diagram.

### 4.2 `ReferenceDocumentsSection` (Order 2)

**Responsibility:** Produces a table of all source files that contribute to the seam's contract surface.

| Column | Source |
|---|---|
| Document ID | Auto-generated (REF-001, REF-002, ...) |
| Title | File name or detected module name |
| Path | Relative path from workspace root |
| Version | Git commit hash or file last-modified date |
| Relevance | Why this file is referenced (e.g., "Defines service interface") |

**Applicability:** Always applicable. At minimum, the seam definition file itself is a reference.

**Key logic:**
- Walks `SeamModel.SourceLocations` to discover all contributing files.
- Queries Git (if available) for commit hashes to populate the Version column.
- Sorts references by relevance category, then alphabetically.

### 4.3 `InterfaceOverviewSection` (Order 3)

**Responsibility:** Provides a high-level table of all API layers the seam exposes and accompanying architecture diagrams.

**Output includes:**
- An **API layers table** listing each interface surface (REST endpoints, gRPC services, event channels, method signatures) with direction (inbound/outbound) and protocol.
- A **component architecture diagram** showing how provider and consumer connect through the seam.
- A **data flow diagram** showing the direction and shape of data crossing the seam boundary.

**Applicability:** Always applicable.

**Key logic:**
- Groups `SeamModel.Contracts` by contract type (HTTP, Event, Method, etc.).
- Requests architecture and data-flow diagrams from the Diagram Generation module.

### 4.4 `ScopeOfResponsibilitySection` (Order 4)

**Responsibility:** Generates a responsibility matrix showing which contract elements are owned by the provider, the consumer, or shared.

**Output structure:**

| Contract Element | Provider | Consumer | Shared | Notes |
|---|---|---|---|---|
| `UserDto` | X | | | Defined in provider, consumed read-only |
| `IAuthService` | X | | | Provider implements, consumer depends |
| `AuthEvent` | | | X | Published by provider, subscribed by consumer |

**Applicability:** Applicable when the seam has identifiable provider and consumer components.

**Key logic:**
- Analyzes `SeamModel.Provider`, `SeamModel.Consumer`, and each `ContractElement.Ownership` field.
- Flags ambiguous ownership for human review with a "Needs Review" note.

### 4.5 `StandardsConventionsSection` (Order 5)

**Responsibility:** Documents coding standards and conventions detected in the seam's source code.

**Detected conventions include:**
- Naming conventions (PascalCase services, camelCase properties, etc.)
- Error handling patterns (Result types, exception hierarchies, error codes)
- Async patterns (Task-based, observable streams)
- Nullability annotations
- Documentation comment style (XML doc, JSDoc, etc.)

**Applicability:** Always applicable. Falls back to a default conventions statement if no specific patterns are detected.

**Key logic:**
- Uses `SeamModel.DetectedConventions` populated by the Seam Detection module.
- Cross-references with project-level `.editorconfig` or analyzer settings if available.

### 4.6 `RegistrationContractSection` (Order 6)

**Responsibility:** Documents how the seam's services are registered in the dependency injection container.

**Output includes:**
- **Injection tokens** -- interface types or string tokens used for resolution.
- **Service descriptors** -- lifetime (Singleton, Scoped, Transient), implementation type, and registration method.
- **Factory registrations** -- any factory delegates or `IServiceProviderFactory` usage.
- **Registration code location** -- source file and line number where registration occurs.

**Applicability:** Applicable when DI registration information is detected in the seam model.

**Key logic:**
- Reads `SeamModel.Registrations` for DI container entries.
- Resolves the registration chain (e.g., `AddScoped<IFoo, Foo>()` maps token `IFoo` to implementation `Foo` with Scoped lifetime).

### 4.7 `ComponentInputContractSection` (Order 7)

**Responsibility:** Lists every input binding (constructor parameters, property bindings, configuration values) for the seam's components.

**Output table:**

| Binding | Type | Required | Default | Validation | Source |
|---|---|---|---|---|---|
| `connectionString` | `string` | Yes | -- | NonEmpty | `appsettings.json` |
| `retryCount` | `int` | No | `3` | Range(1,10) | Constructor param |
| `logger` | `ILogger` | Yes | -- | -- | DI injection |

**Applicability:** Applicable when the seam has components with input bindings.

**Key logic:**
- Extracts from `SeamModel.InputBindings`.
- Resolves default values from source code analysis (default parameter values, property initializers).
- Detects validation attributes (`[Required]`, `[Range]`, custom validators).

### 4.8 `InjectableServicesSection` (Order 8)

**Responsibility:** Produces detailed API tables for all injectable services exposed through the seam.

**Organization:** Services are grouped by purpose category:
- **Core Services** -- primary business logic interfaces.
- **Infrastructure Services** -- logging, caching, messaging.
- **Cross-Cutting Services** -- authorization, validation, telemetry.

**Per-service output:**
- Service interface name and namespace.
- Method signatures with parameter types and return types.
- Summary documentation (from XML doc comments or equivalent).
- Lifetime and registration scope.

**Applicability:** Applicable when `SeamModel.Services` contains at least one entry.

### 4.9 `UtilitiesSection` (Order 9)

**Responsibility:** Documents all exported utility functions that are part of the seam's public surface.

**Output per function:**
- Function signature (name, parameters, return type).
- Description (from doc comments).
- Usage example (if available from test files or doc comments).
- Source file location.

**Applicability:** Applicable when `SeamModel.Utilities` contains at least one entry.

### 4.10 `DataObjectsSection` (Order 10)

**Responsibility:** Documents all data transfer objects, models, and type definitions that cross the seam boundary.

**Organization:** Types are grouped by domain area (e.g., "User Management", "Order Processing") based on namespace or directory structure.

**Per-type output:**
- Type name and kind (class, record, struct, enum, interface).
- Properties/fields with types, nullability, and documentation.
- Validation rules applied to the type.
- Relationships to other types (inheritance, composition).

**Applicability:** Applicable when `SeamModel.DataObjects` contains at least one entry.

**Key logic:**
- Clusters types by detected domain using namespace segments.
- Builds a type relationship graph for cross-referencing.

### 4.11 `LifecycleStateManagementSection` (Order 11)

**Responsibility:** Documents state machines and lifecycle sequences observed in the seam's components.

**Output includes:**
- **State machine diagrams** -- for components with discrete states and transitions.
- **Step-by-step initialization sequences** -- ordered lists of startup/setup operations.
- **Disposal/cleanup sequences** -- teardown operations and resource release order.
- **State transition tables** -- current state, trigger, next state, guard conditions.

**Applicability:** Applicable when the seam model contains lifecycle or state management data (`SeamModel.StateMachines` or `SeamModel.LifecycleHooks`).

**Key logic:**
- Generates PlantUML state diagrams for each detected state machine.
- Orders lifecycle hooks by execution phase (construction, initialization, active, disposal).

### 4.12 `ProtocolsSection` (Order 12)

**Responsibility:** Generates one subsection per communication protocol detected in the seam.

**Supported protocol types:**
- **HTTP/REST** -- endpoints, methods, request/response schemas, status codes.
- **gRPC** -- service definitions, message types, streaming modes.
- **Message Queue** -- topics, message schemas, delivery guarantees.
- **Event Bus** -- event types, publisher/subscriber mapping.
- **SignalR/WebSocket** -- hub methods, client callbacks.
- **Method Invocation** -- direct method calls across the seam boundary.

**Per-protocol output:**
- Protocol name and version.
- Endpoint/channel definitions.
- Message schemas with field-level documentation.
- Sequence diagrams for typical interaction flows.
- Error handling and retry semantics.

**Applicability:** Applicable when `SeamModel.Protocols` contains at least one entry.

### 4.13 `TraceabilityMatrixSection` (Order 13)

**Responsibility:** Produces a traceability matrix mapping every contract element back to its source code location.

**Output table:**

| ID | Contract Element | Type | Source File | Line | Confidence |
|---|---|---|---|---|---|
| CE-001 | `IUserService` | Interface | `Services/IUserService.cs` | 12 | High |
| CE-002 | `UserDto` | DTO | `Models/UserDto.cs` | 1 | High |
| CE-003 | `user.created` | Event | `Events/UserEvents.cs` | 28 | Medium |

**Applicability:** Always applicable. Every seam has at least one traceable contract element.

**Key logic:**
- Iterates all `ContractElement` instances in the seam model.
- Resolves source locations using `SourceLocation` metadata.
- Assigns confidence levels based on detection method (explicit annotation = High, convention-based = Medium, inferred = Low).

### 4.14 `DiagramIndexSection` (Order 14)

**Responsibility:** Collects all diagrams generated across all sections and produces a consolidated index.

**Output table:**

| Diagram | Type | Section | File |
|---|---|---|---|
| System Context | C4 Context | Introduction | `diagrams/context.png` |
| Component Architecture | C4 Component | Interface Overview | `diagrams/component.png` |
| Auth Flow | Sequence | Protocols | `diagrams/auth-sequence.png` |

**Applicability:** Always applicable (there is always at least the context diagram from the Introduction section).

**Key logic:**
- Reads `GenerationContext.DiagramRegistry` which accumulates entries as sections run.
- Groups diagrams by type (C4, Sequence, State, Class, Activity).
- Generates cross-reference links back to the section where each diagram appears.

### 4.15 `RevisionHistorySection` (Order 15)

**Responsibility:** Tracks changes between the current generation run and the previous baseline ICD.

**Output table:**

| Version | Date | Change Description | Affected Sections |
|---|---|---|---|
| 2.0 | 2026-03-30 | Added `RefreshTokenAsync` to `IAuthService` | Injectable Services, Protocols |
| 1.0 | 2026-03-15 | Initial generation | All |

**Applicability:** Always applicable. First-time generation produces a single "Initial generation" row.

**Key logic:**
- Loads the previous baseline from `SeamQ.BaselineDiffing` module.
- Computes a structural diff between the previous and current `IcdDocument`.
- Groups changes by affected section.
- Auto-increments the version number.

---

## 5. Output Formatters

### 5.1 `IOutputFormatter` Interface

```csharp
namespace SeamQ.Generator.Formatting;

public interface IOutputFormatter
{
    OutputFormat Format { get; }
    string FileExtension { get; }
    Task<Stream> FormatAsync(
        IcdDocument document,
        FormatterOptions options,
        CancellationToken cancellationToken = default);
}
```

All formatters receive the same `IcdDocument` and produce a `Stream` containing the formatted output. The orchestrator writes the stream to disk.

### 5.2 `MarkdownFormatter`

**Output:** `.md` file with GitHub-Flavored Markdown.

**Features:**
- Table of contents with anchor links.
- GFM tables for all tabular data.
- Fenced code blocks with language annotations for syntax highlighting.
- Diagram references as `![Alt Text](diagrams/filename.png)` image links.
- Cross-reference links between sections using Markdown anchors.

**Template:** Uses the `icd-markdown` Handlebars template. Falls back to a built-in default template if no custom template is provided.

### 5.3 `HtmlFormatter`

**Output:** Self-contained `.html` file.

**Features:**
- **Navigation sidebar** -- collapsible section tree for quick navigation.
- **Anchor links** -- every heading and table row is addressable by URL fragment.
- **Syntax highlighting** -- code blocks are highlighted using a bundled Prism.js-compatible CSS theme.
- **Embedded diagrams** -- diagram PNGs are referenced as `<img>` tags; optionally inlined as base64 `data:` URIs.
- **Custom CSS** -- the `FormatterOptions.CustomCss` property allows injecting project-specific styles.
- **Responsive layout** -- the HTML template uses CSS Grid for a layout that works on screen and in print.

**Template:** Uses the `icd-html` Handlebars template.

### 5.4 `PdfFormatter`

**Output:** `.pdf` file.

**Implementation:** Delegates to `HtmlFormatter` to produce HTML, then renders it to PDF using **PuppeteerSharp** (headless Chromium).

**Features:**
- Letter-size page format by default (configurable via `PdfOptions`).
- Custom header and footer templates (page numbers, document title, generation date).
- Print-optimized CSS (page breaks before major sections, no sidebar).
- Table of contents with page numbers.
- Embedded diagrams at print resolution.

**Browser management:**
- On first use, `PdfFormatter` calls `EnsureBrowserAsync()` which uses PuppeteerSharp's `BrowserFetcher` to download a compatible Chromium revision if not already cached.
- The browser path is cached in `%LOCALAPPDATA%/SeamQ/chromium/`.
- In CI environments, the `SEAMQ_CHROMIUM_PATH` environment variable can override the browser location.

### Formatting Sequence

![Sequence Diagram - Format Output](diagrams/seq-format-output.png)

---

## 6. Template Engine

### 6.1 `TemplateEngine`

```csharp
namespace SeamQ.Generator.Templating;

public class TemplateEngine
{
    public void RegisterHelpers();
    public HandlebarsTemplate LoadTemplate(string path);
    public string RenderTemplate(string templateName, object context);
    public HandlebarsTemplate CompileInline(string template);
}
```

The `TemplateEngine` wraps **Handlebars.NET** and provides:

- **Template loading** -- loads `.hbs` files from disk and compiles them. Compiled templates are cached in a `ConcurrentDictionary` for reuse across multiple generation runs.
- **Partial registration** -- scans a `partials/` subdirectory and registers each `.hbs` file as a named partial.
- **Custom helpers** -- registers SeamQ-specific Handlebars helpers:

| Helper | Usage | Description |
|---|---|---|
| `{{table rows columns}}` | `{{table services "Name,Lifetime,Description"}}` | Renders a collection as a Markdown or HTML table |
| `{{codeblock content lang}}` | `{{codeblock signature "csharp"}}` | Wraps content in a fenced code block |
| `{{diagram ref}}` | `{{diagram contextDiagram}}` | Renders a diagram reference in the appropriate format |
| `{{anchor title}}` | `{{anchor "Injectable Services"}}` | Generates a section anchor link |
| `{{ifApplicable section}}` | `{{#ifApplicable protocols}}...{{/ifApplicable}}` | Conditionally renders a block if the section has content |
| `{{formatDate date format}}` | `{{formatDate generatedAt "yyyy-MM-dd"}}` | Formats a `DateTimeOffset` value |

- **Template override** -- when `GenerationOptions.TemplatePath` is set, the engine loads templates from that directory first, falling back to built-in templates for any missing files. This allows partial customization (e.g., override only the HTML template while keeping the default Markdown template).

---

## 7. Generation Pipeline

The end-to-end pipeline from seam data to output files is shown in the following sequence diagram.

![Sequence Diagram - Generate ICD](diagrams/seq-generate-icd.png)

### Pipeline Steps in Detail

1. **Entry** -- The caller (CLI command or programmatic API) invokes `IIcdGenerator.GenerateAsync(seam, options, ct)`.

2. **Validation** -- The orchestrator validates:
   - `SeamModel` is non-null and has a valid `SeamId`.
   - `GenerationOptions.OutputDirectory` exists or can be created.
   - At least one `OutputFormat` is requested.
   - Custom template path (if specified) exists and contains valid templates.

3. **Section iteration** -- Sections are sorted by `Order` and processed sequentially. Sequential processing is deliberate: later sections (e.g., `DiagramIndexSection`, `TraceabilityMatrixSection`) depend on results accumulated by earlier sections via the shared `GenerationContext`.

4. **Applicability check** -- Each section's `IsApplicable(seam)` is called. Non-applicable sections are logged at Debug level and skipped.

5. **Section generation** -- `GenerateAsync` is called on each applicable section. The section reads data from the `SeamModel`, generates content (prose, tables, code blocks), requests diagrams, and returns an `IcdSectionResult`.

6. **Content model assembly** -- All `IcdSectionResult` instances are merged into a `ContentModel`. Cross-references between sections are resolved (e.g., a data object mentioned in the Protocols section links to its definition in the Data Objects section).

7. **`IcdDocument` creation** -- The `ContentModel` is wrapped in an `IcdDocument` with metadata (seam ID, title, generation timestamp, version).

8. **Formatting** -- For each requested `OutputFormat`, the matching `IOutputFormatter` is invoked. Formatters run sequentially because `PdfFormatter` depends on `HtmlFormatter` output.

9. **File writing** -- Each formatter's output stream is written to the output directory. Diagram image files are copied alongside. A manifest file (`icd-manifest.json`) is written listing all generated files.

10. **Return** -- The `IcdDocument` is returned to the caller.

---

## 8. State Machine

The generation pipeline transitions through well-defined states. Error in any state transitions to `Failed`.

![State Diagram - Generation Pipeline](diagrams/state-generation.png)

### State Descriptions

| State | Entry Condition | Activities | Exit Condition |
|---|---|---|---|
| **Idle** | Default state | Waiting for `GenerateAsync` call | `GenerateAsync` invoked |
| **LoadingSeam** | `GenerateAsync` called | Validate seam, resolve options, load templates | Validation passes |
| **GeneratingSections** | Seam validated | Iterate sections, check applicability, generate content | All sections processed |
| **Formatting** | Sections complete | Run each requested formatter | All formats rendered |
| **WritingOutput** | Formatting complete | Write files, copy diagrams, generate manifest | All files written |
| **Complete** | Files written | Return `IcdDocument` | Terminal |
| **Failed** | Error in any state | Wrap error in `GenerationException` | Terminal |

---

## 9. Dependency Registration

All components are registered in the DI container via an extension method:

```csharp
namespace SeamQ.Generator;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIcdGeneration(
        this IServiceCollection services)
    {
        // Orchestrator
        services.AddSingleton<IIcdGenerator, IcdGenerator>();

        // Template engine
        services.AddSingleton<TemplateEngine>();

        // Section generators (order is controlled by the Order property)
        services.AddTransient<IIcdSection, IntroductionSection>();
        services.AddTransient<IIcdSection, ReferenceDocumentsSection>();
        services.AddTransient<IIcdSection, InterfaceOverviewSection>();
        services.AddTransient<IIcdSection, ScopeOfResponsibilitySection>();
        services.AddTransient<IIcdSection, StandardsConventionsSection>();
        services.AddTransient<IIcdSection, RegistrationContractSection>();
        services.AddTransient<IIcdSection, ComponentInputContractSection>();
        services.AddTransient<IIcdSection, InjectableServicesSection>();
        services.AddTransient<IIcdSection, UtilitiesSection>();
        services.AddTransient<IIcdSection, DataObjectsSection>();
        services.AddTransient<IIcdSection, LifecycleStateManagementSection>();
        services.AddTransient<IIcdSection, ProtocolsSection>();
        services.AddTransient<IIcdSection, TraceabilityMatrixSection>();
        services.AddTransient<IIcdSection, DiagramIndexSection>();
        services.AddTransient<IIcdSection, RevisionHistorySection>();

        // Output formatters
        services.AddSingleton<IOutputFormatter, MarkdownFormatter>();
        services.AddSingleton<IOutputFormatter, HtmlFormatter>();
        services.AddSingleton<IOutputFormatter, PdfFormatter>();

        return services;
    }
}
```

Section generators are registered as `Transient` so each generation run gets a fresh instance (avoiding stale state). Formatters and the orchestrator are `Singleton` because they are stateless.

---

## 10. Error Handling

### Exception Hierarchy

```
GenerationException (base)
├── SeamValidationException      -- invalid SeamModel
├── SectionGenerationException   -- failure in a specific section
│   └── SectionName: string      -- which section failed
├── FormattingException          -- failure during output formatting
│   └── Format: OutputFormat     -- which format failed
├── TemplateException            -- template loading or rendering failure
└── OutputWriteException         -- file system write failure
```

### Behavior

- **Section failure** -- by default, a failing section is logged as an error and skipped; the remaining sections continue. Set `GenerationOptions.FailFast = true` to abort on the first section failure.
- **Formatter failure** -- if one format fails (e.g., PDF because Chromium is not available), the other formats still complete. The exception is captured in `IcdDocument.Errors`.
- **Cancellation** -- all async methods respect the `CancellationToken`. Cancellation results in an `OperationCanceledException` propagated to the caller.

---

## 11. Configuration

The module reads configuration from the standard SeamQ configuration hierarchy (see [Configuration design](../06-configuration/README.md)).

```json
{
  "SeamQ": {
    "Generator": {
      "OutputFormats": ["Markdown", "Html"],
      "OutputDirectory": "./output/icd",
      "IncludeDiagrams": true,
      "DiagramFormat": "Png",
      "FailFast": false,
      "TemplatePath": null,
      "Pdf": {
        "PageSize": "Letter",
        "Landscape": false,
        "ChromiumPath": null
      }
    }
  }
}
```

| Key | Type | Default | Description |
|---|---|---|---|
| `OutputFormats` | `string[]` | `["Markdown"]` | Which output formats to produce |
| `OutputDirectory` | `string` | `"./output/icd"` | Where to write generated files |
| `IncludeDiagrams` | `bool` | `true` | Whether to generate and embed diagrams |
| `DiagramFormat` | `string` | `"Png"` | Diagram image format (Png, Svg) |
| `FailFast` | `bool` | `false` | Abort generation on first section failure |
| `TemplatePath` | `string?` | `null` | Custom Handlebars template directory |
| `Pdf.PageSize` | `string` | `"Letter"` | PDF page size |
| `Pdf.Landscape` | `bool` | `false` | PDF landscape orientation |
| `Pdf.ChromiumPath` | `string?` | `null` | Override Chromium browser path for PDF |

---

## 12. Diagram Index

All PlantUML diagrams for this design document are listed below. Source `.puml` files are in the `diagrams/` subdirectory.

| Diagram | Type | File | Description |
|---|---|---|---|
| C4 Component - Generator | C4 Component | [`c4-component-generator.puml`](diagrams/c4-component-generator.puml) | Internal components of SeamQ.Generator and external dependencies |
| Class - Generator Core | Class | [`class-generator.puml`](diagrams/class-generator.puml) | IcdGenerator, IIcdSection, and all 15 section implementations |
| Class - Formatters | Class | [`class-formatters.puml`](diagrams/class-formatters.puml) | IOutputFormatter hierarchy, TemplateEngine |
| Sequence - Generate ICD | Sequence | [`seq-generate-icd.puml`](diagrams/seq-generate-icd.puml) | End-to-end generation pipeline |
| Sequence - Format Output | Sequence | [`seq-format-output.puml`](diagrams/seq-format-output.puml) | Markdown, HTML, and PDF formatting flows |
| State - Generation Pipeline | State | [`state-generation.puml`](diagrams/state-generation.puml) | State machine for the generation lifecycle |
