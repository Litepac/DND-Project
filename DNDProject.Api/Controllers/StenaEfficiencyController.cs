using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DNDProject.Api.Data;
using DNDProject.Api.Models;   // ← VIGTIG
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace DNDProject.Api.Controllers
{
    [ApiController]
    [Route("api/stena/efficiency")]
    public class StenaEfficiencyController : ControllerBase
    {
        private readonly AppDbContext _db;

        public StenaEfficiencyController(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Henter alle varenummer (som string) for 660 L-containere fra kapacitets-tabellen.
        /// </summary>
        private async Task<List<string>> Get660LItemNumbersAsync()
        {
            // Hent alle rækker hvor Kapacitet = 660 og Enhed = 'L'
            var nums = await _db.ContainerCapacities
                .Where(c => c.Capacity == 660 && c.Unit == "L")
                .Select(c => c.ItemNumber) // int
                .ToListAsync();

            // Konverter til string, så det matcher StenaReceipt.ItemNumber (nvarchar)
            return nums
                .Select(n => n.ToString(CultureInfo.InvariantCulture))
                .ToList();
        }

        /// <summary>
        /// Parser "Antal" (nvarchar) til decimal kg.
        /// </summary>
        private static decimal ParseAmountToDecimal(string? amountText)
        {
            if (string.IsNullOrWhiteSpace(amountText))
                return 0m;

            // Først invariant (1234.56)
            if (decimal.TryParse(amountText, NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var d))
                return d;

            // Så dansk (1234,56)
            if (decimal.TryParse(amountText, NumberStyles.Any,
                    new CultureInfo("da-DK"), out d))
                return d;

            return 0m;
        }

        // --------------------------------------------------------------------
        // 1) SUMMARY: pr. kunde – hvor mange tømninger under threshold osv.
        // GET: api/stena/efficiency/summary?months=12&thresholdPct=75
        // --------------------------------------------------------------------
        [HttpGet("summary")]
        public async Task<ActionResult<List<ContainerEfficiencySummaryDto>>> GetEfficiencySummary(
            [FromQuery] int months = 12,
            [FromQuery] int thresholdPct = 75)
        {
            if (months <= 0 || months > 36) months = 12;
            if (thresholdPct <= 0 || thresholdPct >= 100) thresholdPct = 75;

            var sinceDate = DateTime.Today.AddMonths(-months);

            // Hent 660 L varenummer som string
            var containerItemNos = await Get660LItemNumbersAsync();

            // Ingen 660 L-containere i kapacitets-tabellen?
            if (containerItemNos.Count == 0)
                return Ok(new List<ContainerEfficiencySummaryDto>());

            // Rå data for alle relevante tømninger
            var raw = await (from m in _db.StenaReceipts
                             where m.ReceiptDate >= sinceDate
                                   && m.Unit == "KG"
                                   && m.CustomerKey != null
                                   && m.ItemNumber != null
                                   && containerItemNos.Contains(m.ItemNumber)
                             select new
                             {
                                 m.CustomerKey,
                                 m.CustomerName,
                                 m.ReceiptDate,
                                 m.Amount
                             })
                .ToListAsync();

            // Gruppér pr. kunde og lav statistik
            const decimal capacityKg = 660m * 0.13m; // 660 L * 0,13 kg/L

            var grouped = raw
                .GroupBy(x => new { x.CustomerKey, x.CustomerName })
                .Select(g =>
                {
                    var weights = g
                        .Select(x => ParseAmountToDecimal(x.Amount))
                        .ToList();

                    var empties = weights
                        .Select(w =>
                        {
                            var fillPct = capacityKg > 0
                                ? (w / capacityKg) * 100m
                                : 0m;

                            return new
                            {
                                WeightKg = w,
                                FillPct = fillPct
                            };
                        })
                        .ToList();

                    var totalEmpties = empties.Count;
                    var inefficientEmpties = empties.Count(e => e.FillPct < thresholdPct);

                    var inefficientPct = totalEmpties > 0
                        ? (float)(100m * inefficientEmpties / totalEmpties)
                        : 0f;

                    var avgFillPct = totalEmpties > 0
                        ? (float)empties.Average(e => e.FillPct)
                        : 0f;

                    return new ContainerEfficiencySummaryDto
                    {
                        CustomerKey = g.Key.CustomerKey ?? 0,
                        CustomerName = g.Key.CustomerName ?? string.Empty,
                        TotalEmpties = totalEmpties,
                        InefficientEmpties = inefficientEmpties,
                        InefficientPct = inefficientPct,
                        AvgFillPct = avgFillPct
                    };
                })
                .OrderByDescending(x => x.InefficientPct)
                .ToList();

            return Ok(grouped);
        }

        // --------------------------------------------------------------------
        // 2) DETAILS for én kunde
        // GET: api/stena/efficiency/customer/{levNr}?months=12&thresholdPct=75
        // --------------------------------------------------------------------
        [HttpGet("customer/{levNr:int}")]
        public async Task<ActionResult<ContainerEfficiencyDetailDto>> GetCustomerEfficiency(
            int levNr,
            [FromQuery] int months = 12,
            [FromQuery] int thresholdPct = 75)
        {
            if (months <= 0 || months > 36) months = 12;
            if (thresholdPct <= 0 || thresholdPct >= 100) thresholdPct = 75;

            var sinceDate = DateTime.Today.AddMonths(-months);

            var containerItemNos = await Get660LItemNumbersAsync();
            if (containerItemNos.Count == 0)
                return NotFound("Ingen 660 L-containere fundet i kapacitetstabellen.");

            var raw = await (from m in _db.StenaReceipts
                             where m.ReceiptDate >= sinceDate
                                   && m.Unit == "KG"
                                   && m.CustomerKey == levNr
                                   && m.ItemNumber != null
                                   && containerItemNos.Contains(m.ItemNumber)
                             orderby m.ReceiptDate descending
                             select new
                             {
                                 m.ReceiptDate,
                                 m.Amount
                             })
                .ToListAsync();

            if (raw.Count == 0)
                return NotFound($"Ingen tømninger fundet for kunde {levNr} i perioden.");

            const decimal capacityKg = 660m * 0.13m;

            var empties = raw
                .Select(x =>
                {
                    var weightKg = ParseAmountToDecimal(x.Amount);
                    var fillPct = capacityKg > 0
                        ? (weightKg / capacityKg) * 100m
                        : 0m;

                    return new ContainerEmptyingDto
                    {
                        Date = x.ReceiptDate,
                        WeightKg = (float)weightKg,
                        FillPct = (float)fillPct
                    };
                })
                .ToList();

            var totalEmpties = empties.Count;
            var inefficientEmpties = empties.Count(e => e.FillPct < thresholdPct);
            var inefficientPct = totalEmpties > 0
                ? 100f * inefficientEmpties / totalEmpties
                : 0f;

            var avgFillPct = totalEmpties > 0
                ? empties.Average(e => e.FillPct)
                : 0f;

            var dto = new ContainerEfficiencyDetailDto
            {
                CustomerKey = levNr,
                // CustomerName kan evt. hentes hvis vi udvider select ovenfor
                TotalEmpties = totalEmpties,
                InefficientEmpties = inefficientEmpties,
                InefficientPct = inefficientPct,
                AvgFillPct = avgFillPct,
                Empties = empties
            };

            return Ok(dto);
        }
    }
}
