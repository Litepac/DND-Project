using System;
using System.Collections.Generic;

namespace DNDProject.Api.Models
{
public class CustomerSummaryDto
{
    public string CustomerKey  { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;

    public float     TotalWeightKg { get; set; }
    public int       ReceiptCount  { get; set; }
    public DateTime? FirstDate     { get; set; }
    public DateTime? LastDate      { get; set; }
}

public class CustomerTimeseriesPointDto
{
    public string   Label         { get; set; } = string.Empty;
    public DateTime PeriodStart   { get; set; }
    public float    TotalWeightKg { get; set; }
}

public class CustomerDashboardDto
{
    public CustomerSummaryDto               Summary    { get; set; } = new();
    public List<CustomerTimeseriesPointDto> Timeseries { get; set; } = new();
}


    
}
