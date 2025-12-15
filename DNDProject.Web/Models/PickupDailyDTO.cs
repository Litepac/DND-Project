namespace DNDProject.Web.Models;

public sealed class PickupDailyDto
{
    public string Skabelonnr { get; set; } = "";
    public DateTime Date { get; set; }
    public double CollectedKg { get; set; }
    public string CustomerNo { get; set; } = "";
    public string CustomerName { get; set; } = "";
}

public sealed class DailyResponseDto
{
    public int Count { get; set; }
    public List<PickupDailyDto> Sample { get; set; } = new();
}
