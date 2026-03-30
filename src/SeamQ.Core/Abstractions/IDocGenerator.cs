using SeamQ.Core.Models;

namespace SeamQ.Core.Abstractions;

/// <summary>
/// Generates comprehensive API reference documentation for an Angular workspace.
/// Creates per-project folders with README.md and PlantUML class diagrams.
/// </summary>
public interface IDocGenerator
{
    /// <summary>
    /// Generates API reference documentation for each project in the workspace.
    /// Returns the list of generated file paths.
    /// </summary>
    Task<IReadOnlyList<string>> GenerateAsync(
        Workspace workspace,
        string outputDirectory,
        CancellationToken cancellationToken = default);
}
