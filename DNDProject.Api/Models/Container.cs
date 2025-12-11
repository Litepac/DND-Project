using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace DNDProject.Api.Models
{
    public enum ContainerMaterial
    {
        Plast = 1,
        Jern = 2
    }

    public class Container
    {
        public int Id { get; set; }

        public string Type { get; set; } = string.Empty;

        public ContainerMaterial Material { get; set; } = ContainerMaterial.Plast;

        public int SizeLiters { get; set; }

        // 👇 VAR double → skal være float for at matche SQL Server (real)
        public float WeeklyAmountKg { get; set; }

        // 👇 VAR double? → skal også være float?
        public float? LastFillPct { get; set; }

        public DateTime? LastPickupDate { get; set; }

        public int? PreferredPickupFrequencyDays { get; set; }

        public int? CustomerId { get; set; }

        [NotMapped]
        public string? ExternalId { get; set; }
    }
}
