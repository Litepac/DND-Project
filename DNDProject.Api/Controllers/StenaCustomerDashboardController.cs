using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DNDProject.Api.Data;
using DNDProject.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DNDProject.Api.Controllers
{
    [ApiController]
    [Route("api/stena/customers")]
    public class StenaCustomerDashboardController : ControllerBase
    {
        private readonly AppDbContext _db;

        // tærskel for “ineffektiv” kunde
        private const decimal LowAverageWeightThresholdKg = 100m;

        public StenaCustomerDashboardController(AppDbContext db)
        {
            _db = db;
        }

        // Hjælpefunktion til at parse "Antal" (string) til decimal kilo
        private static decimal ParseAmountToDecimal(string? amountText)
        {
            if (string.IsNullOrWhiteSpace(amountText))
                return 0m;

            // prøv først invariant (1234.56)
            if (decimal.TryParse(amountText, NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var d))
                return d;

            // prøv dansk (1234,56)
            if (decimal.TryParse(amountText, NumberStyles.Any,
                    new CultureInfo("da-DK"), out d))
                return d;

            // hvis alt fejler, så tæller den bare som 0
            return 0m;
        }

        // GET: api/stena/customers/summary
        // Oversigt over alle kunder: total vægt, antal modtagelser, periode, ineffektiv-flag
        [HttpGet("summary")]
        public async Task<ActionResult<List<CustomerSummaryDto>>> GetCustomerSummary()
        {
            // Hent kun de felter vi bruger – stadig 1 DB-call
            var raw = await _db.StenaReceipts
                .Select(r => new
                {
                    r.CustomerKey,
                    r.CustomerName,
                    r.ReceiptDate,
                    r.Unit,
                    r.Amount
                })
                .ToListAsync();

            // Smid rækker uden customerKey væk (kan tilpasses)
            var data = raw
                .Where(r => r.CustomerKey != null)
                .GroupBy(r => new { r.CustomerKey, r.CustomerName })
                .Select(g =>
                {
                    // total kilo (kun KG-rækker)
                    var weightKg = g
                        .Where(x => x.Unit == "KG")
                        .Sum(x => ParseAmountToDecimal(x.Amount));

                    var receiptCount = g.Count();
                    var avgPerReceipt = receiptCount == 0
                        ? 0m
                        : weightKg / receiptCount;

                    return new CustomerSummaryDto
                    {
                        CustomerKey = g.Key.CustomerKey?.ToString() ?? string.Empty,
                        CustomerName = g.Key.CustomerName,

                        TotalWeightKg = (float)weightKg,
                        ReceiptCount = receiptCount,
                        FirstDate = g.Min(x => x.ReceiptDate),
                        LastDate = g.Max(x => x.ReceiptDate),
                        AverageWeightPerReceiptKg = (float)avgPerReceipt,
                        IsLowAverageWeight = avgPerReceipt < LowAverageWeightThresholdKg
                    };
                })
                .OrderByDescending(x => x.TotalWeightKg)
                .ToList();

            return Ok(data);
        }

        // GET: api/stena/customers/{customerKey}/dashboard?months=6
        // Detaljer for én kunde + time series (vægt pr. måned)
        [HttpGet("{customerKey}/dashboard")]
        public async Task<ActionResult<CustomerDashboardDto>> GetCustomerDashboard(
            string customerKey,
            [FromQuery] int months = 6)
        {
            if (!int.TryParse(customerKey, out var customerKeyInt))
                return BadRequest($"Ugyldigt customerKey: '{customerKey}'.");

            if (months <= 0 || months > 36)
                months = 6;

            var sinceDate = DateTime.Today.AddMonths(-months);

            var raw = await _db.StenaReceipts
                .Where(r => r.CustomerKey == customerKeyInt &&
                            r.ReceiptDate >= sinceDate)
                .Select(r => new
                {
                    r.CustomerKey,
                    r.CustomerName,
                    r.ReceiptDate,
                    r.Unit,
                    r.Amount
                })
                .ToListAsync();

            if (!raw.Any())
                return NotFound(
                    $"Ingen data for kunde '{customerKey}' i de sidste {months} måneder.");

            var weightKgTotal = raw
                .Where(x => x.Unit == "KG")
                .Sum(x => ParseAmountToDecimal(x.Amount));

            var receiptCount = raw.Count;
            var avgPerReceipt = receiptCount == 0
                ? 0m
                : weightKgTotal / receiptCount;

            var summary = new CustomerSummaryDto
            {
                CustomerKey = raw.First().CustomerKey?.ToString() ?? string.Empty,
                CustomerName = raw.First().CustomerName,
                TotalWeightKg = (float)weightKgTotal,
                ReceiptCount = receiptCount,
                FirstDate = raw.Min(x => x.ReceiptDate),
                LastDate = raw.Max(x => x.ReceiptDate),
                AverageWeightPerReceiptKg = (float)avgPerReceipt,
                IsLowAverageWeight = avgPerReceipt < LowAverageWeightThresholdKg
            };

            // tidsserie: pr. måned, kun KG
            var timeseries = raw
                .Where(x => x.Unit == "KG")
                .GroupBy(x => new { x.ReceiptDate.Year, x.ReceiptDate.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g =>
                {
                    var monthWeight = g.Sum(x => ParseAmountToDecimal(x.Amount));

                    return new CustomerTimeseriesPointDto
                    {
                        PeriodStart = new DateTime(g.Key.Year, g.Key.Month, 1),
                        Label = $"{g.Key.Year}-{g.Key.Month:00}",
                        TotalWeightKg = (float)monthWeight
                    };
                })
                .ToList();

            var dto = new CustomerDashboardDto
            {
                Summary = summary,
                Timeseries = timeseries
            };

            return Ok(dto);
        }
    }
}
