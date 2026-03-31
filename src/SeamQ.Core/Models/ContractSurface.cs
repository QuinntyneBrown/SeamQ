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
    public IEnumerable<ContractElement> Types => Elements.Where(e => e.Kind == ContractElementKind.Type);
    public IEnumerable<ContractElement> Enumerations => Elements.Where(e => e.Kind == ContractElementKind.Enum);
    public IEnumerable<ContractElement> Components => Elements.Where(e => e.Kind == ContractElementKind.Component);
    public IEnumerable<ContractElement> Services => Elements.Where(e => e.Kind == ContractElementKind.Injectable);
    public IEnumerable<ContractElement> Directives => Elements.Where(e => e.Kind == ContractElementKind.Directive);
    public IEnumerable<ContractElement> Pipes => Elements.Where(e => e.Kind == ContractElementKind.Pipe);
    public IEnumerable<ContractElement> Observables => Elements.Where(e => e.Kind == ContractElementKind.Observable);
    public IEnumerable<ContractElement> Properties => Elements.Where(e => e.Kind == ContractElementKind.Property);
    public IEnumerable<ContractElement> SignalInputs => Elements.Where(e => e.Kind == ContractElementKind.SignalInput);
    public IEnumerable<ContractElement> Routes => Elements.Where(e => e.Kind == ContractElementKind.Route);
    public IEnumerable<ContractElement> Actions => Elements.Where(e => e.Kind == ContractElementKind.Action);
    public IEnumerable<ContractElement> Selectors => Elements.Where(e => e.Kind == ContractElementKind.Selector);
}
