namespace SeamQ.Core.Models;

public record DataDictionaryEntry
{
    public required string Name { get; init; }
    public DataDictionaryEntryKind Kind { get; init; }
    public IReadOnlyList<FieldDefinition> Fields { get; init; } = [];
    public string? Documentation { get; init; }
    public string? SourceFile { get; init; }
    public int LineNumber { get; init; }
}

public record FieldDefinition
{
    public required string Name { get; init; }
    public required string TypeName { get; init; }
    public bool IsOptional { get; init; }
    public string? DefaultValue { get; init; }
    public string? Documentation { get; init; }
}

public enum DataDictionaryEntryKind { Interface, Class, Enum, TypeAlias }
