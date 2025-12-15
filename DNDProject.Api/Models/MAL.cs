using System.Data;
using System.Globalization;
using System.Text;
using ExcelDataReader;
using Microsoft.ML;
using Microsoft.ML.Data;
using System.Linq;

public record ModtagRow(string Skabelonnr, DateTime KoresDato, string Enhed, double Antal, string CustomerNo, string CustomerName);
public record KoerselRow(string Skabelonnr, string Varenummer);
public record KapRow(string Varenummer, int KapacitetL, string Enhed);
public record PickupDaily(string Skabelonnr, DateTime Date, double CollectedKg, string CustomerNo, string CustomerName);

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

public class ModelInput
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

public class ModelOutput
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

public static class MLTrainingRunner
{
    const double DensityKgPerL = 0.13;
    static readonly int[] AllowedContainers = { 120, 240, 660, 1100, };

    const int MaxDays = 14;

    const double TargetMinFill = 0.80;
    const double TargetMaxFill = 1.00;

    const double SafetyK = 0.75;

    const double PenaltyOver = 1200.0;
    const double PenaltyUnder = 250.0;

    const double PickupCostPerPickup = 1.0;

    static readonly string ModtagFileName  = "Modtagelsesinformationer 01-01-2024 31-12-2024.xlsx";
    static readonly string KoerselFileName = "Kørselsordrer.xlsx";
    static readonly string KapFileName     = "Kapacitet_og_enhed_opdateret.xlsx";

    public static void Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        Console.WriteLine("CurrentDirectory: " + Environment.CurrentDirectory);
        Console.WriteLine("BaseDirectory:    " + AppContext.BaseDirectory);

        var modtagPath  = ResolveFile(ModtagFileName);
        var koerselPath = ResolveFile(KoerselFileName);
        var kapPath     = ResolveFile(KapFileName);

        Console.WriteLine("\nUsing files:");
        Console.WriteLine($"  Modtagelser:     {modtagPath}");
        Console.WriteLine($"  Kørselsordrer:   {koerselPath}");
        Console.WriteLine($"  Kapacitet:       {kapPath}");

        var modtag  = ReadModtagelser(modtagPath);
        var koersel = ReadKoerselordrer(koerselPath);
        var kap     = ReadKapacitetXlsx(kapPath);

        var kapDict = kap
            .Where(k => k.Enhed.Equals("L", StringComparison.OrdinalIgnoreCase))
            .Where(k => AllowedContainers.Contains(k.KapacitetL))
            .GroupBy(k => k.Varenummer)
            .Select(g => g.First())
            .ToDictionary(x => x.Varenummer, x => x.KapacitetL);

        var daily = modtag
            .Where(m => m.Enhed.Equals("KG", StringComparison.OrdinalIgnoreCase))
            .GroupBy(m => (Sk: m.Skabelonnr.Trim(), Dt: m.KoresDato.Date))
            .Select(g => new PickupDaily(
                Skabelonnr: g.Key.Sk,
                Date: g.Key.Dt,
                CollectedKg: g.Sum(x => x.Antal),
                CustomerNo: MostCommonNonEmpty(g.Select(x => x.CustomerNo)),
                CustomerName: MostCommonNonEmpty(g.Select(x => x.CustomerName))
            ))
            .OrderBy(x => x.Skabelonnr).ThenBy(x => x.Date)
            .ToList();

        if (daily.Count < 50)
        {
            Console.WriteLine("For få daily-rækker. Tjek at Modtagelser har KG-data og datoer.");
            return;
        }
        var rows = BuildTrainingRowsWithRolling(daily);

        if (rows.Count < 300)
        {
            Console.WriteLine("For få træningsrækker (efter rolling).");
            return;
        }

        rows = ClipLabelOutliers(rows, p: 0.99);

        var trainEnd = new DateTime(2024, 10, 31);
        var valEnd   = new DateTime(2024, 11, 30);

        var trainRows = rows.Where(r => r.Date <= trainEnd).ToList();
        var valRows   = rows.Where(r => r.Date > trainEnd && r.Date <= valEnd).ToList();
        var testRows  = rows.Where(r => r.Date > valEnd).ToList();

        Console.WriteLine($"\nRows: train={trainRows.Count}, val={valRows.Count}, test={testRows.Count}");

        var ml = new MLContext(seed: 42);

        var trainView = ml.Data.LoadFromEnumerable(trainRows.Select(ToModelInput));
        var valView   = ml.Data.LoadFromEnumerable(valRows.Select(ToModelInput));
        var testView  = ml.Data.LoadFromEnumerable(testRows.Select(ToModelInput));

        var basePipe =
            ml.Transforms.Categorical.OneHotEncoding("SkabelonnrOneHot", nameof(ModelInput.Skabelonnr))
              .Append(ml.Transforms.Concatenate("Features",
                  "SkabelonnrOneHot",
                  nameof(ModelInput.DaysSincePrev),
                  nameof(ModelInput.Month),
                  nameof(ModelInput.Weekday),
                  nameof(ModelInput.PrevCollectedKg),
                  nameof(ModelInput.AvgKgDay_Last3),
                  nameof(ModelInput.AvgKgDay_Last5),
                  nameof(ModelInput.StdKgDay_Last5),
                  nameof(ModelInput.TrendKgDay_Last5)
              ));

        var candidates = new (int leaves, int trees, int minLeaf, float lr)[]
        {
            (20, 300, 10, 0.05f),
            (50, 500,  5, 0.03f),
            (80, 800,  5, 0.02f),
            (100,1200, 10, 0.015f),
        };

        ITransformer? bestModel = null;
        double bestValMae = double.PositiveInfinity;
        string bestDesc = "";

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

            var desc = $"FastTree leaves={c.leaves} trees={c.trees} minLeaf={c.minLeaf} lr={c.lr}";
            Console.WriteLine($"{desc} -> VAL_MAE={valMetrics.MeanAbsoluteError:F4}");

            if (valMetrics.MeanAbsoluteError < bestValMae)
            {
                bestValMae = valMetrics.MeanAbsoluteError;
                bestModel = model;
                bestDesc = desc;
            }
        }

        if (bestModel == null)
        {
            Console.WriteLine("Kunne ikke træne model.");
            return;
        }

        Console.WriteLine($"\nBEST: {bestDesc} (VAL_MAE={bestValMae:F4})");

        var testPred = bestModel.Transform(testView);
        var testMetrics = ml.Regression.Evaluate(testPred, labelColumnName: "Label", scoreColumnName: "Score");

        Console.WriteLine($"TEST_MAE:  {testMetrics.MeanAbsoluteError:F4}");
        Console.WriteLine($"TEST_RMSE: {testMetrics.RootMeanSquaredError:F4}");
        Console.WriteLine($"TEST_R2:   {testMetrics.RSquared:F4}");

     var residualStdBySkabelon = ComputeResidualStdBySkabelonnr(ml, bestModel, trainRows);

        var testPredEnum = ml.Data.CreateEnumerable<ModelOutput>(testPred, reuseRowObject: false).ToList();

        int ok = 0, under = 0, over = 0;
        double avgTrueFill = 0;

        for (int i = 0; i < testRows.Count; i++)
        {
            var sk = testRows[i].Skabelonnr.Trim();

            var predKgDay = testPredEnum[i].Score;
            var residStd = residualStdBySkabelon.TryGetValue(sk, out var s) ? s : residualStdBySkabelon["__GLOBAL__"];

            var predSafe = predKgDay + (float)(SafetyK * residStd);

            var (containerL, containerCount, freqDays, expectedFill, note) =
    ChooseBestComboCostBased(predSafe);


            var trueKgDay = testRows[i].LabelKgPerDay;
            double fillTrue =
    (trueKgDay * freqDays / DensityKgPerL) /
    (containerCount * containerL);


            avgTrueFill += fillTrue;

            if (fillTrue >= TargetMinFill && fillTrue <= TargetMaxFill) ok++;
            else if (fillTrue < TargetMinFill) under++;
            else over++;
        }

        int nTest = testRows.Count;
        avgTrueFill /= Math.Max(1, nTest);

        Console.WriteLine("\n--- Recommendation accuracy on TEST (cost-based + safety) ---");
        Console.WriteLine($"OK (80–100%):  {ok}/{nTest} = {(double)ok / Math.Max(1, nTest):P1}");
        Console.WriteLine($"Under (<80%):  {under}/{nTest} = {(double)under / Math.Max(1, nTest):P1}");
        Console.WriteLine($"Over  (>100%): {over}/{nTest} = {(double)over / Math.Max(1, nTest):P1}");
        Console.WriteLine($"Avg TRUE fill: {avgTrueFill:P1}");

        var latestByCustomer = rows
    .Where(r => !string.IsNullOrWhiteSpace(r.CustomerNo))
    .GroupBy(r => (r.CustomerNo, r.CustomerName))
    .Select(g =>
    {
        var latestPerSkabelon = g
            .GroupBy(x => x.Skabelonnr)
            .Select(sg => sg.OrderByDescending(x => x.Date).First())
            .ToList();

        return new
        {
            g.Key.CustomerNo,
            g.Key.CustomerName,
            Rows = latestPerSkabelon
        };
    })
    .ToList();

var customerRecs = new List<CustomerRecommendation>();

foreach (var c in latestByCustomer)
{
    var inputs = c.Rows.Select(ToModelInputNoLabel).ToList();
    var view = ml.Data.LoadFromEnumerable(inputs);
    var pred = bestModel.Transform(view);
    var preds = ml.Data.CreateEnumerable<ModelOutput>(pred, false).ToList();

    double totalKgPerDay = 0;

    for (int i = 0; i < preds.Count; i++)
    {
        var sk = c.Rows[i].Skabelonnr;

        var residStd = residualStdBySkabelon.TryGetValue(sk, out var s)
            ? s
            : residualStdBySkabelon["__GLOBAL__"];

        totalKgPerDay += preds[i].Score + SafetyK * residStd;
    }

    var (capL, n, freq, fill, _) =
        ChooseBestComboCostBased(totalKgPerDay);

    customerRecs.Add(new CustomerRecommendation(
        c.CustomerNo,
        c.CustomerName,
        capL,
        n,
        freq,
        fill
    ));
}
Console.WriteLine();
Console.WriteLine("--- SØGNING ---");
Console.WriteLine("Skriv kundenr eller kundenavn. Skriv 'exit' for at stoppe.");

while (true)
{
    Console.Write("\nSøg: ");
    var q = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(q))
        continue;

    if (q.Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

    var matches = customerRecs
        .Where(r =>
            r.CustomerNo.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            r.CustomerName.Contains(q, StringComparison.OrdinalIgnoreCase))
        .ToList();

    if (matches.Count == 0)
    {
        Console.WriteLine("Ingen match.");
        continue;
    }

    foreach (var r in matches)
    {
        Console.WriteLine(
            $"{r.CustomerNo} - {r.CustomerName}: " +
            $"{r.ContainerCount} × {r.ContainerL}L hver {r.FrequencyDays} dage | " +
            $"expectedFill={r.ExpectedFill:P0}"
        );
    }
}

}

   static (int containerL, int containerCount, int freqDays, double expectedFill, string note)
ChooseBestComboCostBased(double kgPerDayPredSafe)
{
    double bestCost = double.PositiveInfinity;

    int bestC = AllowedContainers[0];
    int bestN = 1;
    int bestF = 14;
    double bestFill = 0;
    string bestNote = "";

    const int MaxContainers = 5;

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
                double over = Math.Max(0, fill - TargetMaxFill);

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


    static Dictionary<string, double> ComputeResidualStdBySkabelonnr(MLContext ml, ITransformer model, List<TrainRow> trainRows)
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

        double GlobalStd(IEnumerable<double> xs)
        {
            var a = xs.ToArray();
            if (a.Length < 2) return 0;
            double mean = a.Average();
            double var = a.Select(x => (x - mean) * (x - mean)).Sum() / (a.Length - 1);
            return Math.Sqrt(var);
        }

        var outDict = new Dictionary<string, double>();
        var allResiduals = residualsBySk.Values.SelectMany(x => x).ToArray();
        outDict["__GLOBAL__"] = GlobalStd(allResiduals);

        foreach (var kv in residualsBySk)
        {
            if (kv.Value.Count >= 25) outDict[kv.Key] = GlobalStd(kv.Value);
        }

        return outDict;
    }

    static List<TrainRow> BuildTrainingRowsWithRolling(List<PickupDaily> daily)
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

    static List<TrainRow> ClipLabelOutliers(List<TrainRow> rows, double p = 0.99)
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

    static ModelInput ToModelInput(TrainRow r) => new()
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

    static ModelInput ToModelInputNoLabel(TrainRow r) => new()
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

    static string ResolveFile(string fileName)
    {
        static string Norm(string s) => new string((s ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        var wanted = Norm(fileName);

        var baseDir = AppContext.BaseDirectory;
        var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
        var parentRoot  = Directory.GetParent(projectRoot)?.FullName ?? projectRoot;

        var roots = new List<string> { Environment.CurrentDirectory, baseDir, projectRoot, parentRoot };

        var cur = new DirectoryInfo(Environment.CurrentDirectory);
        for (int i = 0; i < 8 && cur.Parent != null; i++) { roots.Add(cur.FullName); cur = cur.Parent; }

        foreach (var root in roots.Distinct())
        {
            var p = Path.Combine(root, fileName);
            if (File.Exists(p)) return p;
        }

        foreach (var root in roots.Distinct())
        {
            if (!Directory.Exists(root)) continue;

            foreach (var f in Directory.GetFiles(root, "*.xlsx", SearchOption.TopDirectoryOnly))
                if (Norm(Path.GetFileName(f)) == wanted) return f;

            foreach (var dir in Directory.GetDirectories(root))
                foreach (var f in Directory.GetFiles(dir, "*.xlsx", SearchOption.TopDirectoryOnly))
                    if (Norm(Path.GetFileName(f)) == wanted) return f;
        }

        throw new FileNotFoundException($"Kunne ikke finde filen '{fileName}'.");
    }

    static DataTable ReadSheet(string path, string? sheetName = null)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);

        var ds = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
        });

        if (ds.Tables.Count == 0) throw new Exception($"No sheets found in {path}");

        if (!string.IsNullOrWhiteSpace(sheetName))
        {
            var table = ds.Tables.Cast<DataTable>()
                .FirstOrDefault(t => t.TableName.Equals(sheetName, StringComparison.OrdinalIgnoreCase));
            if (table != null) return table;
        }

        return ds.Tables[0];
    }

    static int? TryFindCol(DataTable t, params string[] candidates)
    {
        string Norm(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        var cols = t.Columns.Cast<DataColumn>().Select((c, idx) => (idx, name: Norm(c.ColumnName))).ToList();

        foreach (var cand in candidates.Select(Norm))
        {
            var hit = cols.FirstOrDefault(c => c.name == cand);
            if (!Equals(hit, default((int idx, string name)))) return hit.idx;
        }
        foreach (var cand in candidates.Select(Norm))
        {
            var hit = cols.FirstOrDefault(c => c.name.Contains(cand));
            if (!Equals(hit, default((int idx, string name)))) return hit.idx;
        }
        return null;
    }

    static int FindCol(DataTable t, params string[] candidates)
        => TryFindCol(t, candidates) ?? throw new Exception(
            $"Could not find column. Candidates: {string.Join(", ", candidates)}. Columns: {string.Join(", ", t.Columns.Cast<DataColumn>().Select(c => c.ColumnName))}"
        );

    static string GetStr(DataRow r, int col) => (r[col]?.ToString() ?? "").Trim();

    static double GetDouble(DataRow r, int col)
    {
        var s = GetStr(r, col);
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
        if (double.TryParse(s, NumberStyles.Any, new CultureInfo("da-DK"), out v)) return v;
        return double.NaN;
    }

    static DateTime GetDate(DataRow r, int col)
    {
        if (r[col] is DateTime dt) return dt;
        var s = GetStr(r, col);
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) return dt;
        if (DateTime.TryParse(s, new CultureInfo("da-DK"), DateTimeStyles.None, out dt)) return dt;
        throw new Exception($"Could not parse date: '{s}'");
    }

    static List<ModtagRow> ReadModtagelser(string path)
{
    var t = ReadSheet(path);

    int cSk  = FindCol(t, "Skabelonnr", "Skabelon", "Nr", "Nr.");
    int cDt  = FindCol(t, "Køres dato", "KoresDato", "Kørselsdato", "Dato");
    int cEn  = FindCol(t, "Enhed");
    int cAnt = FindCol(t, "Antal", "Mængde", "Maengde", "Qty", "Quantity");

int? cCustNo = TryFindCol(t,
    "Lev.nr.", "Levnr", "Leverandørnr", "Leverandør nr",
    "Kundenr", "KundeNr", "CustomerNo", "DebitorNr", "Debitor");

int? cCustName = TryFindCol(t,
    "Navn", "Navn 2", "Kundenavn", "KundeNavn",
    "CustomerName", "Customer");

    var list = new List<ModtagRow>();

    foreach (DataRow r in t.Rows)
    {
        var sk = GetStr(r, cSk);
        if (string.IsNullOrWhiteSpace(sk)) continue;

        var dt = GetDate(r, cDt);
        var en = GetStr(r, cEn);

        var antal = GetDouble(r, cAnt);
        if (double.IsNaN(antal)) continue;

        var custNo   = cCustNo   is null ? "" : GetStr(r, cCustNo.Value);
        var custName = cCustName is null ? "" : GetStr(r, cCustName.Value);

        list.Add(new ModtagRow(
            Skabelonnr: sk,
            KoresDato: dt,
            Enhed: en,
            Antal: antal,
            CustomerNo: custNo,
            CustomerName: custName
        ));
    }

    return list;
}
    static List<KoerselRow> ReadKoerselordrer(string path)
    {
        DataTable t;
        try { t = ReadSheet(path, "Kørselordreskabelon varer"); }
        catch { t = ReadSheet(path); }

        int cSk = FindCol(t, "Nr.", "Nr", "Skabelonnr", "Skabelonnr.");
        int cVa = FindCol(t, "Varenr.", "Varenr", "Varenummer");

        var list = new List<KoerselRow>();
        foreach (DataRow r in t.Rows)
        {
            var sk = GetStr(r, cSk);
            var va = GetStr(r, cVa);
            if (string.IsNullOrWhiteSpace(sk) || string.IsNullOrWhiteSpace(va)) continue;
            list.Add(new KoerselRow(sk, va));
        }
        return list;
    }

    static List<KapRow> ReadKapacitetXlsx(string path)
    {
        var t = ReadSheet(path);

        int cVa = FindCol(t, "Varenummer", "Varenr", "Varenr.");
        int cKa = FindCol(t, "Kapacitet", "Kapacitet_L", "KapacitetL");
        int cEn = FindCol(t, "Enhed");

        var list = new List<KapRow>();
        foreach (DataRow r in t.Rows)
        {
            var va = GetStr(r, cVa);
            var en = GetStr(r, cEn);
            var capD = GetDouble(r, cKa);
            if (string.IsNullOrWhiteSpace(va) || double.IsNaN(capD)) continue;
            list.Add(new KapRow(va, (int)Math.Round(capD), en));
        }
        return list;
    }
    static string MostCommonNonEmpty(IEnumerable<string> xs)
    {
        var best = xs.Select(x => (x ?? "").Trim())
            .Where(x => x.Length > 0)
            .GroupBy(x => x)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        return best ?? "";
    }
}
