using System.Text.Json;
using Microsoft.Extensions.Logging;
using SeamQ.Core.Models;

namespace SeamQ.Scanner.Parsing;

/// <summary>
/// Parses angular.json to discover projects, their source roots, and types.
/// </summary>
public class AngularWorkspaceParser
{
    private readonly ILogger<AngularWorkspaceParser> _logger;

    public AngularWorkspaceParser(ILogger<AngularWorkspaceParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Reads angular.json and returns a list of discovered projects.
    /// </summary>
    public async Task<IReadOnlyList<Project>> ParseAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        var angularJsonPath = Path.Combine(workspacePath, "angular.json");
        if (!File.Exists(angularJsonPath))
        {
            _logger.LogWarning("angular.json not found at {Path}", angularJsonPath);
            return [];
        }

        var projects = new List<Project>();

        try
        {
            var json = await File.ReadAllTextAsync(angularJsonPath, cancellationToken);
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            if (!document.RootElement.TryGetProperty("projects", out var projectsElement))
            {
                _logger.LogWarning("No 'projects' property found in angular.json");
                return [];
            }

            foreach (var projectProp in projectsElement.EnumerateObject())
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var project = ParseProject(projectProp, workspacePath);
                    if (project is not null)
                    {
                        projects.Add(project);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse project '{Name}' from angular.json", projectProp.Name);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse angular.json at {Path}", angularJsonPath);
        }

        return projects;
    }

    private Project? ParseProject(JsonProperty projectProperty, string workspacePath)
    {
        var name = projectProperty.Name;
        var value = projectProperty.Value;

        // If the value is a string, it's a path reference (Angular CLI <v6 style) — skip.
        if (value.ValueKind == JsonValueKind.String)
        {
            return null;
        }

        var sourceRoot = "src";
        if (value.TryGetProperty("sourceRoot", out var sourceRootProp) &&
            sourceRootProp.ValueKind == JsonValueKind.String)
        {
            sourceRoot = sourceRootProp.GetString() ?? "src";
        }
        else if (value.TryGetProperty("root", out var rootProp) &&
                 rootProp.ValueKind == JsonValueKind.String)
        {
            var root = rootProp.GetString() ?? "";
            sourceRoot = string.IsNullOrEmpty(root) ? "src" : Path.Combine(root, "src");
        }

        var projectType = ProjectType.Application;
        if (value.TryGetProperty("projectType", out var projectTypeProp) &&
            projectTypeProp.ValueKind == JsonValueKind.String)
        {
            var typeStr = projectTypeProp.GetString();
            if (string.Equals(typeStr, "library", StringComparison.OrdinalIgnoreCase))
            {
                projectType = ProjectType.Library;
            }
        }

        var fullSourceRoot = Path.Combine(workspacePath, sourceRoot.Replace('/', Path.DirectorySeparatorChar));

        return new Project
        {
            Name = name,
            Type = projectType,
            SourceRoot = fullSourceRoot
        };
    }
}
