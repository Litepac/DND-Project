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
        [HttpGet("summary")]
        public async Task<ActionResult<List<CustomerSummaryDto>>> GetCustomerSummary()
        {
            var raw = await _db.StenaReceipts
                // Ignorer rækker uden kunde-id (LevNr = NULL)
                .Where(r => r.CustomerKey != null)
                .Select(r => new
                {
                    r.CustomerKey,
                    r.CustomerName,
                    r.ReceiptDate,
                    r.Unit,
                    r.Amount
                })
                .ToListAsync();

            var data = raw
                .GroupBy(r => new { r.CustomerKey, r.CustomerName })
                .Select(g =>
                {
                    var weightKg = g
                        .Where(x => x.Unit == "KG")
                        .Sum(x => ParseAmountToDecimal(x.Amount));

                    return new CustomerSummaryDto
                    {
                        CustomerKey   = g.Key.CustomerKey?.ToString() ?? "UKENDT",
                        CustomerName  = g.Key.CustomerName ?? "(ukendt navn)",
                        TotalWeightKg = (float)weightKg,
                        ReceiptCount  = g.Count(),
                        FirstDate     = g.Min(x => x.ReceiptDate),
                        LastDate      = g.Max(x => x.ReceiptDate)
                    };
                })
                .OrderByDescending(x => x.TotalWeightKg)
                .ToList();

            return Ok(data);
        }

        // GET: api/stena/customers/{customerKey}/dashboard?months=6
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

            var first = raw.First();

            var summary = new CustomerSummaryDto
            {
                CustomerKey   = first.CustomerKey?.ToString() ?? "UKENDT",
                CustomerName  = first.CustomerName ?? "(ukendt navn)",
                TotalWeightKg = (float)weightKgTotal,
                ReceiptCount  = raw.Count,
                FirstDate     = raw.Min(x => x.ReceiptDate),
                LastDate      = raw.Max(x => x.ReceiptDate)
            };

            var timeseries = raw
                .Where(x => x.Unit == "KG")
                .GroupBy(x => new { x.ReceiptDate.Year, x.ReceiptDate.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g =>
                {
                    var monthWeight = g.Sum(x => ParseAmountToDecimal(x.Amount));

                    return new CustomerTimeseriesPointDto
                    {
                        PeriodStart   = new DateTime(g.Key.Year, g.Key.Month, 1),
                        Label         = $"{g.Key.Year}-{g.Key.Month:00}",
                        TotalWeightKg = (float)monthWeight
                    };
                })
                .ToList();

            var dto = new CustomerDashboardDto
            {
                Summary    = summary,
                Timeseries = timeseries
            };

            return Ok(dto);
        }
    }
}
