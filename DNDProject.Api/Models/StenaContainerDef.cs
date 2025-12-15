using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DNDProject.Api.Models
{
    // Matcher tabellen dbo.Kapacitet_og_enhed_opdateret
    [Table("Kapacitet_og_enhed_opdateret")]
    public class StenaContainerDef
    {
        [Key]
public int? Koebsordrenr { get; set; } // Koebsordrenr (int)
public int? Lev_nr { get; set; }       // Lev_nr (int)
public int? Varenr { get; set; }       // Varenr (int)
public string? Beskrivelse { get; set; }
public string? Frekvens { get; set; }
public string? Uge_dag { get; set; }
public DateTime? Start_dato { get; set; }

    }
}
