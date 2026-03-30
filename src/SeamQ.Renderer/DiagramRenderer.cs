using Microsoft.Extensions.Logging;
using SeamQ.Core.Abstractions;
using SeamQ.Core.Models;
using SeamQ.Renderer.C4;
using SeamQ.Renderer.PlantUml.ClassDiagrams;
using SeamQ.Renderer.PlantUml.SequenceDiagrams;

namespace SeamQ.Renderer;

/// <summary>
/// Implements IDiagramRenderer by determining applicable diagram types for a seam,
/// generating PlantUML source for each, and writing .puml files to the output directory.
/// </summary>
public sealed class DiagramRenderer : IDiagramRenderer
{
    private readonly ILogger<DiagramRenderer> _logger;

    public DiagramRenderer(ILogger<DiagramRenderer> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<string>> RenderAsync(Seam seam, string outputDirectory, CancellationToken cancellationToken = default)
    {
        return RenderAsync(seam, outputDirectory, typeFilter: null, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> RenderAsync(Seam seam, string outputDirectory, string? typeFilter, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rendering diagrams for seam '{SeamName}' to '{OutputDir}'", seam.Name, outputDirectory);

        Directory.CreateDirectory(outputDirectory);

        var generatedFiles = new List<string>();
        var applicableDiagrams = DetermineApplicableDiagrams(seam);

        // Apply type filter if specified
        if (!string.IsNullOrEmpty(typeFilter))
        {
            applicableDiagrams = FilterByType(applicableDiagrams, typeFilter);
        }

        foreach (var diagramType in applicableDiagrams)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var content = GenerateDiagramContent(seam, diagramType);
            if (string.IsNullOrEmpty(content))
            {
                continue;
            }

            var fileName = $"{SanitizeFileName(seam.Name)}_{diagramType}.puml";
            var filePath = Path.Combine(outputDirectory, fileName);

            await File.WriteAllTextAsync(filePath, content, cancellationToken);
            generatedFiles.Add(filePath);

            _logger.LogDebug("Generated diagram: {FilePath}", filePath);
        }

        _logger.LogInformation("Generated {Count} diagram(s) for seam '{SeamName}'", generatedFiles.Count, seam.Name);
        return generatedFiles;
    }

    private static IReadOnlyList<DiagramType> DetermineApplicableDiagrams(Seam seam)
    {
        var diagrams = new List<DiagramType>();
        var surface = seam.ContractSurface;
        var hasElements = surface.Elements.Count > 0;
        var hasConsumers = seam.Consumers.Count > 0;

        // Helper: check if any element name matches a pattern (for heuristic detection
        // when scanner classifies everything as Type)
        bool AnyElementNameContains(string pattern) =>
            surface.Elements.Any(e => e.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase));

        // === Class Diagrams ===

        // API surface: when there are interfaces, abstract classes, or types
        if (hasElements)
        {
            diagrams.Add(DiagramType.ClassApiSurface);
        }

        // Frontend services: when there are methods with parent names, or types named *Service
        if (surface.Methods.Any(m => m.ParentName is not null) ||
            AnyElementNameContains("Service"))
        {
            diagrams.Add(DiagramType.ClassFrontendServices);
        }

        // Domain data objects: when there are types or interfaces with child properties
        if (surface.Types.Any() || surface.Elements.Any(e => e.Kind == ContractElementKind.Property))
        {
            diagrams.Add(DiagramType.ClassDomainDataObjects);
        }

        // Registration system: when there are injection tokens or abstract classes
        if (surface.InjectionTokens.Any() || surface.AbstractClasses.Any())
        {
            diagrams.Add(DiagramType.ClassRegistrationSystem);
        }

        // Message interfaces: when there are actions, observables, selectors, or message-like types
        if (surface.Actions.Any() || surface.Observables.Any() || surface.Selectors.Any() ||
            AnyElementNameContains("Message") || AnyElementNameContains("Action"))
        {
            diagrams.Add(DiagramType.ClassMessageInterfaces);
        }

        // Realtime communication: when there are observables or input/output bindings
        if (surface.Observables.Any() || (surface.InputBindings.Any() && surface.OutputBindings.Any()))
        {
            diagrams.Add(DiagramType.ClassRealtimeCommunication);
        }

        // === Sequence Diagrams ===

        // App startup: when there are tokens, interfaces, or service types to register
        if (surface.InjectionTokens.Any() || surface.Interfaces.Any() ||
            AnyElementNameContains("Service"))
        {
            diagrams.Add(DiagramType.SeqAppStartup);
        }

        // Plugin lifecycle: for plugin contracts with consumers
        if (seam.Type == SeamType.PluginContract && hasConsumers)
        {
            diagrams.Add(DiagramType.SeqPluginLifecycle);
        }

        // Request flow: for HTTP API contracts or when there are request-like elements
        if (seam.Type == SeamType.HttpApiContract ||
            surface.Methods.Any(m => m.ParentName?.Contains("Request", StringComparison.OrdinalIgnoreCase) == true ||
                                     m.Name.Contains("request", StringComparison.OrdinalIgnoreCase) ||
                                     m.Name.Contains("send", StringComparison.OrdinalIgnoreCase)) ||
            AnyElementNameContains("Request"))
        {
            diagrams.Add(DiagramType.SeqRequestFlow);
        }

        // Query flow: when there are query-like elements
        if (surface.Methods.Any(m => m.ParentName?.Contains("Query", StringComparison.OrdinalIgnoreCase) == true ||
                                     m.Name.Contains("query", StringComparison.OrdinalIgnoreCase) ||
                                     m.Name.Contains("get", StringComparison.OrdinalIgnoreCase) ||
                                     m.Name.Contains("fetch", StringComparison.OrdinalIgnoreCase)) ||
            AnyElementNameContains("Query"))
        {
            diagrams.Add(DiagramType.SeqQueryFlow);
        }

        // Command flow: when there are command-like elements
        if (surface.Methods.Any(m => m.ParentName?.Contains("Command", StringComparison.OrdinalIgnoreCase) == true ||
                                     m.Name.Contains("command", StringComparison.OrdinalIgnoreCase) ||
                                     m.Name.Contains("execute", StringComparison.OrdinalIgnoreCase)) ||
            AnyElementNameContains("Command"))
        {
            diagrams.Add(DiagramType.SeqCommandFlow);
        }

        // Data consumption: when there are observables or data-oriented types
        if (surface.Observables.Any() || AnyElementNameContains("Telemetry"))
        {
            diagrams.Add(DiagramType.SeqDataConsumption);
        }

        // Error handling: when there are methods or error-related types
        if (surface.Methods.Any() || AnyElementNameContains("Error") || AnyElementNameContains("Response"))
        {
            diagrams.Add(DiagramType.SeqErrorHandling);
        }

        // === C4 Diagrams ===

        // System context: when there are consumers
        if (hasConsumers)
        {
            diagrams.Add(DiagramType.C4SystemContext);
        }

        // Container: when there are consumers
        if (hasConsumers)
        {
            diagrams.Add(DiagramType.C4Container);
        }

        // Component (services): when there are methods, interfaces, tokens, or service-like types
        if (surface.Methods.Any() || surface.Interfaces.Any() || surface.InjectionTokens.Any() ||
            AnyElementNameContains("Service") || AnyElementNameContains("Component"))
        {
            diagrams.Add(DiagramType.C4ComponentServices);
        }

        // Plugin API layers: when there are tokens plus interfaces, or complex contract surfaces
        if ((surface.InjectionTokens.Any() && surface.Interfaces.Any()) ||
            (surface.Elements.Count >= 10 && AnyElementNameContains("Service")))
        {
            diagrams.Add(DiagramType.C4PluginApiLayers);
        }

        // Dynamic: when there are meaningful interactions to show
        if (hasElements && (hasConsumers || surface.Methods.Any() || AnyElementNameContains("Service")))
        {
            diagrams.Add(DiagramType.C4Dynamic);
        }

        // Data flow: when there are observables, actions, or data-oriented types
        if (surface.Observables.Any() || surface.Actions.Any() ||
            (AnyElementNameContains("Message") && AnyElementNameContains("Response")))
        {
            diagrams.Add(DiagramType.C4DataFlow);
        }

        return diagrams;
    }

    private static IReadOnlyList<DiagramType> FilterByType(IReadOnlyList<DiagramType> diagrams, string typeFilter)
    {
        return typeFilter.ToLowerInvariant() switch
        {
            "class" => diagrams.Where(d => d.ToString().StartsWith("Class")).ToList(),
            "sequence" => diagrams.Where(d => d.ToString().StartsWith("Seq")).ToList(),
            "state" => diagrams.Where(d => d.ToString().StartsWith("State")).ToList(),
            "context" or "c4-context" => diagrams.Where(d => d is DiagramType.C4SystemContext or DiagramType.C4ContextWithinArchitecture).ToList(),
            "c4-container" => diagrams.Where(d => d is DiagramType.C4Container or DiagramType.C4DataFlow).ToList(),
            "c4-component" => diagrams.Where(d => d is DiagramType.C4ComponentServices or DiagramType.C4ComponentBackend or DiagramType.C4PluginApiLayers or DiagramType.C4PluginArchitecture).ToList(),
            "c4-code" => diagrams.Where(d => d is DiagramType.C4Dynamic or DiagramType.C4Deployment or DiagramType.C4SubscriptionChannelMap or DiagramType.C4ProtocolStack).ToList(),
            _ => diagrams // Unknown filter: return all
        };
    }

    private static string? GenerateDiagramContent(Seam seam, DiagramType diagramType)
    {
        return diagramType switch
        {
            // Class diagrams
            DiagramType.ClassApiSurface => ApiSurfaceClassDiagram.Generate(seam),
            DiagramType.ClassFrontendServices => FrontendServicesClassDiagram.Generate(seam),
            DiagramType.ClassDomainDataObjects => DomainDataObjectsClassDiagram.Generate(seam),
            DiagramType.ClassRegistrationSystem => RegistrationSystemClassDiagram.Generate(seam),
            DiagramType.ClassMessageInterfaces => MessageInterfacesClassDiagram.Generate(seam),
            DiagramType.ClassRealtimeCommunication => RealtimeCommunicationClassDiagram.Generate(seam),

            // Sequence diagrams
            DiagramType.SeqAppStartup => AppStartupSequence.Generate(seam),
            DiagramType.SeqPluginLifecycle => PluginLifecycleSequence.Generate(seam),
            DiagramType.SeqRequestFlow => RequestFlowSequence.Generate(seam),
            DiagramType.SeqQueryFlow => QueryFlowSequence.Generate(seam),
            DiagramType.SeqCommandFlow => CommandFlowSequence.Generate(seam),
            DiagramType.SeqDataConsumption => DataConsumptionSequence.Generate(seam),
            DiagramType.SeqErrorHandling => ErrorHandlingSequence.Generate(seam),

            // C4 diagrams
            DiagramType.C4SystemContext => C4SystemContext.Generate(seam),
            DiagramType.C4Container => C4Container.Generate(seam),
            DiagramType.C4ComponentServices => C4ComponentServices.Generate(seam),
            DiagramType.C4PluginApiLayers => C4PluginApiLayers.Generate(seam),
            DiagramType.C4Dynamic => C4Dynamic.Generate(seam),
            DiagramType.C4DataFlow => C4DataFlow.Generate(seam),

            _ => null
        };
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return sanitized.Replace(' ', '_');
    }
}
