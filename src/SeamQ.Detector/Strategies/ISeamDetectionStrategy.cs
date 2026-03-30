using SeamQ.Core.Models;

namespace SeamQ.Detector.Strategies;

public interface ISeamDetectionStrategy
{
    string Name { get; }
    Task<IReadOnlyList<SeamCandidate>> DetectAsync(IReadOnlyList<Workspace> workspaces, CancellationToken cancellationToken = default);
}

public record SeamCandidate
{
    public required string Name { get; init; }
    public SeamType Type { get; init; }
    public required Workspace Provider { get; init; }
    public IReadOnlyList<Workspace> Consumers { get; init; } = [];
    public IReadOnlyList<ContractElement> Elements { get; init; } = [];
    public double RawConfidence { get; init; }
}
