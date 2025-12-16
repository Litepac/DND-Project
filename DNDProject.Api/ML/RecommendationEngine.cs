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
    /// Finder bedste container-størrelse + antal, givet predicted kg/dag og frekvens.
    /// ExpectedFill er 0-1 (fx 0.97 = 97%).
    ///
    /// Vi scorer alle kombinationer af (size, count) for at undgå "clamp-fejl".
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
        predKgPerDaySafe = Math.Max(0, predKgPerDaySafe);
        densityKgPerLiter = densityKgPerLiter > 0 ? densityKgPerLiter : 0.13;
        frequencyDays = Math.Clamp(frequencyDays, 1, 90);
        maxContainers = Math.Clamp(maxContainers, 1, 200);

        // Est. liters pr day fra kg/day
        var litersPerDay = predKgPerDaySafe / densityKgPerLiter;

        // Est. liter pr tømning (for hele kunden)
        var litersPerEmptyTotal = litersPerDay * frequencyDays;

        EngineResult? best = null;
        double bestScore = double.PositiveInfinity;

        foreach (var size in Sizes)
        {
            for (int count = 1; count <= maxContainers; count++)
            {
                var totalCapacityLiters = count * (double)size;
                if (totalCapacityLiters <= 0) continue;

                var expectedFill = litersPerEmptyTotal / totalCapacityLiters;

                // ---- penalties ----

                // Overfyldning skal ALDRIG "vinde" (vi straffer hårdt)
                // Især hvis vi er langt over maxFill.
                double overPenalty =
                    expectedFill <= maxFill ? 0.0 :
                    (expectedFill - maxFill) * 200.0;   // 🔥 hård straf

                // Underfyldning er "træls", men mindre kritisk end overfyld
                double underPenalty =
                    expectedFill >= minFill ? 0.0 :
                    (minFill - expectedFill) * 25.0;

                // Vi vil gerne ligge tæt på targetFill
                double targetPenalty = Math.Abs(expectedFill - targetFill) * 8.0;

                // Straf for mange containere (logistik/plads)
                double countPenalty = (count - 1) * 0.45;

                // Ekstra straf for "mange små" (anti 29x120)
                double smallManyPenalty =
                    size <= 240 ? Math.Max(0, count - 6) * 1.2 : 0.0;

                // Lille straf for store containere (så 1100 ikke altid vinder)
                // (men den er lille nok til at 1100 stadig vinder når den er klart bedst)
                double sizePenalty =
                    size == 1100 ? 0.20 :
                    size == 660 ? 0.10 :
                    size == 240 ? 0.05 :
                    0.02;

                // Bonus (lille) hvis vi holder os under 100% (psykologisk/realistisk)
                // (Gør at 0.99 ofte slår 1.03 hvis alt andet er lige)
                double under100Bonus = expectedFill <= 1.0 ? -0.10 : 0.0;

                var score =
                    overPenalty +
                    underPenalty +
                    targetPenalty +
                    countPenalty +
                    smallManyPenalty +
                    sizePenalty +
                    under100Bonus;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = new EngineResult(size, count, frequencyDays, expectedFill);
                }
            }
        }

        // fallback
        return best ?? new EngineResult(1100, 1, frequencyDays, 1.0);
    }
}
