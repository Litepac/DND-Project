using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace DNDProject.Api.Models
{
    [Table("Modtagelse")]
    public class StenaReceipt
    {
        public int Id { get; set; }

        // LevNr i databasen (int, men kan være NULL)
        public int? CustomerKey { get; set; }

        // Navn i databasen (må gerne være null i DB – vi håndterer det som string?)
        public string? CustomerName { get; set; }

        public DateTime ReceiptDate { get; set; }   // KoeresDato

        public string? ItemNumber { get; set; }  // Varenummer
        public string? ItemText   { get; set; }  // Varebeskrivelse

        public string? Unit   { get; set; }      // Enhed (KG/STK/...)
        public string? Amount { get; set; }      // Antal (nvarchar)
    }
}
