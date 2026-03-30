using SeamQ.Core.Models;

namespace SeamQ.Detector.Strategies;

/// <summary>
/// Detects route contract seams by finding:
/// - loadChildren / loadComponent lazy-loading references across workspaces
/// - Route configuration exports that point to other workspace modules
/// </summary>
public class RouteContractStrategy : ISeamDetectionStrategy
{
    public string Name => "RouteContract";

    private static readonly string[] RoutePatterns =
    [
        "loadChildren", "loadComponent", "Route", "Routes",
        "RouterModule", "provideRouter", "ROUTES"
    ];

    public Task<IReadOnlyList<SeamCandidate>> DetectAsync(
        IReadOnlyList<Workspace> workspaces,
        CancellationToken cancellationToken = default)
    {
        var candidates = new List<SeamCandidate>();

        foreach (var provider in workspaces)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var providerExports = GetAllExports(provider);

            // Find route-related exports
            var routeExports = providerExports
                .Where(e => RoutePatterns.Any(pattern =>
                    e.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                    e.Kind.Contains("route", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (routeExports.Count == 0)
                continue;

            var routeExportNames = new HashSet<string>(
                routeExports.Select(e => e.Name),
                StringComparer.OrdinalIgnoreCase);

            // Find consumers: workspaces that have loadChildren/loadComponent pointing to this provider
            var consumers = new List<Workspace>();

            foreach (var consumer in workspaces)
            {
                if (ReferenceEquals(consumer, provider))
                    continue;

                var consumerExports = GetAllExports(consumer);

                // Check if consumer has exports that reference the provider's route symbols
                bool references = consumerExports.Any(ce =>
                    routeExportNames.Contains(ce.Name) ||
                    ce.Name.Contains("loadChildren", StringComparison.OrdinalIgnoreCase) ||
                    ce.Name.Contains("loadComponent", StringComparison.OrdinalIgnoreCase));

                if (references)
                {
                    consumers.Add(consumer);
                }
            }

            var elements = routeExports
                .Select(e => new ContractElement
                {
                    Name = e.Name,
                    Kind = ContractElementKind.Route,
                    SourceFile = e.FilePath,
                    LineNumber = e.LineNumber,
                    Workspace = provider.Alias,
                    TypeSignature = e.TypeSignature,
                    Documentation = e.Documentation,
                    ParentName = e.ParentName
                })
                .ToList();

            double rawConfidence = CalculateRawConfidence(consumers.Count, elements.Count);

            candidates.Add(new SeamCandidate
            {
                Name = $"{provider.Alias}/route-contract",
                Type = SeamType.RouteContract,
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
        return workspace.Exports
            .Where(e => e.Name != "*")
            .ToList();
    }

    private static double CalculateRawConfidence(int consumerCount, int elementCount)
    {
        double confidence = 0.3;
        if (consumerCount > 0) confidence += 0.3;
        if (consumerCount > 1) confidence += 0.1;
        if (elementCount > 1) confidence += 0.15;
        if (elementCount > 3) confidence += 0.15;
        return Math.Clamp(confidence, 0.0, 1.0);
    }
}
