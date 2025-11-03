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

        /// <summary>Affaldstype, fx Pap/Plast/Rest (kan senere flyttes til en WasteType-model)</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>Materiale på selve beholderen (fra mødenotatet: plast/jern)</summary>
        public ContainerMaterial Material { get; set; } = ContainerMaterial.Plast;

        /// <summary>Størrelse i liter (en af de 12 standardstørrelser)</summary>
        public int SizeLiters { get; set; }

        /// <summary>Forventet gennemsnitlig mængde pr. uge (kg) – bruges til anbefalinger</summary>
        public double WeeklyAmountKg { get; set; }

        /// <summary>Valgfri: senest registrerede fyldningsgrad i procent (0–100)</summary>
        public double? LastFillPct { get; set; }

        /// <summary>Valgfri: seneste afhentningsdato</summary>
        public DateTime? LastPickupDate { get; set; }

        /// <summary>Valgfri: foretrukken afhentningsfrekvens (dage) – kan sættes/justeres af anbefalingslogik</summary>
        public int? PreferredPickupFrequencyDays { get; set; }

        /// <summary>Relation til kunde (kan oprettes senere)</summary>
        public int? CustomerId { get; set; }
        // public Customer? Customer { get; set; }  // tilføjes når du laver Customer-modellen

        public string? ExternalId { get; set; } // fx "Enhednr" fra Stena

    }
}



