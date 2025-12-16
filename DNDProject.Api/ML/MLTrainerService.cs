using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML;

namespace DNDProject.Api.ML;

public sealed class MLTrainerService
{
    private readonly MLDataService _data;
    private readonly MLModelStore _store;

    public MLTrainerService(MLDataService data, MLModelStore store)
    {
        _data = data;
        _store = store;
    }

    // A) Train metrics (samme struktur som swagger)
    public sealed record TrainResult(
        int Rows,
        double TestFraction,
        double Mae,
        double Rmse,
        double Rsquared,
        string Message
    );

    // -----------------------------
    // A) Train + tune + cache model
    // -----------------------------
    public async Task<TrainResult> TrainAsync(DateTime from, DateTime to)
    {
        // 1) Hent daily fra DB
        var dailyDb = await _data.LoadDailyPickupsAsync(from, to);

        // 2) Map til MLCore.PickupDaily
        var daily = dailyDb.Select(x => new MLCore.PickupDaily(
            Skabelonnr: x.Skabelonnr,
            Date: x.Date,
            CollectedKg: x.CollectedKg,
            CustomerNo: x.CustomerNo,
            CustomerName: x.CustomerName
        )).ToList();

        if (daily.Count < 50)
            return new TrainResult(0, 0.2, 0, 0, 0, "For få daily-rækker.");

        // 3) Feature engineering
        var rows = MLCore.BuildTrainingRowsWithRolling(daily);

        if (rows.Count < 300)
            return new TrainResult(rows.Count, 0.2, 0, 0, 0, "For få træningsrækker (efter rolling).");

        rows = MLCore.ClipLabelOutliers(rows, p: 0.99);

        // 4) Split 60/20/20 (train/val/test) – shuffle først
        var rnd = new Random(42);
        rows = rows.OrderBy(_ => rnd.Next()).ToList();

        int n = rows.Count;
        int trainCount = (int)(n * 0.60);
        int valCount = (int)(n * 0.20);
        int testCount = n - trainCount - valCount;

        // safety guard
        trainCount = Math.Max(1, trainCount);
        valCount = Math.Max(1, valCount);
        testCount = Math.Max(1, testCount);

        // justér hvis rounding giver problemer
        if (trainCount + valCount + testCount != n)
            testCount = n - trainCount - valCount;

        var trainRows = rows.Take(trainCount).ToList();
        var valRows = rows.Skip(trainCount).Take(valCount).ToList();
        var testRows = rows.Skip(trainCount + valCount).Take(testCount).ToList();

        // 5) ML
        var ml = new MLContext(seed: 42);

        var trainView = ml.Data.LoadFromEnumerable(trainRows.Select(MLCore.ToModelInput));
        var valView = ml.Data.LoadFromEnumerable(valRows.Select(MLCore.ToModelInput));
        var testView = ml.Data.LoadFromEnumerable(testRows.Select(MLCore.ToModelInput));

        var basePipe =
            ml.Transforms.Categorical.OneHotEncoding("SkabelonnrOneHot", nameof(MLCore.ModelInput.Skabelonnr))
              .Append(ml.Transforms.Concatenate("Features",
                  "SkabelonnrOneHot",
                  nameof(MLCore.ModelInput.DaysSincePrev),
                  nameof(MLCore.ModelInput.Month),
                  nameof(MLCore.ModelInput.Weekday),
                  nameof(MLCore.ModelInput.PrevCollectedKg),
                  nameof(MLCore.ModelInput.AvgKgDay_Last3),
                  nameof(MLCore.ModelInput.AvgKgDay_Last5),
                  nameof(MLCore.ModelInput.StdKgDay_Last5),
                  nameof(MLCore.ModelInput.TrendKgDay_Last5)
              ));

        // Kandidater (tuning på VAL_MAE)
        var candidates = new (int leaves, int trees, int minLeaf, float lr)[]
        {
            (20,  300, 10, 0.05f),
            (50,  500,  5, 0.03f),
            (80,  800,  5, 0.02f),
            (100, 1200, 10, 0.015f),
        };

        (int leaves, int trees, int minLeaf, float lr) best = candidates[0];
        double bestValMae = double.PositiveInfinity;
        double bestValRmse = 0;
        double bestValR2 = 0;

        foreach (var c in candidates)
        {
            var trainer = ml.Regression.Trainers.FastTree(
                numberOfLeaves: c.leaves,
                numberOfTrees: c.trees,
                minimumExampleCountPerLeaf: c.minLeaf,
                learningRate: c.lr);

            var model = basePipe.Append(trainer).Fit(trainView);

            var valPred = model.Transform(valView);
            var valMetrics = ml.Regression.Evaluate(valPred, labelColumnName: "Label", scoreColumnName: "Score");

            if (valMetrics.MeanAbsoluteError < bestValMae)
            {
                bestValMae = valMetrics.MeanAbsoluteError;
                bestValRmse = valMetrics.RootMeanSquaredError;
                bestValR2 = valMetrics.RSquared;
                best = c;
            }
        }

        var bestDesc = $"FastTree leaves={best.leaves} trees={best.trees} minLeaf={best.minLeaf} lr={best.lr}";

        // 6) Retrain FINAL model på train + val
        var trainPlusValRows = trainRows.Concat(valRows).ToList();
        var trainPlusValView = ml.Data.LoadFromEnumerable(trainPlusValRows.Select(MLCore.ToModelInput));

        var finalTrainer = ml.Regression.Trainers.FastTree(
            numberOfLeaves: best.leaves,
            numberOfTrees: best.trees,
            minimumExampleCountPerLeaf: best.minLeaf,
            learningRate: best.lr);

        var finalModel = basePipe.Append(finalTrainer).Fit(trainPlusValView);

        // 7) Test metrics
        var testPred = finalModel.Transform(testView);
        var testMetrics = ml.Regression.Evaluate(testPred, labelColumnName: "Label", scoreColumnName: "Score");

        // 8) Compute residual std på train+val (samme data final modellen ser)
        var residualStdBySk = MLCore.ComputeResidualStdBySkabelonnr(ml, finalModel, trainPlusValRows);

        // 9) Gem i memory store
        _store.Set(finalModel, residualStdBySk, from, to);

        return new TrainResult(
            Rows: rows.Count,
            TestFraction: 0.20,
            Mae: testMetrics.MeanAbsoluteError,
            Rmse: testMetrics.RootMeanSquaredError,
            Rsquared: testMetrics.RSquared,
            Message: $"OK | split=60/20/20 | BEST={bestDesc} | VAL: MAE={bestValMae:0.###}, RMSE={bestValRmse:0.###}, R²={bestValR2:0.###} | cached"
        );
    }

    // -----------------------------
    // B) Recommendations (bruger cached model)
    // -----------------------------
    public sealed record RecommendRow(
        string CustomerNo,
        string CustomerName,
        int ContainerL,
        int ContainerCount,
        int FrequencyDays,
        double ExpectedFill,
        double PredKgPerDaySafe
    );

    public async Task<List<RecommendRow>> RecommendAsync(DateTime from, DateTime to, int take = 500)
    {
        // 0) Sikr model
        if (!_store.TryGet(out var model, out var residualStdBySk, out _, out _, out _))
        {
            await TrainAsync(from, to);

            if (!_store.TryGet(out model, out residualStdBySk, out _, out _, out _))
                return new();
        }

        // 1) Hent daily fra DB
        var dailyDb = await _data.LoadDailyPickupsAsync(from, to);

        var daily = dailyDb.Select(x => new MLCore.PickupDaily(
            x.Skabelonnr, x.Date, x.CollectedKg, x.CustomerNo, x.CustomerName
        )).ToList();

        if (daily.Count < 50) return new();

        // 2) Features
        var rows = MLCore.BuildTrainingRowsWithRolling(daily);
        if (rows.Count < 300) return new();

        rows = MLCore.ClipLabelOutliers(rows, p: 0.99);

        var ml = new MLContext(seed: 42);

        // 3) Seneste row pr (customer, skabelon)
        var latestByCustomer = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.CustomerNo))
            .GroupBy(r => (r.CustomerNo, r.CustomerName))
            .Select(g => new
            {
                g.Key.CustomerNo,
                g.Key.CustomerName,
                Rows = g.GroupBy(x => x.Skabelonnr)
                        .Select(sg => sg.OrderByDescending(x => x.Date).First())
                        .ToList()
            })
            .ToList();

        var outList = new List<RecommendRow>();

        foreach (var c in latestByCustomer)
        {
            var inputs = c.Rows.Select(MLCore.ToModelInputNoLabel).ToList();
            var dv = ml.Data.LoadFromEnumerable(inputs);

            var pred = model.Transform(dv);
            var preds = ml.Data.CreateEnumerable<MLCore.ModelOutput>(pred, reuseRowObject: false).ToList();

            double totalKgPerDaySafe = 0;

            for (int i = 0; i < preds.Count; i++)
            {
                var sk = c.Rows[i].Skabelonnr.Trim();
                var residStd = residualStdBySk.TryGetValue(sk, out var s) ? s : residualStdBySk["__GLOBAL__"];
                totalKgPerDaySafe += preds[i].Score + (MLCore.SafetyK * residStd);
            }

            var (capL, n, freq, fill, _) = MLCore.ChooseBestComboCostBased(totalKgPerDaySafe);

            outList.Add(new RecommendRow(
                c.CustomerNo,
                c.CustomerName,
                capL,
                n,
                freq,
                fill,
                totalKgPerDaySafe
            ));
        }

        return outList
            .OrderByDescending(x => x.PredKgPerDaySafe)
            .Take(Math.Max(1, take))
            .ToList();
    }

    // DTO til "1 kunde" (til Blazor detaljevisning)
    public sealed record RecommendationDto(
        string CustomerNo,
        string CustomerName,
        int ContainerL,
        int ContainerCount,
        int FrequencyDays,
        double ExpectedFill,
        double PredKgPerDaySafe
    );

    public async Task<RecommendationDto?> RecommendOneAsync(DateTime from, DateTime to, string customerNo)
    {
        if (string.IsNullOrWhiteSpace(customerNo)) return null;
        customerNo = customerNo.Trim();

        // 0) Sikr model
        if (!_store.TryGet(out var model, out var residualStdBySk, out _, out _, out _))
        {
            await TrainAsync(from, to);

            if (!_store.TryGet(out model, out residualStdBySk, out _, out _, out _))
                return null;
        }

        // 1) Hent data kun for kunden hvis muligt (numeric)
        List<MLDataService.PickupDailyDb> dailyDb;

        if (int.TryParse(customerNo, out var custKey))
            dailyDb = await _data.LoadDailyPickupsForCustomerAsync(from, to, custKey);
        else
            dailyDb = await _data.LoadDailyPickupsAsync(from, to);

        var daily = dailyDb.Select(x => new MLCore.PickupDaily(
            Skabelonnr: x.Skabelonnr,
            Date: x.Date,
            CollectedKg: x.CollectedKg,
            CustomerNo: x.CustomerNo,
            CustomerName: x.CustomerName
        )).ToList();

        if (daily.Count < 10) return null;

        // 2) Features
        var rows = MLCore.BuildTrainingRowsWithRolling(daily);
        if (rows.Count < 10) return null;

        rows = MLCore.ClipLabelOutliers(rows, p: 0.99);

        // 3) Find seneste row pr skabelon for kunden
        var latestRows = rows
            .Where(r => string.Equals(r.CustomerNo, customerNo, StringComparison.OrdinalIgnoreCase))
            .GroupBy(r => r.Skabelonnr)
            .Select(g => g.OrderByDescending(x => x.Date).First())
            .ToList();

        if (latestRows.Count == 0) return null;

        var custName = latestRows.Select(x => x.CustomerName)
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "";

        var ml = new MLContext(seed: 42);

        // 4) Predict + safety sum
        var inputs = latestRows.Select(MLCore.ToModelInputNoLabel).ToList();
        var dv = ml.Data.LoadFromEnumerable(inputs);

        var pred = model.Transform(dv);
        var preds = ml.Data.CreateEnumerable<MLCore.ModelOutput>(pred, reuseRowObject: false).ToList();

        double totalKgPerDaySafe = 0;

        for (int i = 0; i < preds.Count; i++)
        {
            var sk = latestRows[i].Skabelonnr.Trim();
            var residStd = residualStdBySk.TryGetValue(sk, out var s) ? s : residualStdBySk["__GLOBAL__"];
            totalKgPerDaySafe += preds[i].Score + (MLCore.SafetyK * residStd);
        }

        var (capL, n, freq, fill, _) = MLCore.ChooseBestComboCostBased(totalKgPerDaySafe);

        return new RecommendationDto(
            CustomerNo: customerNo,
            CustomerName: custName,
            ContainerL: capL,
            ContainerCount: n,
            FrequencyDays: freq,
            ExpectedFill: fill,
            PredKgPerDaySafe: totalKgPerDaySafe
        );
    }

    // Quick sanity check
    public async Task<int> LoadCountAsync(DateTime from, DateTime to)
    {
        var daily = await _data.LoadDailyPickupsAsync(from, to);
        return daily.Count;
    }
}
