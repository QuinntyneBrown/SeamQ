using SeamQ.Core.Models;

namespace SeamQ.Detector.Strategies;

/// <summary>
/// Detects plugin contracts by finding:
/// - Interfaces/abstract classes exported from one workspace, implemented in another
/// - InjectionToken exports consumed across workspaces
/// - forRoot/forChild patterns
/// </summary>
public class PluginContractStrategy : ISeamDetectionStrategy
{
    public string Name => "PluginContract";

    private static readonly HashSet<string> PluginKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "interface", "abstract class", "abstractclass", "injection-token", "injectiontoken", "InjectionToken"
    };

    private static readonly HashSet<string> ForRootPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "forRoot", "forChild", "forFeature"
    };

    public Task<IReadOnlyList<SeamCandidate>> DetectAsync(
        IReadOnlyList<Workspace> workspaces,
        CancellationToken cancellationToken = default)
    {
        var candidates = new List<SeamCandidate>();

        foreach (var provider in workspaces)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var allProviderExports = GetAllExports(provider);

            // Find plugin-like exports: interfaces, abstract classes, injection tokens
            var pluginExports = allProviderExports
                .Where(e => PluginKinds.Contains(e.Kind))
                .ToList();

            // Find forRoot/forChild pattern exports
            var forRootExports = allProviderExports
                .Where(e => ForRootPatterns.Any(p =>
                    e.Name.Contains(p, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var relevantExports = pluginExports.Concat(forRootExports).Distinct().ToList();

            if (relevantExports.Count == 0)
                continue;

            // Find consumers: other workspaces that reference these exported symbols
            var consumers = new List<Workspace>();
            var consumerExportNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var consumer in workspaces)
            {
                if (ReferenceEquals(consumer, provider))
                    continue;

                var consumerExports = GetAllExports(consumer);
                // Check if any consumer export references the provider's exported names
                // (e.g., implements an interface or uses an injection token)
                foreach (var pExport in relevantExports)
                {
                    if (consumerExports.Any(ce =>
                        ce.Name.Contains(pExport.Name, StringComparison.OrdinalIgnoreCase) ||
                        ce.Kind.Equals("class", StringComparison.OrdinalIgnoreCase) &&
                        pExport.Kind.Equals("interface", StringComparison.OrdinalIgnoreCase)))
                    {
                        consumers.Add(consumer);
                        consumerExportNames.Add(pExport.Name);
                        break;
                    }
                }
            }

            // Build contract elements
            var elements = relevantExports
                .Select(e => new ContractElement
                {
                    Name = e.Name,
                    Kind = MapToContractElementKind(e.Kind),
                    SourceFile = e.FilePath,
                    LineNumber = e.LineNumber,
                    Workspace = provider.Alias,
                    TypeSignature = e.TypeSignature,
                    Documentation = e.Documentation,
                    ParentName = e.ParentName
                })
                .ToList();

            if (elements.Count > 0)
            {
                double rawConfidence = CalculateRawConfidence(consumers.Count, elements.Count, forRootExports.Count);

                candidates.Add(new SeamCandidate
                {
                    Name = $"{provider.Alias}/plugin-contract",
                    Type = SeamType.PluginContract,
                    Provider = provider,
                    Consumers = consumers,
                    Elements = elements,
                    RawConfidence = rawConfidence
                });
            }
        }

        return Task.FromResult<IReadOnlyList<SeamCandidate>>(candidates);
    }

    private static IReadOnlyList<ExportedSymbol> GetAllExports(Workspace workspace)
    {
        return workspace.Exports
            .Where(e => e.Name != "*")
            .ToList();
    }

    private static ContractElementKind MapToContractElementKind(string kind)
    {
        return kind.ToLowerInvariant() switch
        {
            "interface" => ContractElementKind.Interface,
            "abstract class" or "abstractclass" => ContractElementKind.AbstractClass,
            "injection-token" or "injectiontoken" => ContractElementKind.InjectionToken,
            "injectable" => ContractElementKind.Type,
            "inputbinding" => ContractElementKind.InputBinding,
            "outputbinding" => ContractElementKind.OutputBinding,
            "signalinput" or "modelsignal" => ContractElementKind.SignalInput,
            _ => ContractElementKind.Type
        };
    }

    private static double CalculateRawConfidence(int consumerCount, int elementCount, int forRootCount)
    {
        double confidence = 0.3; // Base confidence for having plugin-like exports
        if (consumerCount > 0) confidence += 0.3;
        if (consumerCount > 1) confidence += 0.1;
        if (elementCount > 3) confidence += 0.1;
        if (forRootCount > 0) confidence += 0.2; // forRoot/forChild is a strong plugin signal
        return Math.Clamp(confidence, 0.0, 1.0);
    }
}
