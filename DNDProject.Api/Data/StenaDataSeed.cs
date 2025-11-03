using ClosedXML.Excel;
using DNDProject.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DNDProject.Api.Data
{
    public static class StenaDataSeed
    {
public static async Task SeedAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var sp  = scope.ServiceProvider;
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var db  = sp.GetRequiredService<AppDbContext>();

    // 1) kapacitet + kørsler kan du godt køre hver gang (de er små)
    await ImportKapacitetAsync(db, env);
    await ImportKoerslerAsync(db, env);

    // 2) men modtagelserne er KÆMPE — tjek om vi allerede har noget
    if (!await db.StenaReceipts.AnyAsync())
    {
        await ImportReceiptsAsync(db, env, "Modtagelsesinformationer 01-01-2024 31-12-2024.xlsx");
        await ImportReceiptsAsync(db, env, "Modtagelsesinformationer 01-01-2025 30-09-2025.xlsx");
    }
    else
    {
        Console.WriteLine("ℹ️  StenaReceipts har allerede data – springer stor import over.");
    }
}


        // ------------------ 1) Kapacitet ------------------
        private static async Task ImportKapacitetAsync(AppDbContext db, IWebHostEnvironment env)
        {
            var path = Path.Combine(env.ContentRootPath, "Resources", "Kapacitet_og_enhed_opdateret.xlsx");
            if (!File.Exists(path))
            {
                Console.WriteLine($"❌ Fandt ikke filen: {path}");
                return;
            }

            Console.WriteLine($"✅ Åbner Stena-data: {Path.GetFileName(path)}");

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheets.First();
            var headers = ws.Row(1).Cells().Select((c, i) => new { Index = i, Name = c.GetString().Trim() }).ToList();

            var enhedCol = headers.FirstOrDefault(h =>
                h.Name.Contains("Enhed", StringComparison.OrdinalIgnoreCase) ||
                h.Name.Contains("Container", StringComparison.OrdinalIgnoreCase));

            if (enhedCol == null)
            {
                Console.WriteLine("⚠️ Ingen kolonne med 'Enhednr' fundet!");
                return;
            }

            int added = 0;
            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var id = row.Cell(enhedCol.Index + 1).GetString().Trim();
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (!await db.Containers.AnyAsync(c => c.ExternalId == id))
                {
                    db.Containers.Add(new Container
                    {
                        ExternalId = id,
                        Type = "Ukendt"
                    });
                    added++;
                }
            }

            await db.SaveChangesAsync();
            Console.WriteLine($"✅ Import færdig – {added} nye containere oprettet.");
        }

        // ------------------ 2) Kørsler ------------------
        private static async Task ImportKoerslerAsync(AppDbContext db, IWebHostEnvironment env)
        {
            var path = Path.Combine(env.ContentRootPath, "Resources", "Kørselsordrer.xlsx");
            if (!File.Exists(path))
            {
                Console.WriteLine($"⚠️  Skipper kørsler: kunne ikke finde {path}");
                return;
            }

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheets.First();
            var used = ws.RangeUsed();
            if (used is null)
            {
                Console.WriteLine("⚠️  Kørsler: intet brugt område fundet.");
                return;
            }

            var headers = used.FirstRowUsed().Cells().Select((c, i) => (Index: i + 1, Name: c.GetString().Trim())).ToArray();
            var issCol  = FindHeader(headers, "ISS Container");
            var dateCol = FindHeader(headers, "Start dato") ?? FindHeader(headers, "Dato");

            if (issCol is null || dateCol is null)
            {
                Console.WriteLine("⚠️ Kørsler: mangler ISS eller dato.");
                return;
            }

            // preload containers
            var containerList = await db.Containers.ToListAsync();
            var containers = new Dictionary<string, Container>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in containerList)
                if (!string.IsNullOrWhiteSpace(c.ExternalId))
                    containers[c.ExternalId] = c;

            int added = 0, skippedNoDate = 0, skippedNoContainer = 0, matchedNoContainer = 0;

            foreach (var row in used.RowsUsed().Skip(1))
            {
                DateTime? ts = null;
                var dc = row.Cell(dateCol.Value);
                if (dc.DataType == XLDataType.DateTime && dc.GetDateTime() != default)
                    ts = dc.GetDateTime().Date;
                else if (TryParseDkDate(dc.GetString(), out var dt))
                    ts = dt.Date;
                if (ts is null) { skippedNoDate++; continue; }

                var raw = row.Cell(issCol.Value).GetString().Trim();
                var ids = ExtractContainerIds(raw);
                if (ids.Length == 0) { skippedNoContainer++; continue; }

                foreach (var extId in ids)
                {
                    if (!containers.TryGetValue(extId, out var container))
                    {
                        matchedNoContainer++;
                        continue;
                    }

                    var exists = await db.PickupEvents.AnyAsync(e => e.ContainerId == container.Id && e.Timestamp == ts);
                    if (!exists)
                    {
                        db.PickupEvents.Add(new PickupEvent
                        {
                            ContainerId = container.Id,
                            Timestamp   = ts.Value
                        });
                        added++;
                    }
                }
            }

            await db.SaveChangesAsync();
            Console.WriteLine($"✅ Kørsler import: +{added} events, skippet uden dato: {skippedNoDate}, skippet uden ISS-id: {skippedNoContainer}, manglede match i DB: {matchedNoContainer}");
        }

        // ------------------ 3) Modtagelser → StenaReceipts ------------------
        private static async Task ImportReceiptsAsync(AppDbContext db, IWebHostEnvironment env, string fileName)
        {
            var path = Path.Combine(env.ContentRootPath, "Resources", fileName);
            if (!File.Exists(path))
            {
                Console.WriteLine($"⚠️  Skipper {fileName}: filen findes ikke.");
                return;
            }

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheets.First();
            var used = ws.RangeUsed();
            if (used is null)
            {
                Console.WriteLine($"⚠️  {fileName}: tomt ark.");
                return;
            }

            var headers = used.FirstRowUsed().Cells().Select((c, i) => (Index: i + 1, Name: c.GetString().Trim())).ToArray();

            int? itemNoCol    = FindHeader(headers, "Varenummer", "Varenr", "Varenr.");
            int? itemNameCol  = FindHeader(headers, "Varebeskrivelse", "Varenavn");
            int? unitCol      = FindHeader(headers, "Enhed");
            int? amountCol    = FindHeader(headers, "Antal");
            int? dateCol      = FindHeader(headers, "Modtaget dato", "Dato", "Afhent", "Afh. dato", "Afh.dato");
            int? eakCol       = FindHeader(headers, "EAK kode", "EAK", "EAK kode ");
            int? containerCol = FindHeader(headers, "Container nr.", "Enhednr", "Container", "Containernr");

            Console.WriteLine($"📦 {fileName}: cols → Item:{itemNoCol}, Name:{itemNameCol}, Unit:{unitCol}, Amount:{amountCol}, Date:{dateCol}, EAK:{eakCol}");

            int saved = 0;

            foreach (var row in used.RowsUsed().Skip(1))
            {
                var itemNo   = itemNoCol    is null ? null : row.Cell(itemNoCol.Value).GetString().Trim();
                var itemName = itemNameCol  is null ? null : row.Cell(itemNameCol.Value).GetString().Trim();
                var unit     = unitCol      is null ? null : row.Cell(unitCol.Value).GetString().Trim();
                var amountS  = amountCol    is null ? null : row.Cell(amountCol.Value).GetString().Trim();
                var eak      = eakCol       is null ? null : row.Cell(eakCol.Value).GetString().Trim();
                var rawCont  = containerCol is null ? null : row.Cell(containerCol.Value).GetString().Trim();

                double? amount = ParseDouble(amountS);

                DateTime? ts = null;
                if (dateCol is not null)
                {
                    var dc = row.Cell(dateCol.Value);
                    if (dc.DataType == XLDataType.DateTime && dc.GetDateTime() != default)
                        ts = dc.GetDateTime().Date;
                    else if (TryParseDkDate(dc.GetString(), out var dt))
                        ts = dt.Date;
                }

                // klassificering
                var kind = StenaReceiptKind.Unknown;
                if (!string.IsNullOrWhiteSpace(unit))
                {
                    if (unit.Equals("KG", StringComparison.OrdinalIgnoreCase))
                        kind = StenaReceiptKind.Weight;
                    else if (unit.Equals("STK", StringComparison.OrdinalIgnoreCase))
                        kind = StenaReceiptKind.Emptying;
                }

                // prøv at udlede container-type fra varebeskrivelse
                string? containerTypeText = null;
                if (!string.IsNullOrWhiteSpace(itemName))
                {
                    var lower = itemName.ToLowerInvariant();
                    if (lower.Contains("1100"))
                        containerTypeText = "1100L";
                    else if (lower.Contains("16m3") || lower.Contains("16 m3"))
                        containerTypeText = "16m3";
                    else if (lower.Contains("minicontainer"))
                        containerTypeText = "minicontainer";
                    else if (lower.Contains("container"))
                        containerTypeText = "container";
                }

                db.StenaReceipts.Add(new StenaReceipt
                {
                    Date             = ts,
                    ItemNumber       = itemNo,
                    ItemName         = itemName,
                    Unit             = unit,
                    Amount           = amount,
                    EakCode          = eak,
                    Kind             = kind,
                    ContainerTypeText= containerTypeText,
                    SourceFile       = fileName,
                    RawContainer     = rawCont
                });

                saved++;
            }

            await db.SaveChangesAsync();
            Console.WriteLine($"✅ {fileName}: {saved} rækker gemt i StenaReceipts.");
        }

        // ------------------ helpers ------------------
        private static int? FindHeader((int Index, string Name)[] headers, params string[] candidates)
        {
            foreach (var cand in candidates)
            {
                var h = headers.FirstOrDefault(h => h.Name.Equals(cand, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(h.Name))
                    return h.Index;
            }
            foreach (var cand in candidates)
            {
                var h = headers.FirstOrDefault(h => h.Name.Contains(cand, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(h.Name))
                    return h.Index;
            }
            return null;
        }

        private static double? ParseDouble(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var norm = s.Replace(" ", "").Replace(",", ".");
            return double.TryParse(norm, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : (double?)null;
        }

        private static bool TryParseDkDate(string? s, out DateTime dt)
        {
            dt = default;
            if (string.IsNullOrWhiteSpace(s)) return false;

            var dk = CultureInfo.GetCultureInfo("da-DK");
            var formats = new[] { "dd-MM-yy", "dd-MM-yyyy", "d-M-yy", "d-M-yyyy" };

            if (DateTime.TryParseExact(s.Trim(), formats, dk, DateTimeStyles.None, out dt))
            {
                if (dt.Year < 100) dt = new DateTime(dt.Year + 2000, dt.Month, dt.Day);
                return true;
            }

            return DateTime.TryParse(s, dk, DateTimeStyles.AssumeLocal, out dt);
        }

        private static string[] ExtractContainerIds(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return Array.Empty<string>();

            string[] weekdays = {
                "MANDAG","TIRSDAG","ONSDAG","TORSDAG","FREDAG","LØRDAG","SØNDAG",
                "MAN","TIRS","ONS","TORS","FRE","LØR","SØN"
            };

            var up = input.ToUpperInvariant();
            foreach (var w in weekdays)
                up = Regex.Replace(up, $@"\b{Regex.Escape(w)}\b", "", RegexOptions.CultureInvariant);

            var parts = up.Replace("\r", " ").Replace("\n", " ")
                          .Split(new[] { '+', ',', '/', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(p => p.Trim().TrimEnd('.'))
                          .Where(p => !string.IsNullOrWhiteSpace(p))
                          .ToList();

            var numRe      = new Regex(@"^\d{6,}$", RegexOptions.CultureInvariant);
            var alphaNumRe = new Regex(@"^(?=.*[A-Z])(?=.*\d)[A-Z0-9]{3,}$", RegexOptions.IgnoreCase);

            return parts
                .Where(p => numRe.IsMatch(p) || alphaNumRe.IsMatch(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
