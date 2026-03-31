using SeamQ.Core.Models;

namespace SeamQ.Detector.Strategies;

/// <summary>
/// Detects shared library seams by matching barrel exports (index/public-api exports)
/// from a library workspace to imports consumed in other workspaces.
/// </summary>
public class SharedLibraryStrategy : ISeamDetectionStrategy
{
    public string Name => "SharedLibrary";

    private static readonly HashSet<string> BarrelFileIndicators = new(StringComparer.OrdinalIgnoreCase)
    {
        "index.ts", "public-api.ts", "public_api.ts", "index.js"
    };

    public Task<IReadOnlyList<SeamCandidate>> DetectAsync(
        IReadOnlyList<Workspace> workspaces,
        CancellationToken cancellationToken = default)
    {
        var candidates = new List<SeamCandidate>();

        // Identify library workspaces (Role == Library or projects of type Library)
        var libraryWorkspaces = workspaces
            .Where(w => w.Role == WorkspaceRole.Library ||
                        w.Projects.Any(p => p.Type == ProjectType.Library))
            .ToList();

        foreach (var provider in libraryWorkspaces)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var providerExports = GetAllExports(provider);

            // Focus on barrel exports (from index.ts / public-api.ts) or all if barrel not distinguished
            var barrelExports = providerExports
                .Where(e => BarrelFileIndicators.Any(b =>
                    e.FilePath.EndsWith(b, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // Fall back to all exports if no barrel file exports found
            var relevantExports = barrelExports.Count > 0 ? barrelExports : providerExports;

            if (relevantExports.Count == 0)
                continue;

            var exportNameSet = new HashSet<string>(
                relevantExports.Select(e => e.Name),
                StringComparer.OrdinalIgnoreCase);

            // Find consumers: workspaces that reference any of the provider's exported symbol names
            var consumers = new List<Workspace>();

            foreach (var consumer in workspaces)
            {
                if (ReferenceEquals(consumer, provider))
                    continue;

                var consumerExports = GetAllExports(consumer);

                // A consumer references the library if any of its symbols share names
                // with the library's exports (import matching heuristic)
                bool references = consumerExports.Any(ce =>
                    exportNameSet.Contains(ce.Name));

                if (references)
                {
                    consumers.Add(consumer);
                }
            }

            var elements = relevantExports
                .Select(e => new ContractElement
                {
                    Name = e.Name,
                    Kind = MapKind(e.Kind),
                    SourceFile = e.FilePath,
                    LineNumber = e.LineNumber,
                    Workspace = provider.Alias,
                    TypeSignature = e.TypeSignature,
                    Documentation = e.Documentation,
                    ParentName = e.ParentName
                })
                .ToList();

            double rawConfidence = CalculateRawConfidence(
                consumers.Count, elements.Count, barrelExports.Count > 0);

            candidates.Add(new SeamCandidate
            {
                Name = $"{provider.Alias}/shared-library",
                Type = SeamType.SharedLibrary,
                Provider = provider,
                Consumers = consumers,
                Elements = elements,
                RawConfidence = rawConfidence
            });
        }

        return Task.FromResult<IReadOnlyList<SeamCandidate>>(candidates);
    }

    private static IReadOnlyList<ExportedSymbol> GetAllExports(Workspace workspace)
    {
        // workspace.Exports already aggregates all project exports (see WorkspaceScanner line 79).
        // Avoid doubling by not re-adding project.Exports.
        // Also exclude wildcard barrel placeholders ("*") — the real symbols are captured separately.
        return workspace.Exports
            .Where(e => e.Name != "*")
            .ToList();
    }

    private static ContractElementKind MapKind(string kind)
    {
        return kind.ToLowerInvariant() switch
        {
            "interface" => ContractElementKind.Interface,
            "abstractclass" => ContractElementKind.AbstractClass,
            "class" => ContractElementKind.Type,
            "type" or "type-alias" or "typealias" => ContractElementKind.Type,
            "enum" => ContractElementKind.Enum,
            "function" or "method" => ContractElementKind.Method,
            "property" => ContractElementKind.Property,
            "injectable" => ContractElementKind.Injectable,
            "component" => ContractElementKind.Component,
            "directive" => ContractElementKind.Directive,
            "pipe" => ContractElementKind.Pipe,
            "inputbinding" => ContractElementKind.InputBinding,
            "outputbinding" => ContractElementKind.OutputBinding,
            "signalinput" or "modelsignal" => ContractElementKind.SignalInput,
            "injecteddependency" => ContractElementKind.Property,
            "injectiontoken" => ContractElementKind.InjectionToken,
            "namedexport" or "wildcardexport" or "defaultexport" => ContractElementKind.Type,
            _ => ContractElementKind.Type
        };
    }

    private static double CalculateRawConfidence(int consumerCount, int elementCount, bool hasBarrelExports)
    {
        double confidence = 0.2;
        if (hasBarrelExports) confidence += 0.2;
        if (consumerCount > 0) confidence += 0.25;
        if (consumerCount > 2) confidence += 0.15;
        if (elementCount > 5) confidence += 0.1;
        if (elementCount > 10) confidence += 0.1;
        return Math.Clamp(confidence, 0.0, 1.0);
    }
}
