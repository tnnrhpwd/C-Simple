using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

public class DataService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://mern-plan-web-service.onrender.com/api/data/";

    public DataService()
    {
        _httpClient = new HttpClient();
    }

    private void SetAuthorizationHeader(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // Create new data
    public async Task<object> CreateDataAsync(object data, string token)
    {
        SetAuthorizationHeader(token);

        var jsonContent = new StringContent(JsonSerializer.Serialize(data), System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(BaseUrl, jsonContent);

        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<object>(await response.Content.ReadAsStringAsync());
    }

    // Get all data
    public async Task<object> GetDataAsync(Dictionary<string, string> queryParams, string token)
    {
        SetAuthorizationHeader(token);

        var query = string.Join("&", queryParams.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        var response = await _httpClient.GetAsync($"{BaseUrl}?{query}");

        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<object>(await response.Content.ReadAsStringAsync());
    }

    // Update user data
    public async Task<object> UpdateDataAsync(string id, object data, string token)
    {
        SetAuthorizationHeader(token);

        var jsonContent = new StringContent(JsonSerializer.Serialize(data), System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync($"{BaseUrl}{id}", jsonContent);

        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<object>(await response.Content.ReadAsStringAsync());
    }

    // Delete user data
    public async Task<object> DeleteDataAsync(string id, string token)
    {
        SetAuthorizationHeader(token);

        var response = await _httpClient.DeleteAsync($"{BaseUrl}{id}");

        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<object>(await response.Content.ReadAsStringAsync());
    }
}
