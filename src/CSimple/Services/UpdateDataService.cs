using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;
using System;

public class UpdateDataService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://mern-plan-web-service.onrender.com/api/data/";

    public UpdateDataService()
    {
        _httpClient = new HttpClient();
    }

    private void SetAuthorizationHeader(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // Method to update data
    public async Task<DataClass> UpdateDataAsync(string id, object data, string token)
    {
        SetAuthorizationHeader(token);
        try
        {
            // Serialize the data to JSON format
            var jsonContent = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");

            // Send PUT request to the backend
            var response = await _httpClient.PutAsync($"{BaseUrl}{id}", jsonContent);

            // Handle the response
            return await HandleResponse<DataClass>(response);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating data: {ex.Message}");
            throw;
        }
    }

    // Handle the response from the API and return the parsed response
    private async Task<T> HandleResponse<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            // Parse response data if the request is successful
            return await JsonSerializer.DeserializeAsync<T>(await response.Content.ReadAsStreamAsync());
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"Error: {errorContent}");
            throw new HttpRequestException($"Request failed with status: {response.StatusCode}");
        }
    }
}
