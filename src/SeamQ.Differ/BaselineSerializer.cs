using System.Text.Json;
using System.Text.Json.Serialization;
using SeamQ.Core.Models;

namespace SeamQ.Differ;

/// <summary>
/// Serializes and deserializes a list of Seam objects to/from JSON
/// with stable ordering for consistent baselines.
/// </summary>
public static class BaselineSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Serializes the seam list to JSON with stable ordering.
    /// Elements within each seam's contract surface are sorted by name.
    /// </summary>
    public static string Serialize(IReadOnlyList<Seam> seams)
    {
        // Sort seams by Id and sort contract elements within each seam by Name for stable output
        var orderedSeams = seams
            .OrderBy(s => s.Id)
            .Select(s => s with
            {
                ContractSurface = new ContractSurface
                {
                    Elements = s.ContractSurface.Elements
                        .OrderBy(e => e.Name)
                        .ThenBy(e => e.Kind.ToString())
                        .ToList()
                },
                Consumers = s.Consumers.OrderBy(c => c.Alias).ToList()
            })
            .ToList();

        return JsonSerializer.Serialize(orderedSeams, SerializerOptions);
    }

    /// <summary>
    /// Deserializes a JSON string back into a list of Seam objects.
    /// </summary>
    public static IReadOnlyList<Seam> Deserialize(string json)
    {
        return JsonSerializer.Deserialize<List<Seam>>(json, SerializerOptions) ?? [];
    }

    /// <summary>
    /// Writes the seam list to a file as JSON.
    /// </summary>
    public static async Task SaveAsync(IReadOnlyList<Seam> seams, string filePath, CancellationToken cancellationToken = default)
    {
        var json = Serialize(seams);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    /// <summary>
    /// Loads a seam list from a JSON file.
    /// </summary>
    public static async Task<IReadOnlyList<Seam>> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return Deserialize(json);
    }
}
