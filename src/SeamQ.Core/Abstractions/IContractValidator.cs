using SeamQ.Core.Models;

namespace SeamQ.Core.Abstractions;

public record ValidationReport
{
    public IReadOnlyList<ConsumerValidationResult> Results { get; init; } = [];
    public int TotalErrors => Results.Sum(r => r.Errors);
    public int TotalWarnings => Results.Sum(r => r.Warnings);
    public bool IsValid => TotalErrors == 0;
}

public record ConsumerValidationResult
{
    public required string ConsumerName { get; init; }
    public IReadOnlyList<ValidationFinding> Findings { get; init; } = [];
    public int Errors => Findings.Count(f => f.Severity == ValidationSeverity.Error);
    public int Warnings => Findings.Count(f => f.Severity == ValidationSeverity.Warning);
}

public record ValidationFinding
{
    public required string Message { get; init; }
    public ValidationSeverity Severity { get; init; }
    public required string ElementName { get; init; }
}

public enum ValidationSeverity { Error, Warning, Info }

public interface IContractValidator
{
    Task<ValidationReport> ValidateAsync(Seam seam, CancellationToken cancellationToken = default);
}
