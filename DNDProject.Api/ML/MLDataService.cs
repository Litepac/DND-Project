using System.Globalization;
using Microsoft.EntityFrameworkCore;
using DNDProject.Api.Data;

namespace DNDProject.Api.ML;

public sealed class MLDataService
{
    private readonly AppDbContext _db;
    public MLDataService(AppDbContext db) => _db = db;

    // Lokal type (undgår konflikt)
    public record PickupDailyDb(
        string Skabelonnr,
        DateTime Date,
        double CollectedKg,
        string CustomerNo,
        string CustomerName
    );

    // Restaffald-kode (Kørselsordrer.Indhold)
    private const int ResidualIndholdCode = 710100;

    /// <summary>
    /// Henter alle Købsordrenr (Kørselsordrer.Købsordrenr) hvor Indhold=710100.
    /// Returneres som int-set så vi kan matche Modtagelse.KoebsordreNummer (int).
    /// </summary>
    public async Task<HashSet<int>> LoadResidualPurchaseOrdersAsync()
    {
        var raw = await _db.StenaKoerselsordrer.AsNoTracking()
            .Where(k => k.Indhold == ResidualIndholdCode && k.PurchaseOrderNumber != null)
            .Select(k => k.PurchaseOrderNumber)
            .Distinct()
            .ToListAsync();

        var set = new HashSet<int>();

        foreach (var x in raw)
        {
            if (x is null) continue;

            var s = x.ToString();
            if (string.IsNullOrWhiteSpace(s)) continue;

            if (int.TryParse(s.Trim(), out var n))
                set.Add(n);
        }

        return set;
    }

    /// <summary>
    /// Daily pickups (KG) for REST i perioden.
    /// VIGTIGT: Ingen join på Kørselsordrer -> undgår multiplikation af rækker.
    /// Skabelonnr sættes til Købsordrenr som string.
    /// </summary>
    public async Task<List<PickupDailyDb>> LoadDailyPickupsAsync(DateTime fromDate, DateTime toDate)
    {
        fromDate = fromDate.Date;
        toDate = toDate.Date;
        if (toDate < fromDate) (fromDate, toDate) = (toDate, fromDate);

        var residualOrders = await LoadResidualPurchaseOrdersAsync();
        if (residualOrders.Count == 0) return new();

        var raw = await _db.StenaReceipts.AsNoTracking()
            .Where(r => r.Unit == "KG"
                        && r.ReceiptDate >= fromDate
                        && r.ReceiptDate <= toDate
                        && r.PurchaseOrderNumber != null
                        && residualOrders.Contains(r.PurchaseOrderNumber.Value)
                        && r.CustomerKey != null)
            .Select(r => new
            {
                Date = r.ReceiptDate,
                AmountStr = r.Amount,
                CustomerNo = r.CustomerKey,
                CustomerName = r.CustomerName,
                PurchaseOrder = r.PurchaseOrderNumber
            })
            .ToListAsync();

        var daily = raw
            .Select(x => new
            {
                Sk = x.PurchaseOrder?.ToString() ?? "",
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
}
