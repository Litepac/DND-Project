// DNDProject.Web/Models/CustomerDashboardDTOs.cs
using System;
using System.Collections.Generic;

namespace DNDProject.Web.Models
{
    public class CustomerSummaryDto
    {
        public string CustomerKey { get; set; } = string.Empty;
        public string? CustomerName { get; set; }

        public float TotalWeightKg { get; set; }
        public int ReceiptCount { get; set; }
        public DateTime FirstDate { get; set; }
        public DateTime LastDate { get; set; }

        // Nye felter
        public float AverageWeightPerReceiptKg { get; set; }
        public bool IsLowAverageWeight { get; set; }  // fx < 100 kg pr. modtagelse
    }

    public class CustomerTimeseriesPointDto
    {
        public DateTime PeriodStart { get; set; }           // 1. i måneden
        public string Label { get; set; } = string.Empty;   // "2025-09"
        public float TotalWeightKg { get; set; }
    }

    public class CustomerDashboardDto
    {
        public CustomerSummaryDto Summary { get; set; } = new();
        public List<CustomerTimeseriesPointDto> Timeseries { get; set; } = new();
    }
}
