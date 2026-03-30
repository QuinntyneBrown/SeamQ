namespace SeamQ.Core.Models;

public record Workspace
{
    public required string Path { get; init; }
    public required string Alias { get; init; }
    public WorkspaceRole Role { get; init; }
    public WorkspaceType Type { get; init; }
    public IReadOnlyList<Project> Projects { get; init; } = [];
    public IReadOnlyList<ExportedSymbol> Exports { get; init; } = [];
}

public enum WorkspaceRole { Framework, Plugin, Library, Application }
public enum WorkspaceType { AngularCli, NxMonorepo, Standalone, Unknown }
