using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using SeamQ.Core.Models;

namespace SeamQ.Detector;

/// <summary>
/// In-memory storage for detected seams with query methods.
/// Thread-safe for concurrent reads and writes.
/// </summary>
public class SeamRegistry
{
    private readonly ConcurrentDictionary<string, Seam> _seams = new();

    /// <summary>
    /// Adds or updates a seam in the registry.
    /// </summary>
    public void Register(Seam seam)
    {
        _seams[seam.Id] = seam;
    }

    /// <summary>
    /// Adds multiple seams to the registry, replacing any existing entries.
    /// </summary>
    public void RegisterAll(IEnumerable<Seam> seams)
    {
        foreach (var seam in seams)
        {
            _seams[seam.Id] = seam;
        }
    }

    /// <summary>
    /// Retrieves a seam by its unique identifier.
    /// </summary>
    public Seam? GetById(string id)
    {
        _seams.TryGetValue(id, out var seam);
        return seam;
    }

    /// <summary>
    /// Retrieves all seams of the specified type.
    /// </summary>
    public IReadOnlyList<Seam> GetByType(SeamType type)
    {
        return _seams.Values.Where(s => s.Type == type).ToList();
    }

    /// <summary>
    /// Retrieves all registered seams.
    /// </summary>
    public IReadOnlyList<Seam> GetAll()
    {
        return _seams.Values.ToList();
    }

    /// <summary>
    /// Retrieves all seams where the given workspace is the provider.
    /// </summary>
    public IReadOnlyList<Seam> GetByProvider(string workspaceAlias)
    {
        return _seams.Values
            .Where(s => string.Equals(s.Provider.Alias, workspaceAlias, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Retrieves all seams where the given workspace is a consumer.
    /// </summary>
    public IReadOnlyList<Seam> GetByConsumer(string workspaceAlias)
    {
        return _seams.Values
            .Where(s => s.Consumers.Any(c =>
                string.Equals(c.Alias, workspaceAlias, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>
    /// Returns seams whose confidence score meets or exceeds the given threshold.
    /// </summary>
    public IReadOnlyList<Seam> GetByMinConfidence(double minConfidence)
    {
        return _seams.Values.Where(s => s.Confidence >= minConfidence).ToList();
    }

    /// <summary>
    /// Removes all seams from the registry.
    /// </summary>
    public void Clear()
    {
        _seams.Clear();
    }

    /// <summary>
    /// Gets the total number of registered seams.
    /// </summary>
    public int Count => _seams.Count;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Serializes all seams to a JSON file at the specified path.
    /// Creates the parent directory if it does not exist.
    /// </summary>
    public async Task SaveToFileAsync(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var seams = _seams.Values.ToList();
        var json = JsonSerializer.Serialize(seams, s_jsonOptions);
        await File.WriteAllTextAsync(fullPath, json);
    }

    /// <summary>
    /// Deserializes seams from a JSON file and loads them into the registry.
    /// Existing entries are replaced on ID collision.
    /// </summary>
    public async Task LoadFromFileAsync(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) return;
        var json = await File.ReadAllTextAsync(fullPath);
        var seams = JsonSerializer.Deserialize<List<Seam>>(json, s_jsonOptions);
        if (seams is not null)
        {
            RegisterAll(seams);
        }
    }

    /// <summary>
    /// Default registry file path relative to the current working directory.
    /// </summary>
    public static string DefaultRegistryPath =>
        Path.Combine(Directory.GetCurrentDirectory(), ".seamq", "registry.json");
}
