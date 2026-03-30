using SeamQ.Core.Models;

namespace SeamQ.Scanner.Parsing;

/// <summary>
/// Detects the type of an Angular/Nx workspace by checking for known config files.
/// </summary>
public static class WorkspaceTypeDetector
{
    public static WorkspaceType Detect(string workspacePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        if (File.Exists(Path.Combine(workspacePath, "nx.json")))
        {
            return WorkspaceType.NxMonorepo;
        }

        if (File.Exists(Path.Combine(workspacePath, "angular.json")))
        {
            return WorkspaceType.AngularCli;
        }

        // Check for a standalone project (has tsconfig.json but no workspace config)
        if (File.Exists(Path.Combine(workspacePath, "tsconfig.json")) ||
            File.Exists(Path.Combine(workspacePath, "tsconfig.base.json")))
        {
            return WorkspaceType.Standalone;
        }

        return WorkspaceType.Unknown;
    }
}
