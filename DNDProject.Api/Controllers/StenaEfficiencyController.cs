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

        private static string NormalizeUnit(string? unit)
            => (unit ?? string.Empty).Trim().ToUpperInvariant();

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

private async Task<Dictionary<int, int>> BuildCapacityLookupAsync(IEnumerable<int> containerItemNumbers)
{
    var keys = containerItemNumbers.Distinct().ToList();
    if (keys.Count == 0)
        return new Dictionary<int, int>();

    var caps = await _db.ContainerCapacities
        .Where(c => keys.Contains(c.ItemNumber) &&
                    c.Unit != null &&
                    c.Unit.Trim().ToUpper() == "L")
        .Select(c => new { c.ItemNumber, c.Capacity })
        .ToListAsync();

    return caps
        .Where(x => x.Capacity > 0)
        .GroupBy(x => x.ItemNumber)
        .ToDictionary(g => g.Key, g => g.First().Capacity);
}



        // GET api/stena/efficiency/summary?months=12&thresholdPct=80&liters=660
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

            // NOTE: join kræver at StenaReceipt.PurchaseOrderNumber er mappet til Modtagelse.KoebsordreNummer
            // og StenaKoerselsordre.PurchaseOrderNumber er mappet til Koerselsordrer.Koebsordrenr
            var raw = await (
                from m in _db.StenaReceipts
join k in _db.StenaKoerselsordrer
    on new { Lev = m.CustomerKey, Koeb = m.PurchaseOrderNumber }
    equals new { Lev = k.Lev_nr, Koeb = k.PurchaseOrderNumber }
    into jk
from k in jk.DefaultIfEmpty()

                where m.ReceiptDate >= sinceDate
                      && m.CustomerKey.HasValue
                      && m.Unit != null
                      && m.PurchaseOrderNumber.HasValue
                select new
                {
                    CustomerKey = m.CustomerKey!.Value,
                    CustomerName = m.CustomerName,
                    Unit = m.Unit,
                    Amount = m.Amount,
                    ContainerItemNumber = k != null ? k.Varenr : (int?)null,
                    OrderDescription = k != null ? k.Beskrivelse : null
                }
            ).ToListAsync();

            var kgRows = raw.Where(r => NormalizeUnit(r.Unit) == "KG").ToList();
            if (kgRows.Count == 0)
                return Ok(new List<ContainerEfficiencySummaryDto>());

            var itemNumbers = kgRows
                .Select(r => r.ContainerItemNumber)
                .Where(v => v.HasValue && v.Value > 0)
                .Select(v => v!.Value)
                .Distinct()
                .ToList();

            var capacityLookup = await BuildCapacityLookupAsync(itemNumbers);

            var events = new List<(int CustomerKey, string CustomerName, decimal FillPct)>();

            foreach (var r in kgRows)
            {
                int? capLiters = null;

                if (r.ContainerItemNumber.HasValue &&
                    capacityLookup.TryGetValue(r.ContainerItemNumber.Value, out var lFromTable))
                {
                    capLiters = lFromTable;
                }
                else
                {
                    capLiters = TryParseLitersFromDescription(r.OrderDescription);
                }

                if (capLiters is null || capLiters.Value != liters)
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

        // GET api/stena/efficiency/customer/{levNr}?months=12&thresholdPct=80&liters=660
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

            var raw = await (
                from m in _db.StenaReceipts
join k in _db.StenaKoerselsordrer
    on new { Lev = m.CustomerKey, Koeb = m.PurchaseOrderNumber }
    equals new { Lev = k.Lev_nr, Koeb = k.PurchaseOrderNumber }
    into jk
from k in jk.DefaultIfEmpty()

                where m.ReceiptDate >= sinceDate
                      && m.CustomerKey == levNr
                      && m.Unit != null
                      && m.PurchaseOrderNumber.HasValue
                orderby m.ReceiptDate descending
                select new
                {
                    CustomerName = m.CustomerName,
                    Unit = m.Unit,
                    ReceiptDate = m.ReceiptDate,
                    Amount = m.Amount,
                    ContainerItemNumber = k != null ? k.Varenr : (int?)null,
                    OrderDescription = k != null ? k.Beskrivelse : null
                }
            ).ToListAsync();

            var kgRows = raw.Where(r => NormalizeUnit(r.Unit) == "KG").ToList();
            if (kgRows.Count == 0)
                return NotFound($"Ingen KG-modtagelser fundet for kunde {levNr} i perioden.");

            var itemNumbers = kgRows
                .Select(r => r.ContainerItemNumber)
                .Where(v => v.HasValue && v.Value > 0)
                .Select(v => v!.Value)
                .Distinct()
                .ToList();

            var capacityLookup = await BuildCapacityLookupAsync(itemNumbers);

            var empties = new List<ContainerEmptyingDto>();

            foreach (var r in kgRows)
            {
                int? capLiters = null;

                if (r.ContainerItemNumber.HasValue &&
                    capacityLookup.TryGetValue(r.ContainerItemNumber.Value, out var lFromTable))
                {
                    capLiters = lFromTable;
                }
                else
                {
                    capLiters = TryParseLitersFromDescription(r.OrderDescription);
                }

                if (capLiters is null || capLiters.Value != liters)
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
                TotalEmpties = total,
                InefficientEmpties = inefficient,
                InefficientPct = total > 0 ? 100f * inefficient / total : 0f,
                AvgFillPct = total > 0 ? empties.Average(e => e.FillPct) : 0f,
                Empties = empties
            });
        }
    }
}
