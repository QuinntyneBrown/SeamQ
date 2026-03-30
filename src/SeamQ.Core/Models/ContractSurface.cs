namespace SeamQ.Core.Models;

public record ContractSurface
{
    public IReadOnlyList<ContractElement> Elements { get; init; } = [];

    public IEnumerable<ContractElement> Interfaces => Elements.Where(e => e.Kind == ContractElementKind.Interface);
    public IEnumerable<ContractElement> InjectionTokens => Elements.Where(e => e.Kind == ContractElementKind.InjectionToken);
    public IEnumerable<ContractElement> AbstractClasses => Elements.Where(e => e.Kind == ContractElementKind.AbstractClass);
    public IEnumerable<ContractElement> InputBindings => Elements.Where(e => e.Kind == ContractElementKind.InputBinding);
    public IEnumerable<ContractElement> OutputBindings => Elements.Where(e => e.Kind == ContractElementKind.OutputBinding);
    public IEnumerable<ContractElement> Methods => Elements.Where(e => e.Kind == ContractElementKind.Method);
    public IEnumerable<ContractElement> Types => Elements.Where(e => e.Kind is ContractElementKind.Type or ContractElementKind.Enum);
}
