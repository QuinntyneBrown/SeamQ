using System.Text.Json;
using System.Text.Json.Serialization;
using SeamQ.Core.Abstractions;
using SeamQ.Core.Models;

namespace SeamQ.Generator;

/// <summary>
/// Implements IDataExporter by exporting contract-surface.json, data-dictionary.json,
/// and traceability-matrix.json for a given seam using System.Text.Json with indented formatting.
/// </summary>
public sealed class JsonDataExporter : IDataExporter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task<IReadOnlyList<string>> ExportAsync(Seam seam, string outputDirectory, string format, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Export format '{format}' is not supported. Only 'json' is currently supported.");
        }

        Directory.CreateDirectory(outputDirectory);

        var exportedFiles = new List<string>();

        // Export contract-surface.json
        var contractSurfacePath = Path.Combine(outputDirectory, "contract-surface.json");
        var contractSurfaceData = new
        {
            seamId = seam.Id,
            seamName = seam.Name,
            seamType = seam.Type.ToString(),
            provider = seam.Provider.Alias,
            consumers = seam.Consumers.Select(c => c.Alias).ToList(),
            elements = seam.ContractSurface.Elements.Select(e => new
            {
                name = e.Name,
                kind = e.Kind.ToString(),
                sourceFile = e.SourceFile,
                lineNumber = e.LineNumber,
                workspace = e.Workspace,
                typeSignature = e.TypeSignature,
                documentation = e.Documentation,
                parentName = e.ParentName
            }).OrderBy(e => e.name).ToList()
        };
        await WriteJsonFileAsync(contractSurfacePath, contractSurfaceData, cancellationToken);
        exportedFiles.Add(contractSurfacePath);

        // Export data-dictionary.json
        var dataDictionaryPath = Path.Combine(outputDirectory, "data-dictionary.json");
        var dataDictionaryEntries = BuildDataDictionary(seam);
        await WriteJsonFileAsync(dataDictionaryPath, dataDictionaryEntries, cancellationToken);
        exportedFiles.Add(dataDictionaryPath);

        // Export traceability-matrix.json
        var traceabilityPath = Path.Combine(outputDirectory, "traceability-matrix.json");
        var traceabilityEntries = BuildTraceabilityMatrix(seam);
        await WriteJsonFileAsync(traceabilityPath, traceabilityEntries, cancellationToken);
        exportedFiles.Add(traceabilityPath);

        return exportedFiles;
    }

    private static List<DataDictionaryEntry> BuildDataDictionary(Seam seam)
    {
        var entries = new List<DataDictionaryEntry>();

        // Group contract elements by their parent to create dictionary entries
        var topLevelElements = seam.ContractSurface.Elements
            .Where(e => e.Kind is ContractElementKind.Interface
                        or ContractElementKind.AbstractClass
                        or ContractElementKind.Type
                        or ContractElementKind.Enum)
            .OrderBy(e => e.Name);

        foreach (var element in topLevelElements)
        {
            var childElements = seam.ContractSurface.Elements
                .Where(e => e.ParentName == element.Name)
                .OrderBy(e => e.Name);

            var fields = childElements.Select(child => new FieldDefinition
            {
                Name = child.Name,
                TypeName = child.TypeSignature ?? "unknown",
                IsOptional = false,
                Documentation = child.Documentation
            }).ToList();

            var kind = element.Kind switch
            {
                ContractElementKind.Interface => DataDictionaryEntryKind.Interface,
                ContractElementKind.AbstractClass => DataDictionaryEntryKind.Class,
                ContractElementKind.Enum => DataDictionaryEntryKind.Enum,
                _ => DataDictionaryEntryKind.TypeAlias
            };

            entries.Add(new DataDictionaryEntry
            {
                Name = element.Name,
                Kind = kind,
                Fields = fields,
                Documentation = element.Documentation,
                SourceFile = element.SourceFile,
                LineNumber = element.LineNumber
            });
        }

        return entries;
    }

    private static List<TraceabilityEntry> BuildTraceabilityMatrix(Seam seam)
    {
        return seam.ContractSurface.Elements
            .Select(e => new TraceabilityEntry
            {
                ElementName = e.Name,
                SourceFile = e.SourceFile,
                LineNumber = e.LineNumber,
                Workspace = e.Workspace
            })
            .OrderBy(e => e.Workspace)
            .ThenBy(e => e.SourceFile)
            .ThenBy(e => e.LineNumber)
            .ToList();
    }

    private static async Task WriteJsonFileAsync<T>(string filePath, T data, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(data, SerializerOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
}
