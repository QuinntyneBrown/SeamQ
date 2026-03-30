# SeamQ — Solution Folder Hierarchy

## Overview

SeamQ follows a modular .NET solution structure with clear separation between the CLI entry point, core domain models, feature-specific libraries, and tests. The project uses `System.CommandLine` with a file-per-command pattern and `Microsoft.Extensions` for dependency injection, logging, and configuration.

---

## Solution Root

```
SeamQ/
├── SeamQ.sln                              # Visual Studio solution file
├── Directory.Build.props                   # Shared MSBuild properties (LangVersion, Nullable, ImplicitUsings)
├── Directory.Packages.props                # Central Package Management (NuGet versions)
├── .editorconfig                           # Code style rules
├── global.json                             # .NET SDK version pinning
├── nuget.config                            # NuGet feed configuration
├── seamq.config.json                       # Example/default configuration
├── .gitignore                              # Git ignore rules
├── README.md                               # Project README
├── LICENSE                                 # License file
│
├── src/                                    # Source projects
│   ├── SeamQ.Cli/                          # CLI entry point (dotnet tool)
│   ├── SeamQ.Core/                         # Core domain models and abstractions
│   ├── SeamQ.Scanner/                      # Workspace discovery and TypeScript parsing
│   ├── SeamQ.Detector/                     # Seam detection engine
│   ├── SeamQ.Generator/                    # ICD document generation
│   ├── SeamQ.Renderer/                     # Diagram generation (PlantUML + C4)
│   ├── SeamQ.Differ/                       # Baseline comparison
│   └── SeamQ.Validator/                    # Contract compliance checking
│
├── test/                                   # Test projects
│   ├── SeamQ.Tests.Unit/                   # Unit tests
│   ├── SeamQ.Tests.Integration/            # Integration tests (Scanner→Detector→Generator pipeline)
│   ├── SeamQ.Tests.E2E/                    # End-to-end CLI tests (System.CommandLine.Testing)
│   └── fixtures/                           # Angular workspace test fixtures
│       ├── dashboard-framework/            # Framework with tile plugin contract
│       ├── weather-tile-plugin/            # Plugin implementing tile contract
│       ├── alerts-tile-plugin/             # Plugin with intentional violations
│       ├── shared-ui-lib/                  # Shared library workspace
│       └── nx-monorepo/                    # Single Nx workspace with libraries
│
├── templates/                              # ICD document templates (Handlebars)
│   ├── default.md.hbs                      # Default Markdown template
│   ├── default.html.hbs                    # Default HTML template
│   └── mil-std-498.md.hbs                  # MIL-STD-498 compliant template
│
├── themes/                                 # PlantUML themes
│   ├── seamq-default.puml                  # Default SeamQ theme
│   └── seamq-dark.puml                     # Dark theme variant
│
└── docs/                                   # Documentation
    ├── specs/                              # Requirements specifications
    │   ├── L1.md                           # High-level requirements
    │   └── L2.md                           # Detailed requirements with acceptance criteria
    ├── detailed-designs/                   # Feature-level detailed designs
    │   ├── 01-workspace-scanning/
    │   ├── 02-seam-detection/
    │   ├── 03-icd-generation/
    │   ├── 04-diagram-generation/
    │   ├── 05-cli-interface/
    │   ├── 06-configuration/
    │   ├── 07-baseline-diffing/
    │   ├── 08-contract-validation/
    │   ├── 09-data-export/
    │   └── 10-core-models/
    ├── ui-design.pen                       # CLI UI screen designs (Pencil)
    └── solution-structure.md               # This document
```

---

## Source Projects

### SeamQ.Cli (Console Application — .NET Tool)

The CLI entry point, packaged as a `dotnet tool` via NuGet. Uses `System.CommandLine` with one file per command.

```
src/SeamQ.Cli/
├── SeamQ.Cli.csproj                        # PackAsTool=true, ToolCommandName=seamq
├── Program.cs                              # Entry point: build host, configure DI, build root command
├── HostBuilderExtensions.cs                # IHostBuilder extension methods for SeamQ services
├── GlobalOptions.cs                        # --verbose, --quiet, --no-color, --output-dir, --config
├── Commands/                               # One file per CLI command
│   ├── ScanCommand.cs                      # seamq scan <paths> [--save-baseline]
│   ├── ListCommand.cs                      # seamq list [--type] [--provider] [--confidence]
│   ├── GenerateCommand.cs                  # seamq generate <seam-id|--all> [--format]
│   ├── DiagramCommand.cs                   # seamq diagram <seam-id|--all> [--type]
│   ├── InspectCommand.cs                   # seamq inspect <seam-id>
│   ├── ValidateCommand.cs                  # seamq validate <seam-id|--all>
│   ├── DiffCommand.cs                      # seamq diff <baseline-path>
│   ├── InitCommand.cs                      # seamq init (interactive config wizard)
│   ├── ExportCommand.cs                    # seamq export <seam-id|--all> --format json
│   └── ServeCommand.cs                     # seamq serve [--port]
├── Rendering/                              # CLI output formatting
│   ├── IConsoleRenderer.cs                 # Abstraction for formatted output
│   ├── ConsoleRenderer.cs                  # ANSI-colored terminal output
│   ├── TableRenderer.cs                    # Table formatting (seamq list output)
│   └── ProgressRenderer.cs                 # Progress/spinner display
└── Properties/
    └── launchSettings.json                 # Debug launch profiles
```

### SeamQ.Core (Class Library)

Core domain models and shared abstractions. No external dependencies beyond .NET BCL.

```
src/SeamQ.Core/
├── SeamQ.Core.csproj
├── Models/                                 # Domain models
│   ├── Workspace.cs                        # Scanned Angular workspace
│   ├── WorkspaceType.cs                    # Enum: AngularCli, NxMonorepo, Standalone
│   ├── Project.cs                          # Angular project within a workspace
│   ├── ProjectType.cs                      # Enum: Application, Library
│   ├── Seam.cs                             # Detected interface boundary
│   ├── SeamType.cs                         # Enum: PluginContract, SharedLibrary, etc.
│   ├── ContractSurface.cs                  # Collection of contract elements at a seam
│   ├── ContractElement.cs                  # Base contract element (name, kind, source)
│   ├── ContractElementKind.cs              # Enum: Interface, InjectionToken, etc.
│   ├── DataDictionaryEntry.cs              # Type crossing a seam boundary
│   ├── TraceabilityEntry.cs                # Source-to-requirement mapping
│   ├── DiagramSpec.cs                      # Diagram generation specification
│   └── ExitCodes.cs                        # Constants: Success=0, PartialFailure=1, Fatal=2
├── Configuration/                          # Configuration models
│   ├── SeamQConfig.cs                      # Root configuration
│   ├── WorkspaceConfig.cs                  # Workspace definition
│   ├── SeamFilterConfig.cs                 # Seam type filtering
│   ├── OutputConfig.cs                     # Output settings
│   ├── DiagramConfig.cs                    # Diagram rendering settings
│   ├── AnalysisConfig.cs                   # Analysis options
│   └── IcdMetadataConfig.cs                # ICD document metadata
├── Abstractions/                           # Shared interfaces
│   ├── IWorkspaceScanner.cs
│   ├── ISeamDetector.cs
│   ├── IIcdGenerator.cs
│   ├── IDiagramRenderer.cs
│   ├── ISeamDiffer.cs
│   ├── IContractValidator.cs
│   └── IDataExporter.cs
└── Extensions/                             # Utility extensions
    ├── StringExtensions.cs
    └── PathExtensions.cs
```

### SeamQ.Scanner (Class Library)

Workspace discovery and TypeScript AST parsing.

```
src/SeamQ.Scanner/
├── SeamQ.Scanner.csproj
├── DependencyInjection/
│   └── ScannerServiceCollectionExtensions.cs  # DI registration
├── WorkspaceScanner.cs                     # Orchestrates scanning of one workspace
├── Parsing/                                # Workspace configuration parsers
│   ├── IWorkspaceParser.cs                 # Common parser interface
│   ├── AngularWorkspaceParser.cs           # Parses angular.json
│   ├── NxWorkspaceParser.cs                # Parses nx.json, project.json
│   └── WorkspaceTypeDetector.cs            # Auto-detects workspace type
├── TypeScript/                             # TypeScript source parsing
│   ├── TsConfigResolver.cs                 # Resolves path aliases, project references
│   ├── BarrelExportParser.cs               # Parses public-api.ts, index.ts
│   ├── TypeScriptAstParser.cs              # Parses .ts files into models
│   ├── AngularMetadataExtractor.cs         # Extracts @Component, @Injectable, etc.
│   ├── TsDocParser.cs                      # Extracts TSDoc/JSDoc comments
│   └── Models/                             # Parsed AST models
│       ├── ParsedFile.cs
│       ├── ParsedInterface.cs
│       ├── ParsedClass.cs
│       ├── ParsedService.cs
│       ├── ParsedComponent.cs
│       ├── ParsedEnum.cs
│       ├── ParsedTypeAlias.cs
│       ├── ParsedMethod.cs
│       ├── ParsedProperty.cs
│       └── ParsedDecorator.cs
├── Caching/
│   ├── AstCache.cs                         # File-hash-based AST cache
│   └── FileHasher.cs                       # SHA-256 file hashing
└── Exclusion/
    ├── PathExcluder.cs                     # Applies .seamqignore and --exclude patterns
    └── SeamqIgnoreParser.cs                # Parses .seamqignore file
```

### SeamQ.Detector (Class Library)

Seam detection engine using strategy pattern.

```
src/SeamQ.Detector/
├── SeamQ.Detector.csproj
├── DependencyInjection/
│   └── DetectorServiceCollectionExtensions.cs
├── SeamDetector.cs                         # Orchestrates all detection strategies
├── SeamRegistry.cs                         # Stores and queries detected seams
├── ConfidenceScorer.cs                     # Calculates confidence scores
├── Strategies/                             # Detection strategies
│   ├── ISeamDetectionStrategy.cs           # Strategy interface
│   ├── PluginContractStrategy.cs           # Plugin contracts (interfaces, tokens, forRoot)
│   ├── SharedLibraryStrategy.cs            # Shared library exports/imports
│   ├── MessageBusStrategy.cs               # RxJS Subject, SignalR hubs, event bus
│   ├── RouteContractStrategy.cs            # Route configs, loadChildren, guards
│   ├── StateContractStrategy.cs            # NgRx, signal stores, shared state
│   ├── HttpApiContractStrategy.cs          # HTTP client services, DTOs
│   └── CustomDecoratorStrategy.cs          # User-configured decorators
└── Analysis/                               # Cross-workspace analysis
    ├── CrossWorkspaceResolver.cs           # Matches exports to imports across workspaces
    ├── TypeMatcher.cs                      # Matches interface implementations
    └── TokenTracer.cs                      # Traces injection token usage
```

### SeamQ.Generator (Class Library)

ICD document generation.

```
src/SeamQ.Generator/
├── SeamQ.Generator.csproj
├── DependencyInjection/
│   └── GeneratorServiceCollectionExtensions.cs
├── IcdGenerator.cs                         # Orchestrates section generation + formatting
├── Sections/                               # One class per ICD section
│   ├── IIcdSection.cs                      # Section interface
│   ├── IntroductionSection.cs
│   ├── ReferenceDocumentsSection.cs
│   ├── InterfaceOverviewSection.cs
│   ├── ScopeOfResponsibilitySection.cs
│   ├── StandardsConventionsSection.cs
│   ├── RegistrationContractSection.cs
│   ├── ComponentInputContractSection.cs
│   ├── InjectableServicesSection.cs
│   ├── UtilitiesSection.cs
│   ├── DataObjectsSection.cs
│   ├── LifecycleStateManagementSection.cs
│   ├── DataConsumptionPatternsSection.cs
│   ├── TimingConstraintsSection.cs
│   ├── ProtocolsSection.cs
│   ├── DefinitionsAcronymsSection.cs
│   ├── TbdItemsSection.cs
│   ├── TraceabilityMatrixSection.cs
│   ├── ChecklistTraceSection.cs
│   ├── DiagramIndexSection.cs
│   └── RevisionHistorySection.cs
├── Formatters/                             # Output format renderers
│   ├── IOutputFormatter.cs
│   ├── MarkdownFormatter.cs
│   ├── HtmlFormatter.cs
│   ├── PdfFormatter.cs
│   └── DocxFormatter.cs
├── Templates/
│   └── TemplateEngine.cs                   # Handlebars template support
└── Models/
    ├── IcdDocument.cs                      # Complete ICD document model
    ├── IcdSection.cs                       # Section content model
    └── IcdTable.cs                         # Table content model
```

### SeamQ.Renderer (Class Library)

Diagram generation — PlantUML and C4.

```
src/SeamQ.Renderer/
├── SeamQ.Renderer.csproj
├── DependencyInjection/
│   └── RendererServiceCollectionExtensions.cs
├── DiagramRenderer.cs                      # Orchestrates diagram generation for a seam
├── PlantUml/                               # PlantUML diagram generators
│   ├── IPlantUmlDiagram.cs                 # Diagram generator interface
│   ├── PlantUmlEncoder.cs                  # Generates valid PlantUML syntax
│   ├── ClassDiagrams/                      # 12 class diagram generators
│   │   ├── ApiSurfaceClassDiagram.cs
│   │   ├── BackendContractsClassDiagram.cs
│   │   ├── BackendControllersClassDiagram.cs
│   │   ├── DatastoreSchemaClassDiagram.cs
│   │   ├── DomainDataObjectsClassDiagram.cs
│   │   ├── FrontendServicesClassDiagram.cs
│   │   ├── MessageInterfacesClassDiagram.cs
│   │   ├── RealtimeCommunicationClassDiagram.cs
│   │   ├── TelemetryModelsClassDiagram.cs
│   │   ├── TelemetryServiceClassDiagram.cs
│   │   ├── RegistrationSystemClassDiagram.cs
│   │   └── FileStorageClassDiagram.cs
│   ├── SequenceDiagrams/                   # 15 sequence diagram generators
│   │   ├── AppStartupSequence.cs
│   │   ├── PluginLifecycleSequence.cs
│   │   ├── DataConsumptionSequence.cs
│   │   ├── TileAddSubscribeSequence.cs
│   │   ├── TileRemoveUnsubscribeSequence.cs
│   │   ├── RequestFlowSequence.cs
│   │   ├── QueryFlowSequence.cs
│   │   ├── CommandFlowSequence.cs
│   │   ├── CommandResponseUiSequence.cs
│   │   ├── ConfigurationCrudSequence.cs
│   │   ├── AdvisoryMessageSequence.cs
│   │   ├── TelemetrySubscribeSequence.cs
│   │   ├── ErrorHandlingSequence.cs
│   │   ├── MessageBusRoutingSequence.cs
│   │   └── ReviewTelemetrySequence.cs
│   └── StateDiagrams/                      # 2 state diagram generators
│       ├── DatastoreStateDiagram.cs
│       └── SubscriptionLifecycleStateDiagram.cs
├── C4/                                     # C4 architecture diagram generators
│   ├── C4SystemContext.cs
│   ├── C4ContextWithinArchitecture.cs
│   ├── C4Container.cs
│   ├── C4ComponentServices.cs
│   ├── C4ComponentBackend.cs
│   ├── C4PluginApiLayers.cs
│   ├── C4PluginArchitecture.cs
│   ├── C4DataFlow.cs
│   ├── C4SubscriptionChannelMap.cs
│   ├── C4ProtocolStack.cs
│   ├── C4Dynamic.cs
│   └── C4Deployment.cs
└── Rendering/
    ├── ImageRenderer.cs                    # Renders .puml → SVG/PNG
    └── PlantUmlServerClient.cs             # Communicates with PlantUML server
```

### SeamQ.Differ (Class Library)

Baseline comparison.

```
src/SeamQ.Differ/
├── SeamQ.Differ.csproj
├── DependencyInjection/
│   └── DifferServiceCollectionExtensions.cs
├── SeamDiffer.cs                           # Compares current scan vs. baseline
├── ChangeClassifier.cs                     # Classifies changes: Added, Modified, Removed
├── BaselineSerializer.cs                   # Serializes/deserializes baseline JSON
├── ContractSurfaceComparer.cs              # Deep comparison of contract surfaces
└── Models/
    ├── DiffReport.cs                       # Per-seam change lists + summary
    ├── SeamChange.cs                       # Single change description
    └── ChangeType.cs                       # Enum: Added, Modified, Removed
```

### SeamQ.Validator (Class Library)

Contract compliance checking.

```
src/SeamQ.Validator/
├── SeamQ.Validator.csproj
├── DependencyInjection/
│   └── ValidatorServiceCollectionExtensions.cs
├── ContractValidator.cs                    # Orchestrates validation rules
├── Rules/                                  # Validation rules
│   ├── IValidationRule.cs
│   ├── InterfaceImplementationRule.cs
│   ├── InjectionTokenRule.cs
│   └── InputOutputRule.cs
└── Models/
    ├── ValidationReport.cs                 # Aggregated validation results
    ├── ValidationResult.cs                 # Per-consumer results
    └── ValidationSeverity.cs               # Enum: Error, Warning, Info
```

---

## Test Projects

### SeamQ.Tests.Unit

```
test/SeamQ.Tests.Unit/
├── SeamQ.Tests.Unit.csproj                 # References: xUnit, FluentAssertions, NSubstitute
├── Scanner/
│   ├── AngularWorkspaceParserTests.cs
│   ├── NxWorkspaceParserTests.cs
│   ├── TsConfigResolverTests.cs
│   ├── BarrelExportParserTests.cs
│   ├── TypeScriptAstParserTests.cs
│   ├── AngularMetadataExtractorTests.cs
│   ├── TsDocParserTests.cs
│   └── AstCacheTests.cs
├── Detector/
│   ├── SeamDetectorTests.cs
│   ├── PluginContractStrategyTests.cs
│   ├── SharedLibraryStrategyTests.cs
│   ├── MessageBusStrategyTests.cs
│   ├── RouteContractStrategyTests.cs
│   ├── StateContractStrategyTests.cs
│   ├── HttpApiContractStrategyTests.cs
│   └── ConfidenceScorerTests.cs
├── Generator/
│   ├── IcdGeneratorTests.cs
│   ├── MarkdownFormatterTests.cs
│   ├── HtmlFormatterTests.cs
│   └── Sections/                           # Per-section tests
│       └── ...
├── Renderer/
│   ├── DiagramRendererTests.cs
│   ├── PlantUmlEncoderTests.cs
│   └── ClassDiagrams/
│       └── ...
├── Differ/
│   ├── SeamDifferTests.cs
│   ├── ChangeClassifierTests.cs
│   └── BaselineSerializerTests.cs
└── Validator/
    ├── ContractValidatorTests.cs
    ├── InterfaceImplementationRuleTests.cs
    ├── InjectionTokenRuleTests.cs
    └── InputOutputRuleTests.cs
```

### SeamQ.Tests.Integration

```
test/SeamQ.Tests.Integration/
├── SeamQ.Tests.Integration.csproj          # References: xUnit, Verify
├── ScanDetectPipelineTests.cs              # Scanner → Detector pipeline
├── DetectGeneratePipelineTests.cs          # Detector → Generator pipeline
├── FullPipelineTests.cs                    # Scan → Detect → Generate → Render
└── SnapshotTests/                          # Verify snapshot tests
    ├── IcdOutputSnapshotTests.cs
    └── DiagramOutputSnapshotTests.cs
```

### SeamQ.Tests.E2E

```
test/SeamQ.Tests.E2E/
├── SeamQ.Tests.E2E.csproj                  # References: xUnit, System.CommandLine.Testing
├── Infrastructure/                         # Test infrastructure (Page Object Model pattern)
│   ├── SeamQCliDriver.cs                   # CLI test driver (wraps System.CommandLine.Testing)
│   ├── CliResult.cs                        # Parsed CLI output (exit code, stdout, stderr)
│   ├── Pages/                              # Page Objects for each command output
│   │   ├── HelpPage.cs
│   │   ├── ScanResultPage.cs
│   │   ├── ListResultPage.cs
│   │   ├── InspectResultPage.cs
│   │   ├── GenerateResultPage.cs
│   │   ├── DiagramResultPage.cs
│   │   ├── ValidateResultPage.cs
│   │   ├── DiffResultPage.cs
│   │   └── ErrorPage.cs
│   └── Fixtures/
│       └── FixtureManager.cs              # Manages test fixture workspaces
├── ScanCommandTests.cs
├── ListCommandTests.cs
├── GenerateCommandTests.cs
├── DiagramCommandTests.cs
├── InspectCommandTests.cs
├── ValidateCommandTests.cs
├── DiffCommandTests.cs
├── InitCommandTests.cs
├── ExportCommandTests.cs
├── ServeCommandTests.cs
├── GlobalOptionsTests.cs
└── ErrorHandlingTests.cs
```

### Test Fixtures

```
test/fixtures/
├── dashboard-framework/                    # Angular CLI workspace — framework role
│   ├── angular.json
│   ├── tsconfig.json
│   ├── tsconfig.base.json
│   └── libs/
│       └── tile-api/
│           ├── ng-package.json
│           ├── src/
│           │   ├── public-api.ts
│           │   └── lib/
│           │       ├── tile.interface.ts
│           │       ├── tile-config.interface.ts
│           │       ├── tile-data-provider.interface.ts
│           │       ├── tile.tokens.ts
│           │       ├── base-tile.component.ts
│           │       ├── tile-size.enum.ts
│           │       └── tile-lifecycle.enum.ts
│           └── tsconfig.lib.json
│
├── weather-tile-plugin/                    # Angular CLI workspace — plugin role
│   ├── angular.json
│   ├── tsconfig.json
│   └── src/
│       └── app/
│           ├── weather-tile.component.ts
│           └── weather-data.service.ts
│
├── alerts-tile-plugin/                     # Plugin with intentional violations
│   ├── angular.json
│   ├── tsconfig.json
│   └── src/
│       └── app/
│           └── alerts-tile.component.ts    # Missing onTileDestroy()
│
├── shared-ui-lib/                          # Shared library workspace
│   ├── angular.json
│   ├── tsconfig.json
│   └── libs/
│       ├── components/
│       │   └── src/public-api.ts
│       └── design-tokens/
│           └── src/public-api.ts
│
└── nx-monorepo/                            # Nx monorepo workspace
    ├── nx.json
    ├── workspace.json
    ├── tsconfig.base.json
    └── libs/
        ├── shared-models/
        │   ├── project.json
        │   └── src/index.ts
        └── shared-services/
            ├── project.json
            └── src/index.ts
```

---

## Key Configuration Files

### Directory.Build.props

Shared across all projects:
- `<TargetFramework>net8.0</TargetFramework>`
- `<LangVersion>latest</LangVersion>`
- `<Nullable>enable</Nullable>`
- `<ImplicitUsings>enable</ImplicitUsings>`
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`

### SeamQ.Cli.csproj (Tool Packaging)

- `<PackAsTool>true</PackAsTool>`
- `<ToolCommandName>seamq</ToolCommandName>`
- `<PackageId>SeamQ</PackageId>`
- `<RollForward>Major</RollForward>`

### NuGet Dependencies by Project

| Project | Key Dependencies |
|---------|-----------------|
| SeamQ.Cli | System.CommandLine, Microsoft.Extensions.Hosting, Microsoft.Extensions.DependencyInjection |
| SeamQ.Core | (none — BCL only) |
| SeamQ.Scanner | System.Text.Json, Microsoft.Extensions.Logging.Abstractions |
| SeamQ.Detector | Microsoft.Extensions.Logging.Abstractions |
| SeamQ.Generator | Markdig, Stubble.Core (Handlebars) |
| SeamQ.Renderer | System.Diagnostics.Process (PlantUML invocation) |
| SeamQ.Differ | System.Text.Json |
| SeamQ.Validator | Microsoft.Extensions.Logging.Abstractions |
| Tests | xUnit, FluentAssertions, NSubstitute, Verify.Xunit, System.CommandLine.Testing |
