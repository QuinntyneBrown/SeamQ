using SeamQ.Core.Abstractions;
using SeamQ.Core.Models;
using SeamQ.Detector.Strategies;

namespace SeamQ.Detector;

/// <summary>
/// Orchestrates all registered detection strategies, collects SeamCandidate results,
/// scores, deduplicates, and converts them to Seam models.
/// </summary>
public class SeamDetector : ISeamDetector
{
    private readonly IEnumerable<ISeamDetectionStrategy> _strategies;
    private readonly ConfidenceScorer _scorer;
    private readonly SeamRegistry _registry;

    public SeamDetector(
        IEnumerable<ISeamDetectionStrategy> strategies,
        ConfidenceScorer scorer,
        SeamRegistry registry)
    {
        _strategies = strategies;
        _scorer = scorer;
        _registry = registry;
    }

    public async Task<IReadOnlyList<Seam>> DetectAsync(
        IReadOnlyList<Workspace> workspaces,
        CancellationToken cancellationToken = default)
    {
        var allCandidates = new List<SeamCandidate>();

        // Run each strategy and collect candidates
        foreach (var strategy in _strategies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidates = await strategy.DetectAsync(workspaces, cancellationToken);
            allCandidates.AddRange(candidates);
        }

        // Score each candidate
        var scored = allCandidates
            .Select(c => (Candidate: c, Score: _scorer.Score(c)))
            .ToList();

        // Deduplicate: group by provider alias + name + type, keep the highest-scored entry
        var deduplicated = scored
            .GroupBy(s => BuildDeduplicationKey(s.Candidate))
            .Select(g => g.OrderByDescending(x => x.Score).First())
            .ToList();

        // Convert to Seam models
        var seams = deduplicated
            .Select(item => ConvertToSeam(item.Candidate, item.Score))
            .OrderByDescending(s => s.Confidence)
            .ToList();

        // Store in registry
        _registry.Clear();
        _registry.RegisterAll(seams);

        return seams;
    }

    private static string BuildDeduplicationKey(SeamCandidate candidate)
    {
        return $"{candidate.Provider.Alias}::{candidate.Name}::{candidate.Type}";
    }

    private static Seam ConvertToSeam(SeamCandidate candidate, double confidence)
    {
        return new Seam
        {
            Id = GenerateId(candidate),
            Name = candidate.Name,
            Type = candidate.Type,
            Provider = candidate.Provider,
            Consumers = candidate.Consumers,
            Confidence = confidence,
            ContractSurface = new ContractSurface
            {
                Elements = candidate.Elements
            }
        };
    }

    private static string GenerateId(SeamCandidate candidate)
    {
        var raw = $"{candidate.Provider.Alias}:{candidate.Type}:{candidate.Name}";
        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(raw)))[..16].ToLowerInvariant();
    }
}
