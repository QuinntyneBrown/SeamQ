using SeamQ.Core.Abstractions;
using SeamQ.Core.Models;

namespace SeamQ.Validator.Rules;

/// <summary>
/// Checks that consumers implement all required interfaces defined in the contract surface.
/// Looks for exported symbols in the consumer workspace that match interface names.
/// </summary>
public sealed class InterfaceImplementationRule : IValidationRule
{
    public string Name => "InterfaceImplementation";

    public Task<IReadOnlyList<ValidationFinding>> EvaluateAsync(Seam seam, Workspace consumer, CancellationToken cancellationToken = default)
    {
        var findings = new List<ValidationFinding>();

        var requiredInterfaces = seam.ContractSurface.Interfaces.ToList();
        if (requiredInterfaces.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<ValidationFinding>>(findings);
        }

        // Collect all exported symbol names from the consumer workspace
        var consumerExports = new HashSet<string>(
            consumer.Exports.Select(e => e.Name)
                .Concat(consumer.Projects.SelectMany(p => p.Exports.Select(e => e.Name))),
            StringComparer.OrdinalIgnoreCase);

        foreach (var iface in requiredInterfaces)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Look for any export whose name suggests it implements this interface.
            // Convention: an implementation of IFoo might be named FooImpl, DefaultFoo, FooService, etc.
            var interfaceName = iface.Name;
            var baseName = interfaceName.StartsWith('I') && interfaceName.Length > 1 && char.IsUpper(interfaceName[1])
                ? interfaceName[1..]
                : interfaceName;

            var hasImplementation = consumerExports.Any(export =>
                export.Contains(baseName, StringComparison.OrdinalIgnoreCase) ||
                export.Equals(interfaceName, StringComparison.OrdinalIgnoreCase));

            if (!hasImplementation)
            {
                findings.Add(new ValidationFinding
                {
                    Message = $"Consumer '{consumer.Alias}' does not appear to implement required interface '{interfaceName}'.",
                    Severity = ValidationSeverity.Error,
                    ElementName = interfaceName
                });
            }
        }

        return Task.FromResult<IReadOnlyList<ValidationFinding>>(findings);
    }
}
