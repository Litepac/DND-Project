// Models/StenaReceipt.cs
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace DNDProject.Api.Models
{
// Models/StenaReceipt.cs
[Table("Modtagelse")]
public class StenaReceipt
{
    public int Id { get; set; }

    // LevNr i databasen (int)
    public int? CustomerKey { get; set; }

    // Navn i databasen
    public string CustomerName { get; set; } = string.Empty;

    public DateTime ReceiptDate { get; set; }   // KoeresDato

    public string ItemNumber { get; set; } = string.Empty;   // Varenummer
    public string ItemText   { get; set; } = string.Empty;   // Varebeskrivelse

    public string Unit   { get; set; } = string.Empty;       // Enhed
    public string Amount { get; set; } = string.Empty;       // Antal (nvarchar)
}


}
