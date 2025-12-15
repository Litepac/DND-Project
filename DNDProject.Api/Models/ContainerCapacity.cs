using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DNDProject.Api.Models
{
    [Table("Kapacitet_og_enhed_opdateret", Schema = "dbo")]
    public class ContainerCapacity
    {
        [Key]
        [Column("Varenummer")]
        public int ItemNumber { get; set; }

        // SQL FLOAT -> C# double
        [Column("Kapacitet")]
        public double? Capacity { get; set; }

        [Column("Enhed")]
        public string? Unit { get; set; }
    }
}
