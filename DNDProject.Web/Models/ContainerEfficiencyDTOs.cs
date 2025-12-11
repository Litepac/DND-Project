using System;
using System.Collections.Generic;

namespace DNDProject.Web.Models
{
    public class ContainerEfficiencyCustomerSummaryDto
    {
        public int LevNr { get; set; }
        public string? CustomerName { get; set; }

        public int TotalEmpties { get; set; }
        public int InefficientEmpties { get; set; }
        public float InefficientPct { get; set; }
        public float AvgFillPct { get; set; }
    }

    public class ContainerEmptyDto
    {
        public int Id { get; set; }
        public DateTime KoeresDato { get; set; }

        public float WeightKg { get; set; }
        public float FillPct { get; set; }
    }

    public class ContainerEfficiencyCustomerDetailDto
    {
        public int LevNr { get; set; }
        public string? CustomerName { get; set; }

        public float CapacityKg { get; set; }
        public float ThresholdPct { get; set; }

        public int TotalEmpties { get; set; }
        public int InefficientEmpties { get; set; }
        public float InefficientPct { get; set; }
        public float AvgFillPct { get; set; }

        public List<ContainerEmptyDto> Empties { get; set; } = new();
    }
}
