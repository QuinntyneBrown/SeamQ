using SeamQ.Core.Models;

namespace SeamQ.Core.Abstractions;

/// <summary>
/// Generates public API documentation for an Angular workspace.
/// Creates a separate markdown file per project documenting public types,
/// methods, enumerations, and classes.
/// </summary>
public interface IPublicApiGenerator
{
    /// <summary>
    /// Generates public API markdown documentation for each project in the workspace.
    /// Returns the list of generated file paths.
    /// </summary>
    Task<IReadOnlyList<string>> GenerateAsync(
        Workspace workspace,
        string outputDirectory,
        CancellationToken cancellationToken = default);
}
