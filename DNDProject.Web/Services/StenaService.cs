using System.Net.Http.Json;

namespace DNDProject.Web.Services
{
    public class StenaDashboardDto
    {
        public int totalRows { get; set; }
        public double totalKg { get; set; }
        public int totalEmptyings { get; set; }
        public List<TopTypeDto> topTypes { get; set; } = new();
        public List<StenaLatestDto> latest { get; set; } = new();
    }

    public class TopTypeDto
    {
        public string type { get; set; } = "";
        public int count { get; set; }
    }

    public class StenaLatestDto
    {
        public DateTime? date { get; set; }
        public string? itemNumber { get; set; }
        public string? itemName { get; set; }
        public string? unit { get; set; }
        public double? amount { get; set; }
        public int kind { get; set; }          // 1 = weight, 2 = emptying
        public string? containerTypeText { get; set; }
    }

    public class StenaService
    {
        private readonly HttpClient _http;

        public StenaService(HttpClient http)
        {
            _http = http;
        }

        public async Task<StenaDashboardDto?> GetDashboardAsync()
        {
            return await _http.GetFromJsonAsync<StenaDashboardDto>("api/stena/dashboard");
        }
    }
}
