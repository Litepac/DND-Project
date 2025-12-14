using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DNDProject.Api.Data;

namespace DNDProject.Api.Controllers
{
    [ApiController]
    [Route("api/stena/raw")]
    public class StenaRawController : ControllerBase
    {
        private readonly AppDbContext _db;

        public StenaRawController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<StenaRawSummaryDto>> GetSummary()
        {
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();

            async Task<int> CountAsync(string tableSqlName)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT COUNT(*) FROM {tableSqlName}";
                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }

            var dto = new StenaRawSummaryDto
            {
                KapacitetOgEnhedRows =
                    await CountAsync("[dbo].[Kapacitet_og_enhed_opdateret]"),

                KoerselsordrerRows =
                    await CountAsync("[dbo].[Kørselsordrer]"),

                ModtagelseRows =
                    await CountAsync("[dbo].[Modtagelse]"),

                Modtagelsesinfo2024Rows =
                    await CountAsync("[dbo].[Modtagelsesinformationer 01-01-2024 31-12-2024]"),

                Modtagelsesinfo2025Rows =
                    await CountAsync("[dbo].[Modtagelsesinformationer 01-01-2025 31-12-2025]")
            };

            return Ok(dto);
        }
    }

    public sealed class StenaRawSummaryDto
    {
        public int KapacitetOgEnhedRows { get; set; }
        public int KoerselsordrerRows   { get; set; }
        public int ModtagelseRows       { get; set; }

        public int Modtagelsesinfo2024Rows { get; set; }
        public int Modtagelsesinfo2025Rows { get; set; }

        public int ModtagelsesinfoTotal => 
            Modtagelsesinfo2024Rows + Modtagelsesinfo2025Rows;
    }
}
