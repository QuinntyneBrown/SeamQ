using SeamQ.Core.Abstractions;
using SeamQ.Core.Models;

namespace SeamQ.Validator.Rules;

/// <summary>
/// Checks that consumers comply with input/output binding requirements.
/// Verifies that input bindings have corresponding providers and that output
/// bindings have corresponding subscribers in the consumer workspace.
/// </summary>
public sealed class InputOutputRule : IValidationRule
{
    public string Name => "InputOutputBinding";

    public Task<IReadOnlyList<ValidationFinding>> EvaluateAsync(Seam seam, Workspace consumer, CancellationToken cancellationToken = default)
    {
        var findings = new List<ValidationFinding>();

        var inputBindings = seam.ContractSurface.InputBindings.ToList();
        var outputBindings = seam.ContractSurface.OutputBindings.ToList();

        if (inputBindings.Count == 0 && outputBindings.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<ValidationFinding>>(findings);
        }

        // Collect all exported symbol names from the consumer workspace
        var consumerExports = new HashSet<string>(
            consumer.Exports.Select(e => e.Name)
                .Concat(consumer.Projects.SelectMany(p => p.Exports.Select(e => e.Name))),
            StringComparer.OrdinalIgnoreCase);

        // Validate input bindings: consumer should be supplying data to these inputs
        foreach (var input in inputBindings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var inputHandled = consumerExports.Any(export =>
                export.Contains(input.Name, StringComparison.OrdinalIgnoreCase));

            if (!inputHandled)
            {
                findings.Add(new ValidationFinding
                {
                    Message = $"Consumer '{consumer.Alias}' does not appear to bind to required input '{input.Name}'.",
                    Severity = ValidationSeverity.Warning,
                    ElementName = input.Name
                });
            }
        }

        // Validate output bindings: consumer should be subscribing to these outputs
        foreach (var output in outputBindings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var outputHandled = consumerExports.Any(export =>
                export.Contains(output.Name, StringComparison.OrdinalIgnoreCase));

            if (!outputHandled)
            {
                findings.Add(new ValidationFinding
                {
                    Message = $"Consumer '{consumer.Alias}' does not appear to subscribe to output '{output.Name}'.",
                    Severity = ValidationSeverity.Warning,
                    ElementName = output.Name
                });
            }
        }

        return Task.FromResult<IReadOnlyList<ValidationFinding>>(findings);
    }
}
