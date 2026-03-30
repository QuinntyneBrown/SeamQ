using SeamQ.Core.Models;

namespace SeamQ.Generator.Sections;

public class DiagramIndexSection : IIcdSection
{
    public string Title => "Diagram Index";
    public int Order => 110;

    public Task<string> GenerateAsync(Seam seam, CancellationToken cancellationToken = default)
    {
        var diagrams = new List<(string Name, string Type, string Description)>();

        // Determine which diagrams are relevant based on the seam content
        if (seam.ContractSurface.Interfaces.Any() || seam.ContractSurface.AbstractClasses.Any())
        {
            diagrams.Add(("class-contract-surface", "Class Diagram",
                $"Shows interfaces and abstract classes defined in the {seam.Name} contract surface."));
        }

        if (seam.ContractSurface.InjectionTokens.Any())
        {
            diagrams.Add(("di-registration", "Component Diagram",
                $"Dependency injection registration flow for {seam.Name} tokens."));
        }

        if (seam.ContractSurface.InputBindings.Any() || seam.ContractSurface.OutputBindings.Any())
        {
            diagrams.Add(("component-io", "Component Diagram",
                $"Input/output binding flow for {seam.Name} components."));
        }

        var actions = seam.ContractSurface.Elements.Where(e => e.Kind == ContractElementKind.Action).Any();
        var selectors = seam.ContractSurface.Elements.Where(e => e.Kind == ContractElementKind.Selector).Any();
        if (actions || selectors)
        {
            diagrams.Add(("state-flow", "Sequence Diagram",
                $"State management action/selector flow for {seam.Name}."));
        }

        var routes = seam.ContractSurface.Elements.Where(e => e.Kind == ContractElementKind.Route).Any();
        if (routes)
        {
            diagrams.Add(("route-map", "Sequence Diagram",
                $"Route navigation and guard flow for {seam.Name}."));
        }

        // Always include the integration overview diagram
        diagrams.Add(("integration-overview", "C4 Container Diagram",
            $"High-level integration view of {seam.Name} showing provider/consumer relationships."));

        var rows = diagrams.Select((d, idx) =>
            $"| {idx + 1} | {d.Name} | {d.Type} | {d.Description} |");

        var md = $"""
            ## 13. Diagram Index

            The following diagrams are recommended for this seam's ICD. Diagram files should be co-located
            in a `diagrams/` subdirectory alongside this document.

            | # | Diagram | Type | Description |
            |:-:|---------|------|-------------|
            {string.Join("\n", rows)}
            """;

        return Task.FromResult(md);
    }
}
