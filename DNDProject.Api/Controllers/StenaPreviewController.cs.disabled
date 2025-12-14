using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using DNDProject.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DNDProject.Api.Controllers
{
    [ApiController]
    [Route("api/stena/preview")]
    public class StenaPreviewController : ControllerBase
    {
        private readonly AppDbContext _db;

        public StenaPreviewController(AppDbContext db)
        {
            _db = db;
        }

        // GET: /api/stena/preview/modtagelse?top=50
        [HttpGet("{table}")]
        public async Task<ActionResult<TablePreviewDto>> GetPreview(string table, [FromQuery] int top = 50)
        {
            // Vi vil KUN tillade bestemte tabeller (ingen SQL injection 🙃)
            string? tableSqlName = table.ToLower() switch
            {
                "modtagelse"      => "[dbo].[Modtagelse]",
                "koerselsordrer"  => "[dbo].[Kørselsordrer]",
                "kapacitet"       => "[dbo].[Kapacitet_og_enhed_opdateret]",
                _                 => null
            };

            if (tableSqlName is null)
                return BadRequest("Ukendt tabel. Brug: modtagelse, koerselsordrer eller kapacitet.");

            if (top <= 0 || top > 5000)
                top = 50;

            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT TOP (@top) * FROM {tableSqlName}";
            var pTop = cmd.CreateParameter();
            pTop.ParameterName = "@top";
            pTop.DbType = DbType.Int32;
            pTop.Value = top;
            cmd.Parameters.Add(pTop);

            using var reader = await cmd.ExecuteReaderAsync();

            var columns = Enumerable.Range(0, reader.FieldCount)
                .Select(reader.GetName)
                .ToList();

            var rows = new List<List<string?>>();

            while (await reader.ReadAsync())
            {
                var row = new List<string?>(reader.FieldCount);
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString();
                    row.Add(value);
                }
                rows.Add(row);
            }

            var dto = new TablePreviewDto
            {
                Columns = columns,
                Rows = rows
            };

            return Ok(dto);
        }
    }

    public class TablePreviewDto
    {
        public List<string> Columns { get; set; } = new();
        public List<List<string?>> Rows { get; set; } = new();
    }
}
