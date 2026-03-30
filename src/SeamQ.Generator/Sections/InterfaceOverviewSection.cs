using SeamQ.Core.Models;

namespace SeamQ.Generator.Sections;

public class InterfaceOverviewSection : IIcdSection
{
    public string Title => "Interface Overview";
    public int Order => 20;

    public Task<string> GenerateAsync(Seam seam, CancellationToken cancellationToken = default)
    {
        var surface = seam.ContractSurface;

        var layers = new List<(string Layer, string Description, int Count)>
        {
            ("Interfaces", "Abstract service contracts", surface.Interfaces.Count()),
            ("Abstract Classes", "Base class contracts", surface.AbstractClasses.Count()),
            ("Injection Tokens", "DI registration tokens", surface.InjectionTokens.Count()),
            ("Input Bindings", "Component input properties", surface.InputBindings.Count()),
            ("Output Bindings", "Component output events", surface.OutputBindings.Count()),
            ("Methods", "Callable API methods", surface.Methods.Count()),
            ("Types", "Shared data types and enums", surface.Types.Count()),
        };

        var rows = string.Join("\n", layers
            .Where(l => l.Count > 0)
            .Select(l => $"| {l.Layer} | {l.Description} | {l.Count} |"));

        var md = $"""
            ## 4. Interface Overview

            The following table summarizes the API layers exposed by this seam.

            | API Layer | Description | Element Count |
            |-----------|-------------|:-------------:|
            {rows}

            **Total contract elements:** {surface.Elements.Count}
            """;

        return Task.FromResult(md);
    }
}
