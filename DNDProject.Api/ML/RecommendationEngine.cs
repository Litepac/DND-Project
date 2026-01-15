using System;

namespace DNDProject.Api.ML;

public sealed class RecommendationEngine
{
    public sealed record EngineResult(
        int ContainerSize,
        int ContainerCount,
        int FrequencyDays,
        double ExpectedFill
    );

    // standard størrelser (restaffald)
    private static readonly int[] Sizes = { 120, 240, 660, 1100 };

    // ================================
    // Public API (A): best for ONE fixed frequency
    // ================================
    public EngineResult Recommend(
        double predKgPerDaySafe,
        double densityKgPerLiter,
        int frequencyDays,
        double targetFill = 0.95,
        double minFill = 0.80,
        double maxFill = 1.05,
        int maxContainers = 30)
    {
        var (best, _) = RecommendWithScore(
            predKgPerDaySafe,
            densityKgPerLiter,
            frequencyDays,
            targetFill,
            minFill,
            maxFill,
            maxContainers
        );

        return best;
    }

    // ================================
    // Public API (B): best across FREQUENCY + containers
    // ================================
    public EngineResult RecommendBest(
        double predKgPerDaySafe,
        double densityKgPerLiter,
        int minFrequencyDays = 1,
        int maxFrequencyDays = 14,
        double targetFill = 0.95,
        double minFill = 0.80,
        double maxFill = 1.05,
        int maxContainers = 30,
        double pickupWeightPerYear = 0.03)
    {
        predKgPerDaySafe = Math.Max(0, predKgPerDaySafe);
        densityKgPerLiter = densityKgPerLiter > 0 ? densityKgPerLiter : 0.13;

        minFrequencyDays = Math.Clamp(minFrequencyDays, 1, 365);
        maxFrequencyDays = Math.Clamp(maxFrequencyDays, minFrequencyDays, 365);

        maxContainers = Math.Clamp(maxContainers, 1, 200);

        EngineResult? bestOverall = null;
        double bestOverallScore = double.PositiveInfinity;

        for (int freq = minFrequencyDays; freq <= maxFrequencyDays; freq++)
        {
            var (bestForFreq, scoreForFreq) = RecommendWithScore(
                predKgPerDaySafe,
                densityKgPerLiter,
                freq,
                targetFill,
                minFill,
                maxFill,
                maxContainers
            );

            // Pickup penalty: fewer days => more pickups/year => higher penalty
            // This makes frequency a real part of the optimization.
            double pickupsPerYear = 365.0 / freq;
            double pickupPenalty = pickupsPerYear * pickupWeightPerYear;

            double totalScore = scoreForFreq + pickupPenalty;

            if (totalScore < bestOverallScore)
            {
                bestOverallScore = totalScore;
                bestOverall = bestForFreq;
            }
        }

        return bestOverall ?? new EngineResult(1100, 1, Math.Clamp(minFrequencyDays, 1, 365), 1.0);
    }

    // ==========================================================
    // Internal: choose best (size,count) for a given frequency
    // Returns (result, score) so RecommendBest can compare freqs.
    // ==========================================================
    private static (EngineResult result, double score) RecommendWithScore(
        double predKgPerDaySafe,
        double densityKgPerLiter,
        int frequencyDays,
        double targetFill,
        double minFill,
        double maxFill,
        int maxContainers)
    {
        predKgPerDaySafe = Math.Max(0, predKgPerDaySafe);
        densityKgPerLiter = densityKgPerLiter > 0 ? densityKgPerLiter : 0.13;
        frequencyDays = Math.Clamp(frequencyDays, 1, 90);
        maxContainers = Math.Clamp(maxContainers, 1, 200);

        // Est. liters per day from kg/day
        var litersPerDay = predKgPerDaySafe / densityKgPerLiter;

        // Est. liters per emptying (for the whole customer)
        var litersPerEmptyTotal = litersPerDay * frequencyDays;

        EngineResult? best = null;
        double bestScore = double.PositiveInfinity;

        // Tolerance for tie-break stability
        const double TieEps = 0.05;

        foreach (var size in Sizes)
        {
            for (int count = 1; count <= maxContainers; count++)
            {
                var totalCapacityLiters = count * (double)size;
                if (totalCapacityLiters <= 0) continue;

                var expectedFill = litersPerEmptyTotal / totalCapacityLiters;

                double score = ScoreCandidate(
                    expectedFill: expectedFill,
                    size: size,
                    count: count,
                    targetFill: targetFill,
                    minFill: minFill,
                    maxFill: maxFill
                );

                bool isBetter = score < bestScore - 1e-12;

                bool isTieButPreferThis = false;
                if (!isBetter && best is not null && Math.Abs(score - bestScore) <= TieEps)
                {
                    // Tie-break:
                    // 1) fewer containers
                    // 2) larger size
                    // 3) closer to target fill
                    if (count < best.ContainerCount) isTieButPreferThis = true;
                    else if (count == best.ContainerCount && size > best.ContainerSize) isTieButPreferThis = true;
                    else if (count == best.ContainerCount && size == best.ContainerSize)
                    {
                        var thisDist = Math.Abs(expectedFill - targetFill);
                        var bestDist = Math.Abs(best.ExpectedFill - targetFill);
                        if (thisDist < bestDist) isTieButPreferThis = true;
                    }
                }

                if (isBetter || isTieButPreferThis)
                {
                    bestScore = score;
                    best = new EngineResult(size, count, frequencyDays, expectedFill);
                }
            }
        }

        return (best ?? new EngineResult(1100, 1, frequencyDays, 1.0), bestScore);
    }

    // ==========================================================
    // Scoring function (container decision quality)
    // ==========================================================
    private static double ScoreCandidate(
        double expectedFill,
        int size,
        int count,
        double targetFill,
        double minFill,
        double maxFill)
    {
        // Overfill should never "win"
        double overPenalty =
            expectedFill <= maxFill ? 0.0 :
            (expectedFill - maxFill) * 200.0;

        // Underfill is less critical than overfill
        double underPenalty =
            expectedFill >= minFill ? 0.0 :
            (minFill - expectedFill) * 25.0;

        // Prefer close to targetFill
        double targetPenalty = Math.Abs(expectedFill - targetFill) * 8.0;

        // Penalize many containers (space/logistics)
        double countPenalty = (count - 1) * 0.45;

        // Anti "many small" setups
        double smallManyPenalty =
            size <= 240 ? Math.Max(0, count - 6) * 1.2 : 0.0;

        // Slight bias so 1100 doesn't always win
        double sizePenalty =
            size == 1100 ? 0.20 :
            size == 660 ? 0.10 :
            size == 240 ? 0.05 :
            0.02;

        // Small bonus if <= 100%
        double under100Bonus = expectedFill <= 1.0 ? -0.10 : 0.0;

        return
            overPenalty +
            underPenalty +
            targetPenalty +
            countPenalty +
            smallManyPenalty +
            sizePenalty +
            under100Bonus;
    }
}
