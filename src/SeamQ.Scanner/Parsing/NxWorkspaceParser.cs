using System.Text.Json;
using Microsoft.Extensions.Logging;
using SeamQ.Core.Models;

namespace SeamQ.Scanner.Parsing;

/// <summary>
/// Parses nx.json and per-project project.json files to discover projects in an Nx monorepo.
/// </summary>
public class NxWorkspaceParser
{
    private readonly ILogger<NxWorkspaceParser> _logger;

    public NxWorkspaceParser(ILogger<NxWorkspaceParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Scans an Nx workspace and returns all discovered projects.
    /// Looks for project.json files under common Nx project directories (apps/, libs/, packages/).
    /// Also falls back to workspace.json or angular.json if present.
    /// </summary>
    public async Task<IReadOnlyList<Project>> ParseAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        var projects = new List<Project>();

        // Strategy 1: Try workspace.json (older Nx format)
        var workspaceJsonPath = Path.Combine(workspacePath, "workspace.json");
        if (File.Exists(workspaceJsonPath))
        {
            var parsed = await ParseWorkspaceJsonAsync(workspaceJsonPath, workspacePath, cancellationToken);
            if (parsed.Count > 0)
            {
                return parsed;
            }
        }

        // Strategy 2: Discover project.json files in conventional Nx directories
        var searchDirs = new[] { "apps", "libs", "packages" };

        foreach (var dir in searchDirs)
        {
            var fullDir = Path.Combine(workspacePath, dir);
            if (!Directory.Exists(fullDir))
            {
                continue;
            }

            var projectJsonFiles = Directory.GetFiles(fullDir, "project.json", SearchOption.AllDirectories);

            foreach (var projectJsonPath in projectJsonFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var project = await ParseProjectJsonAsync(projectJsonPath, workspacePath, cancellationToken);
                    if (project is not null)
                    {
                        projects.Add(project);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse project.json at {Path}", projectJsonPath);
                }
            }
        }

        // Strategy 3: Fall back to angular.json projects section (Nx can have one)
        if (projects.Count == 0)
        {
            var angularJsonPath = Path.Combine(workspacePath, "angular.json");
            if (File.Exists(angularJsonPath))
            {
                var angularParser = new AngularWorkspaceParser(
                    Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
                        .CreateLogger<AngularWorkspaceParser>());
                return await angularParser.ParseAsync(workspacePath, cancellationToken);
            }
        }

        return projects;
    }

    private async Task<IReadOnlyList<Project>> ParseWorkspaceJsonAsync(
        string workspaceJsonPath,
        string workspacePath,
        CancellationToken cancellationToken)
    {
        var projects = new List<Project>();

        try
        {
            var json = await File.ReadAllTextAsync(workspaceJsonPath, cancellationToken);
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            if (!document.RootElement.TryGetProperty("projects", out var projectsElement))
            {
                return projects;
            }

            foreach (var projectProp in projectsElement.EnumerateObject())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var name = projectProp.Name;

                // Value can be a string (path to project) or an object with config
                string projectRoot;
                if (projectProp.Value.ValueKind == JsonValueKind.String)
                {
                    projectRoot = projectProp.Value.GetString() ?? "";
                }
                else if (projectProp.Value.TryGetProperty("root", out var rootProp) &&
                         rootProp.ValueKind == JsonValueKind.String)
                {
                    projectRoot = rootProp.GetString() ?? "";
                }
                else
                {
                    continue;
                }

                var sourceRoot = Path.Combine(workspacePath, projectRoot.Replace('/', Path.DirectorySeparatorChar), "src");
                var projectType = DetermineProjectType(projectRoot);

                projects.Add(new Project
                {
                    Name = name,
                    Type = projectType,
                    SourceRoot = sourceRoot
                });
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse workspace.json at {Path}", workspaceJsonPath);
        }

        return projects;
    }

    private async Task<Project?> ParseProjectJsonAsync(
        string projectJsonPath,
        string workspacePath,
        CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(projectJsonPath, cancellationToken);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        var root = document.RootElement;
        var projectDir = Path.GetDirectoryName(projectJsonPath)!;

        // Get project name
        string name;
        if (root.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
        {
            name = nameProp.GetString()!;
        }
        else
        {
            // Derive name from directory structure
            name = Path.GetRelativePath(workspacePath, projectDir).Replace(Path.DirectorySeparatorChar, '/');
        }

        // Get source root
        var sourceRoot = Path.Combine(projectDir, "src");
        if (root.TryGetProperty("sourceRoot", out var srcRootProp) && srcRootProp.ValueKind == JsonValueKind.String)
        {
            var srcRootValue = srcRootProp.GetString()!;
            sourceRoot = Path.IsPathRooted(srcRootValue)
                ? srcRootValue
                : Path.Combine(workspacePath, srcRootValue.Replace('/', Path.DirectorySeparatorChar));
        }

        // Determine project type
        var projectType = ProjectType.Library;
        if (root.TryGetProperty("projectType", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
        {
            var typeStr = typeProp.GetString();
            if (string.Equals(typeStr, "application", StringComparison.OrdinalIgnoreCase))
            {
                projectType = ProjectType.Application;
            }
        }
        else
        {
            var relativePath = Path.GetRelativePath(workspacePath, projectDir);
            projectType = DetermineProjectType(relativePath);
        }

        return new Project
        {
            Name = name,
            Type = projectType,
            SourceRoot = sourceRoot
        };
    }

    private static ProjectType DetermineProjectType(string projectPath)
    {
        var normalized = projectPath.Replace(Path.DirectorySeparatorChar, '/');
        return normalized.StartsWith("apps/", StringComparison.OrdinalIgnoreCase)
            ? ProjectType.Application
            : ProjectType.Library;
    }
}
