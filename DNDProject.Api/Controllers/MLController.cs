using DNDProject.Api.ML;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNDProject.Api.Controllers;

[ApiController]
[Route("api/ml")]
[Authorize]
public class MLController : ControllerBase
{
    private readonly MLTrainerService _trainer;

    public MLController(MLTrainerService trainer)
    {
        _trainer = trainer;
    }

    // Træn model og returnér metrics
    [HttpPost("train")]
    public async Task<IActionResult> Train(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var result = await _trainer.TrainAsync(
            from ?? new DateTime(2024, 1, 1),
            to   ?? new DateTime(2024, 12, 31)
        );

        return Ok(result);
    }

    // B) Lav anbefalinger pr. kunde (container + frekvens)
    // (kan godt være POST, men GET er også OK her – jeg lader den være som du har den)
    [HttpPost("recommend")]
    public async Task<IActionResult> Recommend(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var list = await _trainer.RecommendAsync(
            from ?? new DateTime(2024, 1, 1),
            to   ?? new DateTime(2024, 12, 31)
        );

        return Ok(new
        {
            from = (from ?? new DateTime(2024, 1, 1)).Date,
            to = (to ?? new DateTime(2024, 12, 31)).Date,
            count = list.Count,
            items = list
        });
    }

    // C) Anbefaling for én kunde
    [HttpGet("recommend-one")]
    public async Task<IActionResult> RecommendOne(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? customerNo = null)
    {
        if (string.IsNullOrWhiteSpace(customerNo))
            return BadRequest("customerNo is required");

        var result = await _trainer.RecommendOneAsync(
            from ?? new DateTime(2024, 1, 1),
            to   ?? new DateTime(2024, 12, 31),
            customerNo
        );

        if (result is null) return NotFound();
        return Ok(result);
    }
}
