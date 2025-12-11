using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace DNDProject.Api.Models
{
    // Mappes til tabellen dbo.Modtagelse
    [Table("Modtagelse")]
    public class StenaReceipt
    {
        public int Id { get; set; }

        // LevNr i databasen — KAN være NULL → derfor int?
        public int? CustomerKey { get; set; }

        // Navn i databasen (Navn) — KAN være NULL → derfor string?
        public string? CustomerName { get; set; }

        // KoeresDato — i databasen er den ALTID sat (datetime)
        public DateTime ReceiptDate { get; set; }

        // Varenummer — nvarchar, kan være NULL → derfor string?
        public string? ItemNumber { get; set; }

        // Varebeskrivelse — nvarchar, kan være NULL → derfor string?
        public string? ItemText { get; set; }

        // Enhed (STK, KG, osv.) — KAN være NULL → string?
        public string? Unit { get; set; }

        // Antal — ligger som NVARCHAR → string? (vi parser senere)
        public string? Amount { get; set; }
    }
}
