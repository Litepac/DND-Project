using System;
using System.Collections.Generic;

namespace DNDProject.Api.Models
{
    /// <summary>
    /// Samlet overblik pr. kunde (til venstre liste på Blazor-siden).
    /// Bruges både til single-size summary og all-sizes summary.
    /// </summary>
    public class ContainerEfficiencySummaryDto
    {
        public int CustomerKey { get; set; }              // LevNr
        public string CustomerName { get; set; } = "";    // Navn

        public int Liters { get; set; }                   // 120/240/660/1100 (alt andet ignoreres)

        public int TotalEmpties { get; set; }             // antal tømninger i perioden
        public int InefficientEmpties { get; set; }       // antal tømninger under threshold
        public float InefficientPct { get; set; }         // % ineffektive tømninger
        public float AvgFillPct { get; set; }             // gennemsnitlig fyldningsgrad i %
        public float CapacityKg { get; set; }             // liters * 0.13
        public int ThresholdPct { get; set; }             // fx 80
    }

    /// <summary>
    /// En enkelt tømning (dato, kg og fyldningsgrad).
    /// </summary>
    public class ContainerEmptyingDto
    {
        public DateTime Date { get; set; }    // ReceiptDate
        public float WeightKg { get; set; }   // vægt i kg
        public float FillPct { get; set; }    // fyldningsgrad i %
    }

    /// <summary>
    /// Detaljer for én kunde (højre side på analysen).
    /// </summary>
    public class ContainerEfficiencyDetailDto
    {
        public int CustomerKey { get; set; }              // LevNr
        public string CustomerName { get; set; } = "";

        public int Liters { get; set; }                   // 120/240/660/1100
        public float CapacityKg { get; set; }             // liters * 0.13
        public int ThresholdPct { get; set; }             // fx 80

        public int TotalEmpties { get; set; }
        public int InefficientEmpties { get; set; }
        public float InefficientPct { get; set; }
        public float AvgFillPct { get; set; }

        public List<ContainerEmptyingDto> Empties { get; set; } = new();
    }

    /// <summary>
    /// Beholdt for bagudkompatibilitet (hvis du allerede har kode der refererer til den).
    /// Men du kan også slette den og bare bruge ContainerEfficiencySummaryDto overalt.
    /// </summary>
    public class ContainerEfficiencySummaryBySizeDto : ContainerEfficiencySummaryDto
    {
        // arver alle felter fra ContainerEfficiencySummaryDto
        // (CustomerKey, CustomerName, Liters, totals, pct, CapacityKg, ThresholdPct)
    }
}
