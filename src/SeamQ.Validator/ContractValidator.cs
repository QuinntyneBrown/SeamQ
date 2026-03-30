using Microsoft.Extensions.Logging;
using SeamQ.Core.Abstractions;
using SeamQ.Core.Models;
using SeamQ.Validator.Rules;

namespace SeamQ.Validator;

/// <summary>
/// Implements IContractValidator by running all registered validation rules
/// against each consumer workspace and collecting findings into a ValidationReport.
/// </summary>
public sealed class ContractValidator : IContractValidator
{
    private readonly IEnumerable<IValidationRule> _rules;
    private readonly ILogger<ContractValidator> _logger;

    public ContractValidator(IEnumerable<IValidationRule> rules, ILogger<ContractValidator> logger)
    {
        _rules = rules;
        _logger = logger;
    }

    public async Task<ValidationReport> ValidateAsync(Seam seam, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating seam '{SeamName}' against {ConsumerCount} consumer(s)", seam.Name, seam.Consumers.Count);

        var results = new List<ConsumerValidationResult>();

        foreach (var consumer in seam.Consumers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogDebug("Validating consumer '{ConsumerAlias}' for seam '{SeamName}'", consumer.Alias, seam.Name);

            var findings = new List<ValidationFinding>();

            foreach (var rule in _rules)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogDebug("Running rule '{RuleName}' for consumer '{ConsumerAlias}'", rule.Name, consumer.Alias);

                var ruleFindings = await rule.EvaluateAsync(seam, consumer, cancellationToken);
                findings.AddRange(ruleFindings);
            }

            results.Add(new ConsumerValidationResult
            {
                ConsumerName = consumer.Alias,
                Findings = findings
            });

            if (findings.Count > 0)
            {
                _logger.LogInformation(
                    "Consumer '{ConsumerAlias}': {ErrorCount} error(s), {WarningCount} warning(s)",
                    consumer.Alias,
                    findings.Count(f => f.Severity == ValidationSeverity.Error),
                    findings.Count(f => f.Severity == ValidationSeverity.Warning));
            }
        }

        var report = new ValidationReport { Results = results };

        _logger.LogInformation(
            "Validation complete for seam '{SeamName}': {TotalErrors} error(s), {TotalWarnings} warning(s), valid={IsValid}",
            seam.Name, report.TotalErrors, report.TotalWarnings, report.IsValid);

        return report;
    }
}
