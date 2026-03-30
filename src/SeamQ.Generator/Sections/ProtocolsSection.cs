using SeamQ.Core.Models;

namespace SeamQ.Generator.Sections;

public class ProtocolsSection : IIcdSection
{
    public string Title => "Protocols";
    public int Order => 90;

    public Task<string> GenerateAsync(Seam seam, CancellationToken cancellationToken = default)
    {
        var routes = seam.ContractSurface.Elements
            .Where(e => e.Kind == ContractElementKind.Route)
            .ToList();

        var properties = seam.ContractSurface.Elements
            .Where(e => e.Kind == ContractElementKind.Property)
            .ToList();

        var sections = new List<string>();

        // Route protocol subsection
        if (routes.Count > 0)
        {
            var rows = routes.Select(r =>
                $"| `{EscapePipe(r.Name)}` | `{EscapePipe(r.TypeSignature ?? "-")}` | `{EscapePipe(r.ParentName ?? "-")}` | `{EscapePipe(r.SourceFile)}:{r.LineNumber}` | {EscapePipe(r.Documentation ?? "-")} |");

            sections.Add($"""
                ### Route Protocol

                Routes that define navigation and HTTP endpoint contracts for this seam.

                | Route | Type / Method | Parent | Source | Description |
                |-------|--------------|--------|--------|-------------|
                {string.Join("\n", rows)}
                """);
        }

        // Property protocol subsection
        if (properties.Count > 0)
        {
            var rows = properties.Select(p =>
                $"| `{EscapePipe(p.Name)}` | `{EscapePipe(p.TypeSignature ?? "-")}` | `{EscapePipe(p.ParentName ?? "-")}` | `{EscapePipe(p.SourceFile)}:{p.LineNumber}` | {EscapePipe(p.Documentation ?? "-")} |");

            sections.Add($"""
                ### Property Protocol

                Public properties exposed as part of the contract surface.

                | Property | Type | Parent | Source | Description |
                |----------|------|--------|--------|-------------|
                {string.Join("\n", rows)}
                """);
        }

        // Initialization protocol (derived from seam type)
        sections.Add(GenerateInitializationProtocol(seam));

        if (sections.Count == 1)
        {
            // Only the initialization protocol exists, no route/property elements found
            var md = $"""
                ## 11. Protocols

                {sections[0]}
                """;
            return Task.FromResult(md);
        }

        var combined = $"""
            ## 11. Protocols

            Protocol definitions governing how this seam's contract elements are accessed and invoked.

            {string.Join("\n\n", sections)}
            """;

        return Task.FromResult(combined);
    }

    private static string GenerateInitializationProtocol(Seam seam)
    {
        var protocol = seam.Type switch
        {
            SeamType.PluginContract => "Plugin modules must register via the plugin manifest and provide all required injection tokens before the host module bootstraps.",
            SeamType.SharedLibrary => "Shared library consumers import the library module and gain access to exported services, types, and components.",
            SeamType.MessageBus => "Message bus participants subscribe to declared topics/channels. Producers emit typed messages; consumers register handlers.",
            SeamType.RouteContract => "Route contracts are resolved at navigation time. Lazy-loaded modules must register routes and guards.",
            SeamType.StateContract => "State contracts are initialized when the feature store is registered. Actions, reducers, and selectors must be provided.",
            SeamType.HttpApiContract => "HTTP API contracts are consumed via typed HTTP clients. Base URL, authentication, and error handling must be configured.",
            _ => "Standard module initialization applies."
        };

        return $"""
            ### Initialization Protocol

            {protocol}
            """;
    }

    private static string EscapePipe(string value) => value.Replace("|", "\\|");
}
