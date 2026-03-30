namespace SeamQ.Core.Models;

public record Seam
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public SeamType Type { get; init; }
    public required Workspace Provider { get; init; }
    public IReadOnlyList<Workspace> Consumers { get; init; } = [];
    public double Confidence { get; init; }
    public ContractSurface ContractSurface { get; init; } = new();
}

public enum SeamType
{
    PluginContract,
    SharedLibrary,
    MessageBus,
    RouteContract,
    StateContract,
    HttpApiContract
}
