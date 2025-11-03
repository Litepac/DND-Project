using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting; // For IWebHostEnvironment
using System;
using System.IO;
using System.Linq;

namespace DNDProject.Api.Data
{
    public static class StenaTestReader
    {
        public static void Run(IWebHostEnvironment env)
        {
            var path = Path.Combine(env.ContentRootPath, "Resources", "Kapacitet_og_enhed_opdateret.xlsx");

            if (!File.Exists(path))
            {
                Console.WriteLine($"❌ Fandt ikke filen: {path}");
                return;
            }

            Console.WriteLine($"✅ Åbner: {Path.GetFileName(path)}");

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheets.First();

            // Hent kolonnenavne (øverste række)
            var headers = ws.Row(1).Cells().Select((c, i) => new { Index = i, Name = c.GetString() }).ToList();
            Console.WriteLine("📄 Kolonner fundet: " + string.Join(" | ", headers.Select(h => h.Name)));

            // Find kolonnen med "Enhed" eller "Container" i navnet
            var enhedCol = headers.FirstOrDefault(h =>
                h.Name.Contains("Enhed", StringComparison.OrdinalIgnoreCase) ||
                h.Name.Contains("Container", StringComparison.OrdinalIgnoreCase));

            if (enhedCol == null)
            {
                Console.WriteLine("⚠️ Ingen kolonne med navn der ligner 'Enhednr' fundet!");
                return;
            }

            Console.WriteLine($"\n📦 Læser 'Enhednr' fra kolonne '{enhedCol.Name}':\n");

            // Udskriv de første 10 rækker (uden overskriften)
            foreach (var row in ws.RowsUsed().Skip(1).Take(10))
            {
                var id = row.Cell(enhedCol.Index + 1).GetString();
                if (!string.IsNullOrWhiteSpace(id))
                    Console.WriteLine($"→ {id}");
            }
        }
    }
}
