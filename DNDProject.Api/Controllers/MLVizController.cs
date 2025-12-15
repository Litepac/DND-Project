using System.Globalization;
using DNDProject.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DNDProject.Api.Controllers;

[ApiController]
[Route("api/ml-viz")]
[Authorize]
public sealed class MLVizController : ControllerBase
{
    private readonly AppDbContext _db;

    // Restaffald (fra Kørselsordrer.Indhold)
    private const int ResidualIndholdCode = 710100;

    public MLVizController(AppDbContext db)
    {
        _db = db;
    }

    // --------------------------------------------------------------------
    // GET api/ml-viz/customers?from=2024-01-01&to=2024-12-31&take=200&search=novo
    //
    // Returnerer top-kunder baseret på REST (KG) i perioden.
    // - "Days" = antal unikke datoer med modtagelser i perioden (ikke kalenderdage)
    // - "TotalKg" = sum af Antal (KG) i perioden
    // - "AvgKgPerDay" = TotalKg / Days
    //
    // search matcher kundenr eller kundenavn (contains)
    // --------------------------------------------------------------------
    [HttpGet("customers")]
    public async Task<IActionResult> Customers(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int take = 200,
        [FromQuery] string? search = null)
    {
        var f = (from ?? new DateTime(2024, 1, 1)).Date;
        var t = (to ?? new DateTime(2024, 12, 31)).Date;

        if (t < f) (f, t) = (t, f);

        take = Math.Clamp(take, 1, 2000);
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        // 1) Hent "REST" købsordrenumre (int)
        var residualOrderNumbers = await GetResidualPurchaseOrderNumbersAsync();
        if (residualOrderNumbers.Count == 0)
        {
            return Ok(new
            {
                from = f,
                to = t,
                totalCustomers = 0,
                returned = 0,
                top = Array.Empty<CustomerTopRow>()
            });
        }

        // 2) Receipts uden join (undgår multiplikation)
        //    NOTE: vi filtrerer i SQL på purchaseOrder IN residualOrders, og parse kg i .NET.
        var baseQuery = _db.StenaReceipts.AsNoTracking()
            .Where(r => r.Unit == "KG"
                     && r.ReceiptDate >= f
                     && r.ReceiptDate <= t
                     && r.CustomerKey != null
                     && r.PurchaseOrderNumber != null
                     && residualOrderNumbers.Contains(r.PurchaseOrderNumber.Value));

        // Search (kundenr eller navn)
if (search is not null)
{
    baseQuery = baseQuery.Where(r =>
        (
            r.CustomerKey.HasValue &&
            r.CustomerKey.Value.ToString().Contains(search)
        )
        ||
        (
            r.CustomerName != null &&
            EF.Functions.Like(r.CustomerName, $"%{search}%")
        )
    );
}


        var raw = await baseQuery
            .Select(r => new
            {
                CustomerNo = r.CustomerKey!,
                CustomerName = r.CustomerName,
                Date = r.ReceiptDate,
                AmountStr = r.Amount
            })
            .ToListAsync();

        // 3) Parse + group i .NET (sikker DK parsing)
        var grouped = raw
            .Select(x => new
            {
                CustomerNo = x.CustomerNo.ToString(),
                CustomerName = (x.CustomerName ?? "").Trim(),
                Dt = x.Date.Date,
                Kg = ParseAmount(x.AmountStr)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.CustomerNo))
            .GroupBy(x => new { x.CustomerNo, x.CustomerName })
            .Select(g =>
            {
                var totalKg = g.Sum(z => z.Kg);
                var days = g.Select(z => z.Dt).Distinct().Count();
                var lastDate = g.Max(z => z.Dt);

                return new CustomerTopRow(
                    CustomerNo: g.Key.CustomerNo ?? "",
                    CustomerName: g.Key.CustomerName ?? "",
                    Days: days,
                    TotalKg: totalKg,
                    AvgKgPerDay: days > 0 ? totalKg / days : 0,
                    LastDate: lastDate
                );
            })
            .OrderByDescending(x => x.TotalKg)
            .ToList();

        var totalCustomers = grouped.Count;
        var top = grouped.Take(take).ToList();

        return Ok(new
        {
            from = f,
            to = t,
            totalCustomers,
            returned = top.Count,
            top
        });
    }

    public sealed record CustomerTopRow(
        string CustomerNo,
        string CustomerName,
        int Days,
        double TotalKg,
        double AvgKgPerDay,
        DateTime LastDate
    );

    // --------------------------------------------------------------------
    // GET api/ml-viz/customer-daily?from=...&to=...&customerNo=358924
    // (til evt. historik-graf)
    // --------------------------------------------------------------------
    [HttpGet("customer-daily")]
    public async Task<IActionResult> CustomerDaily(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? customerNo = null)
    {
        if (string.IsNullOrWhiteSpace(customerNo))
            return BadRequest("customerNo is required");

        if (!int.TryParse(customerNo.Trim(), out var custKey))
            return BadRequest("customerNo must be numeric");

        var f = (from ?? new DateTime(2024, 1, 1)).Date;
        var t = (to ?? new DateTime(2024, 12, 31)).Date;
        if (t < f) (f, t) = (t, f);

        var residualOrderNumbers = await GetResidualPurchaseOrderNumbersAsync();
        if (residualOrderNumbers.Count == 0)
            return Ok(new { customerNo = customerNo.Trim(), from = f, to = t, points = 0, series = Array.Empty<DailyPoint>() });

        var raw = await _db.StenaReceipts.AsNoTracking()
            .Where(r => r.Unit == "KG"
                     && r.CustomerKey == custKey
                     && r.ReceiptDate >= f
                     && r.ReceiptDate <= t
                     && r.PurchaseOrderNumber != null
                     && residualOrderNumbers.Contains(r.PurchaseOrderNumber.Value))
            .Select(r => new
            {
                Date = r.ReceiptDate,
                AmountStr = r.Amount,
                CustomerName = r.CustomerName
            })
            .ToListAsync();

        var custName = raw.Select(x => (x.CustomerName ?? "").Trim())
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "";

        var series = raw
            .Select(x => new { Dt = x.Date.Date, Kg = ParseAmount(x.AmountStr) })
            .GroupBy(x => x.Dt)
            .Select(g => new DailyPoint(g.Key, g.Sum(z => z.Kg)))
            .OrderBy(x => x.Date)
            .ToList();

        return Ok(new
        {
            customerNo = customerNo.Trim(),
            customerName = custName,
            from = f,
            to = t,
            points = series.Count,
            series
        });
    }

    public sealed record DailyPoint(DateTime Date, double CollectedKg);

    // -------------------------
    // Helpers
    // -------------------------
    private async Task<List<int>> GetResidualPurchaseOrderNumbersAsync()
    {
        // PurchaseOrderNumber på StenaKoerselsordre er typisk string i jeres model
        var list = await _db.StenaKoerselsordrer.AsNoTracking()
            .Where(k => k.Indhold == ResidualIndholdCode && k.PurchaseOrderNumber != null)
            .Select(k => k.PurchaseOrderNumber!)
            .Distinct()
            .ToListAsync();

        var outList = new List<int>(list.Count);
        foreach (var s in list)
        {
            // hvis det allerede er int (eller noget andet), ToString og parse
            var txt = s.ToString();
            if (string.IsNullOrWhiteSpace(txt)) continue;

            if (int.TryParse(txt.Trim(), out var n))
                outList.Add(n);
        }

        return outList;
    }

    private static double ParseAmount(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;

        s = s.Trim();

        // kræver using System.Globalization;
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
        if (double.TryParse(s, NumberStyles.Any, new CultureInfo("da-DK"), out v)) return v;

        // fallback: "1.234,56" -> "1234.56"
        s = s.Replace(".", "").Replace(",", ".");
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v)) return v;

        return 0;
    }
}
