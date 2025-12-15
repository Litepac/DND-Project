using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace DNDProject.Api.Models
{
    [Table("Modtagelse")]
    public class StenaReceipt
    {
        public int Id { get; set; }

        public int? CustomerKey { get; set; }
        public string? CustomerName { get; set; }

        public DateTime ReceiptDate { get; set; }

        public string? ItemNumber { get; set; }
        public string? ItemText { get; set; }

        public string? Unit { get; set; }
        public string? Amount { get; set; }

        [Column("KoebsordreNummer")]  // <- VIGTIGT: matcher Modtagelse kolonnen
        public int? PurchaseOrderNumber { get; set; }
    }
}
