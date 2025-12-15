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

    /// <summary>
    /// Finder "bedste" container-størrelse + antal, givet predicted kg/dag og frekvens.
    /// ExpectedFill er 0-1 (fx 0.97 = 97%).
    ///
    /// Scoring:
    /// - Vi prøver at ramme targetFill (fx 0.90-1.00)
    /// - Straf for overfyldning
    /// - Straf for underfyldning
    /// - Straf for mange containere
    /// - Lille straf for store containere (så 1100 ikke altid vinder)
    /// </summary>
    public EngineResult Recommend(
        double predKgPerDaySafe,
        double densityKgPerLiter,
        int frequencyDays,
        double targetFill = 0.95,
        double minFill = 0.80,
        double maxFill = 1.05,
        int maxContainers = 30)
    {
        if (predKgPerDaySafe <= 0) predKgPerDaySafe = 0;
        if (densityKgPerLiter <= 0) densityKgPerLiter = 0.13;
        frequencyDays = Math.Clamp(frequencyDays, 1, 90);

        // Est. liters pr day fra kg/day
        var litersPerDay = predKgPerDaySafe / densityKgPerLiter;

        EngineResult? best = null;
        double bestScore = double.PositiveInfinity;

        foreach (var size in Sizes)
        {
            // volumen pr tømning pr container
            var litersPerEmptyPerContainer = size;

            // est. liter pr tømning (for hele kunden)
            var litersPerEmptyTotal = litersPerDay * frequencyDays;

            // hvor mange containere kræves for at holde fill ~ target?
            // antal = litersPerEmptyTotal / (size * targetFill)
            var idealCount = litersPerEmptyTotal / (litersPerEmptyPerContainer * targetFill);
            var count = (int)Math.Ceiling(idealCount);

            count = Math.Clamp(count, 1, maxContainers);

            // beregn expected fill ved dette valg
            var totalCapacityLiters = count * litersPerEmptyPerContainer;
            var expectedFill = totalCapacityLiters <= 0 ? 0 : litersPerEmptyTotal / totalCapacityLiters;

            // score komponenter
            var underPenalty = expectedFill < minFill ? (minFill - expectedFill) * 10.0 : 0.0;
            var overPenalty  = expectedFill > maxFill ? (expectedFill - maxFill) * 25.0 : 0.0;

            // “distance” fra target
            var targetPenalty = Math.Abs(expectedFill - targetFill) * 4.0;

            // straf for mange containere (kraftig)
            var countPenalty = Math.Max(0, count - 1) * 0.35;

            // ekstra “anti-29x120”: straffer især mange små containere
            // (giver jer mere realistiske layouts)
            var smallManyPenalty = (size <= 240 ? Math.Max(0, count - 6) * 0.6 : 0.0);

            // lille straf for store containere (så 1100 ikke automatisk vinder)
            // 1100 får mest straf, 660 lidt, 240/120 næsten ingen
            var sizePenalty =
                size == 1100 ? 0.35 :
                size == 660  ? 0.15 :
                size == 240  ? 0.05 :
                0.02;

            // samlet score
            var score = underPenalty + overPenalty + targetPenalty + countPenalty + smallManyPenalty + sizePenalty;

            // vælg bedste
            if (score < bestScore)
            {
                bestScore = score;
                best = new EngineResult(size, count, frequencyDays, expectedFill);
            }
        }

        // fallback
        return best ?? new EngineResult(1100, 1, frequencyDays, 1.0);
    }
}
