using SeamQ.Detector.Strategies;

namespace SeamQ.Detector;

/// <summary>
/// Calculates a normalized confidence score for a SeamCandidate based on
/// evidence strength, cross-boundary usage, symbol count, and documentation presence.
/// </summary>
public class ConfidenceScorer
{
    /// <summary>
    /// Produces a confidence score between 0.0 and 1.0 for the given candidate.
    /// </summary>
    public double Score(SeamCandidate candidate)
    {
        double score = 0.0;

        // Factor 1: Raw confidence from the strategy (weight 0.30)
        double rawFactor = Math.Clamp(candidate.RawConfidence, 0.0, 1.0);
        score += rawFactor * 0.30;

        // Factor 2: Cross-boundary usage - how many consumers reference the provider (weight 0.30)
        int consumerCount = candidate.Consumers.Count;
        double crossBoundaryFactor = consumerCount switch
        {
            0 => 0.0,
            1 => 0.5,
            2 => 0.75,
            _ => 1.0
        };
        score += crossBoundaryFactor * 0.30;

        // Factor 3: Symbol count - more elements in the contract surface = higher confidence (weight 0.25)
        int elementCount = candidate.Elements.Count;
        double symbolFactor = elementCount switch
        {
            0 => 0.0,
            1 => 0.3,
            <= 3 => 0.6,
            <= 7 => 0.85,
            _ => 1.0
        };
        score += symbolFactor * 0.25;

        // Factor 4: Documentation presence - elements that have documentation (weight 0.15)
        double documentedRatio = candidate.Elements.Count > 0
            ? (double)candidate.Elements.Count(e => !string.IsNullOrWhiteSpace(e.Documentation)) / candidate.Elements.Count
            : 0.0;
        score += documentedRatio * 0.15;

        return Math.Clamp(score, 0.0, 1.0);
    }
}
