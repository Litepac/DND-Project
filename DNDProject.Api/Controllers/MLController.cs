using System.Text.Json;
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
    private readonly RecommendationEngine _engine;

    // Restaffald bulk density (kg/L) – jeres antagelse
    private const double ResidualDensityKgPerLiter = 0.13;

    public MLController(MLTrainerService trainer)
    {
        _trainer = trainer;
        _engine = new RecommendationEngine();
    }

    // Træn model og returnér metrics
    [HttpPost("train")]
    public async Task<IActionResult> Train(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var result = await _trainer.TrainAsync(
            from ?? new DateTime(2024, 1, 1),
            to ?? new DateTime(2024, 12, 31)
        );

        return Ok(result);
    }

    // B) Lav anbefalinger pr. kunde (container + frekvens)
    [HttpPost("recommend")]
    public async Task<IActionResult> Recommend(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var f = (from ?? new DateTime(2024, 1, 1)).Date;
        var t = (to ?? new DateTime(2024, 12, 31)).Date;

        var list = await _trainer.RecommendAsync(f, t);

        // Post-process: fix container valg (anti-“altid 1100”, anti “29x120” osv.)
        var fixedList = PostProcessList(list);

        return Ok(new
        {
            from = f,
            to = t,
            count = fixedList.Count,
            items = fixedList
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

        var f = (from ?? new DateTime(2024, 1, 1)).Date;
        var t = (to ?? new DateTime(2024, 12, 31)).Date;

        var result = await _trainer.RecommendOneAsync(f, t, customerNo);

        if (result is null) return NotFound();

        var fixedOne = PostProcessOne(result);
        return Ok(fixedOne);
    }

    // -----------------------------
    // Post-processing helpers
    // -----------------------------

    // Matcher din Blazor DTO (MLCustomer.razor)
    private sealed record RecommendationDto(
        string CustomerNo,
        string CustomerName,
        int ContainerL,
        int ContainerCount,
        int FrequencyDays,
        double ExpectedFill,
        double PredKgPerDaySafe
    );

    private RecommendationDto PostProcessOne(object trainerResult)
    {
        var dto = DeserializeToDto(trainerResult);

        // Hvis intet pred -> returnér bare hvad trainer gav
        if (dto.PredKgPerDaySafe <= 0 || dto.FrequencyDays <= 0)
            return dto;

        // Kør engine med jeres antagelser
        var eng = _engine.Recommend(
            predKgPerDaySafe: dto.PredKgPerDaySafe,
            densityKgPerLiter: ResidualDensityKgPerLiter,
            frequencyDays: dto.FrequencyDays,
            targetFill: 0.95,
            minFill: 0.80,
            maxFill: 1.05,
            maxContainers: 30
        );

        // Overskriv kun container/fill (frekvens beholdes som engine returnerer)
        return dto with
        {
            ContainerL = eng.ContainerSize,
            ContainerCount = eng.ContainerCount,
            FrequencyDays = eng.FrequencyDays,
            ExpectedFill = eng.ExpectedFill
        };
    }

    private List<RecommendationDto> PostProcessList(object trainerList)
    {
        // trainerList er typisk List<something>. Vi gør det robust via JSON.
        var json = JsonSerializer.Serialize(trainerList);
        var items = JsonSerializer.Deserialize<List<RecommendationDto>>(json) ?? new List<RecommendationDto>();

        for (var i = 0; i < items.Count; i++)
        {
            var dto = items[i];

            if (dto.PredKgPerDaySafe <= 0 || dto.FrequencyDays <= 0)
                continue;

            var eng = _engine.Recommend(
                predKgPerDaySafe: dto.PredKgPerDaySafe,
                densityKgPerLiter: ResidualDensityKgPerLiter,
                frequencyDays: dto.FrequencyDays,
                targetFill: 0.95,
                minFill: 0.80,
                maxFill: 1.05,
                maxContainers: 30
            );

            items[i] = dto with
            {
                ContainerL = eng.ContainerSize,
                ContainerCount = eng.ContainerCount,
                FrequencyDays = eng.FrequencyDays,
                ExpectedFill = eng.ExpectedFill
            };
        }

        return items;
    }

    private static RecommendationDto DeserializeToDto(object trainerResult)
    {
        var json = JsonSerializer.Serialize(trainerResult);
        var dto = JsonSerializer.Deserialize<RecommendationDto>(json);

        if (dto is null)
            throw new InvalidOperationException("Trainer-result kunne ikke parses til RecommendationDto (felter matcher ikke).");

        return dto;
    }
}
