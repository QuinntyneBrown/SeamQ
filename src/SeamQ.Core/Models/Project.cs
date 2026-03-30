namespace SeamQ.Core.Models;

public record Project
{
    public required string Name { get; init; }
    public ProjectType Type { get; init; }
    public required string SourceRoot { get; init; }
    public IReadOnlyList<ExportedSymbol> Exports { get; init; } = [];
}

public enum ProjectType { Application, Library }

public record ExportedSymbol
{
    public required string Name { get; init; }
    public required string FilePath { get; init; }
    public int LineNumber { get; init; }
    public required string Kind { get; init; }
}
