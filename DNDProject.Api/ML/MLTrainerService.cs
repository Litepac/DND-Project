using System.Linq;
using Microsoft.ML;

namespace DNDProject.Api.ML;

public sealed class MLTrainerService
{
    private readonly MLDataService _data;

    public MLTrainerService(MLDataService data)
    {
        _data = data;
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

        // 4) Split 80/20
        const double testFraction = 0.2;
        int n = rows.Count;
        int testCount = (int)Math.Round(n * testFraction);
        testCount = Math.Max(1, Math.Min(testCount, n - 1));

        var trainRows = rows.Take(n - testCount).ToList();
        var testRows = rows.Skip(n - testCount).ToList();

        // 5) Train FastTree
        var ml = new MLContext(seed: 42);

        var trainView = ml.Data.LoadFromEnumerable(trainRows.Select(MLCore.ToModelInput));
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

        var trainer = ml.Regression.Trainers.FastTree(
            numberOfLeaves: 50,
            numberOfTrees: 500,
            minimumExampleCountPerLeaf: 5,
            learningRate: 0.03f);

        var model = basePipe.Append(trainer).Fit(trainView);

        var testPred = model.Transform(testView);
        var metrics = ml.Regression.Evaluate(testPred, labelColumnName: "Label", scoreColumnName: "Score");

        return new TrainResult(
            Rows: rows.Count,
            TestFraction: testFraction,
            Mae: metrics.MeanAbsoluteError,
            Rmse: metrics.RootMeanSquaredError,
            Rsquared: metrics.RSquared,
            Message: "OK"
        );
    }

    // -----------------------------
    // B) Recommendations (predict -> container+frekvens)
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
        var dailyDb = await _data.LoadDailyPickupsAsync(from, to);

        var daily = dailyDb.Select(x => new MLCore.PickupDaily(
            x.Skabelonnr, x.Date, x.CollectedKg, x.CustomerNo, x.CustomerName
        )).ToList();

        if (daily.Count < 50) return new();

        var rows = MLCore.BuildTrainingRowsWithRolling(daily);
        if (rows.Count < 300) return new();

        rows = MLCore.ClipLabelOutliers(rows, p: 0.99);

        var ml = new MLContext(seed: 42);

        var view = ml.Data.LoadFromEnumerable(rows.Select(MLCore.ToModelInput));

        var pipe =
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
              ))
              .Append(ml.Regression.Trainers.FastTree(
                  numberOfLeaves: 50,
                  numberOfTrees: 500,
                  minimumExampleCountPerLeaf: 5,
                  learningRate: 0.03f));

        var model = pipe.Fit(view);

        // safety via residual std
        var residualStdBySk = MLCore.ComputeResidualStdBySkabelonnr(ml, model, rows);

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
        var all = await RecommendAsync(from, to, take: 5000);

        var hit = all.FirstOrDefault(x =>
            string.Equals(x.CustomerNo, customerNo, StringComparison.OrdinalIgnoreCase));

        if (hit is null) return null;

        return new RecommendationDto(
            hit.CustomerNo,
            hit.CustomerName,
            hit.ContainerL,
            hit.ContainerCount,
            hit.FrequencyDays,
            hit.ExpectedFill,
            hit.PredKgPerDaySafe
        );
    }

    // Quick sanity check
    public async Task<int> LoadCountAsync(DateTime from, DateTime to)
    {
        var daily = await _data.LoadDailyPickupsAsync(from, to);
        return daily.Count;
    }
}
