using DNDProject.Api.Data;
using DNDProject.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DNDProject.Api.Controllers;

[ApiController]
[Route("api/containers/{containerId:int}/events")]
public class ContainerEventsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ContainerEventsController(AppDbContext db) => _db = db;

    // GET:
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PickupEvent>>> Get(
        int containerId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200)
    {
        var q = _db.PickupEvents.AsNoTracking().Where(e => e.ContainerId == containerId);
        if (from is not null) q = q.Where(e => e.Timestamp >= from);
        if (to   is not null) q = q.Where(e => e.Timestamp <= to);

        var list = await q.OrderByDescending(e => e.Timestamp)
                          .Skip((page - 1) * pageSize)
                          .Take(pageSize)
                          .ToListAsync();

        return list;
    }

    // POST: api/containers/1/events  (til import senere)
    [HttpPost]
    public async Task<IActionResult> Create(int containerId, PickupEvent input)
    {
        input.ContainerId = containerId;
        _db.PickupEvents.Add(input);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { containerId }, input);
    }
}
