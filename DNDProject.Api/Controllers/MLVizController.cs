using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DNDProject.Api.ML;

namespace DNDProject.Api.Controllers;

[ApiController]
[Route("api/ml-viz")]
[Authorize]
public class MLVizController : ControllerBase
{
    private readonly MLDataService _data;

    public MLVizController(MLDataService data)
    {
        _data = data;
    }

    // Returnerer pr. kunde: antal dage med pickup, total KG, gennemsnit KG pr. dag,
    // samt sidste dato (super til dashboards)
    [HttpGet("customers")]
    public async Task<IActionResult> Customers(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var daily = await _data.LoadDailyPickupsAsync(from, to);

        var result = daily
            .Where(x => !string.IsNullOrWhiteSpace(x.CustomerNo))
            .GroupBy(x => new { x.CustomerNo, x.CustomerName })
            .Select(g => new
            {
                customerNo = g.Key.CustomerNo,
                customerName = g.Key.CustomerName,
                days = g.Count(),
                totalKg = g.Sum(x => x.CollectedKg),
                avgKgPerDay = g.Average(x => x.CollectedKg),
                lastDate = g.Max(x => x.Date)
            })
            .OrderByDescending(x => x.totalKg)
            .ToList();

        return Ok(new
        {
            from = from.Date,
            to = to.Date,
            customers = result.Count,
            top = result.Take(200) // begræns lidt i swagger/UI
        });
    }

    // Returnerer “daily pickups” for en enkelt kunde (perfekt til tidsserie-chart)
    [HttpGet("customer-daily")]
    public async Task<IActionResult> CustomerDaily(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string customerNo)
    {
        if (string.IsNullOrWhiteSpace(customerNo))
            return BadRequest("customerNo is required");

        var daily = await _data.LoadDailyPickupsAsync(from, to);

        var series = daily
            .Where(x => x.CustomerNo == customerNo)
            .OrderBy(x => x.Date)
            .Select(x => new
            {
                date = x.Date,
                collectedKg = x.CollectedKg,
                skabelonnr = x.Skabelonnr,
                customerName = x.CustomerName
            })
            .ToList();

        return Ok(new
        {
            customerNo,
            from = from.Date,
            to = to.Date,
            points = series.Count,
            series
        });
    }
}
