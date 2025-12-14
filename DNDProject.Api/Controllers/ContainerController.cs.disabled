using DNDProject.Api.Data;
using DNDProject.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace DNDProject.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContainersController : ControllerBase
{
    private readonly AppDbContext _db;
    public ContainersController(AppDbContext db) => _db = db;

    // GET: api/containers
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Container>>> GetAll()
        => await _db.Containers.AsNoTracking().ToListAsync();

    // GET: api/containers/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Container>> GetById(int id)
    {
        var c = await _db.Containers.FindAsync(id);
        return c is null ? NotFound() : c;
    }

    // POST: api/containers
    [HttpPost]
    [Consumes("application/json")]
    public async Task<ActionResult<Container>> Create([FromBody] Container input)
    {
        _db.Containers.Add(input);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = input.Id }, input);
    }

    // DELETE: api/containers/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await _db.Containers.FindAsync(id);
        if (c is null) return NotFound();

        _db.Containers.Remove(c);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // GET: api/containers/{id}/recommendation
    [HttpGet("{id:int}/recommendation")]
    public async Task<IActionResult> Recommendation(int id)
    {
        var c = await _db.Containers.FindAsync(id);
        if (c is null) return NotFound();

        var avg = c.LastFillPct ?? 70;
        var recDays = avg > 90 ? 7 : avg < 50 ? 21 : 14;
        return Ok(new { averageFillPct = avg, recommendedFrequencyDays = recDays });
    }
    // PUT: api/containers/{id}
    [HttpPut("{id:int}")]
    [Consumes("application/json")]
    public async Task<IActionResult> Update(int id, [FromBody] Container input)
    {
        var c = await _db.Containers.FindAsync(id);
        if (c is null) return NotFound();
        c.Type = input.Type;
        c.Material = input.Material;
        c.SizeLiters = input.SizeLiters;
        c.WeeklyAmountKg = input.WeeklyAmountKg;
        c.LastFillPct = input.LastFillPct;
        c.LastPickupDate = input.LastPickupDate;
        c.PreferredPickupFrequencyDays = input.PreferredPickupFrequencyDays;
        c.CustomerId = input.CustomerId;
    await _db.SaveChangesAsync();
    return NoContent();
}


}
