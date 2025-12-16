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

        // Lader stadig trainer levere “pred + frekvens” osv.
        // (Hvis du vil bruge engine her også, kan vi udvide senere)
        var list = await _trainer.RecommendAsync(f, t);

        return Ok(new
        {
            from = f,
            to = t,
            count = list.Count,
            items = list
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

        // 1) Hent ML-trainerens resultat (har bl.a. predKgPerDaySafe + frequencyDays + navn)
        var baseRec = await _trainer.RecommendOneAsync(f, t, customerNo.Trim());
        if (baseRec is null) return NotFound();

        // 2) Kør container-valget gennem RecommendationEngine (så vi ikke ender i 29×120)
        var eng = _engine.Recommend(
            predKgPerDaySafe: baseRec.PredKgPerDaySafe,
            densityKgPerLiter: ResidualDensityKgPerLiter,
            frequencyDays: baseRec.FrequencyDays
        );

        // 3) Returnér samme shape som før, men med engine-valgte containerL/containerCount/expectedFill
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
