using DNDProject.Api.Data;
using DNDProject.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DNDProject.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StenaController : ControllerBase
    {
        private readonly AppDbContext _db;

        public StenaController(AppDbContext db)
        {
            _db = db;
        }

        // -------------------------------------------------------------
        // DASHBOARD
        // -------------------------------------------------------------
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            // alle receipts
            var total = await _db.StenaReceipts.CountAsync();

            // kun vægt (KG)
            var totalKg = await _db.StenaReceipts
                .Where(x => x.Kind == StenaReceiptKind.Weight && x.Amount != null)
                .SumAsync(x => x.Amount!.Value);

            // kun tømninger (STK)
            var totalEmptyings = await _db.StenaReceipts
                .Where(x => x.Kind == StenaReceiptKind.Emptying)
                .CountAsync();

            // top 5 container-typer
            var topTypes = await _db.StenaReceipts
                .Where(x => x.Kind == StenaReceiptKind.Emptying && x.ContainerTypeText != null)
                .GroupBy(x => x.ContainerTypeText!)
                .Select(g => new { type = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .Take(5)
                .ToListAsync();

            // seneste 20 linjer (til tabel)
            var latest = await _db.StenaReceipts
                .OrderByDescending(x => x.Id)
                .Take(20)
                .Select(x => new
                {
                    x.Date,
                    x.ItemNumber,
                    x.ItemName,
                    x.Unit,
                    x.Amount,
                    x.Kind,
                    x.ContainerTypeText
                })
                .ToListAsync();

            return Ok(new
            {
                totalRows = total,
                totalKg,
                totalEmptyings,
                topTypes,
                latest
            });
        }

        // -------------------------------------------------------------
        // NYE ENDPOINTS
        // -------------------------------------------------------------

        // 1) Vægt pr. dag (seneste X dage ud fra nyeste dato i data)
        [HttpGet("weights/daily")]
        public async Task<IActionResult> GetDailyWeights([FromQuery] int days = 30)
        {
            // find seneste dato i databasen
            var maxDate = await _db.StenaReceipts
                .Where(x => x.Kind == StenaReceiptKind.Weight && x.Date != null)
                .MaxAsync(x => x.Date);

            if (maxDate == null)
                return Ok(Array.Empty<object>());

            var since = maxDate.Value.Date.AddDays(-days);

            var data = await _db.StenaReceipts
                .Where(x => x.Kind == StenaReceiptKind.Weight &&
                            x.Date >= since && x.Date <= maxDate)
                .GroupBy(x => x.Date!.Value.Date)
                .Select(g => new
                {
                    date = g.Key,
                    totalKg = g.Sum(x => x.Amount ?? 0)
                })
                .OrderBy(x => x.date)
                .ToListAsync();

            return Ok(data);
        }

        // 2) Tømninger pr. container-type (seneste X dage ud fra nyeste dato)
        [HttpGet("emptyings/top")]
        public async Task<IActionResult> GetTopEmptyings([FromQuery] int days = 30)
        {
            var maxDate = await _db.StenaReceipts
                .Where(x => x.Kind == StenaReceiptKind.Emptying && x.Date != null)
                .MaxAsync(x => x.Date);

            if (maxDate == null)
                return Ok(Array.Empty<object>());

            var since = maxDate.Value.Date.AddDays(-days);

            var data = await _db.StenaReceipts
                .Where(x => x.Kind == StenaReceiptKind.Emptying &&
                            x.Date >= since && x.Date <= maxDate)
                .GroupBy(x => x.ContainerTypeText ?? "(ukendt)")
                .Select(g => new
                {
                    type = g.Key,
                    count = g.Count()
                })
                .OrderByDescending(x => x.count)
                .Take(10)
                .ToListAsync();

            return Ok(data);
        }

        // 3) Seneste vægt-linjer (så du kan se konkrete data)
        [HttpGet("weights/latest")]
        public async Task<IActionResult> GetLatestWeights([FromQuery] int take = 50)
        {
            var data = await _db.StenaReceipts
                .Where(x => x.Kind == StenaReceiptKind.Weight)
                .OrderByDescending(x => x.Date)
                .ThenByDescending(x => x.Id)
                .Take(take)
                .Select(x => new
                {
                    x.Date,
                    x.ItemNumber,
                    x.ItemName,
                    x.Unit,
                    x.Amount,
                    x.EakCode
                })
                .ToListAsync();

            return Ok(data);
        }
    }
}
