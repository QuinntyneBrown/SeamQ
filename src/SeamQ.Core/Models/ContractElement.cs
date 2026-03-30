namespace SeamQ.Core.Models;

public record ContractElement
{
    public required string Name { get; init; }
    public ContractElementKind Kind { get; init; }
    public required string SourceFile { get; init; }
    public int LineNumber { get; init; }
    public required string Workspace { get; init; }
    public string? TypeSignature { get; init; }
    public string? Documentation { get; init; }
    public string? ParentName { get; init; }
}

public enum ContractElementKind
{
    Interface,
    AbstractClass,
    InjectionToken,
    InputBinding,
    OutputBinding,
    SignalInput,
    Method,
    Property,
    Observable,
    Type,
    Enum,
    Route,
    Action,
    Selector
}
