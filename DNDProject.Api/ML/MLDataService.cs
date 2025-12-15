using System.Globalization;
using Microsoft.EntityFrameworkCore;
using DNDProject.Api.Data;

namespace DNDProject.Api.ML;

public sealed class MLDataService
{
    private readonly AppDbContext _db;
    public MLDataService(AppDbContext db) => _db = db;

    // lokal type (undgår konflikt med din eksisterende PickupDaily)
    public record PickupDailyDb(
        string Skabelonnr,
        DateTime Date,
        double CollectedKg,
        string CustomerNo,
        string CustomerName
    );

    // ML-ready row (features + label)
    public record TrainRowDb(
        string Skabelonnr,
        DateTime Date,
        string CustomerNo,
        string CustomerName,
        float DaysSincePrev,
        float Month,
        float Weekday,
        float PrevCollectedKg,
        float AvgKgDay_Last3,
        float AvgKgDay_Last5,
        float StdKgDay_Last5,
        float TrendKgDay_Last5,
        float LabelKgPerDay
    );

    public async Task<List<PickupDailyDb>> LoadDailyPickupsAsync(DateTime fromDate, DateTime toDate)
    {
        // Hent “rå” rækker (vi parser Amount i .NET bagefter – SQL kan ikke sikkert da-DK parse)
        var raw = await (
            from r in _db.StenaReceipts.AsNoTracking()
            join k in _db.StenaKoerselsordrer.AsNoTracking()
                on r.PurchaseOrderNumber equals k.PurchaseOrderNumber into kj
            from k in kj.DefaultIfEmpty()
            where r.Unit != null
               && r.Unit == "KG"
               && r.ReceiptDate >= fromDate
               && r.ReceiptDate <= toDate
            select new
            {
                Skabelonnr = k != null
                    ? k.Nr
                    : (r.PurchaseOrderNumber.HasValue ? r.PurchaseOrderNumber.Value.ToString() : null),

                Date = r.ReceiptDate,
                AmountStr = r.Amount,
                CustomerNo = r.CustomerKey,
                CustomerName = r.CustomerName
            }
        ).ToListAsync();

        var daily = raw
            .Select(x => new
            {
                Sk = (x.Skabelonnr ?? "").Trim(),
                Dt = x.Date.Date,
                Kg = ParseAmount(x.AmountStr),
                CustNo = x.CustomerNo?.ToString() ?? "",
                CustName = x.CustomerName ?? ""
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Sk))
            .GroupBy(x => (x.Sk, x.Dt))
            .Select(g => new PickupDailyDb(
                Skabelonnr: g.Key.Sk,
                Date: g.Key.Dt,
                CollectedKg: g.Sum(z => z.Kg),
                CustomerNo: g.Select(z => z.CustNo).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "",
                CustomerName: g.Select(z => z.CustName).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? ""
            ))
            .OrderBy(x => x.Skabelonnr)
            .ThenBy(x => x.Date)
            .ToList();

        return daily;
    }

    // C: Byg træningsrækker (features + label) ud fra daily pickups
    public async Task<List<TrainRowDb>> BuildTrainRowsAsync(DateTime fromDate, DateTime toDate)
    {
        var daily = await LoadDailyPickupsAsync(fromDate, toDate);

        var rows = new List<TrainRowDb>();

        foreach (var grp in daily.GroupBy(d => d.Skabelonnr))
        {
            var list = grp.OrderBy(x => x.Date).ToList();

            // historik af kg/day for rolling features
            var kgDayHistory = new List<double>();

            for (int i = 1; i < list.Count; i++)
            {
                var prev = list[i - 1];
                var cur = list[i];

                int days = (cur.Date - prev.Date).Days;
                if (days <= 0) continue;
                if (days > 180) continue; // drop lange huller som i din excel-version

                double kgPerDay = cur.CollectedKg / days;

                double avg3 = RollingAvg(kgDayHistory, 3);
                double avg5 = RollingAvg(kgDayHistory, 5);
                double std5 = RollingStd(kgDayHistory, 5);
                double tr5  = RollingTrend(kgDayHistory, 5);

                if (kgDayHistory.Count < 3) avg3 = kgPerDay;
                if (kgDayHistory.Count < 5) { avg5 = kgPerDay; std5 = 0; tr5 = 0; }

                rows.Add(new TrainRowDb(
                    Skabelonnr: cur.Skabelonnr,
                    Date: cur.Date,
                    CustomerNo: string.IsNullOrWhiteSpace(cur.CustomerNo) ? prev.CustomerNo : cur.CustomerNo,
                    CustomerName: string.IsNullOrWhiteSpace(cur.CustomerName) ? prev.CustomerName : cur.CustomerName,
                    DaysSincePrev: days,
                    Month: cur.Date.Month,
                    Weekday: ((int)cur.Date.DayOfWeek + 6) % 7, // man=0...søn=6
                    PrevCollectedKg: (float)prev.CollectedKg,
                    AvgKgDay_Last3: (float)avg3,
                    AvgKgDay_Last5: (float)avg5,
                    StdKgDay_Last5: (float)std5,
                    TrendKgDay_Last5: (float)tr5,
                    LabelKgPerDay: (float)kgPerDay
                ));

                kgDayHistory.Add(kgPerDay);
            }
        }

        return rows;
    }

    private static double ParseAmount(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;

        s = s.Trim();

        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
        if (double.TryParse(s, NumberStyles.Any, new CultureInfo("da-DK"), out v)) return v;

        // fallback: "1.234,56" -> "1234.56"
        s = s.Replace(".", "").Replace(",", ".");
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v)) return v;

        return 0;
    }

    private static double RollingAvg(List<double> xs, int k)
    {
        if (xs.Count == 0) return 0;
        var take = xs.Skip(Math.Max(0, xs.Count - k)).ToArray();
        return take.Average();
    }

    private static double RollingStd(List<double> xs, int k)
    {
        if (xs.Count < 2) return 0;
        var take = xs.Skip(Math.Max(0, xs.Count - k)).ToArray();
        if (take.Length < 2) return 0;

        double mean = take.Average();
        double var = take.Select(x => (x - mean) * (x - mean)).Sum() / (take.Length - 1);
        return Math.Sqrt(var);
    }

    private static double RollingTrend(List<double> xs, int k)
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

        return (n * sumXY - sumX * sumY) / denom;
    }
}
