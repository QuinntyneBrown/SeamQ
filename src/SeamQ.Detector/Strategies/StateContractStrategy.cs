using SeamQ.Core.Models;

namespace SeamQ.Detector.Strategies;

/// <summary>
/// Detects state contract seams by finding:
/// - NgRx actions, selectors, reducers, effects exported across workspaces
/// - Signal store exports (signalStore, patchState, withState)
/// - State management patterns shared between workspaces
/// </summary>
public class StateContractStrategy : ISeamDetectionStrategy
{
    public string Name => "StateContract";

    private static readonly string[] StatePatterns =
    [
        "Action", "Selector", "Reducer", "Effect", "Store",
        "createAction", "createSelector", "createReducer", "createEffect", "createFeature",
        "signalStore", "patchState", "withState", "withMethods", "withComputed",
        "StoreModule", "EffectsModule", "provideStore", "provideState", "provideEffects"
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

            // Find state-management-related exports
            var stateExports = providerExports
                .Where(e => StatePatterns.Any(pattern =>
                    e.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                    e.Kind.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (stateExports.Count == 0)
                continue;

            var stateExportNames = new HashSet<string>(
                stateExports.Select(e => e.Name),
                StringComparer.OrdinalIgnoreCase);

            // Find consumers: workspaces that reference these state exports
            var consumers = new List<Workspace>();

            foreach (var consumer in workspaces)
            {
                if (ReferenceEquals(consumer, provider))
                    continue;

                var consumerExports = GetAllExports(consumer);
                bool references = consumerExports.Any(ce =>
                    stateExportNames.Contains(ce.Name));

                if (references)
                {
                    consumers.Add(consumer);
                }
            }

            // Map to appropriate contract element kinds
            var elements = stateExports
                .Select(e => new ContractElement
                {
                    Name = e.Name,
                    Kind = MapToContractElementKind(e.Name),
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
                Name = $"{provider.Alias}/state-contract",
                Type = SeamType.StateContract,
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

    private static ContractElementKind MapToContractElementKind(string name)
    {
        if (name.Contains("Action", StringComparison.OrdinalIgnoreCase))
            return ContractElementKind.Action;
        if (name.Contains("Selector", StringComparison.OrdinalIgnoreCase))
            return ContractElementKind.Selector;
        if (name.Contains("Store", StringComparison.OrdinalIgnoreCase))
            return ContractElementKind.Type;
        return ContractElementKind.Property;
    }

    private static double CalculateRawConfidence(int consumerCount, int elementCount)
    {
        double confidence = 0.3;
        if (consumerCount > 0) confidence += 0.3;
        if (consumerCount > 1) confidence += 0.1;
        if (elementCount > 2) confidence += 0.15;
        if (elementCount > 5) confidence += 0.15;
        return Math.Clamp(confidence, 0.0, 1.0);
    }
}
