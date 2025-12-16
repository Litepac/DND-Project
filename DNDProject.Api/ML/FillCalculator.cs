using System;

namespace DNDProject.Api.ML.Tools; // <-- andet namespace

public static class FillCalculator
{
    public static double ExpectedFill(
        double kgPerDay,
        double densityKgPerLiter,
        int frequencyDays,
        int containerSizeLiters,
        int containerCount)
    {
        kgPerDay = Math.Max(0, kgPerDay);
        densityKgPerLiter = densityKgPerLiter > 0 ? densityKgPerLiter : 0.13;
        frequencyDays = Math.Max(1, frequencyDays);
        containerSizeLiters = Math.Max(1, containerSizeLiters);
        containerCount = Math.Max(1, containerCount);

        double litersPerDay = kgPerDay / densityKgPerLiter;
        double litersPerEmptyTotal = litersPerDay * frequencyDays;
        double totalCapacityLiters = containerCount * (double)containerSizeLiters;

        return litersPerEmptyTotal / totalCapacityLiters; // 0..1+
    }
}
