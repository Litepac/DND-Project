using System;
using System.Collections.Generic;

namespace DNDProject.Api.Models
{
    /// <summary>
    /// Samlet overblik pr. kunde (til venstre liste på Blazor-siden).
    /// </summary>
    public class ContainerEfficiencySummaryDto
    {
        public int CustomerKey { get; set; }              // LevNr
        public string CustomerName { get; set; } = "";    // Navn

        public int TotalEmpties { get; set; }             // antal tømninger i perioden
        public int InefficientEmpties { get; set; }       // antal tømninger under threshold
        public float InefficientPct { get; set; }         // % ineffektive tømninger
        public float AvgFillPct { get; set; }             // gennemsnitlig fyldningsgrad i %
        public float CapacityKg { get; set; }
        public int ThresholdPct { get; set; }

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
        public int CustomerKey { get; set; }
        public string CustomerName { get; set; } = "";

        public int TotalEmpties { get; set; }
        public int InefficientEmpties { get; set; }
        public float InefficientPct { get; set; }
        public float AvgFillPct { get; set; }

        public List<ContainerEmptyingDto> Empties { get; set; } = new();
    }
}
