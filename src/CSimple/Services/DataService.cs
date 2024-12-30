using Microsoft.Maui.Storage;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

public class DataService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://mern-plan-web-service.onrender.com/api/data/";
    private readonly UpdateDataService _updateDataService;

    public DataService()
    {
        _httpClient = new HttpClient();
        _updateDataService = new UpdateDataService();
    }

    private void SetAuthorizationHeader(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // Create new data
    public async Task<DataClass> CreateDataAsync(string data, string token)
    {
        SetAuthorizationHeader(token);

        // Serialize the data to JSON
        var jsonData = JsonSerializer.Serialize(new { data });
        var jsonContent = new StringContent(jsonData, System.Text.Encoding.UTF8, "application/json");

        // Calculate the size of the JSON content
        var dataSize = System.Text.Encoding.UTF8.GetByteCount(jsonData);
        Debug.WriteLine($"----Size of the data being sent: {dataSize} bytes----");

        // Send the POST request
        var response = await _httpClient.PostAsync(BaseUrl, jsonContent);

        // Handle the response
        return await HandleResponse<DataClass>(response);
    }

    // Get data with multiple 'data.text' query parameters
    public async Task<List<DataClass>> GetDataAsync(List<string> searchStrings, string token)
    {
        SetAuthorizationHeader(token);
        var results = new List<DataClass>();

        foreach (var searchString in searchStrings)
        {
            var url = $"{BaseUrl}";
            var queryParams = new Dictionary<string, string> { { "data.text", searchString } };
            var queryString = new FormUrlEncodedContent(queryParams).ReadAsStringAsync().Result;

            Debug.WriteLine($"Calling GET URL: {url}");
            Debug.WriteLine($"Calling GET Params: {queryString}");

            const int maxRetries = 5;
            const int delayMilliseconds = 10000; // 10 seconds
            int retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    var response = await _httpClient.GetAsync($"{url}?{queryString}");

                    // Log the raw response content for debugging
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine("Length of responseContent:" + JsonSerializer.Serialize(responseContent).Length.ToString());
                    Debug.WriteLine($"1. (DataService.GetDataAsync) Raw response data: {responseContent}");

                    // Handle the response
                    var dataClass = await HandleResponse<DataClass>(response);
                    results.Add(dataClass);
                    break;
                }
                catch (HttpRequestException ex)
                {
                    retryCount++;
                    Debug.WriteLine($"Attempt {retryCount} failed: {ex.Message}");
                    if (retryCount >= maxRetries)
                    {
                        throw;
                    }
                    Debug.WriteLine($"Retrying in {delayMilliseconds / 1000} seconds...");
                    await Task.Delay(delayMilliseconds);
                }
            }
        }

        return results;
    }

    // Update existing data using the backend's "compress" or "update" method
    // Modified Update method to delegate to UpdateDataService
    public async Task<DataClass> UpdateDataAsync(string id, object data, string token)
    {
        return await _updateDataService.UpdateDataAsync(id, data, token);
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
        var jsonContent = new StringContent(
            JsonSerializer.Serialize(userData),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PostAsync(BaseUrl + "login", jsonContent);
        Debug.WriteLine($"Request URL: {BaseUrl}login");
        Debug.WriteLine($"Request content: {JsonSerializer.Serialize(userData)}");
        Debug.WriteLine($"Response status: {response.StatusCode}");
        var responseContent = await response.Content.ReadAsStringAsync();
        Debug.WriteLine($"Response content: {responseContent}");

        if (response.IsSuccessStatusCode)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var user = JsonSerializer.Deserialize<User>(responseContent, options);
            if (user == null || string.IsNullOrEmpty(user.Token))
            {
                Debug.WriteLine("User deserialization failed or token is null.");
            }
            else
            {
                Debug.WriteLine($"Setting secure storage... Token: {user.Token}, Nickname: {user.Nickname}, Email: {user.Email}, ID: {user._id}");
                await SecureStorage.SetAsync("userToken", user.Token);
                await SecureStorage.SetAsync("userNickname", user.Nickname);
                await SecureStorage.SetAsync("userEmail", user.Email);
                await SecureStorage.SetAsync("userID", user._id);
                Debug.WriteLine("Login successful. Token:" + user.Token + ", Name:" + user.Nickname + ", Email:" + user.Email + ", ID:" + user._id);
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
        SecureStorage.Remove("userID");
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
            var id = await SecureStorage.GetAsync("userID");

            if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(nickname) && !string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(id))
            {
                return new User
                {
                    Token = token,
                    Nickname = nickname,
                    Email = email,
                    _id = id
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
            var responseContent = await response.Content.ReadAsStringAsync();
            Debug.WriteLine("Length of responseContent:" + JsonSerializer.Serialize(responseContent).Length.ToString());
            Debug.WriteLine($"1 (DataService.HandleResponse) Raw response data: {responseContent}");
            return JsonSerializer.Deserialize<T>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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
    public string _id { get; set; }
    public string Token { get; set; }
    public string Nickname { get; set; }
    public string Email { get; set; }
}

public class DataClass
{
    public List<string> Data { get; set; } = new List<string>();
    public bool DataIsError { get; set; } = false;
    public bool DataIsSuccess { get; set; } = false;
    public bool DataIsLoading { get; set; } = false;
    public string DataMessage { get; set; } = string.Empty;
    public string Operation { get; set; } = null;
}