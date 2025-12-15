using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DNDProject.Web.Models
{
    public class ContainerEfficiencyCustomerSummaryDto
    {
        [JsonPropertyName("customerKey")]
        public int CustomerKey { get; set; }

        [JsonPropertyName("customerName")]
        public string? CustomerName { get; set; }

        [JsonPropertyName("liters")]
        public int Liters { get; set; }                 // 120/240/660/1100

        [JsonPropertyName("totalEmpties")]
        public int TotalEmpties { get; set; }

        [JsonPropertyName("inefficientEmpties")]
        public int InefficientEmpties { get; set; }

        [JsonPropertyName("inefficientPct")]
        public float InefficientPct { get; set; }

        [JsonPropertyName("avgFillPct")]
        public float AvgFillPct { get; set; }

        [JsonPropertyName("capacityKg")]
        public float CapacityKg { get; set; }

        [JsonPropertyName("thresholdPct")]
        public int ThresholdPct { get; set; }
    }

    public class ContainerEfficiencyCustomerDetailDto
    {
        [JsonPropertyName("customerKey")]
        public int CustomerKey { get; set; }

        [JsonPropertyName("customerName")]
        public string? CustomerName { get; set; }

        [JsonPropertyName("liters")]
        public int Liters { get; set; }

        [JsonPropertyName("capacityKg")]
        public float CapacityKg { get; set; }

        [JsonPropertyName("thresholdPct")]
        public int ThresholdPct { get; set; }

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
    }

    public class ContainerEmptyingDto
    {
        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        [JsonPropertyName("weightKg")]
        public float WeightKg { get; set; }

        [JsonPropertyName("fillPct")]
        public float FillPct { get; set; }
    }
}
