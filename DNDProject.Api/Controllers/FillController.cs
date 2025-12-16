using Microsoft.AspNetCore.Mvc;
using DNDProject.Api.ML.Tools;

namespace DNDProject.Api.Controllers;

[ApiController]
[Route("api/fill")]
public sealed class FillController : ControllerBase
{
    public sealed record FillRequest(
        double KgPerDay,
        double DensityKgPerLiter,
        int FrequencyDays,
        int ContainerSizeLiters,
        int ContainerCount
    );

    public sealed record FillResponse(double ExpectedFill, double ExpectedFillPercent);

    [HttpPost("calc")]
    public ActionResult<FillResponse> Calc([FromBody] FillRequest req)
    {
        var fill = FillCalculator.ExpectedFill(
            req.KgPerDay,
            req.DensityKgPerLiter,
            req.FrequencyDays,
            req.ContainerSizeLiters,
            req.ContainerCount);

        return Ok(new FillResponse(fill, fill * 100.0));
    }
}
