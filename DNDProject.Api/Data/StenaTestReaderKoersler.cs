using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DNDProject.Api.Data
{
    public static class StenaTestReaderKoersler
    {
        public static void Run(IWebHostEnvironment env)
        {
            var path = Path.Combine(env.ContentRootPath, "Resources", "Kørselsordrer.xlsx");
            if (!File.Exists(path))
            {
                Console.WriteLine($"❌ Fandt ikke filen: {path}");
                return;
            }

            Console.WriteLine($"✅ Åbner: {Path.GetFileName(path)}");
            using var wb = new XLWorkbook(path);

            foreach (var ws in wb.Worksheets)
            {
                var used = ws.RangeUsed();
                if (used is null) continue;

                // Header antages at være første brugte række i arket "Kørselsordreskabelon varer"
                var headerRow = used.FirstRowUsed();
                var headers = headerRow.Cells().Select((c, i) => new { Index = i + 1, Name = c.GetString().Trim() }).ToList();

                // find relevante kolonner
                var issCol = headers.FirstOrDefault(h => h.Name.Contains("ISS Container", StringComparison.OrdinalIgnoreCase));
                var dateCol = headers.FirstOrDefault(h => h.Name.Contains("Start dato", StringComparison.OrdinalIgnoreCase)
                                                       || h.Name.Contains("Dato", StringComparison.OrdinalIgnoreCase));

                Console.WriteLine($"\n=== Ark: {ws.Name} ===");
                Console.WriteLine("Kolonner fundet: " + string.Join(" | ", headers.Select(h => h.Name)));
                if (issCol == null)
                {
                    Console.WriteLine("⚠️ Fandt ikke kolonnen 'ISS Container' i dette ark.");
                    continue;
                }
                if (dateCol == null)
                {
                    Console.WriteLine("⚠️ Fandt ikke kolonnen 'Start dato' i dette ark.");
                }

                var rows = used.RowsUsed().Skip(1).Take(25).ToList();
                Console.WriteLine($"\n📦 Eksempel (top {rows.Count} rækker):\n");

                foreach (var row in rows)
                {
                    var rawIss = row.Cell(issCol.Index).GetString().Trim();
                    var ids = ExtractContainerIds(rawIss);
                    string dateOut = "(ingen dato)";
                    if (dateCol != null)
                    {
                        var cell = row.Cell(dateCol.Index);
                        if (cell.DataType == XLDataType.DateTime && cell.GetDateTime() != default)
                        {
                            dateOut = cell.GetDateTime().ToString("yyyy-MM-dd");
                        }
                        else
                        {
                            var s = cell.GetString().Trim();
                            if (TryParseDkDate(s, out var dt))
                                dateOut = dt.ToString("yyyy-MM-dd");
                            else if (!string.IsNullOrEmpty(s))
                                dateOut = $"(ukendt format: {s})";
                        }
                    }

                    Console.WriteLine($"ISS rå: \"{rawIss}\"  →  [{string.Join(", ", ids)}]  |  Start dato: {dateOut}");
                }
            }
        }

        // Heuristisk udtræk: fjern ugedageord, split på + , / ; linjeskift, og behold tokens som:
        // - rene store tal (mindst 6 cifre) ELLER
        // - alfanumeriske koder med mindst 3 tegn og mindst 1 tal (fx MD10631, K180184L)
        private static readonly string[] WeekdayWords = new[] {
            "MANDAG","TIRSDAG","ONSDAG","TORSDAG","FREDAG","LØRDAG","SØNDAG",
            "MAN","TIRS","ONS","TORS","FRE","LØR","SØN"
        };

        private static string NormalizeWeekdays(string s)
        {
            var up = s.ToUpperInvariant();
            foreach (var w in WeekdayWords)
                up = Regex.Replace(up, $@"\b{Regex.Escape(w)}\b", "", RegexOptions.CultureInvariant);
            return up;
        }

        private static string[] ExtractContainerIds(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return Array.Empty<string>();

            var cleaned = NormalizeWeekdays(input);
            var parts = cleaned
                .Replace("\r", " ").Replace("\n", " ")
                .Split(new[] { '+', ',', '/', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim().TrimEnd('.'))
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            // kandidater: rene tal >= 6 cifre, eller alfanum med mindst 3 tegn, mindst 1 bogstav + 1 tal
            var numRe = new Regex(@"^\d{6,}$", RegexOptions.CultureInvariant);
            var alphaNumRe = new Regex(@"^(?=.*[A-Z])(?=.*\d)[A-Z0-9]{3,}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            var ids = parts
                .Where(p => numRe.IsMatch(p) || alphaNumRe.IsMatch(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return ids;
        }

        private static bool TryParseDkDate(string? s, out DateTime dt)
        {
            dt = default;
            if (string.IsNullOrWhiteSpace(s)) return false;

            // håndter 2-cifret år (dd-MM-yy) og 4-cifret (dd-MM-yyyy)
            var formats = new[] { "dd-MM-yy", "dd-MM-yyyy", "d-M-yy", "d-M-yyyy" };
            var dk = CultureInfo.GetCultureInfo("da-DK");

            // hvis der står noget som 18-02-27, antag 2027
            if (DateTime.TryParseExact(s.Trim(), formats, dk, DateTimeStyles.None, out dt))
            {
                if (dt.Year < 100) dt = new DateTime(dt.Year + 2000, dt.Month, dt.Day);
                return true;
            }
            // fallback normal parse
            return DateTime.TryParse(s, dk, DateTimeStyles.AssumeLocal, out dt);
        }
    }
}
