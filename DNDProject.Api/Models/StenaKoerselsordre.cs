using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DNDProject.Api.Models;

[Table("Kørselsordre")] // <-- VIGTIG: singular (som i din sys.tables)
public class StenaKoerselsordre
{
    [Key]
    [Column("Nr")]
    public string Nr { get; set; } = string.Empty;

    [Column("Lev_nr")]
    public int? Lev_nr { get; set; }

    [Column("Navn")]
    public string? Navn { get; set; }

    [Column("Varenr")]
    public int? Varenr { get; set; }

    [Column("Beskrivelse")]
    public string? Beskrivelse { get; set; }

    [Column("Indhold")]
    public int? Indhold { get; set; }

    [Column("Indholdsbeskrivelse")]
    public string? Indholdsbeskrivelse { get; set; }

    [Column("Frekvens")]
    public string? Frekvens { get; set; }

    [Column("Uge_dag")]
    public string? Uge_dag { get; set; }

    [Column("Start_dato")]
    public DateTime? Start_dato { get; set; }

    [Column("Købsordrenr")]
    public int? PurchaseOrderNumber { get; set; }
}
