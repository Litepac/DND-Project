using System.Net.Http.Json;
using DNDProject.Web.Models;

namespace DNDProject.Web.Services;

public sealed class MlApiClient
{
    private readonly HttpClient _http;
    public MlApiClient(HttpClient http) => _http = http;

    public async Task<DailyResponseDto?> GetDailyAsync()
        => await _http.GetFromJsonAsync<DailyResponseDto>("api/ml-test/daily");
}
