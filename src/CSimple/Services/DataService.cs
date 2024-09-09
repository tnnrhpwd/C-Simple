using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Maui.Storage;
using System.Collections.Generic;
using Newtonsoft.Json;

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
    public async Task<DataClass> CreateDataAsync(object data, string token)
    {
        SetAuthorizationHeader(token);
        var jsonContent = new StringContent(System.Text.Json.JsonSerializer.Serialize(data), System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(BaseUrl, jsonContent);
        return await HandleResponse<DataClass>(response);
    }
    // Get all data
    public async Task<DataClass> GetDataAsync(Dictionary<string, string> queryParams, string token)
    {
        SetAuthorizationHeader(token);
        var query = string.Join("&", queryParams.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        var response = await _httpClient.GetAsync($"{BaseUrl}?{query}");
        return await HandleResponse<DataClass>(response);
    }
    // Update user data
    public async Task<DataClass> UpdateDataAsync(string id, object data, string token)
    {
        SetAuthorizationHeader(token);
        var jsonContent = new StringContent(System.Text.Json.JsonSerializer.Serialize(data), System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync($"{BaseUrl}{id}", jsonContent);
        return await HandleResponse<DataClass>(response);
    }
    // Delete user data
    public async Task<DataClass> DeleteDataAsync(string id, string token)
    {
        SetAuthorizationHeader(token);
        var response = await _httpClient.DeleteAsync($"{BaseUrl}{id}");
        return await HandleResponse<DataClass>(response);
    }
    // Login user and store token and nickname locally
    public async Task<User> LoginAsync(string email, string password)
    {
        var userData = new { email, password };
        var response = await _httpClient.PostAsJsonAsync(BaseUrl + "login", userData);
        Debug.WriteLine($"Response status: {response.StatusCode}");
        var responseContent = await response.Content.ReadAsStringAsync();
        Debug.WriteLine($"Response content: {responseContent}");

        if (response.IsSuccessStatusCode)
        {
            var user = JsonConvert.DeserializeObject<User>(responseContent);
            if (user == null)
            {
                Debug.WriteLine("User deserialization failed.");
            }
            else
            {
                await SecureStorage.SetAsync("userToken", user.Token);
                await SecureStorage.SetAsync("userNickname", user.Nickname);
                await SecureStorage.SetAsync("userEmail", user.Email);
                Debug.WriteLine("Login successful. Token:" + user.Token + ", Name:" + user.Nickname + ", Email:" + user.Email);
                return user;
            }
        }
        // If login fails, return null
        Debug.WriteLine("Login failed.");
        return null;
    }
    // Logout user
    public void Logout()
    {
        SecureStorage.Remove("userToken");
        SecureStorage.Remove("userNickname");
        SecureStorage.Remove("userEmail");
        Debug.WriteLine("User logged out.");
    }
    // Check if the user is logged in by verifying if the token exists
    public async Task<bool> IsLoggedInAsync()
    {
        try
        {
            var token = await SecureStorage.GetAsync("userToken");
            return !string.IsNullOrEmpty(token);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking login status: {ex.Message}");
            return false;
        }
    }
    // Get user details from secure storage
    public async Task<User> GetStoredUserAsync()
    {
        try
        {
            var token = await SecureStorage.GetAsync("userToken");
            var nickname = await SecureStorage.GetAsync("userNickname");
            var email = await SecureStorage.GetAsync("userEmail");

            if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(nickname) && !string.IsNullOrEmpty(email))
            {
                return new User
                {
                    Token = token,
                    Nickname = nickname,
                    Email = email
                };
            }
            return null; // User is not logged in or missing data
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error retrieving stored user: {ex.Message}");
            return null;
        }
    }
    // Handle responses, ensure success or handle error
    private async Task<T> HandleResponse<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return await System.Text.Json.JsonSerializer.DeserializeAsync<T>(await response.Content.ReadAsStreamAsync());
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"Error: {errorContent}");
            throw new HttpRequestException($"Request failed: {response.StatusCode}");
        }
    }
}
// Define a model class to deserialize the user response
public class User
{
    public string Token { get; set; }
    public string Nickname { get; set; }
    public string Email { get; set; }
}

public class DataClass
{
    public List<User> User { get; set; } = new List<User>();
    public List<object> Data { get; set; } = new List<object>();
    public bool DataIsError { get; set; } = false;
    public bool DataIsSuccess { get; set; } = false;
    public bool DataIsLoading { get; set; } = false;
    public string DataMessage { get; set; } = string.Empty;
    public string Operation { get; set; } = null;
}
// { Ideal data state:
//   data: {
//     user: {
//       _id: '65673ec1fcacdd019a167520',
//       nickname: 'tnnrhpwd',
//       email: 'tnnrhpwd@gmail.com',
//       token: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpZCI6IjY1NjczZWMxZmNhY2RkMDE5YTE2NzUyMCIsImlhdCI6MTcyNTczMTExOCwiZXhwIjoxNzI4MzIzMTE4fQ.f9TqWqfjQfkDdNqk4Y8-rzFobJFz_en8tUI4YwR1rsI'
//     },
//     data: {
//       data: [
//         'Creator:65673ec1fcacdd019a167520|Goal:Identify the movie with brown hair guy has beach house blown up and loses his guitar on the roof. The movie was made before year 2000',
//         'Creator:65673ec1fcacdd019a167520|Goal:hello',
//         'Creator:64efe9e2c42368e193ee6977|Goal:hello',
//         'Creator:65673ec1fcacdd019a167520|Goal:Build a house',
//         'Creator:65673ec1fcacdd019a167520|Goal:Build a house'
//       ]
//     },
//     dataIsError: false,
//     dataIsSuccess: true,
//     dataIsLoading: false,
//     dataMessage: '',
//     operation: 'get'
//   }
// }
