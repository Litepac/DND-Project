using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DNDProject.Web.Models
{
    public class ContainerEfficiencyCustomerSummaryDto
    {
        [JsonPropertyName("customerKey")]
        public int LevNr { get; set; }

        [JsonPropertyName("customerName")]
        public string? CustomerName { get; set; }

        [JsonPropertyName("totalEmpties")]
        public int TotalEmpties { get; set; }

        [JsonPropertyName("inefficientEmpties")]
        public int InefficientEmpties { get; set; }

        [JsonPropertyName("inefficientPct")]
        public float InefficientPct { get; set; }

        [JsonPropertyName("avgFillPct")]
        public float AvgFillPct { get; set; }
    }

    public class ContainerEfficiencyCustomerDetailDto
    {
        [JsonPropertyName("customerKey")]
        public int LevNr { get; set; }

        [JsonPropertyName("customerName")]
        public string? CustomerName { get; set; }

        [JsonPropertyName("totalEmpties")]
        public int TotalEmpties { get; set; }

        [JsonPropertyName("inefficientEmpties")]
        public int InefficientEmpties { get; set; }

        [JsonPropertyName("inefficientPct")]
        public float InefficientPct { get; set; }

        [JsonPropertyName("avgFillPct")]
        public float AvgFillPct { get; set; }

        [JsonPropertyName("empties")]
        public List<ContainerEmptyingDto> Empties { get; set; } = new();

        // UI-hjælpere (ikke fra API)
        [JsonIgnore]
        public float CapacityKg => 85.8f; // 660L * 0.13 kg/L

        [JsonIgnore]
        public int ThresholdPct { get; set; }
    }

    public class ContainerEmptyingDto
    {
        [JsonPropertyName("date")]
        public DateTime KoeresDato { get; set; }

        [JsonPropertyName("weightKg")]
        public float WeightKg { get; set; }

        [JsonPropertyName("fillPct")]
        public float FillPct { get; set; }
    }
}
