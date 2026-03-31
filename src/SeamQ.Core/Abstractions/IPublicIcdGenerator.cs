using SeamQ.Core.Models;

namespace SeamQ.Core.Abstractions;

/// <summary>
/// Generates detailed public Interface Control Documents for each project in a workspace.
/// Produces Markdown with overview, type descriptions, sequence diagrams, class diagrams, and C4 diagrams.
/// </summary>
public interface IPublicIcdGenerator
{
    /// <summary>
    /// Generates public ICD documentation for each project in the workspace.
    /// Returns the list of generated file paths.
    /// </summary>
    Task<IReadOnlyList<string>> GenerateAsync(
        Workspace workspace,
        string outputDirectory,
        CancellationToken cancellationToken = default);
}
