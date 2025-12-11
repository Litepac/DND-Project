using DNDProject.Api.Data;
using DNDProject.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DNDProject.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestDbController : ControllerBase
    {
        private readonly AppDbContext _db;

        public TestDbController(AppDbContext db)
        {
            _db = db;
        }

        // GET: /api/testdb
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Container>>> Get()
        {
            var data = await _db.Containers
                                .OrderBy(c => c.Id)
                                .Take(10)
                                .ToListAsync();

            return Ok(data);
        }
    }
}
