using SeamQ.Core.Models;

namespace SeamQ.Generator.Sections;

public class ScopeOfResponsibilitySection : IIcdSection
{
    public string Title => "Scope of Responsibility";
    public int Order => 30;

    public Task<string> GenerateAsync(Seam seam, CancellationToken cancellationToken = default)
    {
        var providerElements = seam.ContractSurface.Elements
            .Where(e => e.Workspace == seam.Provider.Alias)
            .GroupBy(e => e.Kind)
            .OrderBy(g => g.Key.ToString())
            .ToList();

        var consumerAliases = seam.Consumers.Select(c => c.Alias).ToHashSet();
        var consumerElements = seam.ContractSurface.Elements
            .Where(e => consumerAliases.Contains(e.Workspace))
            .GroupBy(e => e.Workspace)
            .ToList();

        var rows = new List<string>();

        foreach (var group in providerElements)
        {
            rows.Add($"| `{seam.Provider.Alias}` | Provider | {group.Key} | {group.Count()} | Defines and maintains |");
        }

        foreach (var wsGroup in consumerElements)
        {
            var byKind = wsGroup.GroupBy(e => e.Kind);
            foreach (var kindGroup in byKind)
            {
                rows.Add($"| `{wsGroup.Key}` | Consumer | {kindGroup.Key} | {kindGroup.Count()} | Consumes and implements |");
            }
        }

        if (rows.Count == 0)
        {
            rows.Add($"| `{seam.Provider.Alias}` | Provider | (all) | {seam.ContractSurface.Elements.Count} | Defines and maintains |");
        }

        var table = string.Join("\n", rows);

        var md = $"""
            ## 5. Scope of Responsibility

            The responsibility matrix below defines which workspace owns each part of the contract surface.

            | Workspace | Role | Element Kind | Count | Responsibility |
            |-----------|------|-------------|:-----:|----------------|
            {table}
            """;

        return Task.FromResult(md);
    }
}
