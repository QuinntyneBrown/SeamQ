using SeamQ.Core.Models;
using SeamQ.Core.Abstractions;

namespace SeamQ.Validator.Rules;

public interface IValidationRule
{
    string Name { get; }
    Task<IReadOnlyList<ValidationFinding>> EvaluateAsync(Seam seam, Workspace consumer, CancellationToken cancellationToken = default);
}
