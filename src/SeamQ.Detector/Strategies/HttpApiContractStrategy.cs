using SeamQ.Core.Models;

namespace SeamQ.Detector.Strategies;

/// <summary>
/// Detects HTTP API contract seams by finding:
/// - HttpClient wrapper services exported from a workspace
/// - DTO / request / response model exports consumed by other workspaces
/// - API service patterns (naming conventions like *ApiService, *HttpService, *Client)
/// </summary>
public class HttpApiContractStrategy : ISeamDetectionStrategy
{
    public string Name => "HttpApiContract";

    private static readonly string[] HttpPatterns =
    [
        "HttpClient", "ApiService", "HttpService", "RestService",
        "Client", "Endpoint", "Api"
    ];

    private static readonly string[] DtoPatterns =
    [
        "Dto", "DTO", "Request", "Response", "Payload",
        "Command", "Query", "ViewModel"
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

            // Find HTTP-related service exports
            var httpExports = providerExports
                .Where(e => HttpPatterns.Any(pattern =>
                    e.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // Find DTO/model exports
            var dtoExports = providerExports
                .Where(e => DtoPatterns.Any(pattern =>
                    e.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var allRelevant = httpExports.Concat(dtoExports).Distinct().ToList();

            if (allRelevant.Count == 0)
                continue;

            var relevantNames = new HashSet<string>(
                allRelevant.Select(e => e.Name),
                StringComparer.OrdinalIgnoreCase);

            // Find consumers
            var consumers = new List<Workspace>();

            foreach (var consumer in workspaces)
            {
                if (ReferenceEquals(consumer, provider))
                    continue;

                var consumerExports = GetAllExports(consumer);
                bool references = consumerExports.Any(ce =>
                    relevantNames.Contains(ce.Name));

                if (references)
                {
                    consumers.Add(consumer);
                }
            }

            var elements = allRelevant
                .Select(e => new ContractElement
                {
                    Name = e.Name,
                    Kind = MapToContractElementKind(e),
                    SourceFile = e.FilePath,
                    LineNumber = e.LineNumber,
                    Workspace = provider.Alias,
                    TypeSignature = null,
                    Documentation = null
                })
                .ToList();

            double rawConfidence = CalculateRawConfidence(
                consumers.Count, httpExports.Count, dtoExports.Count);

            candidates.Add(new SeamCandidate
            {
                Name = $"{provider.Alias}/http-api-contract",
                Type = SeamType.HttpApiContract,
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
        var exports = new List<ExportedSymbol>(workspace.Exports);
        foreach (var project in workspace.Projects)
        {
            exports.AddRange(project.Exports);
        }
        return exports;
    }

    private static ContractElementKind MapToContractElementKind(ExportedSymbol symbol)
    {
        if (DtoPatterns.Any(p => symbol.Name.Contains(p, StringComparison.OrdinalIgnoreCase)))
            return ContractElementKind.Type;

        if (symbol.Kind.Equals("interface", StringComparison.OrdinalIgnoreCase))
            return ContractElementKind.Interface;

        if (symbol.Kind.Equals("class", StringComparison.OrdinalIgnoreCase))
            return ContractElementKind.Type;

        return ContractElementKind.Method;
    }

    private static double CalculateRawConfidence(int consumerCount, int httpCount, int dtoCount)
    {
        double confidence = 0.25;
        if (httpCount > 0) confidence += 0.2;
        if (dtoCount > 0) confidence += 0.15;
        if (consumerCount > 0) confidence += 0.2;
        if (consumerCount > 1) confidence += 0.1;
        if (httpCount + dtoCount > 5) confidence += 0.1;
        return Math.Clamp(confidence, 0.0, 1.0);
    }
}
