using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DNDProject.Api.Models
{
    // Matcher tabellen dbo.Kapacitet_og_enhed_opdateret
    [Table("Kapacitet_og_enhed_opdateret")]
    public class StenaContainerDef
    {
        [Key]
        public int Varenummer { get; set; }

        public string VareBeskrivelse { get; set; } = string.Empty;

        public int? Kapacitet { get; set; }          // fx 660
        public string? Enhed { get; set; }           // fx "L"

        public decimal? Lejeomkostning_pr_måned { get; set; }
    }
}
