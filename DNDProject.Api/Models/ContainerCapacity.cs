using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DNDProject.Api.Models
{
    [Table("Kapacitet_og_enhed_opdateret")]
    public class ContainerCapacity
    {
        [Key]
        [Column("Varenummer")]
        public int ItemNumber { get; set; }
        
        [Column("Kapacitet")]
        public int Capacity { get; set; }

        [Column("Enhed")]
        public string? Unit { get; set; }
    }
}
