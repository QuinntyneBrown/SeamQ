using Microsoft.Extensions.Logging;
using SeamQ.Core.Abstractions;
using SeamQ.Core.Models;

namespace SeamQ.Differ;

/// <summary>
/// Implements ISeamDiffer by loading a baseline JSON file, comparing each seam's
/// contract surface against the current seams, and classifying changes.
/// </summary>
public sealed class SeamDiffer : ISeamDiffer
{
    private readonly ILogger<SeamDiffer> _logger;

    public SeamDiffer(ILogger<SeamDiffer> logger)
    {
        _logger = logger;
    }

    public async Task<DiffReport> DiffAsync(string baselinePath, IReadOnlyList<Seam> currentSeams, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Diffing against baseline: {BaselinePath}", baselinePath);

        IReadOnlyList<Seam> baselineSeams;

        if (!File.Exists(baselinePath))
        {
            _logger.LogWarning("Baseline file not found at '{BaselinePath}'. Treating all seams as new.", baselinePath);
            baselineSeams = [];
        }
        else
        {
            baselineSeams = await BaselineSerializer.LoadAsync(baselinePath, cancellationToken);
        }

        var baselineById = baselineSeams.ToDictionary(s => s.Id);
        var currentById = currentSeams.ToDictionary(s => s.Id);

        var seamDiffs = new List<SeamDiff>();

        // Process seams that exist in current
        foreach (var currentSeam in currentSeams)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (baselineById.TryGetValue(currentSeam.Id, out var baselineSeam))
            {
                // Seam exists in both -- compare contract surfaces
                var changes = ChangeClassifier.Classify(baselineSeam.ContractSurface, currentSeam.ContractSurface);
                if (changes.Count > 0)
                {
                    seamDiffs.Add(new SeamDiff
                    {
                        SeamId = currentSeam.Id,
                        SeamName = currentSeam.Name,
                        Changes = changes
                    });
                }
            }
            else
            {
                // Seam is new -- all elements are additions
                var changes = currentSeam.ContractSurface.Elements
                    .Select(e => new SeamChange
                    {
                        ElementName = e.Name,
                        ChangeType = ChangeType.Added,
                        NewValue = $"{e.Kind}: {e.Name}"
                    })
                    .ToList();

                if (changes.Count > 0)
                {
                    seamDiffs.Add(new SeamDiff
                    {
                        SeamId = currentSeam.Id,
                        SeamName = currentSeam.Name,
                        Changes = changes
                    });
                }
            }
        }

        // Process seams that were removed (in baseline but not in current)
        foreach (var baselineSeam in baselineSeams)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!currentById.ContainsKey(baselineSeam.Id))
            {
                var changes = baselineSeam.ContractSurface.Elements
                    .Select(e => new SeamChange
                    {
                        ElementName = e.Name,
                        ChangeType = ChangeType.Removed,
                        OldValue = $"{e.Kind}: {e.Name}"
                    })
                    .ToList();

                if (changes.Count > 0)
                {
                    seamDiffs.Add(new SeamDiff
                    {
                        SeamId = baselineSeam.Id,
                        SeamName = baselineSeam.Name,
                        Changes = changes
                    });
                }
            }
        }

        var report = new DiffReport { SeamDiffs = seamDiffs };

        _logger.LogInformation(
            "Diff complete: {Additions} addition(s), {Modifications} modification(s), {Removals} removal(s)",
            report.TotalAdditions, report.TotalModifications, report.TotalRemovals);

        return report;
    }
}
