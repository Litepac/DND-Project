using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DNDProject.Api.Data;
using DNDProject.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DNDProject.Api.Controllers
{
    [ApiController]
    [Route("api/stena/efficiency")]
    public class StenaEfficiencyController : ControllerBase
    {
        private readonly AppDbContext _db;

        private const decimal KgPerLiter = 0.13m;
        private static readonly int[] AllowedLiters = { 120, 240, 660, 1100 };

        public StenaEfficiencyController(AppDbContext db) => _db = db;

        private static decimal ParseAmountToDecimal(string? amountText)
        {
            if (string.IsNullOrWhiteSpace(amountText))
                return 0m;

            if (decimal.TryParse(amountText, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return d;

            if (decimal.TryParse(amountText, NumberStyles.Any, new CultureInfo("da-DK"), out d))
                return d;

            return 0m;
        }

        private static int? TryParseLitersFromDescription(string? description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return null;

            var text = description.ToLowerInvariant();
            var m = Regex.Match(text, @"\b(120|240|660|1100)\b\s*(ltr|l|liter)\b");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var liters))
                return liters;

            return null;
        }

        private static int? ResolveLiters(int? itemNumber, string? description, Dictionary<int, int> capacityLookup)
        {
            // 1) Master data lookup (Kapacitet_og_enhed_opdateret) -> kun L og >0
            if (itemNumber.HasValue && itemNumber.Value > 0 &&
                capacityLookup.TryGetValue(itemNumber.Value, out var liters) &&
                AllowedLiters.Contains(liters))
            {
                return liters;
            }

            // 2) Fallback: parse fra beskrivelse (660 ltr., 1100 ltr. osv.)
            var parsed = TryParseLitersFromDescription(description);
            if (parsed.HasValue && AllowedLiters.Contains(parsed.Value))
                return parsed;

            return null;
        }

        private async Task<Dictionary<int, int>> BuildCapacityLookupAsync(IEnumerable<int> containerItemNumbers)
        {
            var keys = containerItemNumbers
                .Where(n => n > 0)
                .Distinct()
                .ToList();

            if (keys.Count == 0)
                return new Dictionary<int, int>();

            // Henter kun enhed "L" -> m3 ignoreres automatisk
            var caps = await _db.ContainerCapacities
                .AsNoTracking()
                .Where(c => keys.Contains(c.ItemNumber)
                            && c.Unit != null
                            && c.Capacity != null
                            && c.Capacity > 0
                            && c.Unit.Trim().ToUpper() == "L")
                .Select(c => new
                {
                    c.ItemNumber,
                    Capacity = c.Capacity!.Value
                })
                .ToListAsync();

            // Hvis dubletter pr varenummer: vælg MAX, og rund til int liter.
            return caps
                .GroupBy(x => x.ItemNumber)
                .ToDictionary(
                    g => g.Key,
                    g => (int)Math.Round(g.Max(x => x.Capacity), MidpointRounding.AwayFromZero)
                );
        }

        // ------------------------------------------------------------
        // SUMMARY (single size)  - eksisterende endpoint
        // GET api/stena/efficiency/summary?months=12&thresholdPct=80&liters=660
        // ------------------------------------------------------------
        [HttpGet("summary")]
        public async Task<ActionResult<List<ContainerEfficiencySummaryDto>>> GetEfficiencySummary(
            [FromQuery] int months = 12,
            [FromQuery] int thresholdPct = 80,
            [FromQuery] int liters = 660)
        {
            if (months <= 0 || months > 36) months = 12;
            if (thresholdPct <= 0 || thresholdPct >= 100) thresholdPct = 80;
            if (!AllowedLiters.Contains(liters)) liters = 660;

            var sinceDate = DateTime.Today.AddMonths(-months);
            var capacityKg = liters * KgPerLiter;

            // Kun KG direkte fra DB
            var kgRows = await (
                from m in _db.StenaReceipts.AsNoTracking()
                join k in _db.StenaKoerselsordrer.AsNoTracking()
                    on new { Lev = m.CustomerKey, Koeb = m.PurchaseOrderNumber }
                    equals new { Lev = k.Lev_nr, Koeb = k.PurchaseOrderNumber }
                    into jk
                from k in jk.DefaultIfEmpty()
                where m.ReceiptDate >= sinceDate
                      && m.CustomerKey.HasValue
                      && m.PurchaseOrderNumber.HasValue
                      && m.Unit != null
                      && m.Unit.Trim().ToUpper() == "KG"
                select new
                {
                    CustomerKey = m.CustomerKey!.Value,
                    CustomerName = m.CustomerName,
                    Amount = m.Amount,
                    ContainerItemNumber = k != null ? k.Varenr : (int?)null,
                    OrderDescription = k != null ? k.Beskrivelse : null
                }
            ).ToListAsync();

            if (kgRows.Count == 0)
                return Ok(new List<ContainerEfficiencySummaryDto>());

            var itemNumbers = kgRows
                .Select(r => r.ContainerItemNumber)
                .Where(v => v.HasValue && v.Value > 0)
                .Select(v => v!.Value)
                .Distinct()
                .ToList();

            var capacityLookup = await BuildCapacityLookupAsync(itemNumbers);

            var events = new List<(int CustomerKey, string CustomerName, decimal FillPct)>(kgRows.Count);

            foreach (var r in kgRows)
            {
                var capLiters = ResolveLiters(r.ContainerItemNumber, r.OrderDescription, capacityLookup);

                // IGNORER alt andet end allowed liters + filtrér til den valgte størrelse
                if (!capLiters.HasValue || capLiters.Value != liters)
                    continue;

                var w = ParseAmountToDecimal(r.Amount);
                if (w <= 0) continue;

                var fillPct = (w / capacityKg) * 100m;
                events.Add((r.CustomerKey, r.CustomerName ?? string.Empty, fillPct));
            }

            if (events.Count == 0)
                return Ok(new List<ContainerEfficiencySummaryDto>());

            var result = events
                .GroupBy(x => new { x.CustomerKey, x.CustomerName })
                .Select(g =>
                {
                    var total = g.Count();
                    var inefficient = g.Count(e => e.FillPct < thresholdPct);
                    var avgFill = g.Average(e => e.FillPct);

                    return new ContainerEfficiencySummaryDto
                    {
                        CustomerKey = g.Key.CustomerKey,
                        CustomerName = g.Key.CustomerName,
                        Liters = liters,
                        TotalEmpties = total,
                        InefficientEmpties = inefficient,
                        InefficientPct = total > 0 ? (float)(100m * inefficient / total) : 0f,
                        AvgFillPct = total > 0 ? (float)avgFill : 0f,
                        CapacityKg = (float)capacityKg,
                        ThresholdPct = thresholdPct
                    };
                })
                .OrderByDescending(x => x.InefficientPct)
                .ToList();

            return Ok(result);
        }

        // ------------------------------------------------------------
        // SUMMARY (all sizes) - NYT endpoint
        // GET api/stena/efficiency/summary/all?months=12&thresholdPct=80
        // Returnerer pr kunde + liters (120/240/660/1100)
        // ------------------------------------------------------------
        [HttpGet("summary/all")]
        public async Task<ActionResult<List<ContainerEfficiencySummaryDto>>> GetEfficiencySummaryAllSizes(
            [FromQuery] int months = 12,
            [FromQuery] int thresholdPct = 80)
        {
            if (months <= 0 || months > 36) months = 12;
            if (thresholdPct <= 0 || thresholdPct >= 100) thresholdPct = 80;

            var sinceDate = DateTime.Today.AddMonths(-months);

            // Kun KG direkte fra DB
            var kgRows = await (
                from m in _db.StenaReceipts.AsNoTracking()
                join k in _db.StenaKoerselsordrer.AsNoTracking()
                    on new { Lev = m.CustomerKey, Koeb = m.PurchaseOrderNumber }
                    equals new { Lev = k.Lev_nr, Koeb = k.PurchaseOrderNumber }
                    into jk
                from k in jk.DefaultIfEmpty()
                where m.ReceiptDate >= sinceDate
                      && m.CustomerKey.HasValue
                      && m.PurchaseOrderNumber.HasValue
                      && m.Unit != null
                      && m.Unit.Trim().ToUpper() == "KG"
                select new
                {
                    CustomerKey = m.CustomerKey!.Value,
                    CustomerName = m.CustomerName,
                    Amount = m.Amount,
                    ContainerItemNumber = k != null ? k.Varenr : (int?)null,
                    OrderDescription = k != null ? k.Beskrivelse : null
                }
            ).ToListAsync();

            if (kgRows.Count == 0)
                return Ok(new List<ContainerEfficiencySummaryDto>());

            var itemNumbers = kgRows
                .Select(r => r.ContainerItemNumber)
                .Where(v => v.HasValue && v.Value > 0)
                .Select(v => v!.Value)
                .Distinct()
                .ToList();

            var capacityLookup = await BuildCapacityLookupAsync(itemNumbers);

            var events = new List<(int CustomerKey, string CustomerName, int Liters, decimal FillPct)>(kgRows.Count);

            foreach (var r in kgRows)
            {
                var capLiters = ResolveLiters(r.ContainerItemNumber, r.OrderDescription, capacityLookup);

                // IGNORER alt andet end 120/240/660/1100
                if (!capLiters.HasValue)
                    continue;

                var w = ParseAmountToDecimal(r.Amount);
                if (w <= 0) continue;

                var capacityKg = capLiters.Value * KgPerLiter;
                if (capacityKg <= 0) continue;

                var fillPct = (w / capacityKg) * 100m;
                events.Add((r.CustomerKey, r.CustomerName ?? string.Empty, capLiters.Value, fillPct));
            }

            if (events.Count == 0)
                return Ok(new List<ContainerEfficiencySummaryDto>());

            var result = events
                .GroupBy(x => new { x.CustomerKey, x.CustomerName, x.Liters })
                .Select(g =>
                {
                    var total = g.Count();
                    var inefficient = g.Count(e => e.FillPct < thresholdPct);
                    var avgFill = g.Average(e => e.FillPct);

                    var capacityKg = g.Key.Liters * KgPerLiter;

                    return new ContainerEfficiencySummaryDto
                    {
                        CustomerKey = g.Key.CustomerKey,
                        CustomerName = g.Key.CustomerName,
                        Liters = g.Key.Liters,
                        TotalEmpties = total,
                        InefficientEmpties = inefficient,
                        InefficientPct = total > 0 ? (float)(100m * inefficient / total) : 0f,
                        AvgFillPct = total > 0 ? (float)avgFill : 0f,
                        CapacityKg = (float)capacityKg,
                        ThresholdPct = thresholdPct
                    };
                })
                .OrderByDescending(x => x.InefficientPct)
                .ThenByDescending(x => x.TotalEmpties)
                .ToList();

            return Ok(result);
        }

        // ------------------------------------------------------------
        // DETAILS (single size)
        // GET api/stena/efficiency/customer/{levNr}?months=12&thresholdPct=80&liters=660
        // ------------------------------------------------------------
        [HttpGet("customer/{levNr:int}")]
        public async Task<ActionResult<ContainerEfficiencyDetailDto>> GetCustomerEfficiency(
            int levNr,
            [FromQuery] int months = 12,
            [FromQuery] int thresholdPct = 80,
            [FromQuery] int liters = 660)
        {
            if (months <= 0 || months > 36) months = 12;
            if (thresholdPct <= 0 || thresholdPct >= 100) thresholdPct = 80;
            if (!AllowedLiters.Contains(liters)) liters = 660;

            var sinceDate = DateTime.Today.AddMonths(-months);
            var capacityKg = liters * KgPerLiter;

            // Kun KG direkte fra DB
            var kgRows = await (
                from m in _db.StenaReceipts.AsNoTracking()
                join k in _db.StenaKoerselsordrer.AsNoTracking()
                    on new { Lev = m.CustomerKey, Koeb = m.PurchaseOrderNumber }
                    equals new { Lev = k.Lev_nr, Koeb = k.PurchaseOrderNumber }
                    into jk
                from k in jk.DefaultIfEmpty()
                where m.ReceiptDate >= sinceDate
                      && m.CustomerKey == levNr
                      && m.PurchaseOrderNumber.HasValue
                      && m.Unit != null
                      && m.Unit.Trim().ToUpper() == "KG"
                orderby m.ReceiptDate descending
                select new
                {
                    CustomerName = m.CustomerName,
                    ReceiptDate = m.ReceiptDate,
                    Amount = m.Amount,
                    ContainerItemNumber = k != null ? k.Varenr : (int?)null,
                    OrderDescription = k != null ? k.Beskrivelse : null
                }
            ).ToListAsync();

            if (kgRows.Count == 0)
                return NotFound($"Ingen KG-modtagelser fundet for kunde {levNr} i perioden.");

            var itemNumbers = kgRows
                .Select(r => r.ContainerItemNumber)
                .Where(v => v.HasValue && v.Value > 0)
                .Select(v => v!.Value)
                .Distinct()
                .ToList();

            var capacityLookup = await BuildCapacityLookupAsync(itemNumbers);

            var empties = new List<ContainerEmptyingDto>(Math.Min(50, kgRows.Count));

            foreach (var r in kgRows)
            {
                var capLiters = ResolveLiters(r.ContainerItemNumber, r.OrderDescription, capacityLookup);

                // IGNORER alt andet end allowed liters + filtrér til valgte størrelse
                if (!capLiters.HasValue || capLiters.Value != liters)
                    continue;

                var w = ParseAmountToDecimal(r.Amount);
                if (w <= 0) continue;

                var fillPct = (w / capacityKg) * 100m;

                empties.Add(new ContainerEmptyingDto
                {
                    Date = r.ReceiptDate,
                    WeightKg = (float)w,
                    FillPct = (float)fillPct
                });

                if (empties.Count >= 50) break;
            }

            if (empties.Count == 0)
                return NotFound($"Ingen tømninger matchede {liters}L for kunde {levNr} i perioden.");

            var total = empties.Count;
            var inefficient = empties.Count(e => e.FillPct < thresholdPct);

            return Ok(new ContainerEfficiencyDetailDto
            {
                CustomerKey = levNr,
                CustomerName = kgRows.Select(x => x.CustomerName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? string.Empty,

                Liters = liters,
                CapacityKg = (float)capacityKg,
                ThresholdPct = thresholdPct,

                TotalEmpties = total,
                InefficientEmpties = inefficient,
                InefficientPct = total > 0 ? 100f * inefficient / total : 0f,
                AvgFillPct = total > 0 ? empties.Average(e => e.FillPct) : 0f,
                Empties = empties
            });
        }
    }
}
