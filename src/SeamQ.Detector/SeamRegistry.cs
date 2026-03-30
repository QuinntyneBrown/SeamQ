using System.Collections.Concurrent;
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
}
