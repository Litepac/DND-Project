using System.Globalization;
using DNDProject.Api.Data;
using DNDProject.Api.ML;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DNDProject.Api.Controllers;

[ApiController]
[Route("api/ml")]
[Authorize]
public sealed class MLController : ControllerBase
{
    private readonly MLTrainerService _trainer;
    private readonly AppDbContext _db;

    private readonly RecommendationEngine _engine = new();

    // Restaffald (Kørselsordrer.Indhold)
    private const int ResidualIndholdCode = 710100;

    // Antaget bulk density for rest (kg/L)
    private const double ResidualDensityKgPerLiter = 0.13;

    public MLController(MLTrainerService trainer, AppDbContext db)
    {
        _trainer = trainer;
        _db = db;
    }

    // -------------------------
    // A) Træn model og returnér metrics
    // POST api/ml/train?from=2024-01-01&to=2024-12-31
    // -------------------------
    [HttpPost("train")]
    public async Task<IActionResult> Train(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var f = (from ?? new DateTime(2024, 1, 1)).Date;
        var t = (to ?? new DateTime(2024, 12, 31)).Date;
        if (t < f) return BadRequest("to must be >= from");

        var result = await _trainer.TrainAsync(f, t);
        return Ok(result);
    }

// -------------------------
// B) Lav anbefalinger pr. kunde (container + frekvens)
// POST api/ml/recommend?from=2024-01-01&to=2024-12-31
// -------------------------
[HttpPost("recommend")]
public async Task<IActionResult> Recommend(
    [FromQuery] DateTime? from = null,
    [FromQuery] DateTime? to = null)
{
    var f = (from ?? new DateTime(2024, 1, 1)).Date;
    var t = (to ?? new DateTime(2024, 12, 31)).Date;
    if (t < f) return BadRequest("to must be >= from");

    // 1) Hent “basis” fra trainer (pred + frekvens + kundenavn osv.)
    var baseList = await _trainer.RecommendAsync(f, t);

    // 2) Kør container-valg gennem RecommendationEngine for hver kunde
    var items = baseList.Select(baseRec =>
    {
        var eng = _engine.Recommend(
            predKgPerDaySafe: baseRec.PredKgPerDaySafe,
            densityKgPerLiter: ResidualDensityKgPerLiter,
            frequencyDays: baseRec.FrequencyDays,
            targetFill: 0.95,
            minFill: 0.80,
            maxFill: 1.05,
            maxContainers: 30
        );

        // Returnér samme “shape” som UI/Swagger forventer
        return new
        {
            customerNo = baseRec.CustomerNo,
            customerName = baseRec.CustomerName,
            containerL = eng.ContainerSize,
            containerCount = eng.ContainerCount,
            frequencyDays = eng.FrequencyDays,
            expectedFill = eng.ExpectedFill,
            predKgPerDaySafe = baseRec.PredKgPerDaySafe
        };
    }).ToList();

    return Ok(new
    {
        from = f,
        to = t,
        count = items.Count,
        items
    });
}


// -------------------------
// C) Anbefaling for én kunde
// GET api/ml/recommend-one?from=2024-01-01&to=2024-12-31&customerNo=358924
// -------------------------
[HttpGet("recommend-one")]
public async Task<IActionResult> RecommendOne(
    [FromQuery] DateTime? from = null,
    [FromQuery] DateTime? to = null,
    [FromQuery] string? customerNo = null)
{
    if (string.IsNullOrWhiteSpace(customerNo))
        return BadRequest("customerNo is required");

    var f = (from ?? new DateTime(2024, 1, 1)).Date;
    var t = (to ?? new DateTime(2024, 12, 31)).Date;
    if (t < f) return BadRequest("to must be >= from");

    // (Valgfrit men anbefalet) – undgå 1-dags edgecases hvis trainer ikke kan det.
    // Hvis du ikke vil have denne begrænsning, kan du bare slette blokken.
    var periodDays = (t - f).Days + 1;
    if (periodDays < 7)
        return BadRequest("Period is too short for recommend-one. Please use at least 7 days (e.g. a full month or year).");

    var custNo = customerNo.Trim();

    try
    {
        // 1) Hent ML-trainerens resultat
        var baseRec = await _trainer.RecommendOneAsync(f, t, custNo);
        if (baseRec is null) return NotFound();

        // 2) Sikkerhed: beskyt engine mod NaN/Infinity/negative
        var pred = baseRec.PredKgPerDaySafe;
        if (double.IsNaN(pred) || double.IsInfinity(pred) || pred < 0) pred = 0;

        var freq = baseRec.FrequencyDays;
        if (freq <= 0) freq = 7;

        // 3) Kør container-valget gennem RecommendationEngine
        var eng = _engine.Recommend(
            predKgPerDaySafe: pred,
            densityKgPerLiter: ResidualDensityKgPerLiter,
            frequencyDays: freq,
            targetFill: 0.95,
            minFill: 0.80,
            maxFill: 1.05,
            maxContainers: 30
        );

        // 4) Returnér samme shape som før
        return Ok(new
        {
            customerNo = baseRec.CustomerNo,
            customerName = baseRec.CustomerName,
            containerL = eng.ContainerSize,
            containerCount = eng.ContainerCount,
            frequencyDays = eng.FrequencyDays,
            expectedFill = eng.ExpectedFill,
            predKgPerDaySafe = baseRec.PredKgPerDaySafe
        });
    }
    catch (Exception ex)
    {
        // Gør den robust: hellere en forklarlig 400 end en “mystisk 500”
        return BadRequest($"recommend-one failed for customerNo={custNo} in {f:yyyy-MM-dd}..{t:yyyy-MM-dd}. Details: {ex.Message}");
    }
}


    // -------------------------
    // D) Baseline (historisk) for én kunde (før/efter)
    // GET api/ml/baseline?from=2024-01-01&to=2024-12-31&customerNo=358924
    // -------------------------
    [HttpGet("baseline")]
    public async Task<IActionResult> Baseline(
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
        if (t < f) return BadRequest("to must be >= from");

        var residualOrders = await GetResidualPurchaseOrderNumbersAsync();
        if (residualOrders.Count == 0)
        {
            return Ok(new BaselineDto(customerNo.Trim(), "", f, t, (t - f).Days + 1, 0, 0, 0, 0, 0, 0));
        }

        // receipts uden JOIN (undgår multiplikation)
        var raw = await _db.StenaReceipts.AsNoTracking()
            .Where(r =>
                r.Unit == "KG"
                && r.CustomerKey != null
                && r.CustomerKey == custKey
                && r.ReceiptDate >= f
                && r.ReceiptDate <= t
                && r.PurchaseOrderNumber != null
                && residualOrders.Contains(r.PurchaseOrderNumber.Value)
            )
            .Select(r => new
            {
                Dt = r.ReceiptDate,
                AmountStr = r.Amount,
                CustomerName = r.CustomerName
            })
            .ToListAsync();

        if (raw.Count == 0)
        {
            return Ok(new BaselineDto(customerNo.Trim(), "", f, t, (t - f).Days + 1, 0, 0, 0, 0, 0, 0));
        }

        var custName = raw.Select(x => (x.CustomerName ?? "").Trim())
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "";

        var byDay = raw
            .Select(x => new { Dt = x.Dt.Date, Kg = ParseAmount(x.AmountStr) })
            .GroupBy(x => x.Dt)
            .Select(g => new { Date = g.Key, Kg = g.Sum(z => z.Kg) })
            .OrderBy(x => x.Date)
            .ToList();

        var tripDays = byDay.Count; // “tømmedage” proxy
        var totalKg = byDay.Sum(x => x.Kg);

        var periodDays = (t - f).Days + 1;
        if (periodDays <= 0) periodDays = 1;

        var tripsPerYear = tripDays * 365.0 / periodDays;
        var avgKgPerTrip = tripDays > 0 ? totalKg / tripDays : 0;
        var avgKgPerDayCalendar = totalKg / periodDays;
        var avgKgPerDayOnTripDays = tripDays > 0 ? totalKg / tripDays : 0;

        return Ok(new BaselineDto(
            CustomerNo: customerNo.Trim(),
            CustomerName: custName,
            From: f,
            To: t,
            PeriodDays: periodDays,
            TripDays: tripDays,
            TotalKg: totalKg,
            TripsPerYear: tripsPerYear,
            AvgKgPerTrip: avgKgPerTrip,
            AvgKgPerDayCalendar: avgKgPerDayCalendar,
            AvgKgPerDayOnTripDays: avgKgPerDayOnTripDays
        ));
    }

    public sealed record BaselineDto(
        string CustomerNo,
        string CustomerName,
        DateTime From,
        DateTime To,
        int PeriodDays,
        int TripDays,
        double TotalKg,
        double TripsPerYear,
        double AvgKgPerTrip,
        double AvgKgPerDayCalendar,
        double AvgKgPerDayOnTripDays
    );

    // ============================================================
    // ✅ NY: Baseline V2 (bedre og mere forklarlig)
    // - Historisk trips = DISTINCT (PurchaseOrderNumber + dato)  (proxy for tømning)
    // - Planlagt trips = ud fra Kørselsordrer.Frekvens (hvis udfyldt)
    // GET api/ml/baseline-v2?from=2024-01-01&to=2024-12-31&customerNo=358924
    // ============================================================
    [HttpGet("baseline-v2")]
    public async Task<IActionResult> BaselineV2(
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
        if (t < f) return BadRequest("to must be >= from");

        var periodDays = (t - f).Days + 1;
        if (periodDays <= 0) periodDays = 1;

        var residualOrders = await GetResidualPurchaseOrderNumbersAsync();
        if (residualOrders.Count == 0)
        {
            return Ok(new BaselineV2Dto(
                CustomerNo: customerNo.Trim(),
                CustomerName: "",
                From: f,
                To: t,
                PeriodDays: periodDays,
                TotalKg: 0,
                UniqueActiveDays: 0,
                EstimatedTripsDistinctOrderDate: 0,
                TripsPerYearHistorical: 0,
                PlannedTripsPerYear: 0,
                DataRows: 0,
                DistinctOrders: 0
            ));
        }

        // Hent receipts (uden join) + inkluder PO nr så vi kan lave “order+date” proxy
        var raw = await _db.StenaReceipts.AsNoTracking()
            .Where(r =>
                r.Unit == "KG"
                && r.CustomerKey != null
                && r.CustomerKey == custKey
                && r.ReceiptDate >= f
                && r.ReceiptDate <= t
                && r.PurchaseOrderNumber != null
                && residualOrders.Contains(r.PurchaseOrderNumber.Value)
            )
            .Select(r => new
            {
                Dt = r.ReceiptDate,
                AmountStr = r.Amount,
                CustomerName = r.CustomerName,
                PurchaseOrder = r.PurchaseOrderNumber!.Value
            })
            .ToListAsync();

        if (raw.Count == 0)
        {
            return Ok(new BaselineV2Dto(
                CustomerNo: customerNo.Trim(),
                CustomerName: "",
                From: f,
                To: t,
                PeriodDays: periodDays,
                TotalKg: 0,
                UniqueActiveDays: 0,
                EstimatedTripsDistinctOrderDate: 0,
                TripsPerYearHistorical: 0,
                PlannedTripsPerYear: await GetPlannedTripsPerYearAsync(custKey),
                DataRows: 0,
                DistinctOrders: 0
            ));
        }

        var custName = raw.Select(x => (x.CustomerName ?? "").Trim())
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "";

        // Total kg
        var totalKg = raw.Sum(x => ParseAmount(x.AmountStr));

        // Unique aktive dage (dag hvor der er data)
        var uniqueDays = raw.Select(x => x.Dt.Date).Distinct().Count();

        // ✅ Trips proxy: DISTINCT (PurchaseOrder + date)
        var estTrips = raw
            .Select(x => new { x.PurchaseOrder, Dt = x.Dt.Date })
            .Distinct()
            .Count();

        var tripsPerYearHist = estTrips * 365.0 / periodDays;

        var plannedTripsPerYear = await GetPlannedTripsPerYearAsync(custKey);

        var distinctOrders = raw.Select(x => x.PurchaseOrder).Distinct().Count();

        return Ok(new BaselineV2Dto(
            CustomerNo: customerNo.Trim(),
            CustomerName: custName,
            From: f,
            To: t,
            PeriodDays: periodDays,
            TotalKg: totalKg,
            UniqueActiveDays: uniqueDays,
            EstimatedTripsDistinctOrderDate: estTrips,
            TripsPerYearHistorical: tripsPerYearHist,
            PlannedTripsPerYear: plannedTripsPerYear,
            DataRows: raw.Count,
            DistinctOrders: distinctOrders
        ));
    }

    public sealed record BaselineV2Dto(
        string CustomerNo,
        string CustomerName,
        DateTime From,
        DateTime To,
        int PeriodDays,
        double TotalKg,
        int UniqueActiveDays,
        int EstimatedTripsDistinctOrderDate,
        double TripsPerYearHistorical,
        double PlannedTripsPerYear,
        int DataRows,
        int DistinctOrders
    );

    // -------------------------
    // Helpers
    // -------------------------
    private async Task<HashSet<int>> GetResidualPurchaseOrderNumbersAsync()
    {
        // PurchaseOrderNumber er int? -> ingen Trim(), bare Value
        var list = await _db.StenaKoerselsordrer.AsNoTracking()
            .Where(k => k.Indhold == ResidualIndholdCode && k.PurchaseOrderNumber != null)
            .Select(k => k.PurchaseOrderNumber!.Value)
            .Distinct()
            .ToListAsync();

        return list.ToHashSet();
    }

    // Planlagt trips/år (kontrakt-ish)
    // Vi bruger Frekvens hvis den findes (typisk fx "14", "7", "28" eller "hver 14. dag").
    // Hvis Frekvens ikke kan parses, tæller vi den ikke med (0).
    private async Task<double> GetPlannedTripsPerYearAsync(int custKey)
    {
        var freqs = await _db.StenaKoerselsordrer.AsNoTracking()
            .Where(k =>
                k.Indhold == ResidualIndholdCode
                && k.Lev_nr != null
                && k.Lev_nr.Value == custKey
                && k.Frekvens != null)
            .Select(k => k.Frekvens!)
            .ToListAsync();

        if (freqs.Count == 0) return 0;

        // Find mindste “meningsfulde” frekvens i dage (konservativ)
        int? bestDays = null;

        foreach (var f in freqs)
        {
            var days = TryParseFrequencyDays(f);
            if (days is null) continue;

            if (days.Value <= 0) continue;
            if (bestDays is null || days.Value < bestDays.Value)
                bestDays = days.Value;
        }

        if (bestDays is null) return 0;

        // Trips/år = 365 / dage
        return 365.0 / bestDays.Value;
    }

    private static int? TryParseFrequencyDays(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        s = s.Trim();

        // 1) direkte tal "14"
        if (int.TryParse(s, out var n)) return n;

        // 2) find første tal i tekst "hver 14 dag"
        var digits = new string(s.Where(char.IsDigit).ToArray());
        if (int.TryParse(digits, out n)) return n;

        return null;
    }

    private static double ParseAmount(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        s = s.Trim();

        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
        if (double.TryParse(s, NumberStyles.Any, new CultureInfo("da-DK"), out v)) return v;

        // "1.234,56" -> "1234.56"
        s = s.Replace(".", "").Replace(",", ".");
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v)) return v;

        return 0;
    }
}
