namespace SeamQ.Core.Models;

public record TraceabilityEntry
{
    public required string ElementName { get; init; }
    public required string SourceFile { get; init; }
    public int LineNumber { get; init; }
    public required string Workspace { get; init; }
    public string? ExternalRequirementId { get; init; }
}
