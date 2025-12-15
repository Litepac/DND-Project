using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace DNDProject.Api.ML;

/// <summary>
/// ML-kerne (fra MAL.cs) – uden Excel.
/// Bruges af MLTrainerService.
/// </summary>
public static class MLCore
{
    // ---------- Konstanter / antagelser ----------
    public const double DensityKgPerL = 0.13;
    public static readonly int[] AllowedContainers = { 120, 240, 660, 1100 };

    public const int MaxDays = 14;

    public const double TargetMinFill = 0.80;
    public const double TargetMaxFill = 1.00;

    public const double SafetyK = 0.75;

    public const double PenaltyOver = 1200.0;
    public const double PenaltyUnder = 250.0;

    public const double PickupCostPerPickup = 1.0;

    // ---------- Input DTO (daily) ----------
    // Matcher dit SQL-baserede output fra MLDataService (PickupDailyDb).
    public record PickupDaily(
        string Skabelonnr,
        DateTime Date,
        double CollectedKg,
        string CustomerNo,
        string CustomerName
    );

    // ---------- Train row (features) ----------
    public record TrainRow
    {
        public string Skabelonnr { get; init; } = "";
        public string CustomerNo { get; init; } = "";
        public string CustomerName { get; init; } = "";
        public DateTime Date { get; init; }

        public float DaysSincePrev { get; init; }
        public float Month { get; init; }
        public float Weekday { get; init; }
        public float PrevCollectedKg { get; init; }

        public float AvgKgDay_Last3 { get; init; }
        public float AvgKgDay_Last5 { get; init; }
        public float StdKgDay_Last5 { get; init; }
        public float TrendKgDay_Last5 { get; init; }

        public float LabelKgPerDay { get; init; }
    }

    // ---------- ML.NET input/output ----------
    public sealed class ModelInput
    {
        public string Skabelonnr { get; set; } = "";
        public float DaysSincePrev { get; set; }
        public float Month { get; set; }
        public float Weekday { get; set; }
        public float PrevCollectedKg { get; set; }

        public float AvgKgDay_Last3 { get; set; }
        public float AvgKgDay_Last5 { get; set; }
        public float StdKgDay_Last5 { get; set; }
        public float TrendKgDay_Last5 { get; set; }

        [ColumnName("Label")]
        public float LabelKgPerDay { get; set; }
    }

    public sealed class ModelOutput
    {
        public float Score { get; set; }
    }

    public record CustomerRecommendation(
        string CustomerNo,
        string CustomerName,
        int ContainerL,
        int ContainerCount,
        int FrequencyDays,
        double ExpectedFill
    );

    // =====================================================================
    // 1) Feature engineering: daily -> TrainRow (rolling)
    // =====================================================================
    public static List<TrainRow> BuildTrainingRowsWithRolling(List<PickupDaily> daily)
    {
        var rows = new List<TrainRow>();

        foreach (var grp in daily.GroupBy(d => d.Skabelonnr))
        {
            var list = grp.OrderBy(x => x.Date).ToList();

            var kgDayHistory = new List<double>();

            for (int i = 1; i < list.Count; i++)
            {
                var prev = list[i - 1];
                var cur  = list[i];

                int days = (cur.Date - prev.Date).Days;
                if (days <= 0) continue;
                if (days > 180) continue;

                double kgPerDay = cur.CollectedKg / days;

                double avg3 = RollingAvg(kgDayHistory, 3);
                double avg5 = RollingAvg(kgDayHistory, 5);
                double std5 = RollingStd(kgDayHistory, 5);
                double tr5  = RollingTrend(kgDayHistory, 5);

                if (kgDayHistory.Count < 3) avg3 = kgPerDay;
                if (kgDayHistory.Count < 5) { avg5 = kgPerDay; std5 = 0; tr5 = 0; }

                rows.Add(new TrainRow
                {
                    Skabelonnr = cur.Skabelonnr,
                    CustomerNo = string.IsNullOrWhiteSpace(cur.CustomerNo) ? prev.CustomerNo : cur.CustomerNo,
                    CustomerName = string.IsNullOrWhiteSpace(cur.CustomerName) ? prev.CustomerName : cur.CustomerName,
                    Date = cur.Date,

                    DaysSincePrev = days,
                    Month = cur.Date.Month,
                    Weekday = ((int)cur.Date.DayOfWeek + 6) % 7,
                    PrevCollectedKg = (float)prev.CollectedKg,

                    AvgKgDay_Last3 = (float)avg3,
                    AvgKgDay_Last5 = (float)avg5,
                    StdKgDay_Last5 = (float)std5,
                    TrendKgDay_Last5 = (float)tr5,

                    LabelKgPerDay = (float)kgPerDay
                });

                kgDayHistory.Add(kgPerDay);
            }
        }

        return rows;

        static double RollingAvg(List<double> xs, int k)
        {
            if (xs.Count == 0) return 0;
            var take = xs.Skip(Math.Max(0, xs.Count - k)).ToArray();
            return take.Average();
        }

        static double RollingStd(List<double> xs, int k)
        {
            if (xs.Count < 2) return 0;
            var take = xs.Skip(Math.Max(0, xs.Count - k)).ToArray();
            if (take.Length < 2) return 0;
            double mean = take.Average();
            double var = take.Select(x => (x - mean) * (x - mean)).Sum() / (take.Length - 1);
            return Math.Sqrt(var);
        }

        static double RollingTrend(List<double> xs, int k)
        {
            var take = xs.Skip(Math.Max(0, xs.Count - k)).ToArray();
            int n = take.Length;
            if (n < 2) return 0;

            double sumX = 0, sumY = 0, sumXX = 0, sumXY = 0;
            for (int i = 0; i < n; i++)
            {
                double x = i;
                double y = take[i];
                sumX += x; sumY += y; sumXX += x * x; sumXY += x * y;
            }

            double denom = n * sumXX - sumX * sumX;
            if (Math.Abs(denom) < 1e-9) return 0;

            double slope = (n * sumXY - sumX * sumY) / denom;
            return slope;
        }
    }

    // =====================================================================
    // 2) Outlier clipping
    // =====================================================================
    public static List<TrainRow> ClipLabelOutliers(List<TrainRow> rows, double p = 0.99)
    {
        var labels = rows.Select(r => (double)r.LabelKgPerDay).OrderBy(x => x).ToArray();
        if (labels.Length < 10) return rows;

        int idx = (int)Math.Round(p * (labels.Length - 1));
        idx = Math.Max(0, Math.Min(labels.Length - 1, idx));
        double cap = labels[idx];

        double floor = 0;

        return rows.Select(r =>
        {
            double y = r.LabelKgPerDay;
            if (y > cap) y = cap;
            if (y < floor) y = floor;

            return r with { LabelKgPerDay = (float)y };
        }).ToList();
    }

    // =====================================================================
    // 3) Converters til ML.NET input
    // =====================================================================
    public static ModelInput ToModelInput(TrainRow r) => new()
    {
        Skabelonnr = r.Skabelonnr,
        DaysSincePrev = r.DaysSincePrev,
        Month = r.Month,
        Weekday = r.Weekday,
        PrevCollectedKg = r.PrevCollectedKg,
        AvgKgDay_Last3 = r.AvgKgDay_Last3,
        AvgKgDay_Last5 = r.AvgKgDay_Last5,
        StdKgDay_Last5 = r.StdKgDay_Last5,
        TrendKgDay_Last5 = r.TrendKgDay_Last5,
        LabelKgPerDay = r.LabelKgPerDay
    };

    public static ModelInput ToModelInputNoLabel(TrainRow r) => new()
    {
        Skabelonnr = r.Skabelonnr,
        DaysSincePrev = r.DaysSincePrev,
        Month = r.Month,
        Weekday = r.Weekday,
        PrevCollectedKg = r.PrevCollectedKg,
        AvgKgDay_Last3 = r.AvgKgDay_Last3,
        AvgKgDay_Last5 = r.AvgKgDay_Last5,
        StdKgDay_Last5 = r.StdKgDay_Last5,
        TrendKgDay_Last5 = r.TrendKgDay_Last5,
        LabelKgPerDay = 0f
    };

    // =====================================================================
    // 4) Safety: residual std pr. skabelon (eller global fallback)
    // =====================================================================
    public static Dictionary<string, double> ComputeResidualStdBySkabelonnr(
        MLContext ml, ITransformer model, List<TrainRow> trainRows)
    {
        var view = ml.Data.LoadFromEnumerable(trainRows.Select(ToModelInput));
        var pred = model.Transform(view);
        var preds = ml.Data.CreateEnumerable<ModelOutput>(pred, reuseRowObject: false).ToList();

        var residualsBySk = new Dictionary<string, List<double>>();

        for (int i = 0; i < trainRows.Count; i++)
        {
            var sk = trainRows[i].Skabelonnr.Trim();
            double resid = trainRows[i].LabelKgPerDay - preds[i].Score;

            if (!residualsBySk.TryGetValue(sk, out var list))
            {
                list = new List<double>();
                residualsBySk[sk] = list;
            }
            list.Add(resid);
        }

        static double Std(IEnumerable<double> xs)
        {
            var a = xs.ToArray();
            if (a.Length < 2) return 0;
            double mean = a.Average();
            double var = a.Select(x => (x - mean) * (x - mean)).Sum() / (a.Length - 1);
            return Math.Sqrt(var);
        }

        var outDict = new Dictionary<string, double>();

        var allResiduals = residualsBySk.Values.SelectMany(x => x).ToArray();
        outDict["__GLOBAL__"] = Std(allResiduals);

        foreach (var kv in residualsBySk)
        {
            if (kv.Value.Count >= 25)
                outDict[kv.Key] = Std(kv.Value);
        }

        return outDict;
    }

    // =====================================================================
    // 5) Recommendation: vælg container + frekvens (cost-based)
    // =====================================================================
    public static (int containerL, int containerCount, int freqDays, double expectedFill, string note)
        ChooseBestComboCostBased(double kgPerDayPredSafe)
    {
        double bestCost = double.PositiveInfinity;

        int bestC = AllowedContainers[0];
        int bestN = 1;
        int bestF = MaxDays;
        double bestFill = 0;
        string bestNote = "";

        const int MaxContainers = 30;

        foreach (var capL in AllowedContainers)
        {
            for (int freq = 1; freq <= MaxDays; freq++)
            {
                double volumeNeededL = kgPerDayPredSafe * freq / DensityKgPerL;

                for (int n = 1; n <= MaxContainers; n++)
                {
                    double totalCap = n * capL;
                    double fill = volumeNeededL / totalCap;

                    double under = Math.Max(0, TargetMinFill - fill);
                    double over  = Math.Max(0, fill - TargetMaxFill);

                    double pickupsPerYear = 365.0 / freq;

                    double cost =
                        (PenaltyUnder * under) +
                        (PenaltyOver * over) +
                        (PickupCostPerPickup * pickupsPerYear);

                    bool better =
                        cost < bestCost ||
                        (Math.Abs(cost - bestCost) < 1e-9 && fill > bestFill);

                    if (better)
                    {
                        bestCost = cost;
                        bestC = capL;
                        bestN = n;
                        bestF = freq;
                        bestFill = fill;

                        bestNote =
                            fill < TargetMinFill
                                ? "Underfilled – extra container added"
                                : fill > TargetMaxFill
                                    ? "Overfill risk – extra container added"
                                    : "";
                    }
                }
            }
        }

        return (bestC, bestN, bestF, bestFill, bestNote);
    }
}
