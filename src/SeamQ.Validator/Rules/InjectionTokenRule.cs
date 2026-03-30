using SeamQ.Core.Abstractions;
using SeamQ.Core.Models;

namespace SeamQ.Validator.Rules;

/// <summary>
/// Checks that consumers provide all required injection tokens defined in the contract surface.
/// Looks for exported symbols in the consumer workspace that reference the token names.
/// </summary>
public sealed class InjectionTokenRule : IValidationRule
{
    public string Name => "InjectionToken";

    public Task<IReadOnlyList<ValidationFinding>> EvaluateAsync(Seam seam, Workspace consumer, CancellationToken cancellationToken = default)
    {
        var findings = new List<ValidationFinding>();

        var requiredTokens = seam.ContractSurface.InjectionTokens.ToList();
        if (requiredTokens.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<ValidationFinding>>(findings);
        }

        // Collect all exported symbol names from the consumer workspace
        var consumerExports = new HashSet<string>(
            consumer.Exports.Select(e => e.Name)
                .Concat(consumer.Projects.SelectMany(p => p.Exports.Select(e => e.Name))),
            StringComparer.OrdinalIgnoreCase);

        foreach (var token in requiredTokens)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if any consumer export references this token
            var tokenProvided = consumerExports.Any(export =>
                export.Contains(token.Name, StringComparison.OrdinalIgnoreCase));

            if (!tokenProvided)
            {
                findings.Add(new ValidationFinding
                {
                    Message = $"Consumer '{consumer.Alias}' does not appear to provide required injection token '{token.Name}'.",
                    Severity = ValidationSeverity.Error,
                    ElementName = token.Name
                });
            }
        }

        return Task.FromResult<IReadOnlyList<ValidationFinding>>(findings);
    }
}
