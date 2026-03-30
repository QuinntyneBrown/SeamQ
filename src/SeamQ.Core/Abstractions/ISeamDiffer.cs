using SeamQ.Core.Models;

namespace SeamQ.Core.Abstractions;

public record DiffReport
{
    public IReadOnlyList<SeamDiff> SeamDiffs { get; init; } = [];
    public int TotalAdditions => SeamDiffs.Sum(d => d.Additions);
    public int TotalModifications => SeamDiffs.Sum(d => d.Modifications);
    public int TotalRemovals => SeamDiffs.Sum(d => d.Removals);
    public bool HasChanges => TotalAdditions + TotalModifications + TotalRemovals > 0;
}

public record SeamDiff
{
    public required string SeamId { get; init; }
    public required string SeamName { get; init; }
    public IReadOnlyList<SeamChange> Changes { get; init; } = [];
    public int Additions => Changes.Count(c => c.ChangeType == ChangeType.Added);
    public int Modifications => Changes.Count(c => c.ChangeType == ChangeType.Modified);
    public int Removals => Changes.Count(c => c.ChangeType == ChangeType.Removed);
}

public record SeamChange
{
    public required string ElementName { get; init; }
    public ChangeType ChangeType { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
}

public enum ChangeType { Added, Modified, Removed }

public interface ISeamDiffer
{
    Task<DiffReport> DiffAsync(string baselinePath, IReadOnlyList<Seam> currentSeams, CancellationToken cancellationToken = default);
}
