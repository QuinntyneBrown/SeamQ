using SeamQ.Core.Models;

namespace SeamQ.Detector.Strategies;

/// <summary>
/// Detects message bus seams by finding:
/// - Subject / BehaviorSubject / ReplaySubject exports
/// - EventBus / EventEmitter patterns
/// - Observable-based cross-workspace communication channels
/// </summary>
public class MessageBusStrategy : ISeamDetectionStrategy
{
    public string Name => "MessageBus";

    private static readonly string[] MessageBusPatterns =
    [
        "Subject", "BehaviorSubject", "ReplaySubject", "AsyncSubject",
        "EventBus", "EventEmitter", "MessageBus", "MessageBroker",
        "EventService", "NotificationService"
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

            // Find message-bus-like exports
            var busExports = providerExports
                .Where(e => MessageBusPatterns.Any(pattern =>
                    e.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                    e.Kind.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (busExports.Count == 0)
                continue;

            var busExportNames = new HashSet<string>(
                busExports.Select(e => e.Name),
                StringComparer.OrdinalIgnoreCase);

            // Find consumers that reference the bus exports
            var consumers = new List<Workspace>();

            foreach (var consumer in workspaces)
            {
                if (ReferenceEquals(consumer, provider))
                    continue;

                var consumerExports = GetAllExports(consumer);
                bool references = consumerExports.Any(ce =>
                    busExportNames.Contains(ce.Name));

                if (references)
                {
                    consumers.Add(consumer);
                }
            }

            var elements = busExports
                .Select(e => new ContractElement
                {
                    Name = e.Name,
                    Kind = ContractElementKind.Observable,
                    SourceFile = e.FilePath,
                    LineNumber = e.LineNumber,
                    Workspace = provider.Alias,
                    TypeSignature = null,
                    Documentation = null
                })
                .ToList();

            double rawConfidence = CalculateRawConfidence(consumers.Count, elements.Count);

            candidates.Add(new SeamCandidate
            {
                Name = $"{provider.Alias}/message-bus",
                Type = SeamType.MessageBus,
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

    private static double CalculateRawConfidence(int consumerCount, int elementCount)
    {
        double confidence = 0.35; // Base: having Subject/EventBus exports is a decent signal
        if (consumerCount > 0) confidence += 0.3;
        if (consumerCount > 1) confidence += 0.1;
        if (elementCount > 2) confidence += 0.15;
        if (elementCount > 5) confidence += 0.1;
        return Math.Clamp(confidence, 0.0, 1.0);
    }
}
