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

    public async Task<IReadOnlyList<string>> RenderAsync(Seam seam, string outputDirectory, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rendering diagrams for seam '{SeamName}' to '{OutputDir}'", seam.Name, outputDirectory);

        Directory.CreateDirectory(outputDirectory);

        var generatedFiles = new List<string>();
        var applicableDiagrams = DetermineApplicableDiagrams(seam);

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

        // API surface class diagram is applicable when there are interfaces, abstract classes, or types
        if (seam.ContractSurface.Elements.Count > 0)
        {
            diagrams.Add(DiagramType.ClassApiSurface);
        }

        // Plugin lifecycle sequence diagram is applicable for plugin contracts
        if (seam.Type == SeamType.PluginContract && seam.Consumers.Count > 0)
        {
            diagrams.Add(DiagramType.SeqPluginLifecycle);
        }

        // C4 System Context is always applicable when there are consumers
        if (seam.Consumers.Count > 0)
        {
            diagrams.Add(DiagramType.C4SystemContext);
            diagrams.Add(DiagramType.C4Container);
        }

        return diagrams;
    }

    private static string? GenerateDiagramContent(Seam seam, DiagramType diagramType)
    {
        return diagramType switch
        {
            DiagramType.ClassApiSurface => ApiSurfaceClassDiagram.Generate(seam),
            DiagramType.SeqPluginLifecycle => PluginLifecycleSequence.Generate(seam),
            DiagramType.C4SystemContext => C4SystemContext.Generate(seam),
            DiagramType.C4Container => C4Container.Generate(seam),
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
